using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turn-taking coordinator for multi-party interview
/// Manages who can speak when, tracks conversation flow
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    public string currentSpeaker = "";
    public string lastSpeakerName = "";
    public int totalTurns = 0;
    
    private readonly List<string> speakerHistory = new List<string>();
    private int turnDecisionsThisRound = 0;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public bool RequestTurn(string npcName)
    {
        if (!string.IsNullOrEmpty(currentSpeaker)) return false;
        
        lastSpeakerName = currentSpeaker;
        currentSpeaker = npcName;
        totalTurns++;
        
        speakerHistory.Add(npcName);
        if (speakerHistory.Count > 10) speakerHistory.RemoveAt(0);
        
        NPCManager.Instance?.NotifySpeakerChanged(npcName);
        return true;
    }
    
    public string GetTurnHistory()
    {
        if (speakerHistory.Count == 0) return "";
        
        int showLast = Mathf.Min(3, speakerHistory.Count);
        var recent = new List<string>();
        for (int i = speakerHistory.Count - showLast; i < speakerHistory.Count; i++)
            recent.Add(speakerHistory[i]);
        
        return string.Join(" → ", recent);
    }
    
    public bool WasLastSpeaker(string npcName)
    {
        return speakerHistory.Count > 0 && speakerHistory[speakerHistory.Count - 1] == npcName;
    }
    
    public void ReleaseTurn(string npcName)
    {
        if (currentSpeaker == npcName)
        {
            currentSpeaker = "";
            NPCManager.Instance?.NotifySpeakerChanged(string.Empty);
        }
    }
    
    public void OnUserAnswered(string answer)
    {
        NPCManager.Instance?.NotifySpeakerChanged("User");
        turnDecisionsThisRound = 0;
    }
    
    public bool RecordDecision(string npcName, bool wantsToSpeak)
    {
        turnDecisionsThisRound++;
        
        if (wantsToSpeak) return true;
        
        int totalNPCs = NPCManager.Instance != null ? NPCManager.Instance.GetActiveNPCCount() : 2;
        
        if (turnDecisionsThisRound >= totalNPCs)
        {
            Debug.LogWarning($"⚠️ All NPCs passed! Forcing {npcName}");
            return true;
        }
        
        return false;
    }
    
    [ContextMenu("Clear Interview")]
    public void ClearHistory()
    {
        speakerHistory.Clear();
        currentSpeaker = "";
        lastSpeakerName = "";
        totalTurns = 0;
    }
}

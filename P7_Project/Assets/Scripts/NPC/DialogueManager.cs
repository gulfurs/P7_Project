using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal turn coordination - LLM makes all decisions about relevance and participation
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    [Header("Interview State")]
    public string currentSpeaker = "";
    public string lastSpeakerName = "";
    
    private readonly List<string> speakerHistory = new List<string>();
    private int turnDecisionsThisRound = 0;  // Track how many NPCs decided this round
    
    [Header("Debug Info")]
    public int totalTurns = 0;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Simple turn request - just blocking
    /// </summary>
    public bool RequestTurn(string npcName)
    {
        if (!string.IsNullOrEmpty(currentSpeaker))
        {
            Debug.Log($"⏸️ {npcName} blocked - {currentSpeaker} is speaking");
            return false;
        }
        
        GrantTurn(npcName);
        return true;
    }
    
    /// <summary>
    /// Get conversation context for LLM
    /// </summary>
    public string GetTurnHistory()
    {
        if (speakerHistory.Count == 0)
            return "";
        
        int showLast = Mathf.Min(3, speakerHistory.Count);
        var recent = new List<string>();
        for (int i = speakerHistory.Count - showLast; i < speakerHistory.Count; i++)
        {
            recent.Add(speakerHistory[i]);
        }
        
        return string.Join(" → ", recent);
    }
    
    /// <summary>
    /// Check if NPC spoke last (for LLM context)
    /// </summary>
    public bool WasLastSpeaker(string npcName)
    {
        return speakerHistory.Count > 0 && speakerHistory[speakerHistory.Count - 1] == npcName;
    }
    
    private void GrantTurn(string npcName)
    {
        lastSpeakerName = currentSpeaker;
        currentSpeaker = npcName;
        totalTurns++;
        
        speakerHistory.Add(npcName);
        if (speakerHistory.Count > 10)
            speakerHistory.RemoveAt(0);
        
        Debug.Log($"🎤 {npcName} granted turn (#{totalTurns})");
        NPCManager.Instance?.NotifySpeakerChanged(npcName);
    }
    
    public void ReleaseTurn(string npcName)
    {
        if (currentSpeaker == npcName)
        {
            currentSpeaker = "";
            Debug.Log($"✅ {npcName} released turn");
            NPCManager.Instance?.NotifySpeakerChanged(string.Empty);
        }
    }
    
    public void OnUserAnswered(string answer)
    {
        Debug.Log($"👤 User answered: \"{answer.Substring(0, Mathf.Min(50, answer.Length))}...\"");
        NPCManager.Instance?.NotifySpeakerChanged("User");
        turnDecisionsThisRound = 0;  // Reset for new round
    }
    
    /// <summary>
    /// Track a decision and return whether this NPC should be forced to speak.
    /// If no NPC has responded after all make decisions, force the first one.
    /// </summary>
    public bool RecordDecision(string npcName, bool wantsToSpeak)
    {
        turnDecisionsThisRound++;
        
        // If model wants to speak, allow it
        if (wantsToSpeak)
        {
            Debug.Log($"✅ {npcName} chose to respond (decision #{turnDecisionsThisRound})");
            return true;
        }
        
        // Get total NPC count
        int totalNPCs = NPCManager.Instance != null ? NPCManager.Instance.GetActiveNPCCount() : 2;
        
        // If this is the last NPC to decide and no one has spoken yet, FORCE this one to respond
        if (turnDecisionsThisRound >= totalNPCs)
        {
            Debug.LogWarning($"⚠️ All NPCs passed! Forcing {npcName} to respond.");
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
        Debug.Log("🔄 Interview cleared");
    }
}

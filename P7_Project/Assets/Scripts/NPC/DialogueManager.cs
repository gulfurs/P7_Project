using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal turn coordination - LLM makes all decisions about relevance and participation
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public enum InterviewPhase { Introduction, HRRound, TechRound, Conclusion }

    public static DialogueManager Instance { get; private set; }
    
    [Header("Interview State")]
    public InterviewPhase currentPhase = InterviewPhase.Introduction;
    
    [Header("Phase Settings")]
    public int hrRoundTurns = 2;
    public int techRoundTurns = 2;
    
    [Header("Runtime State")]
    public int turnsInCurrentPhase = 0;
    public string currentSpeaker = "";
    public string lastSpeakerName = "";
    
    private readonly List<string> speakerHistory = new List<string>();
    
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
    
    private void GrantTurn(string npcName)
    {
        lastSpeakerName = currentSpeaker;
        currentSpeaker = npcName;
        totalTurns++;
        turnsInCurrentPhase++;
        
        speakerHistory.Add(npcName);
        if (speakerHistory.Count > 10)
            speakerHistory.RemoveAt(0);
        
        Debug.Log($"🎤 {npcName} granted turn (#{totalTurns}) in phase {currentPhase} (Turn {turnsInCurrentPhase})");
        NPCManager.Instance?.NotifySpeakerChanged(npcName);
    }
    
    public void ReleaseTurn(string npcName)
    {
        if (currentSpeaker == npcName)
        {
            currentSpeaker = "";
            Debug.Log($"✅ {npcName} released turn");
            NPCManager.Instance?.NotifySpeakerChanged(string.Empty);

            CheckPhaseTransition();
        }
    }

    private void CheckPhaseTransition()
    {
        switch (currentPhase)
        {
            case InterviewPhase.Introduction:
                // After the first introduction (usually HR), move to HR round
                // Or if we want both to introduce, we wait for 2 turns.
                // Let's assume 1 turn for Intro is enough for now as per previous logic, 
                // or maybe 2 if we want both to say hi.
                // The user said "cleaner implementation". 
                // Let's stick to: Intro -> HR Round -> Tech Round -> Conclusion
                if (turnsInCurrentPhase >= 1) 
                {
                    TransitionToPhase(InterviewPhase.HRRound);
                }
                break;

            case InterviewPhase.HRRound:
                if (turnsInCurrentPhase >= hrRoundTurns)
                {
                    TransitionToPhase(InterviewPhase.TechRound);
                }
                break;

            case InterviewPhase.TechRound:
                if (turnsInCurrentPhase >= techRoundTurns)
                {
                    TransitionToPhase(InterviewPhase.Conclusion);
                }
                break;
                
            case InterviewPhase.Conclusion:
                // End after 1 turn?
                if (turnsInCurrentPhase >= 1)
                {
                    // Maybe just stay in conclusion or end?
                    // OnUserAnswered handles EndInterview if in Conclusion.
                }
                break;
        }
    }

    private void TransitionToPhase(InterviewPhase nextPhase)
    {
        currentPhase = nextPhase;
        turnsInCurrentPhase = 0;
        Debug.Log($"📜 Interview phase changed to {currentPhase}");
    }
    
    public void OnUserAnswered(string answer)
    {
        if (currentPhase == InterviewPhase.Conclusion)
        {
            EndInterview();
            return;
        }

        NPCManager.Instance?.NotifySpeakerChanged("User");
    }

    private void EndInterview()
    {
        Debug.Log("🎬 Interview Finished! Ending game loop.");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    [ContextMenu("Clear Interview")]
    public void ClearHistory()
    {
        speakerHistory.Clear();
        currentSpeaker = "";
        lastSpeakerName = "";
        totalTurns = 0;
        currentPhase = InterviewPhase.Introduction; // Reset phase
        Debug.Log("🔄 Interview cleared and reset to Introduction phase.");
    }

    /// <summary>
    /// Kicks off the interview by having the first NPC introduce themselves.
    /// </summary>
    public void StartInterview()
    {
        if (currentPhase != InterviewPhase.Introduction || totalTurns > 0)
        {
            Debug.LogWarning("[DialogueManager] StartInterview called but interview has already started.");
            return;
        }

        // Clear history to ensure a fresh start
        ClearHistory();

        var npcInstances = NPCManager.Instance?.npcInstances;
        if (npcInstances != null && npcInstances.Count > 0)
        {
            var firstNpc = npcInstances[0];
            if (firstNpc != null)
            {
                Debug.Log($"[DialogueManager] Kicking off interview. Asking {firstNpc.npcProfile.npcName} to introduce themselves.");
                firstNpc.InitiateIntroduction();
            }
        }
    }
}

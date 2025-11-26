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

    // New: require explicit final user input before concluding
    [Header("Conclusion Settings")]
    public bool requireFinalUserInput = true;
    private bool awaitingFinalUserInput = false;

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
                // End the interview after the conclusion phase has had its configured number of turns (1 by default)
                // This will run when an NPC has taken and released a turn in Conclusion.
                if (turnsInCurrentPhase >= 2)
                {
                    EndInterview();
                }
                break;
        }
    }

    private void TransitionToPhase(InterviewPhase nextPhase)
    {
        currentPhase = nextPhase;
        turnsInCurrentPhase = 0;
        Debug.Log($"📜 Interview phase changed to {currentPhase}");

        // Do NOT auto-trigger NPC conclusion here.
        // If concluding, require a final user input if configured.
        if (currentPhase == InterviewPhase.Conclusion)
        {
            awaitingFinalUserInput = requireFinalUserInput;
            Debug.Log($"🔔 Conclusion entered. Awaiting final user input: {awaitingFinalUserInput}");
        }
    }
    
    public void OnUserAnswered(string answer)
    {
        // If we're in Conclusion and we're configured to require a final user input,
        // consume that final input here (but do NOT end the interview immediately).
        // Let the usual NPC response flow occur (NPCs will RequestTurn/Respond, then ReleaseTurn -> EndInterview).
        if (currentPhase == InterviewPhase.Conclusion && awaitingFinalUserInput)
        {
            awaitingFinalUserInput = false;
            Debug.Log("[DialogueManager] Final user input received for Conclusion. Allowing NPCs to respond.");
            NPCManager.Instance?.NotifySpeakerChanged("User");
            return;
        }

        // Default behavior for other phases: notify UI/animator that the user spoke.
        NPCManager.Instance?.NotifySpeakerChanged("User");
    }

    private void EndInterview()
    {
        Debug.Log("🎬 Interview Finished! Ending game loop.");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
           // Application.Quit();
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
        awaitingFinalUserInput = false;
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

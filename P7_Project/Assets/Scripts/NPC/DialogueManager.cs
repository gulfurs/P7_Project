using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages shared conversation state across all NPCs
/// Tracks turn order, interruptions, and active speakers
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    [Header("Conversation State")]
    public string currentSpeaker = "";
    public float lastSpeakTime = 0f;
    
    [Header("Turn Management")]
    [Tooltip("Minimum time between NPC responses (seconds)")]
    public float minimumTurnGap = 1.5f;
    
    private readonly List<string> speakerHistory = new List<string>();
    private readonly Queue<InterruptionRequest> interruptionQueue = new Queue<InterruptionRequest>();
    private readonly Queue<NPCTurnRequest> turnQueue = new Queue<NPCTurnRequest>();
    
    [Header("Debug Info")]
    public int totalTurns = 0;
    public int totalInterruptions = 0;
    public int queuedRequests = 0;
    
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
    /// Register that an NPC wants to speak - adds to queue if someone is speaking
    /// </summary>
    public bool RequestTurn(string npcName, NPCChatInstance npcInstance, string messageText)
    {
        // If someone is currently speaking, queue this request
        if (!string.IsNullOrEmpty(currentSpeaker))
        {
            turnQueue.Enqueue(new NPCTurnRequest
            {
                npcName = npcName,
                npcInstance = npcInstance,
                messageText = messageText,
                timestamp = Time.time
            });
            queuedRequests = turnQueue.Count;
            Debug.Log($"📋 {npcName} queued to speak ({turnQueue.Count} in queue)");
            return false; // Turn not granted yet
        }
        
        // Check if enough time has passed since last speaker
        if (Time.time - lastSpeakTime < minimumTurnGap)
        {
            Debug.Log($"⏱️ {npcName} tried to speak too soon, queuing...");
            turnQueue.Enqueue(new NPCTurnRequest
            {
                npcName = npcName,
                npcInstance = npcInstance,
                messageText = messageText,
                timestamp = Time.time
            });
            queuedRequests = turnQueue.Count;
            return false;
        }
        
        // Grant turn immediately
        GrantTurn(npcName);
        return true;
    }
    
    /// <summary>
    /// Internal method to grant a turn to an NPC
    /// </summary>
    private void GrantTurn(string npcName)
    {
        currentSpeaker = npcName;
        lastSpeakTime = Time.time;
        
        // Track speaker history
        speakerHistory.Add(npcName);
        if (speakerHistory.Count > 10)
            speakerHistory.RemoveAt(0);
        
        totalTurns++;
        Debug.Log($"🎤 {npcName} is now speaking (Turn #{totalTurns})");
    }
    
    /// <summary>
    /// Register that an NPC has finished speaking - process next in queue
    /// </summary>
    public void ReleaseTurn(string npcName)
    {
        if (currentSpeaker == npcName)
        {
            currentSpeaker = "";
            Debug.Log($"✅ {npcName} finished speaking");
            
            // Process next NPC in queue
            ProcessNextInQueue();
        }
    }
    
    /// <summary>
    /// Process the next NPC waiting in the turn queue
    /// </summary>
    private void ProcessNextInQueue()
    {
        if (turnQueue.Count == 0) return;
        
        // Get next NPC from queue
        var nextRequest = turnQueue.Dequeue();
        queuedRequests = turnQueue.Count;
        
        Debug.Log($"▶️ Processing queued request from {nextRequest.npcName}");
        
        // Grant turn (this updates lastSpeakTime)
        GrantTurn(nextRequest.npcName);
        
        // Trigger the NPC to actually speak
        if (nextRequest.npcInstance != null)
        {
            nextRequest.npcInstance.ExecuteQueuedMessage(nextRequest.messageText);
        }
    }
    
    /// <summary>
    /// Request an interruption
    /// </summary>
    public void RequestInterruption(string npcName, string reason)
    {
        interruptionQueue.Enqueue(new InterruptionRequest
        {
            npcName = npcName,
            reason = reason,
            timestamp = Time.time
        });
        
        totalInterruptions++;
        Debug.Log($"⚠️ {npcName} wants to interrupt: {reason}");
    }
    
    /// <summary>
    /// Check if this NPC should be allowed to interrupt
    /// </summary>
    public bool CanInterrupt(string npcName)
    {
        // Simple rule: if someone is speaking and you have an interruption queued
        if (!string.IsNullOrEmpty(currentSpeaker) && currentSpeaker != npcName)
        {
            foreach (var req in interruptionQueue)
            {
                if (req.npcName == npcName)
                    return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get recent speaker history as string
    /// </summary>
    public string GetSpeakerHistory()
    {
        if (speakerHistory.Count == 0) return "";
        return "Recent speakers: " + string.Join(" → ", speakerHistory);
    }
    
    /// <summary>
    /// Check if NPC spoke recently (to avoid back-to-back turns)
    /// </summary>
    public bool HasSpokeRecently(string npcName)
    {
        if (speakerHistory.Count == 0) return false;
        
        // Check last 2 turns
        int checkCount = Mathf.Min(2, speakerHistory.Count);
        for (int i = speakerHistory.Count - 1; i >= speakerHistory.Count - checkCount; i--)
        {
            if (speakerHistory[i] == npcName)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Clear conversation history
    /// </summary>
    [ContextMenu("Clear Conversation History")]
    public void ClearHistory()
    {
        speakerHistory.Clear();
        interruptionQueue.Clear();
        turnQueue.Clear();
        currentSpeaker = "";
        totalTurns = 0;
        totalInterruptions = 0;
        queuedRequests = 0;
        Debug.Log("🔄 Conversation history cleared");
    }
}

/// <summary>
/// Represents an interruption request
/// </summary>
public class InterruptionRequest
{
    public string npcName;
    public string reason;
    public float timestamp;
}

/// <summary>
/// Represents a queued turn request
/// </summary>
public class NPCTurnRequest
{
    public string npcName;
    public NPCChatInstance npcInstance;
    public string messageText;
    public float timestamp;
}

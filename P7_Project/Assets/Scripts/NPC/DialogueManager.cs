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
    [Tooltip("Minimum time between any NPC responses (seconds)")]
    public float minimumTurnGap = 1.5f;
    
    [Tooltip("Prevent same NPC from speaking twice in a row")]
    public int turnHistoryToCheck = 1;
    
    private readonly List<string> speakerHistory = new List<string>();
    private readonly Queue<NPCTurnRequest> turnQueue = new Queue<NPCTurnRequest>();
    
    [Header("Debug Info")]
    public int totalTurns = 0;
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
        // If someone is currently speaking, queue request
        if (!string.IsNullOrEmpty(currentSpeaker))
        {
            EnqueueRequest(npcName, npcInstance, messageText);
            return false;
        }
        
        // Check minimum time gap since last speaker
        if (Time.time - lastSpeakTime < minimumTurnGap)
        {
            EnqueueRequest(npcName, npcInstance, messageText);
            return false;
        }
        
        // Grant turn immediately
        GrantTurn(npcName);
        return true;
    }
    
    /// <summary>
    /// Add request to queue
    /// </summary>
    private void EnqueueRequest(string npcName, NPCChatInstance npcInstance, string messageText)
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
        
        // Prioritize next speaker to avoid repetition
        NPCTurnRequest nextRequest = FindNextOptimalSpeaker();
        if (nextRequest == null) return;
        
        queuedRequests = turnQueue.Count;
        Debug.Log($"▶️ Processing queued request from {nextRequest.npcName}");
        
        GrantTurn(nextRequest.npcName);
        
        if (nextRequest.npcInstance != null)
            nextRequest.npcInstance.ExecuteQueuedMessage(nextRequest.messageText);
    }
    
    /// <summary>
    /// Find optimal next speaker - prefers speaker different from last
    /// </summary>
    private NPCTurnRequest FindNextOptimalSpeaker()
    {
        if (turnQueue.Count == 0) return null;
        
        // Get last speaker(s) for diversity check
        string lastSpeaker = speakerHistory.Count > 0 ? speakerHistory[speakerHistory.Count - 1] : "";
        
        // Try to find a request from someone who didn't just speak
        foreach (var request in turnQueue)
        {
            if (request.npcName != lastSpeaker)
            {
                turnQueue.Dequeue();
                return request;
            }
        }
        
        // If all queued speakers are the same, just take the first one
        return turnQueue.Dequeue();
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
        
        // Check if this NPC was the last speaker
        return speakerHistory[speakerHistory.Count - 1] == npcName;
    }
    
    /// <summary>
    /// Clear conversation history
    /// </summary>
    [ContextMenu("Clear Conversation History")]
    public void ClearHistory()
    {
        speakerHistory.Clear();
        turnQueue.Clear();
        currentSpeaker = "";
        totalTurns = 0;
        queuedRequests = 0;
        Debug.Log("🔄 Conversation history cleared");
    }
    
    /// <summary>
    /// Clear the turn queue (called when user sends new message to reset conversation)
    /// </summary>
    public void ClearQueue()
    {
        turnQueue.Clear();
        queuedRequests = 0;
        Debug.Log("🔄 Turn queue cleared");
    }
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

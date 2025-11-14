using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages NPC memory - conversation history that gets sent to the LLM
/// </summary>
[Serializable]
public class NPCMemory
{
    [Header("Conversation Memory")]
    [Tooltip("Number of recent conversation turns to remember")]
    public int shortTermCapacity = 10;
    
    private readonly List<DialogueTurn> shortTermMemory = new List<DialogueTurn>();
    
    /// <summary>
    /// Add a dialogue turn to memory
    /// </summary>
    public void AddDialogueTurn(string speaker, string message)
    {
        shortTermMemory.Add(new DialogueTurn
        {
            speaker = speaker,
            message = message,
            timestamp = Time.time
        });
        
        // Keep only recent turns
        while (shortTermMemory.Count > shortTermCapacity)
            shortTermMemory.RemoveAt(0);
    }
    
    /// <summary>
    /// Get conversation history as message array for LLM
    /// Converts dialogue turns to proper chat message format
    /// </summary>
    public List<OllamaChatClient.ChatMessage> GetConversationHistory()
    {
        var messages = new List<OllamaChatClient.ChatMessage>();
        
        foreach (var turn in shortTermMemory)
        {
            // Map speaker names to roles
            string role = turn.speaker == "User" ? "user" : "assistant";
            messages.Add(new OllamaChatClient.ChatMessage 
            { 
                role = role, 
                content = turn.message 
            });
        }
        
        return messages;
    }
    
    /// <summary>
    /// Get short-term memory as formatted string for UI display
    /// </summary>
    public string GetShortTermContext()
    {
        if (shortTermMemory.Count == 0) return "";
        
        var context = new System.Text.StringBuilder("Recent conversation:\n");
        foreach (var turn in shortTermMemory)
            context.AppendFormat("{0}: {1}\n", turn.speaker, turn.message);
        
        return context.ToString();
    }
    
    /// <summary>
    /// Get the most recent dialogue turn
    /// </summary>
    public DialogueTurn GetLastTurn()
    {
        return shortTermMemory.Count > 0 ? shortTermMemory[shortTermMemory.Count - 1] : null;
    }
    
    /// <summary>
    /// Clear all memory
    /// </summary>
    public void ClearAll()
    {
        shortTermMemory.Clear();
    }
    
    /// <summary>
    /// Get current memory count
    /// </summary>
    public int GetCount()
    {
        return shortTermMemory.Count;
    }
}

/// <summary>
/// Represents a single turn in dialogue
/// </summary>
[Serializable]
public class DialogueTurn
{
    public string speaker;
    public string message;
    public float timestamp;
}

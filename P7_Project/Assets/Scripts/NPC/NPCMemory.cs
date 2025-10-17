using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages NPC memory - both short-term dialogue and medium-term facts
/// </summary>
[Serializable]
public class NPCMemory
{
    [Header("Short-term Memory (Recent Dialogue)")]
    [Tooltip("Number of recent conversation turns to remember")]
    public int shortTermCapacity = 5;
    
    private readonly List<DialogueTurn> shortTermMemory = new List<DialogueTurn>();
    
    [Header("Medium-term Memory (Key Facts)")]
    [Tooltip("Maximum number of facts to remember")]
    public int mediumTermCapacity = 20;
    
    private readonly List<string> mediumTermMemory = new List<string>();
    
    /// <summary>
    /// Add a dialogue turn to short-term memory
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
    /// Add a key fact to medium-term memory
    /// </summary>
    public void AddFact(string fact)
    {
        if (string.IsNullOrEmpty(fact) || mediumTermMemory.Contains(fact)) 
            return;
        
        mediumTermMemory.Add(fact);
        
        // Keep within capacity
        while (mediumTermMemory.Count > mediumTermCapacity)
            mediumTermMemory.RemoveAt(0);
        
        Debug.Log($"💡 New fact learned: {fact}");
    }
    
    /// <summary>
    /// Get all facts as a list
    /// </summary>
    public List<string> GetAllFacts()
    {
        return new List<string>(mediumTermMemory);
    }
    
    /// <summary>
    /// Get short-term memory as formatted string for prompt
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
    /// Get medium-term memory as formatted string for prompt
    /// </summary>
    public string GetMediumTermContext()
    {
        return mediumTermMemory.Count == 0 ? "" : "Things you remember:\n- " + string.Join("\n- ", mediumTermMemory);
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
        mediumTermMemory.Clear();
    }
    
    /// <summary>
    /// Clear only short-term memory
    /// </summary>
    public void ClearShortTerm()
    {
        shortTermMemory.Clear();
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

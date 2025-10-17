using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attention state for NPC focus and engagement
/// </summary>
public enum AttentionState
{
    Idle,      // Neutral, no specific focus
    Focused,   // Actively paying attention
    Ignoring   // Actively disengaged or ignoring
}

/// <summary>
/// Metadata structure for NPC non-verbal actions and behaviors
/// </summary>
[Serializable]
public class NPCMetadata
{
    public string animatorTrigger = "";
    public bool isFocused = false;
    public bool isIgnoring = false;
    
    /// <summary>
    /// Parse JSON metadata from LLM response
    /// Format: {"animatorTrigger": "nod", "isFocused": true, "isIgnoring": false}
    /// </summary>
    public static NPCMetadata ParseFromJson(string json)
    {
        var metadata = new NPCMetadata();
        if (string.IsNullOrEmpty(json)) return metadata;
        
        metadata.animatorTrigger = ExtractStringValue(json, "animatorTrigger");
        metadata.isFocused = ExtractBoolValue(json, "isFocused");
        metadata.isIgnoring = ExtractBoolValue(json, "isIgnoring");
        
        return metadata;
    }
    
    private static string ExtractStringValue(string json, string key)
    {
        string searchKey = $"\"{key}\"";
        int keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return "";
        
        int colonPos = json.IndexOf(":", keyIndex);
        if (colonPos < 0) return "";
        
        int startQuote = json.IndexOf("\"", colonPos);
        if (startQuote < 0) return "";
        
        int endQuote = json.IndexOf("\"", startQuote + 1);
        return endQuote < 0 ? "" : json.Substring(startQuote + 1, endQuote - startQuote - 1);
    }
    
    private static bool ExtractBoolValue(string json, string key)
    {
        string searchKey = $"\"{key}\"";
        int keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return false;
        
        int colonPos = json.IndexOf(":", keyIndex);
        if (colonPos < 0) return false;
        
        int endPos = json.IndexOfAny(new[] { ',', '}' }, colonPos);
        if (endPos < 0) endPos = json.Length;
        
        string value = json.Substring(colonPos + 1, endPos - colonPos - 1).Trim();
        return value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Configuration for available animator triggers for an NPC
/// </summary>
[Serializable]
public class NPCAnimatorConfig
{
    [Header("Animator Reference")]
    public Animator animator;
    
    [Header("Available Animator Triggers")]
    [Tooltip("List of all valid animator trigger names that the LLM can use")]
    public List<string> availableTriggers = new List<string>
    {
        "nod",
        "shake_head",
        "lean_forward",
        "lean_back",
        "smile",
        "eye_roll",
        "idle"  // Neutral idle animation
    };
    
    [Header("Attention State Booleans")]
    [Tooltip("Animator boolean parameters for attention states")]
    public string focusedBoolParam = "IsFocused";
    public string ignoringBoolParam = "IsIgnoring";
    
    private AttentionState currentAttentionState = AttentionState.Idle;
    
    /// <summary>
    /// Execute an animator trigger if it's in the available list
    /// </summary>
    public bool TriggerAnimation(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName))
            return false;
        
        // Check if trigger is in the available list
        if (!availableTriggers.Contains(triggerName))
        {
            Debug.LogWarning($"Animator trigger '{triggerName}' not in available triggers list");
            return false;
        }
        
        // DEBUG: Just log the animation trigger instead of executing it
        Debug.Log($"🎭 [DEBUG] Animation trigger requested: {triggerName}");
        return true;
        
        // TODO: Uncomment when ready to use actual animator
        /*
        if (animator == null)
        {
            Debug.LogWarning("Animator not assigned!");
            return false;
        }
        
        animator.SetTrigger(triggerName);
        Debug.Log($"✓ Triggered animation: {triggerName}");
        return true;
        */
    }
    
    /// <summary>
    /// Set the attention state of the NPC (affects animator boolean parameters)
    /// </summary>
    public void SetAttentionState(AttentionState state)
    {
        if (currentAttentionState == state)
            return; // Already in this state
        
        currentAttentionState = state;
        
        // DEBUG: Log attention state changes
        Debug.Log($"👁️ [DEBUG] Attention state changed to: {state}");
        
        // TODO: Uncomment when ready to use actual animator
        /*
        if (animator == null)
            return;
        
        // Reset all attention booleans
        if (!string.IsNullOrEmpty(focusedBoolParam))
            animator.SetBool(focusedBoolParam, false);
        if (!string.IsNullOrEmpty(ignoringBoolParam))
            animator.SetBool(ignoringBoolParam, false);
        
        // Set the appropriate attention state
        switch (state)
        {
            case AttentionState.Focused:
                if (!string.IsNullOrEmpty(focusedBoolParam))
                    animator.SetBool(focusedBoolParam, true);
                break;
            case AttentionState.Ignoring:
                if (!string.IsNullOrEmpty(ignoringBoolParam))
                    animator.SetBool(ignoringBoolParam, true);
                break;
            case AttentionState.Idle:
                // Both booleans already set to false
                TriggerAnimation("idle");
                break;
        }
        */
    }
    
    /// <summary>
    /// Get a formatted string of available triggers for the LLM prompt
    /// </summary>
    public string GetTriggerListForPrompt()
    {
        if (availableTriggers.Count == 0)
            return "none";
        
        return string.Join(", ", availableTriggers);
    }
}

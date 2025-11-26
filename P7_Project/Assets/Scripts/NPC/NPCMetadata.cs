using System;
using System.Collections.Generic;
using UnityEngine;
// Optional: rigging constraint API. If your project doesn't include the
// Unity Rigging package this will cause a compile error; if so, remove the
// Rigging references or add the package via Package Manager.
using UnityEngine.Animations.Rigging;

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
    
    private const string MetadataOpenTag = "[META]";
    private const string MetadataCloseTag = "[/META]";
    
    /// <summary>
    /// Extract metadata and clean display text from LLM response
    /// </summary>
    public static (NPCMetadata metadata, string displayText) ProcessResponse(string response)
    {
        var metadata = new NPCMetadata();
        var displayText = new System.Text.StringBuilder();
        
        int metaStart = response.IndexOf(MetadataOpenTag);
        int metaEnd = response.IndexOf(MetadataCloseTag);
        
        if (metaStart >= 0 && metaEnd > metaStart)
        {
            int jsonStart = metaStart + MetadataOpenTag.Length;
            string json = response.Substring(jsonStart, metaEnd - jsonStart);
            metadata = ParseFromJson(json);
            
            // Add text before and after metadata
            displayText.Append(response.Substring(0, metaStart));
            displayText.Append(response.Substring(metaEnd + MetadataCloseTag.Length));
        }
        else
        {
            displayText.Append(response);
        }
        
        return (metadata, displayText.ToString());
    }
    
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
    
    [Header("Gaze Settings")]
    public Transform gazeOrigin;
    public Transform neutralLookTarget;
    public Transform ignoreLookTarget;
    // Gaze smoothing/rotation should be handled by Unity's Multi-Aim constraints.
    // Do not perform per-frame lerp/rotation in code — we only provide the target transform.
    
    [Header("Rigging (optional)")]
    [Tooltip("Optional Multi-Aim constraint that controls head/eyes. If assigned, TickGaze will adjust its source weights.")]
    public MultiAimConstraint multiAimConstraint;
    
    [Header("Runtime State")]
    [Tooltip("Set by external systems (TTS/audio) to indicate this NPC is speaking.")]
    public bool isSpeaking = false;
    
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
    private Transform currentLookTarget;
    private Transform speakerLookTarget;
    
    // Expose current look target for Multi-Aim constraints or other systems
    public Transform CurrentLookTargetTransform => currentLookTarget;

    /// <summary>
    /// External callers (TTS/audio system) can set this to indicate the NPC
    /// is currently speaking. TickGaze will prefer the primary target while
    /// speaking.
    /// </summary>
    public void SetSpeaking(bool speaking)
    {
        isSpeaking = speaking;
    }
    
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
        
        if (animator == null)
        {
            Debug.LogWarning("Animator not assigned!");
            return false;
        }
        
        animator.SetTrigger(triggerName);
        Debug.Log($"✓ Triggered animation: {triggerName}");
        return true;
    }
    
    /// <summary>
    /// Set the attention state of the NPC (affects animator boolean parameters)
    /// </summary>
    public void SetAttentionState(AttentionState state, bool immediate = false)
    {
        bool stateChanged = currentAttentionState != state;

        if (stateChanged)
        {
            currentAttentionState = state;
            Debug.Log($"👁️ Attention state changed to: {state}");
        }

        UpdateLookTarget(immediate || stateChanged);

        if (animator == null)
            return;

        // Reset all attention booleans
        if (!string.IsNullOrEmpty(focusedBoolParam))
            animator.SetBool(focusedBoolParam, false);
        if (!string.IsNullOrEmpty(ignoringBoolParam))
            animator.SetBool(ignoringBoolParam, false);

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
                TriggerAnimation("idle");
                break;
        }
    }

    public AttentionState CurrentAttentionState => currentAttentionState;
    public bool IsIgnoring => currentAttentionState == AttentionState.Ignoring;

    public void ApplyMetadata(NPCMetadata metadata, Transform speakerTarget)
    {
        if (metadata == null)
            return;

        if (speakerTarget != null)
            speakerLookTarget = speakerTarget;

        AttentionState nextState = AttentionState.Idle;
        if (metadata.isIgnoring)
            nextState = AttentionState.Ignoring;
        else if (metadata.isFocused)
            nextState = AttentionState.Focused;

    SetAttentionState(nextState);
    }

    public void SetSpeakerTarget(Transform target, bool immediate = false)
    {
        speakerLookTarget = target ?? neutralLookTarget;
        UpdateLookTarget(immediate);
    }

    public void TickGaze()
    {
        /*
        // Keep API compatibility — we don't drive transforms directly here.
        // If a Multi-Aim Constraint is assigned, update its source weights.
        if (multiAimConstraint == null)
            return;

        // Primary source (index 0) should be the look target (currentLookTarget).
        // Secondary source (index 1) is expected to be the other actor's head/root.
        // Compute simple weights based on speaking state and attention state.
        float primaryWeight = 0f;
        float secondaryWeight = 0f;

        if (isSpeaking)
        {
            // If this NPC is speaking, favor the primary target fully.
            primaryWeight = 1f;
            secondaryWeight = 0f;
        }
        else
        {
            primaryWeight = 0f;
            secondaryWeight = 1f;
        }

        var sources = multiAimConstraint.data.sourceObjects;
        if (sources.Count > 0)
        {
            // Set first source weight (target)
            sources.SetWeight(0, Mathf.Clamp01(primaryWeight));
        }
        if (sources.Count > 1)
        {
            // Set second source weight (other actor head/root)
            sources.SetWeight(1, Mathf.Clamp01(secondaryWeight));
        }

        // Reassign back in case the constraint needs the updated array.
        multiAimConstraint.data.sourceObjects = sources; */
    }

    private void UpdateLookTarget(bool immediate)
    {
        Transform desiredTarget = null;

        switch (currentAttentionState)
        {
            case AttentionState.Focused:
                desiredTarget = speakerLookTarget ?? neutralLookTarget;
                break;
            case AttentionState.Ignoring:
                desiredTarget = ignoreLookTarget ?? neutralLookTarget;
                break;
            default:
                desiredTarget = neutralLookTarget ?? speakerLookTarget;
                break;
        }

        // Assign the transform target. The actual aiming/rotation is expected
        // to be handled by Multi-Aim constraints that reference this transform.
        currentLookTarget = desiredTarget;

        // If immediate is requested, optionally snap gazeOrigin to face the
        // target once. This is a simple instantaneous LookAt (no smoothing).
        // Multi-Aim constraints usually handle pose changes, so this is
        // optional and kept minimal.
        if (immediate && gazeOrigin != null && currentLookTarget != null)
        {
            gazeOrigin.LookAt(currentLookTarget.position, Vector3.up);
        }
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

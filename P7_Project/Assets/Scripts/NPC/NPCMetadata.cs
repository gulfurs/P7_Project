using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attention state for NPC focus and engagement
/// These states control gaze direction and are NOT spoken by the NPC
/// </summary>
public enum AttentionState
{
    Idle,      // Neutral, no specific focus
    Focused,   // Actively paying attention (looking at speaker)
    Ignoring   // Actively disengaged or ignoring (looking away)
}

/// <summary>
/// Metadata structure for NPC non-verbal actions and behaviors
/// This data is extracted from LLM output and processed internally
/// IMPORTANT: None of this metadata is spoken - only used for animations and gaze
/// </summary>
[Serializable]
public class NPCMetadata
{
    [Tooltip("Animation trigger name (e.g., 'nod', 'shake_head', 'smile')")]
    public string animatorTrigger = "";
    
    [Tooltip("True = NPC is focused on speaker, False = neutral")]
    public bool isFocused = false;
    
    [Tooltip("True = NPC is ignoring/disengaged, False = neutral")]
    public bool isIgnoring = false;
    
    private const string MetadataOpenTag = "[META]";
    private const string MetadataCloseTag = "[/META]";

    /// <summary>
    /// Extract metadata and clean display text from LLM response.
    /// Enforces that metadata must appear at the very start of the response.
    /// Returns default metadata if parsing fails.
    /// 
    /// IMPORTANT: The metadata is for internal use only (animations/gaze).
    /// Only the cleaned display text should be shown/spoken to the user.
    /// </summary>
    public static (NPCMetadata metadata, string displayText) ProcessResponse(string response)
    {
        var metadata = new NPCMetadata();
        if (string.IsNullOrEmpty(response))
            return (metadata, string.Empty);

        response = response.TrimStart(); // allow leading whitespace

        // Metadata must be at the start (after optional whitespace)
        if (!response.StartsWith(MetadataOpenTag, StringComparison.Ordinal))
        {
            // No metadata block at start - return full response as display text
            return (metadata, response);
        }

        int metaEnd = response.IndexOf(MetadataCloseTag, StringComparison.Ordinal);
        if (metaEnd < 0)
        {
            // Closing tag not found - treat whole response as text
            Debug.LogWarning("[NPCMetadata] Missing closing [/META] tag in LLM response.");
            return (metadata, response);
        }

        int jsonStart = MetadataOpenTag.Length;
        string json = response.Substring(jsonStart, metaEnd - jsonStart).Trim();

        // Clean JSON: remove surrounding quotes and unescape if model escaped the JSON
        if (json.Length >= 2 && ((json.StartsWith("\"") && json.EndsWith("\"")) || (json.StartsWith("'" ) && json.EndsWith("'"))))
        {
            json = json.Substring(1, json.Length - 2);
            try { json = System.Text.RegularExpressions.Regex.Unescape(json); } catch { }
        }

        // Strip any backticks or code fences the model might include
        json = json.Trim('\n', '\r', ' ', '`');

        // Attempt to parse JSON using manual parsing first (more robust)
        try
        {
            metadata = ParseFromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NPCMetadata] Failed to parse metadata JSON manually: {e.Message}. JSON: '{json}'");
            // Fallback to Unity's JsonUtility
            try
            {
                var wrapper = JsonUtility.FromJson<NPCMetadataJsonWrapper>(json);
                if (wrapper != null)
                {
                    metadata.animatorTrigger = wrapper.animatorTrigger ?? string.Empty;
                    metadata.isFocused = wrapper.isFocused;
                    metadata.isIgnoring = wrapper.isIgnoring;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCMetadata] Failed to parse metadata JSON with JsonUtility: {ex.Message}. JSON: '{json}'");
            }
        }

        // The spoken text is whatever follows the closing tag
        string after = response.Substring(metaEnd + MetadataCloseTag.Length).TrimStart('\n', '\r', ' ');
        return (metadata, after);
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

    // Helper wrapper matching the expected JSON shape so we can use JsonUtility
    [Serializable]
    private class NPCMetadataJsonWrapper
    {
        public string animatorTrigger = "";
        public bool isFocused = false;
        public bool isIgnoring = false;
    }
    
    private static string ExtractStringValue(string json, string key)
    {
        // Try quoted key first
        string searchKey = $"\"{key}\"";
        int keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
        {
            // Try unquoted key
            searchKey = key;
            keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        }
        if (keyIndex < 0) return "";
        
        int colonPos = json.IndexOf(":", keyIndex);
        if (colonPos < 0) return "";
        
        int startQuote = json.IndexOf("\"", colonPos);
        if (startQuote >= 0)
        {
            // Quoted value
            int endQuote = json.IndexOf("\"", startQuote + 1);
            return endQuote < 0 ? "" : json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        else
        {
            // Unquoted value, find comma or }
            int endPos = json.IndexOfAny(new[] { ',', '}' }, colonPos);
            if (endPos < 0) endPos = json.Length;
            string value = json.Substring(colonPos + 1, endPos - colonPos - 1).Trim();
            // Remove quotes if present
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                value = value.Substring(1, value.Length - 2);
            return value;
        }
    }
    
    private static bool ExtractBoolValue(string json, string key)
    {
        // Try quoted key first
        string searchKey = $"\"{key}\"";
        int keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
        {
            // Try unquoted key
            searchKey = key;
            keyIndex = json.IndexOf(searchKey, System.StringComparison.OrdinalIgnoreCase);
        }
        if (keyIndex < 0) return false;
        
        int colonPos = json.IndexOf(":", keyIndex);
        if (colonPos < 0) return false;
        
        int endPos = json.IndexOfAny(new[] { ',', '}' }, colonPos);
        if (endPos < 0) endPos = json.Length;
        
        string value = json.Substring(colonPos + 1, endPos - colonPos - 1).Trim().ToLowerInvariant();
        // Remove quotes if present
        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            value = value.Substring(1, value.Length - 2);
        return value == "true";
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
    [Range(0.1f, 20f)]
    public float gazeLerpSpeed = 8f;
    
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
    
    /// <summary>
    /// Execute an animator trigger if it's in the available list
    /// </summary>
    public bool TriggerAnimation(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName))
            return false;
        // Warn if trigger not in the advertised list but continue (LLM may use synonyms)
        if (!availableTriggers.Contains(triggerName))
        {
            Debug.LogWarning($"Animator trigger '{triggerName}' not in available triggers list");
        }

        Debug.Log($"🎭 Animation trigger requested: {triggerName}");

        if (animator == null)
        {
            Debug.LogWarning("Animator not assigned!");
            return false;
        }

        try
        {
            // Prefer SetTrigger if the controller defines the trigger parameter
            var parameters = animator.parameters;
            foreach (var p in parameters)
            {
                if (p.name == triggerName && p.type == UnityEngine.AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(triggerName);
                    Debug.Log($"✓ Set animator trigger parameter: {triggerName}");
                    return true;
                }
            }

            // Fallback: attempt to play an animation state by name
            animator.Play(triggerName, 0);
            Debug.Log($"✓ Played animator state: {triggerName}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to trigger animator '{triggerName}': {e.Message}");
            return false;
        }
        
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
            Debug.Log($"👁️ [DEBUG] Attention state changed to: {state}");
        }

        UpdateLookTarget(immediate || stateChanged);
        // Apply animator boolean parameters if available
        if (animator != null)
        {
            try
            {
                if (!string.IsNullOrEmpty(focusedBoolParam))
                    animator.SetBool(focusedBoolParam, state == AttentionState.Focused);
                if (!string.IsNullOrEmpty(ignoringBoolParam))
                    animator.SetBool(ignoringBoolParam, state == AttentionState.Ignoring);

                if (state == AttentionState.Idle)
                {
                    // Try to play an idle trigger/state if available
                    if (availableTriggers.Contains("idle"))
                        TriggerAnimation("idle");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to set animator attention booleans: {e.Message}");
            }
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

    public void TickGaze(float deltaTime)
    {
        if (gazeOrigin == null || currentLookTarget == null)
            return;

        Vector3 toTarget = currentLookTarget.position - gazeOrigin.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        if (gazeLerpSpeed <= 0f)
            gazeOrigin.rotation = desiredRotation;
        else
            gazeOrigin.rotation = Quaternion.Slerp(gazeOrigin.rotation, desiredRotation, deltaTime * gazeLerpSpeed);
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

        if (desiredTarget == null)
        {
            currentLookTarget = null;
            return;
        }

        currentLookTarget = desiredTarget;

        if (immediate && gazeOrigin != null)
        {
            Vector3 toTarget = currentLookTarget.position - gazeOrigin.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                gazeOrigin.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }
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

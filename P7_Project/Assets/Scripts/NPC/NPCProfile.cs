using UnityEngine;
using System;

[Serializable]
public class NPCProfile
{
    [Header("Identity")]
    public string npcName;
    
    [Header("Role & Expertise")]
    [TextArea(2, 4)] public string role;
    [TextArea(2, 4)] public string expertise;
    
    [Header("Personality")]
    [TextArea(2, 4)] public string personalityTraits;
    
    [Header("LLM Parameters")]
    [Range(0.1f, 2.0f)] public float temperature = 0.7f;
    [Range(1.0f, 1.5f)] public float repeatPenalty = 1.1f;
    
    [Header("TTS Settings")]
    public string voiceName = "en_US-lessac-medium";
    public bool enableTTS = true;
    public AudioSource audioSource;
    
    [Header("Non-Verbal Behavior")]
    public NPCAnimatorConfig animatorConfig;
    public GameObject npcGameObject;
    
    /// <summary>
    /// Get a SHORT system prompt for quick responses (used for decision-making)
    /// </summary>
    public string GetShortSystemPrompt()
    {
        return $"You are {npcName}. Ask a brief interview question (under 30 words).";
    }
    
    /// <summary>
    /// Get the full system prompt for this interviewer NPC
    /// Core roleplaying instructions are defined at the LLM root level
    /// This adds NPC-specific personality and expertise
    /// </summary>
    public string GetFullSystemPrompt()
    {
        var prompt = new System.Text.StringBuilder();
        
        // Core identity
        prompt.AppendLine($"You are {npcName}.");
        prompt.AppendLine($"Role: {role}");
        
        if (!string.IsNullOrEmpty(expertise))
            prompt.AppendLine($"Expertise: {expertise}");
            
        if (!string.IsNullOrEmpty(personalityTraits))
            prompt.AppendLine($"Personality: {personalityTraits}");
        
        // Interview context (simplified - core rules come from LLMConfig)
        prompt.AppendLine("\n=== YOUR ROLE ===");
        prompt.AppendLine("You are conducting a multi-party job interview alongside a co-interviewer.");
        prompt.AppendLine("- Ask ONE focused question at a time (under 50 words)");
        prompt.AppendLine("- Stay in character as a professional interviewer");
        prompt.AppendLine("- Evaluate answers based on YOUR expertise");
        prompt.AppendLine("- Don't repeat what your co-interviewer just asked");
        
        // Non-verbal behavior instructions
        prompt.AppendLine("\n=== INTERNAL METADATA (NOT SPOKEN) ===");
        prompt.AppendLine("Your responses MUST start with metadata, then your spoken words:");
        prompt.AppendLine("Format: [META]{\"animatorTrigger\":\"action\",\"isFocused\":true/false,\"isIgnoring\":false/true}[/META] Your spoken words here.");
        
        if (animatorConfig != null && animatorConfig.availableTriggers.Count > 0)
        {
            prompt.AppendLine($"\nAvailable non-verbal actions: {animatorConfig.GetTriggerListForPrompt()}");
            prompt.AppendLine("• nod = agreement/encouragement, shake_head = skepticism/concern");
            prompt.AppendLine("• lean_forward = high interest, lean_back = evaluating critically");
            prompt.AppendLine("• smile = impressed/positive, eye_roll = weak response, idle = neutral");
        }
        
        prompt.AppendLine("\nAttention states (gaze direction - internal only):");
        prompt.AppendLine("• isFocused=true → Looking at speaker attentively");
        prompt.AppendLine("• isIgnoring=true → Looking away (weak/off-topic answer)");
        prompt.AppendLine("• Both false → Neutral listening posture");
        
        prompt.AppendLine("\nIMPORTANT: Only your spoken words after [/META] will be heard. Keep them natural and under 50 words.");

        return prompt.ToString();
    }
}

// MonoBehaviour that allows you to create NPC profiles in the inspector
public class NPCProfileHolder : MonoBehaviour
{
    public NPCProfile profile;
}

[CreateAssetMenu(fileName = "New NPC Profile", menuName = "NPC/NPC Profile")]
public class NPCProfileAsset : ScriptableObject
{
    public NPCProfile profile;
}
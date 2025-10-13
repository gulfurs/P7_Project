using UnityEngine;
using System;

[Serializable]
public class NPCProfile
{
    public string npcName;
    
    [TextArea(3, 6)]
    public string systemPrompt;
    
    [TextArea(3, 6)]
    public string contextPrompt;
    
    [TextArea(2, 4)]
    public string personalityTraits;
    
    // LLM parameters for this specific NPC
    [Range(0.1f, 2.0f)]
    public float temperature = 0.7f;
    
    [Range(1.0f, 1.5f)]
    public float repeatPenalty = 1.1f;
    
    [Header("TTS Settings")]
    public string voiceName = "en_US-lessac-medium"; // Piper voice model name
    public bool enableTTS = true;
    public AudioSource audioSource;
    
    [Header("Animator & Non-Verbal Actions")]
    public NPCAnimatorConfig animatorConfig;
    
    // Optional reference to the NPC's visual representation
    public GameObject npcGameObject;
    
    // Get the full system prompt combining all elements
    public string GetFullSystemPrompt()
    {
        string fullPrompt = "You are " + npcName + ". " + systemPrompt;
        
        if (!string.IsNullOrEmpty(contextPrompt))
            fullPrompt += "\n\nContext: " + contextPrompt;
            
        if (!string.IsNullOrEmpty(personalityTraits))
            fullPrompt += "\n\nPersonality: " + personalityTraits;
        
        // Add interview-specific guidelines
        fullPrompt += "\n\n=== INTERVIEW GUIDELINES ===";
        fullPrompt += "\n- Stay professional and realistic; speak like a human interviewer";
        fullPrompt += "\n- Ask one question at a time. Wait for their answer before moving on";
        fullPrompt += "\n- Keep answers under 50 words unless asking complex behavioral questions";
        fullPrompt += "\n- Use natural probing follow-ups like: “Can you elaborate?”, “What was the impact?”, “Why did you take that approach?";
        fullPrompt += "\n- Reference what the candidate or co-interviewer said naturally";
        fullPrompt += "\n- Always Stay in character, while always remaining professional and respectful";
        fullPrompt += "\n- Build on your co-interviewers questions, NEVER interrupt them. Add to their questions rather than repeating them";
        
        // Add non-verbal action metadata instructions
        fullPrompt += "\n\n=== NON-VERBAL ACTIONS ===";
        fullPrompt += "\nInclude JSON at START: [META]{\"animatorTrigger\":\"<trigger>\",\"isFocused\":true/false,\"isIgnoring\":true/false}[/META]";
        
        if (animatorConfig != null && animatorConfig.availableTriggers.Count > 0)
        {
            fullPrompt += "\nTriggers: " + animatorConfig.GetTriggerListForPrompt();
            fullPrompt += "\n  • nod=agree/encouraged, shake_head=concerned/skeptical";
            fullPrompt += "\n  • smile=impressed, eye_roll=red flag detected";
            fullPrompt += "\n  • lean_forward=interested, lean_back=evaluating";
            fullPrompt += "\n  • idle=neutral professional listening";
        }
        
        fullPrompt += "\nAttention States (Interview Context):";
        fullPrompt += "\n  • isFocused=true: Actively listening/evaluating";
        fullPrompt += "\n  • isIgnoring=true: Answer is weak/off-topic, losing interest";
        fullPrompt += "\n  • Both false: Neutral listening";
        
        fullPrompt += "\nInterview Examples:";
        fullPrompt += "\n  Good answer: [META]{\"animatorTrigger\":\"nod\",\"isFocused\":true,\"isIgnoring\":false}[/META]That's a solid example.";
        fullPrompt += "\n  Vague answer: [META]{\"animatorTrigger\":\"lean_forward\",\"isFocused\":true,\"isIgnoring\":false}[/META]Can you be more specific about your role?";
        fullPrompt += "\n  Red flag: [META]{\"animatorTrigger\":\"lean_back\",\"isFocused\":false,\"isIgnoring\":false}[/META]I see. Let's move on.";
            
        return fullPrompt;
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
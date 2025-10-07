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
        
        // Add conversation guidelines
        fullPrompt += "\n\nConversation Guidelines:";
        fullPrompt += "\n- Keep responses conversational and under 40 words";
        fullPrompt += "\n- Ask questions to other characters to keep the conversation flowing";
        fullPrompt += "\n- Reference what others have said when appropriate";
        fullPrompt += "\n- Stay in character and be engaging";
        
        // Add non-verbal action metadata instructions
        fullPrompt += "\n\n=== NON-VERBAL ACTIONS ===";
        fullPrompt += "\nInclude JSON at START: [META]{\"animatorTrigger\":\"<trigger>\",\"isFocused\":true/false,\"isIgnoring\":true/false}[/META]";
        
        if (animatorConfig != null && animatorConfig.availableTriggers.Count > 0)
        {
            fullPrompt += "\nTriggers: " + animatorConfig.GetTriggerListForPrompt();
            fullPrompt += "\n  • nod=agree, shake_head=disagree";
            fullPrompt += "\n  • smile=happy, eye_roll=dismissive";
            fullPrompt += "\n  • lean_forward=engaged, lean_back=disengaged";
            fullPrompt += "\n  • idle=neutral/casual stance";
        }
        
        fullPrompt += "\nAttention States:";
        fullPrompt += "\n  • isFocused=true: Actively paying attention, engaged";
        fullPrompt += "\n  • isIgnoring=true: Disengaged, not interested";
        fullPrompt += "\n  • Both false: Neutral/idle state";
        
        fullPrompt += "\nExample: [META]{\"animatorTrigger\":\"nod\",\"isFocused\":true,\"isIgnoring\":false}[/META]Hello there!";
            
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
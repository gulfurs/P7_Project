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
        
        fullPrompt += "\n\n=== INTERVIEW DYNAMICS ===";
        fullPrompt += "\nMulti-party interview: You, your co-interviewer, candidate.";
        fullPrompt += "\n- Stay professional and realistic; speak like a human interviewer";
        fullPrompt += "\n- ONE question at a time, keep under 50 words";
        fullPrompt += "\n- Use YOUR expertise to decide topic relevance";
        fullPrompt += "\n- Always Stay in character, while always remaining professional and respectful";
        fullPrompt += "\n- Build naturally, don't repeat co-interviewer";
        
        fullPrompt += "\n\n=== NONVERBAL REACTIONS ===";
        fullPrompt += "\nALWAYS include JSON at START: [META]{\"animatorTrigger\":\"<trigger>\",\"isFocused\":true/false,\"isIgnoring\":true/false}[/META]";

        if (animatorConfig != null && animatorConfig.availableTriggers.Count > 0)
        {
            fullPrompt += "\nActions: " + animatorConfig.GetTriggerListForPrompt();
            fullPrompt += "\n- 'nod'=agree, 'shake_head'=skeptical, 'smile'=impressed";
            fullPrompt += "\n- 'lean_forward'=interested, 'lean_back'=evaluating, 'idle'=neutral";
            fullPrompt += "\nisFocused=true if relevant, false if weak/off-topic";
            fullPrompt += "\n- When isFocused=true, maintain eye contact with whoever is currently speaking";
            fullPrompt += "\n- When isIgnoring=true, glance away or focus on notes instead of the speaker";
        }
        
        fullPrompt += "\nAttention States (Interview Context):";
        fullPrompt += "\n  • isFocused=true: Actively listening/evaluating";
        fullPrompt += "\n  • isIgnoring=true: Answer is weak/off-topic, losing interest";
        fullPrompt += "\n  • Both false: Neutral listening";

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
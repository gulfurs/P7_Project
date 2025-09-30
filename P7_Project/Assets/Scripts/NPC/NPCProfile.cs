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
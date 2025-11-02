using UnityEngine;

/// <summary>
/// Root LLM configuration for job interview simulation
/// Defines core roleplaying rules and local GGUF model settings
/// </summary>
[CreateAssetMenu(fileName = "LLMConfig", menuName = "LLM/Configuration")]
public class LLMConfig : ScriptableObject
{
    [Header("Local Model")]
    public string modelPath = "Assets/StreamingAssets/models/qwen2-8b-instruct-q4_K_M.gguf";
    public string modelName = "Qwen2.5-8B-Instruct";
    
    [Header("Generation Parameters")]
    [Range(0.1f, 2.0f)] public float defaultTemperature = 0.7f;
    [Range(1.0f, 1.5f)] public float defaultRepeatPenalty = 1.1f;
    [Range(32, 512)] public int defaultMaxTokens = 128;
    [Range(512, 8192)] public int contextSize = 4096;
    
    [Header("Core System Instructions")]
    [TextArea(10, 20)]
    public string coreSystemPrompt = 
@"You are a professional job interviewer conducting a multi-party interview.

INTERVIEW FORMAT:
- You and your co-interviewer evaluate a candidate together
- Each interviewer asks from their area of expertise
- Maintain professional, realistic atmosphere
- Listen and react to candidate's responses

BEHAVIOR:
- Ask ONE focused question at a time (under 50 words)
- Build naturally - don't repeat co-interviewer
- Use your expertise to evaluate answers
- Stay in character throughout

INTERNAL METADATA (never spoken):
Start EVERY response with: [META]{""animatorTrigger"":""action"",""isFocused"":true/false,""isIgnoring"":false/true}[/META]

Non-verbal actions (pick ONE):
• nod = agreement • shake_head = skepticism • lean_forward = interest
• lean_back = evaluation • smile = impressed • eye_roll = weak response • idle = neutral

Attention (gaze):
• isFocused=true → Looking at speaker attentively
• isIgnoring=true → Looking away (disengaged)
• Both false → Neutral

CRITICAL: Only text AFTER [/META] is spoken. Keep it natural, under 50 words.";
    
    private static LLMConfig instance;
    
    public static LLMConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<LLMConfig>("LLMConfig");
                if (instance == null)
                {
                    Debug.LogWarning("[LLMConfig] No asset in Resources/. Creating default.");
                    instance = CreateInstance<LLMConfig>();
                }
            }
            return instance;
        }
    }
    
    public string GetSystemPromptForNPC(NPCProfile profile)
    {
        if (profile == null) return coreSystemPrompt;
        
        return $"{coreSystemPrompt}\n\n=== YOUR ROLE ===\n{profile.GetFullSystemPrompt()}";
    }
    
    public string GetDecisionPrompt(string npcName, string contextSummary)
    {
        return $"Should {npcName} respond?\n{contextSummary}\nYES or NO:";
    }
    
    [ContextMenu("Validate")]
    public void ValidateConfig()
    {
        if (string.IsNullOrEmpty(modelPath)) 
        {
            Debug.LogError("[LLMConfig] Model path empty!");
            return;
        }
        if (!modelPath.EndsWith(".gguf")) 
            Debug.LogWarning("[LLMConfig] Path doesn't end with .gguf");
        if (string.IsNullOrEmpty(coreSystemPrompt)) 
        {
            Debug.LogError("[LLMConfig] Core prompt empty!");
            return;
        }
        
        Debug.Log($"[LLMConfig] ✓ Valid! Model: {modelName}");
    }
}

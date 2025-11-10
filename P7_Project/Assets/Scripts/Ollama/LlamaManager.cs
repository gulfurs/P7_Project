using UnityEngine;

/// <summary>
/// Coordinator for LlamaBridge + shared memory
/// Optional - NPCs can use LlamaBridge directly
/// </summary>
public class LlamaManager : MonoBehaviour
{
    public LlamaBridge bridge;
    public LlamaMemory memory;
    public string modelPath = "";

    private void Start()
    {
        if (!bridge) { Debug.LogError("[LlamaManager] No LlamaBridge!"); return; }
        if (!memory) memory = LlamaMemory.Instance;

        // Only use LlamaBridge if in LocalGGUF mode
        if (LLMConfig.Instance != null && !LLMConfig.Instance.IsLocalMode)
        {
            Debug.Log("[LlamaManager] Skipping LlamaBridge setup - not in LocalGGUF mode");
            return;
        }

        // Don't initialize here if LlamaBridge will initialize itself
        // LlamaBridge.Start() already calls Initialize()
        if (string.IsNullOrEmpty(bridge.modelPath))
        {
            if (string.IsNullOrEmpty(modelPath) && LLMConfig.Instance != null)
                modelPath = LLMConfig.Instance.modelPath;
            
            bridge.modelPath = modelPath;
            // Bridge will initialize itself in its Start() method
        }
    }

    public void SendPrompt(string userPrompt)
    {
        if (!bridge || !memory) return;

        memory.AddDialogueTurn("User", userPrompt);
        string context = memory.GetFullConversation();
        
        var config = LLMConfig.Instance;
        if (config == null)
        {
            Debug.LogError("[LlamaManager] LLMConfig not found!");
            return;
        }

        string reply = bridge.GenerateText(context, config.defaultTemperature, config.defaultRepeatPenalty, config.defaultMaxTokens);
        
        memory.AddDialogueTurn("Assistant", reply);
    }
}

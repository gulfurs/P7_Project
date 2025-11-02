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

        if (string.IsNullOrEmpty(modelPath))
            modelPath = LLMConfig.Instance.modelPath;
        
        bridge.modelPath = modelPath;
        bridge.Initialize();
    }

    public void SendPrompt(string userPrompt)
    {
        if (!bridge || !memory) return;

        memory.AddDialogueTurn("User", userPrompt);
        string context = memory.GetFullConversation();
        
        var config = LLMConfig.Instance;
        string reply = bridge.GenerateText(context, config.defaultTemperature, config.defaultRepeatPenalty, config.defaultMaxTokens);
        
        memory.AddDialogueTurn("Assistant", reply);
    }
}

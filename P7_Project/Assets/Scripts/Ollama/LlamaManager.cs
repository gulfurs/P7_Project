using UnityEngine;

public class LlamaManager : MonoBehaviour
{
    [Header(" LLaMA Components")]
    public LlamaBridge bridge;

    [Header(" Model Path")]
    public string modelPath = "Assets/StreamingAssets/models/llama2888.gguf";

    public LlamaMemory memory;

    private void Start()
    {
        if (bridge == null)
        {
            Debug.LogError("[LLaMA Manager] Missing LlamaBridge!");
            return;
        }

        bridge.modelPath = modelPath;
        bridge.Initialize();

        Debug.Log("[LLaMA Manager] Model initialized, ready for prompts...");
    }

    //  This function lets Whisper send messages.
    public void SendPrompt(string userPrompt)
    {
        if (bridge == null)
        {
            Debug.LogError("[LLaMA Manager] Missing bridge reference!");
            return;
        }

        if (memory == null)
        {
            Debug.LogError("[LLaMA Manager] Missing memory reference!");
            return;
        }

    //  Add user message to shared memory
    memory.AddDialogueTurn("User", userPrompt);

    //  Build full prompt from memory (assumes "Assistant" is registered NPC name)
    string fullPrompt = memory.GetFullConversation();

    Debug.Log($"[LLaMA Manager] Sending conversation:\n{fullPrompt}");

    // Generate reply using the explicit overload so we don't rely on bridge.prompt
    string reply = bridge.GenerateText(fullPrompt, 0.7f, 1.1f, 256).Trim();
    Debug.Log($"[LLaMA Manager] Assistant replied:\n{reply}");

    // Store reply in shared memory
    memory.AddDialogueTurn("Assistant", reply);
    }


    private void OnDestroy()
    {
        Debug.Log("[LLaMA Manager] Destroyed, cleaning up resources...");
    }
}

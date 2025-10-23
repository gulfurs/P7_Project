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

        //  Add user message
        memory.AddUserMessage(userPrompt);

        //  Build full prompt from memory
        string fullPrompt = memory.GetFullConversation();
        bridge.prompt = fullPrompt;

        Debug.Log($"[LLaMA Manager] Sending conversation:\n{fullPrompt}");

        //  Creates model reply
        bridge.GenerateText();

        string reply = bridge.generatedText.Trim();
        Debug.Log($"[LLaMA Manager] Assistant replied:\n{reply}");

        // Store reply in memory
        memory.AddAssistantMessage(reply);
    }


    private void OnDestroy()
    {
        Debug.Log("[LLaMA Manager] Destroyed, cleaning up resources...");
    }
}

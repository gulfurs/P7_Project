using System.Text;
using UnityEngine;

public class LlamaMemory : MonoBehaviour
{
    [Header("Conversation Memory")]
    [TextArea(5, 10)]
    public string conversationPreview;

    private StringBuilder memory = new StringBuilder();
    private const int maxMemoryLength = 8000; // prevent overflow

    [Tooltip("A short instruction to define how the assistant should behave.")]
    public string systemPrompt = "System: You are a helpful and concise assistant.";

    void Awake()
    {
        memory.AppendLine(systemPrompt);
    }

    /// <summary>
    /// Adds a new user message to memory.
    /// </summary>
    public void AddUserMessage(string userInput)
    {
        memory.AppendLine($"User: {userInput}");
        memory.AppendLine("Assistant:");
        UpdatePreview();
    }

    /// <summary>
    /// Adds the assistant's reply to memory.
    /// </summary>
    public void AddAssistantMessage(string reply)
    {
        memory.AppendLine($"Assistant: {reply}");
        UpdatePreview();

        // Trim memory if it gets too long
        if (memory.Length > maxMemoryLength)
        {
            int cutIndex = memory.Length / 2;
            memory.Remove(0, cutIndex);
            memory.Insert(0, systemPrompt + "\n");
        }
    }

    /// <summary>
    /// Returns the full conversation for LLaMA prompt input.
    /// </summary>
    public string GetFullConversation()
    {
        return memory.ToString();
    }

    private void UpdatePreview()
    {
        conversationPreview = memory.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;

public static class LlamaBridge
{
    private const string DLL_NAME = "llama_unity";

    // Initialise model + internal state (returns true on success)
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool llama_init_from_file(
        [MarshalAs(UnmanagedType.LPStr)] string modelPath);

    // Start a new streamed generation for this prompt
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool llama_generate_stream_begin(
        [MarshalAs(UnmanagedType.LPStr)] string prompt);

    // Get next chunk of text. Returns IntPtr.Zero when done.
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr llama_generate_stream_next();

    // Free all native resources
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void llama_free_all();
}

public class LlamaController : MonoBehaviour
{
    [Header("Model Settings")]
    public string modelFileName = "llama3.gguf";

    [TextArea(4, 8)]
    public string systemPrompt = "You are a helpful assistant.";

    [TextArea(4, 14)]
    public string assistantPreview;

    private bool initialized;

    private void Start()
    {
        string path = System.IO.Path.Combine(
            Application.streamingAssetsPath, "models", modelFileName);

        if (!System.IO.File.Exists(path))
        {
            Debug.LogError($"[LLaMA] Model not found at: {path}");
            return;
        }

        if (!LlamaBridge.llama_init_from_file(path))
        {
            Debug.LogError("[LLaMA] llama_init_from_file failed.");
            return;
        }

        initialized = true;

        Debug.Log("[LLaMA] Model initialized.");
    }

    /// <summary>
    /// Generate reply from a list of chat messages (stateless - no internal memory)
    /// </summary>
    public string GenerateReply(List<OllamaChatClient.ChatMessage> messages)
    {
        if (!initialized)
        {
            Debug.LogError("[LLaMA] Not initialized.");
            return "[Error: Not initialized]";
        }

        if (messages == null || messages.Count == 0)
        {
            Debug.LogWarning("[LLaMA] No messages provided.");
            return "[Error: No messages]";
        }

        // Build prompt from messages
        var promptBuilder = new StringBuilder();
        
        // Add system prompt
        promptBuilder.AppendLine("<|system|>");
        promptBuilder.AppendLine(systemPrompt);
        promptBuilder.AppendLine();
        
        // Add conversation history
        foreach (var msg in messages)
        {
            if (msg.role == "system")
            {
                // Override default system prompt if provided
                promptBuilder.Clear();
                promptBuilder.AppendLine("<|system|>");
                promptBuilder.AppendLine(msg.content);
                promptBuilder.AppendLine();
            }
            else if (msg.role == "user")
            {
                promptBuilder.AppendLine("<|user|>");
                promptBuilder.AppendLine(msg.content.Trim());
                promptBuilder.AppendLine();
            }
            else if (msg.role == "assistant")
            {
                promptBuilder.AppendLine("<|assistant|>");
                promptBuilder.AppendLine(msg.content.Trim());
                promptBuilder.AppendLine();
            }
        }

        string fullPrompt = promptBuilder.ToString();
        Debug.Log($"[LLaMA] Sending prompt ({fullPrompt.Length} chars)");

        if (!LlamaBridge.llama_generate_stream_begin(fullPrompt))
        {
            Debug.LogError("[LLaMA] llama_generate_stream_begin FAILED");
            return "[Error: Generation failed]";
        }

        StringBuilder response = new StringBuilder();

        while (true)
        {
            IntPtr ptr = LlamaBridge.llama_generate_stream_next();
            if (ptr == IntPtr.Zero) break;

            string token = Marshal.PtrToStringAnsi(ptr);
            if (string.IsNullOrEmpty(token)) break;

            response.Append(token);
        }

        string result = response.ToString().Trim();
        if (string.IsNullOrEmpty(result))
            result = "[Error: decode failed]";

        assistantPreview = result;
        Debug.Log($"[LLaMA] Assistant: {result}");
        
        return result;
    }

    private void OnDestroy()
    {
        if (initialized)
        {
            LlamaBridge.llama_free_all();
            Debug.Log("[LLaMA] Freed model + context.");
        }
    }
}

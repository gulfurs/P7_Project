using System;
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

    // Sliding-window memory (keeps stable context)
    private const int MAX_CONTEXT_CHARS = 28001;
    private const int MIN_CONTEXT_AFTER_TRIM = 18001;

    private readonly StringBuilder conversation = new StringBuilder();

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

        conversation.AppendLine("<|system|>");
        conversation.AppendLine(systemPrompt);
        conversation.AppendLine();

        Debug.Log("[LLaMA] Model initialized.");
    }

    // Called by WhisperContinuous
    public void GenerateReply(string userMessage)
    {
        if (!initialized)
        {
            Debug.LogError("[LLaMA] Not initialized.");
            return;
        }

        if (string.IsNullOrWhiteSpace(userMessage))
            return;

        AddUserMessage(userMessage);
        TrimMemory();

        string fullPrompt = conversation.ToString();
        Debug.Log($"[LLaMA] Sending prompt:\n{fullPrompt}");

        if (!LlamaBridge.llama_generate_stream_begin(fullPrompt))
        {
            Debug.LogError("[LLaMA] llama_generate_stream_begin FAILED");
            return;
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

        AddAssistantMessage(result);
        assistantPreview = result;

        Debug.Log($"[LLaMA] Assistant: {result}");
    }

    private void AddUserMessage(string text)
    {
        conversation.AppendLine("<|user|>");
        conversation.AppendLine(text.Trim());
        conversation.AppendLine();
    }

    private void AddAssistantMessage(string text)
    {
        conversation.AppendLine("<|assistant|>");
        conversation.AppendLine(text.Trim());
        conversation.AppendLine();
    }

    private void TrimMemory()
    {
        if (conversation.Length <= MAX_CONTEXT_CHARS)
            return;

        string full = conversation.ToString();
        int start = Math.Max(0, full.Length - MIN_CONTEXT_AFTER_TRIM);

        int safeStart = full.IndexOf("<|user|>", start, StringComparison.Ordinal);
        if (safeStart == -1)
            safeStart = full.IndexOf("<|assistant|>", start, StringComparison.Ordinal);

        if (safeStart == -1)
            safeStart = start;

        string systemBlock =
            "<|system|>\n" + systemPrompt + "\n\n";

        string tail = full.Substring(safeStart);

        conversation.Clear();
        conversation.Append(systemBlock);
        conversation.Append(tail);

        Debug.Log($"[LLaMA] Memory trimmed ? {conversation.Length} chars");
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

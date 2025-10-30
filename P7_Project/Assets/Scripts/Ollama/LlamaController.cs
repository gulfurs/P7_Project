using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;

public class LlamaController : MonoBehaviour
{
    [Header("Model Settings")]
    public string modelFileName = "llama3.gguf";
    [TextArea(2, 5)] public string systemPrompt = "System: You are a helpful assistant.";
    [TextArea(5, 10)] public string userPrompt = "Hello, who are you?";
    [TextArea(5, 10)] public string responsePreview;

    private StringBuilder memory = new StringBuilder();
    private const int MAX_MEMORY = 8000;

    private IntPtr ctx;
    private const string DLL_NAME = "llama_unity";

    // --- Native Imports ---
    [DllImport(DLL_NAME, EntryPoint = "llama_init_from_file", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_init_from_file([MarshalAs(UnmanagedType.LPStr)] string modelPath);

    [DllImport(DLL_NAME, EntryPoint = "llama_free_context", CallingConvention = CallingConvention.Cdecl)]
    private static extern void llama_free_context(IntPtr ctx);

    [DllImport(DLL_NAME, EntryPoint = "llama_generate", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_generate(IntPtr ctx, [MarshalAs(UnmanagedType.LPStr)] string prompt);

    void Start()
    {
        memory.AppendLine(systemPrompt);

        string modelPath = Path.Combine(Application.streamingAssetsPath, "models", modelFileName);

        if (!File.Exists(modelPath))
        {
            Debug.LogError($"[LLaMA] Model file not found: {modelPath}");
            return;
        }

        // Initialize model safely
        //ctx = llama_init_from_file(modelPath);
       // if (ctx == IntPtr.Zero)
       // {
       //     Debug.LogError("[LLaMA] Failed to initialize model. Context is null. Check if the model is valid GGUF and matches your llama_unity build.");
       //     return;
       // }

        Debug.Log("[LLaMA] Model initialized successfully!");

        // Generate an immediate test reply
       // GenerateReply(userPrompt);
    }

    public void GenerateReply(string input)
    {
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] Model not initialized.");
            return;
        }

        AddUserMessage(input);
        string fullPrompt = memory.ToString();

        Debug.Log($"[LLaMA] Generating response for:\n{fullPrompt}");

        IntPtr resultPtr = llama_generate(ctx, fullPrompt);
        if (resultPtr == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] llama_generate returned null pointer!");
            return;
        }

        string result = Marshal.PtrToStringAnsi(resultPtr);
        result ??= "[Empty response]";

        AddAssistantMessage(result);
        responsePreview = result;

        Debug.Log($"[LLaMA] Assistant:\n{result}");
    }

    private void AddUserMessage(string msg)
    {
        memory.AppendLine($"User: {msg}");
        memory.AppendLine("Assistant:");
        TrimMemory();
    }

    private void AddAssistantMessage(string msg)
    {
        memory.AppendLine($"Assistant: {msg}");
        TrimMemory();
    }

    private void TrimMemory()
    {
        if (memory.Length > MAX_MEMORY)
        {
            int cutIndex = memory.Length / 2;
            memory.Remove(0, cutIndex);
            memory.Insert(0, systemPrompt + "\n");
        }
    }

    void OnDestroy()
    {
        if (ctx != IntPtr.Zero)
        {
            llama_free_context(ctx);
            ctx = IntPtr.Zero;
            Debug.Log("[LLaMA] Freed context.");
        }
    }
}

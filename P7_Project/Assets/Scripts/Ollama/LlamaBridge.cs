using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class LlamaBridge : MonoBehaviour
{
    [Header("LLaMA Settings")]
    public string modelPath = "Assets/StreamingAssets/models/llama3.gguf";
    [TextArea(2, 5)] public string prompt = "Hello, who are you?";
    [TextArea(5, 10)] public string generatedText;

    private IntPtr ctx;
    private const string DLL_NAME = "llama_unity";

    [DllImport(DLL_NAME, EntryPoint = "llama_init_from_file", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_init_from_file([MarshalAs(UnmanagedType.LPStr)] string modelPath);

    [DllImport(DLL_NAME, EntryPoint = "llama_free_context", CallingConvention = CallingConvention.Cdecl)]
    private static extern void llama_free_context(IntPtr ctx);

    [DllImport(DLL_NAME, EntryPoint = "llama_generate", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_generate(IntPtr ctx, [MarshalAs(UnmanagedType.LPStr)] string prompt);

    private void Start()
    {
        Initialize();

        
       // StartCoroutine(GenerateAfterDelay(5.5f));
    }

    private System.Collections.IEnumerator GenerateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GenerateText();
    }

    public void Initialize()
    {
        Debug.Log($"[LLaMA] Initializing model from path: {modelPath}");

        ctx = llama_init_from_file(modelPath);
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] Failed to initialize model. Context is null.");
            return;
        }

        Debug.Log("[LLaMA] Model loaded successfully!");
    }

    public void GenerateText()
    {
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] Model not initialized. Call Initialize() first.");
            return;
        }

        Debug.Log($"[LLaMA] Sending prompt: {prompt}");
        IntPtr resultPtr = llama_generate(ctx, prompt);

        if (resultPtr == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] llama_generate returned null pointer!");
            return;
        }

        string result = Marshal.PtrToStringAnsi(resultPtr);
        generatedText = result ?? "[Empty response]";
        Debug.Log($"[LLaMA] Output: {generatedText}");
    }

    private void OnDestroy()
    {
        if (ctx != IntPtr.Zero)
        {
            llama_free_context(ctx);
            ctx = IntPtr.Zero;
            Debug.Log("[LLaMA] Freed context.");
        }
    }
}

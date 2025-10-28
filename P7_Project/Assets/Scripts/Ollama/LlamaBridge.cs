using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class LlamaBridge : MonoBehaviour
{
    public static LlamaBridge Instance { get; private set; }

    [Header("LLaMA Settings")]
    public string modelPath = "Assets/StreamingAssets/models/llama2888.gguf";
    [TextArea(5, 10)]
    public string generatedText;

    private IntPtr ctx;
    private const string DLL_NAME = "llama_unity";
    // Queue to serialize requests to the native model to avoid concurrent calls
    private System.Collections.Generic.Queue<Request> requestQueue = new System.Collections.Generic.Queue<Request>();
    private bool isProcessingQueue = false;

    private class Request
    {
        public string prompt;
        public float temperature;
        public float repeatPenalty;
        public int maxTokens;
        public Action<string> callback;
    }

    [DllImport(DLL_NAME, EntryPoint = "llama_init_from_file", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_init_from_file([MarshalAs(UnmanagedType.LPStr)] string modelPath);

    [DllImport(DLL_NAME, EntryPoint = "llama_free_context", CallingConvention = CallingConvention.Cdecl)]
    private static extern void llama_free_context(IntPtr ctx);

    [DllImport(DLL_NAME, EntryPoint = "llama_generate", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_generate(IntPtr ctx, [MarshalAs(UnmanagedType.LPStr)] string prompt);

    private void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Initialize();

        
       // StartCoroutine(GenerateAfterDelay(5.5f));
    }

    // (Removed legacy delayed generation helper to keep API simple)

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

    // Legacy parameterless GenerateText removed. Use GenerateText(customPrompt, ...) instead.

    /// <summary>
    /// Generate text with custom prompt and parameters
    /// </summary>
    public string GenerateText(string customPrompt, float temperature = 0.7f, float repeatPenalty = 1.1f, int maxTokens = 256)
    {
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] Model not initialized. Call Initialize() first.");
            return "";
        }

        // Log the full prompt being sent (first 500 chars for readability)
        string promptPreview = customPrompt.Length > 500 ? customPrompt.Substring(0, 500) + "...[truncated]" : customPrompt;
        Debug.Log($"[LLaMA] Full prompt being sent:\n{promptPreview}");
        Debug.Log($"[LLaMA] Temperature: {temperature}, Repeat Penalty: {repeatPenalty}, Max Tokens: {maxTokens}");
        
    IntPtr resultPtr = llama_generate(ctx, customPrompt);

        if (resultPtr == IntPtr.Zero)
        {
            Debug.LogError("[LLaMA] llama_generate returned null pointer!");
            return "";
        }

        string result = Marshal.PtrToStringAnsi(resultPtr);
        generatedText = result ?? "[Empty response]";
        Debug.Log($"[LLaMA] Raw output from model:\n{generatedText}");
        return generatedText;
    }

    /// <summary>
    /// Enqueue a prompt and process it on a single queue. Callback is invoked when the response is ready.
    /// Use this from other coroutines to avoid concurrent llama_generate calls.
    /// </summary>
    public void EnqueueGenerate(string customPrompt, float temperature, float repeatPenalty, int maxTokens, Action<string> callback)
    {
        var req = new Request
        {
            prompt = customPrompt,
            temperature = temperature,
            repeatPenalty = repeatPenalty,
            maxTokens = maxTokens,
            callback = callback
        };

        requestQueue.Enqueue(req);
        if (!isProcessingQueue)
            StartCoroutine(ProcessQueue());
    }

    private System.Collections.IEnumerator ProcessQueue()
    {
        isProcessingQueue = true;
        while (requestQueue.Count > 0)
        {
            var req = requestQueue.Dequeue();
            // Synchronous call to native model (may block) but ensures no concurrent calls
            string result = GenerateText(req.prompt, req.temperature, req.repeatPenalty, req.maxTokens);
            try
            {
                req.callback?.Invoke(result ?? string.Empty);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LLaMA] Callback threw exception: {e.Message}");
            }

            // allow a frame between requests to keep UI responsive
            yield return null;
        }
        isProcessingQueue = false;
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

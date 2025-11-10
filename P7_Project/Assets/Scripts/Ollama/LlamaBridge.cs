using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Native DLL bridge for local GGUF model inference
/// Thread-safe generation queue for Unity integration
/// </summary>
public class LlamaBridge : MonoBehaviour
{
    public static LlamaBridge Instance { get; private set; }

    [Header("Model Configuration")]
    public string modelPath = "";
    
    [Header("Debug")]
    [TextArea(5, 10)] public string generatedText;

    private IntPtr ctx;
    private const string DLL_NAME = "llama_unity";
    private System.Collections.Generic.Queue<Request> requestQueue = new System.Collections.Generic.Queue<Request>();
    private bool isProcessing = false;

    private class Request
    {
        public string prompt;
        public float temperature;
        public float repeatPenalty;
        public int maxTokens;
        public Action<string> callback;
    }

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_init_from_file([MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void llama_free_context(IntPtr ctx);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_generate(IntPtr ctx, [MarshalAs(UnmanagedType.LPStr)] string prompt);

    private void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Only initialize local GGUF mode
        if (LLMConfig.Instance != null && !LLMConfig.Instance.IsLocalMode)
        {
            Debug.Log("[LlamaBridge] Skipping initialization - not in LocalGGUF mode");
            return;
        }

        if (string.IsNullOrEmpty(modelPath) && LLMConfig.Instance != null)
            modelPath = LLMConfig.Instance.modelPath;
        
        // Only initialize if not already initialized
        if (ctx == IntPtr.Zero)
            Initialize();
    }

    public void Initialize()
    {
        // Prevent double initialization
        if (ctx != IntPtr.Zero)
        {
            Debug.LogWarning("[LlamaBridge] Already initialized, skipping.");
            return;
        }

        ctx = llama_init_from_file(modelPath);
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError($"[LlamaBridge] Failed to load model: {modelPath}");
            return;
        }
        
        string modelName = LLMConfig.Instance != null ? LLMConfig.Instance.modelName : "Unknown";
        Debug.Log($"[LlamaBridge] ✓ Loaded: {modelName}");
    }

    public string GenerateText(string prompt, float temperature = 0.7f, float repeatPenalty = 1.1f, int maxTokens = 256)
    {
        if (ctx == IntPtr.Zero)
        {
            Debug.LogError("[LlamaBridge] Model not initialized");
            return "";
        }

        IntPtr resultPtr = llama_generate(ctx, prompt);
        if (resultPtr == IntPtr.Zero) return "";

        generatedText = Marshal.PtrToStringAnsi(resultPtr) ?? "";
        return generatedText;
    }

    public void EnqueueGenerate(string prompt, float temp, float penalty, int maxTokens, Action<string> callback)
    {
        requestQueue.Enqueue(new Request
        {
            prompt = prompt,
            temperature = temp,
            repeatPenalty = penalty,
            maxTokens = maxTokens,
            callback = callback
        });

        if (!isProcessing)
            StartCoroutine(ProcessQueue());
    }

    private System.Collections.IEnumerator ProcessQueue()
    {
        isProcessing = true;
        
        while (requestQueue.Count > 0)
        {
            var req = requestQueue.Dequeue();
            string result = GenerateText(req.prompt, req.temperature, req.repeatPenalty, req.maxTokens);
            
            try { req.callback?.Invoke(result); }
            catch (Exception e) { Debug.LogWarning($"[LlamaBridge] Callback error: {e.Message}"); }

            yield return null;
        }
        
        isProcessing = false;
    }

    private void OnDestroy()
    {
        if (ctx != IntPtr.Zero)
        {
            llama_free_context(ctx);
            ctx = IntPtr.Zero;
        }
    }
}

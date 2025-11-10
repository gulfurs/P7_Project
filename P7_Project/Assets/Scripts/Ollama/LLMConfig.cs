using UnityEngine;

/// <summary>
/// Centralized LLM configuration
/// Toggle between Ollama HTTP server and local GGUF model
/// </summary>
public class LLMConfig : MonoBehaviour
{
    public static LLMConfig Instance { get; private set; }

    [Header("LLM Mode")]
    public LLMMode mode = LLMMode.OllamaHTTP;

    [Header("Local GGUF Model")]
    public string modelPath = "models/llama3.gguf";
    public string modelName = "llama3";

    [Header("Ollama HTTP Server")]
    public string ollamaEndpoint = "http://localhost:11434/api/chat";
    public string ollamaModel = "qwen3:4b-instruct-2507-q4_K_M";

    [Header("Generation Parameters")]
    [Range(0.0f, 2.0f)]
    public float defaultTemperature = 0.7f;
    
    [Range(1.0f, 2.0f)]
    public float defaultRepeatPenalty = 1.1f;
    
    [Range(64, 2048)]
    public int defaultMaxTokens = 256;

    [Range(0.1f, 1.0f)]
    public float topP = 0.9f;

    [Header("Python TTS Configuration")]
    [Tooltip("Path to Python executable for TTS generation. Leave empty to auto-detect.")]
    public string pythonExecutablePath = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        ValidateConfiguration();
    }

    /// <summary>
    /// Get the Python executable path for TTS generation
    /// Uses custom path if set in Inspector, otherwise uses system Python
    /// </summary>
    public string GetPythonExecutablePath()
    {
        if (!string.IsNullOrEmpty(pythonExecutablePath))
            return pythonExecutablePath;
        
        return "python";  // Use system Python from PATH
    }

    /// <summary>
    /// Validate configuration on startup
    /// </summary>
    private void ValidateConfiguration()
    {
        Debug.Log($"[LLMConfig] ✓ Initialized with {mode} mode");
        if (mode == LLMMode.OllamaHTTP)
            Debug.Log($"[LLMConfig] ✓ Ollama: {ollamaEndpoint} with model {ollamaModel}");
        else
            Debug.Log($"[LLMConfig] ✓ Local GGUF: {modelPath}");
    }

    /// <summary>
    /// Check if using local GGUF model
    /// </summary>
    public bool IsLocalMode => mode == LLMMode.LocalGGUF;

    /// <summary>
    /// Check if using Ollama HTTP server
    /// </summary>
    public bool IsOllamaMode => mode == LLMMode.OllamaHTTP;

    /// <summary>
    /// Switch LLM mode at runtime
    /// </summary>
    public void SetMode(LLMMode newMode)
    {
        mode = newMode;
        Debug.Log($"[LLMConfig] Switched to {mode} mode");
    }
}

/// <summary>
/// Available LLM modes
/// </summary>
public enum LLMMode
{
    LocalGGUF,    // Local GGUF file via native DLL
    OllamaHTTP    // Ollama HTTP server
}

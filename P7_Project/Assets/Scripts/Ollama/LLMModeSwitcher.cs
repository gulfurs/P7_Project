using UnityEngine;

/// <summary>
/// Utility script to easily switch between LLM modes at runtime
/// Add this as a component to a GameObject in your scene for debugging
/// </summary>
public class LLMModeSwitcher : MonoBehaviour
{
    private void Update()
    {
        // Press 'L' to switch to Local GGUF mode
        if (Input.GetKeyDown(KeyCode.L))
        {
            SwitchToLocalGGUF();
        }

        // Press 'O' to switch to Ollama HTTP mode
        if (Input.GetKeyDown(KeyCode.O))
        {
            SwitchToOllamaHTTP();
        }

        // Press 'M' to print current mode
        if (Input.GetKeyDown(KeyCode.M))
        {
            PrintCurrentMode();
        }
    }

    public void SwitchToLocalGGUF()
    {
        if (LLMConfig.Instance != null)
        {
            LLMConfig.Instance.SetMode(LLMMode.LocalGGUF);
            Debug.Log("[LLMModeSwitcher] Switched to LocalGGUF mode");
            var controller = LLMConfig.Instance.GetLlamaController();
            if (controller != null)
            {
                string modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "models", controller.modelFileName);
                Debug.Log($"[LLMModeSwitcher] Model path: {modelPath}");
            }
            else
            {
                Debug.LogWarning("[LLMModeSwitcher] No LlamaController found for local mode.");
            }
        }
        else
        {
            Debug.LogError("[LLMModeSwitcher] LLMConfig not found!");
        }
    }

    public void SwitchToOllamaHTTP()
    {
        if (LLMConfig.Instance != null)
        {
            LLMConfig.Instance.SetMode(LLMMode.OllamaHTTP);
            Debug.Log("[LLMModeSwitcher] Switched to OllamaHTTP mode");
            Debug.Log($"[LLMModeSwitcher] Endpoint: {LLMConfig.Instance.ollamaEndpoint}");
            Debug.Log($"[LLMModeSwitcher] Model: {LLMConfig.Instance.ollamaModel}");
        }
        else
        {
            Debug.LogError("[LLMModeSwitcher] LLMConfig not found!");
        }
    }

    public void PrintCurrentMode()
    {
        if (LLMConfig.Instance != null)
        {
            Debug.Log($"[LLMModeSwitcher] Current mode: {LLMConfig.Instance.mode}");
            
            if (LLMConfig.Instance.IsLocalMode)
            {
                var controller = LLMConfig.Instance.GetLlamaController();
                if (controller != null)
                {
                    string modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "models", controller.modelFileName);
                    Debug.Log($"  - Model path: {modelPath}");
                    Debug.Log($"  - Model name: {controller.modelFileName}");
                }
                else
                {
                    Debug.LogWarning("  - No LlamaController found for local mode.");
                }
            }
            else
            {
                Debug.Log($"  - Endpoint: {LLMConfig.Instance.ollamaEndpoint}");
                Debug.Log($"  - Model: {LLMConfig.Instance.ollamaModel}");
            }

            Debug.Log($"  - Temperature: {LLMConfig.Instance.defaultTemperature}");
            Debug.Log($"  - Repeat Penalty: {LLMConfig.Instance.defaultRepeatPenalty}");
            Debug.Log($"  - Max Tokens: {LLMConfig.Instance.defaultMaxTokens}");
        }
        else
        {
            Debug.LogError("[LLMModeSwitcher] LLMConfig not found!");
        }
    }
}

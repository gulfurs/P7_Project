using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

/// <summary>
/// [DEPRECATED] Ollama API Client - Being phased out in favor of local GGUF inference
/// This class is maintained for backward compatibility but should not be used in new code.
/// Use LlamaBridge for local inference with GGUF models instead.
/// </summary>
[System.Obsolete("OllamaChatClient is deprecated. Use LlamaBridge for local GGUF inference instead.", false)]
public class OllamaChatClient : MonoBehaviour
{
    [Header("Ollama Connection [DEPRECATED - Use LlamaBridge instead]")]
    public string endpoint = "http://localhost:11434/api/chat";
    [Tooltip("Override model name (leave empty to use LLMConfig default)")]
    public string model = ""; // Will use LLMConfig if empty
    [Range(0.1f, 1.0f)]
    public float topP = 0.9f;
    [Range(64, 512)]
    public int maxTokens = 150;
    
    private void Start()
    {
        Debug.LogWarning("[OllamaChatClient] This class is deprecated. Consider migrating to LlamaBridge for local GGUF inference.");
        
        // Load defaults from LLMConfig if not explicitly set
        if (string.IsNullOrEmpty(model))
        {
            var config = LLMConfig.Instance;
            model = config.modelName;
            Debug.Log($"[OllamaChatClient] Using model name from LLMConfig: {model}");
        }
    }

    
    private static readonly HttpClient http = new HttpClient();

    [Serializable] 
    public class ChatMessage 
    { 
        public string role; 
        public string content; 
    }

    public async Task<ChatResponse> SendChatAsync(List<ChatMessage> messages, float temperature, float repeatPenalty, 
        Action<string> onStreamUpdate = null, Action<string> onTokenReceived = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string json = BuildRequestJson(model, messages, temperature, repeatPenalty);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            var fullResponse = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var delta = ExtractContent(line);
                if (!string.IsNullOrEmpty(delta))
                {
                    fullResponse.Append(delta);
                    onStreamUpdate?.Invoke(fullResponse.ToString());
                    onTokenReceived?.Invoke(delta); // Send individual token/chunk immediately
                }
            }

            return new ChatResponse
            {
                content = fullResponse.ToString(),
                isComplete = true,
                error = null
            };
        }
        catch (Exception ex)
        {
            return new ChatResponse
            {
                content = "",
                isComplete = true,
                error = ex.Message
            };
        }
    }

    public class ChatResponse
    {
        public string content;
        public bool isComplete;
        public string error;
    }

    private string BuildRequestJson(string model, List<ChatMessage> messages, float temperature, float repeatPenalty)
    {
        var sb = new StringBuilder();
        sb.Append("{\"model\":").Append(Quote(model))
          .Append(",\"stream\":true")
          .Append(",\"options\":{")
          .Append("\"temperature\":").Append(temperature.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
          .Append(",\"repeat_penalty\":").Append(repeatPenalty.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
          .Append(",\"top_p\":").Append(topP.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
          .Append(",\"max_tokens\":").Append(maxTokens)
          .Append(",\"num_ctx\":4096")
          .Append("}")
          .Append(",\"messages\":[");
        
        for (int i = 0; i < messages.Count; i++)
        {
            var m = messages[i];
            sb.Append("{\"role\":").Append(Quote(m.role)).Append(",\"content\":").Append(Quote(m.content)).Append("}");
            if (i < messages.Count - 1) sb.Append(",");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static string Quote(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }

    private static string ExtractContent(string ndjsonLine)
    {
        const string key = "\"content\":\"";
        int i = ndjsonLine.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i += key.Length;
        var sb = new StringBuilder();
        bool esc = false;
        while (i < ndjsonLine.Length)
        {
            char c = ndjsonLine[i++];
            if (esc)
            {
                sb.Append(c switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => c
                });
                esc = false;
            }
            else if (c == '\\') esc = true;
            else if (c == '"') break;
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
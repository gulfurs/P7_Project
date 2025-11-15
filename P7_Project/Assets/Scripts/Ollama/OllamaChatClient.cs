using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class OllamaChatClient : MonoBehaviour
{
    private static readonly HttpClient http = new HttpClient();

    private void Start()
    {
        // LLMConfig is now mandatory - don't override
        if (LLMConfig.Instance == null)
        {
            Debug.LogError("[OllamaChatClient] ❌ LLMConfig not found in scene!");
            enabled = false;
            return;
        }
        
        Debug.Log("[OllamaChatClient] ✓ Connected to LLMConfig");
    }

    [Serializable] 
    public class ChatMessage 
    { 
        public string role; 
        public string content; 
    }

    [Serializable]
    private class OllamaRequestOptions
    {
        public float temperature;
        public float repeat_penalty;
        public float top_p;
        public int num_ctx;
    }

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public bool stream;
        public OllamaRequestOptions options;
    }

    public async Task<ChatResponse> SendChatAsync(List<ChatMessage> messages, float temperature, float repeatPenalty, 
        Action<string> onStreamUpdate = null, Action<string> onTokenReceived = null, CancellationToken cancellationToken = default)
    {
        // OllamaChatClient now only handles HTTP Ollama mode
        // Local GGUF mode is handled directly by NPCChatInstance → LlamaController
        try
        {
            // Read from LLMConfig every time (single source of truth)
            string endpoint = LLMConfig.Instance.ollamaEndpoint;
            
            string json = BuildRequestJson(messages, temperature, repeatPenalty);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
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
                    string capturedFull = fullResponse.ToString();
                    string capturedDelta = delta;
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        onStreamUpdate?.Invoke(capturedFull);
                        onTokenReceived?.Invoke(capturedDelta); // Send individual token/chunk immediately
                    });
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

    private string BuildRequestJson(List<ChatMessage> messages, float temperature, float repeatPenalty)
    {
        var requestData = new OllamaRequest
        {
            model = LLMConfig.Instance.ollamaModel,
            messages = messages,
            stream = true,
            options = new OllamaRequestOptions
            {
                temperature = temperature,
                repeat_penalty = repeatPenalty,
                top_p = LLMConfig.Instance.topP,
                num_ctx = 4096 // A reasonable default context window
            }
        };
        
        return JsonUtility.ToJson(requestData);
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
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
    [Header("Ollama")]
    public string endpoint = "http://localhost:11434/api/chat";
    public string model = "qwen3:4b-instruct-2507-q4_K_M";
    [Range(0.1f, 2.0f)]
    public float temperature = 0.7f;
    [Range(1.0f, 1.5f)]
    public float repeatPenalty = 1.1f;
    [Range(0.1f, 1.0f)]
    public float topP = 0.9f;
    [Range(64, 512)]
    public int maxTokens = 150; // Limit response length

    [Header("NPC")]
    [TextArea(3, 6)]
    public string systemPrompt = "You are a friendly NPC named Nira. Keep replies under 2 sentences. Stay in character.";
    public int maxHistory = 6;

    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    
    private static readonly HttpClient http = new HttpClient();
    private readonly List<Msg> history = new List<Msg>();
    private CancellationTokenSource cts;

    [Serializable] class Msg { public string role; public string content; }

    public async void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText)) return;

        // Update local history
        history.Add(new Msg { role = "user", content = userText });
        TrimHistory();

        // Build message array: system + history
        var messages = new List<Msg> { new Msg { role = "system", content = systemPrompt } };
        messages.AddRange(history);

        // Start request
        cts?.Cancel();
        cts = new CancellationTokenSource();

        if (outputText) outputText.text = "";

        string json = BuildRequestJson(model, messages);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        try
        {
            using var resp = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            var assistantReply = new StringBuilder();

            while (!reader.EndOfStream && !cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // NDJSON: { "message": {"role":"assistant","content":"..."}, "done":false }
                var delta = ExtractContent(line);
                if (!string.IsNullOrEmpty(delta))
                {
                    assistantReply.Append(delta);
                    if (outputText) outputText.text = assistantReply.ToString();
                }
            }

            // Save assistant reply to history
            history.Add(new Msg { role = "assistant", content = assistantReply.ToString() });
            TrimHistory();
        }
        catch (Exception ex)
        {
            if (outputText) outputText.text = "Error: " + ex.Message;
        }
    }

    public void CancelStream()
    {
        cts?.Cancel();
    }

    private void TrimHistory()
    {
        // Keep only the last N pairs (user+assistant), but preserve ordering
        int pairsToKeep = Mathf.Max(0, maxHistory);
        // Remove any leading system if present (we rebuild it every time)
        var filtered = new List<Msg>();
        foreach (var m in history) if (m.role != "system") filtered.Add(m);

        // Count pairs from end
        int count = 0;
        for (int i = filtered.Count - 1; i >= 0 && count < pairsToKeep * 2; i--) count++;

        int start = Mathf.Max(0, filtered.Count - count);
        history.Clear();
        for (int i = start; i < filtered.Count; i++) history.Add(filtered[i]);
    }

    private string BuildRequestJson(string model, List<Msg> messages)
    {
        // Manual JSON to avoid Unity's JsonUtility limitations with arrays/dicts
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
        // Fast substring parser for "content":"...".
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
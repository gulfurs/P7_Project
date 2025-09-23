using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

public class NPCChatInstance : MonoBehaviour
{
    [Header("NPC Configuration")]
    public NPCProfile npcProfile;
    
    [Header("Ollama Client Reference")]
    public OllamaChatClient ollamaClient;
    
    [Header("Chat Settings")]
    public bool broadcastResponses = true;
    
    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text externalMessagesText;
    
    private readonly List<string> externalMessages = new List<string>();
    private CancellationTokenSource cts;
    
    // TTS
    private const string TTS_ENDPOINT = "http://localhost:8880/v1/audio/speech";
    private static readonly HttpClient httpClient = new HttpClient();

    void Start()
    {
        // Auto-find OllamaClient if not assigned
        if (ollamaClient == null)
        {
            ollamaClient = FindObjectOfType<OllamaChatClient>();
            if (ollamaClient == null)
            {
                Debug.LogError($"NPCChatInstance '{gameObject.name}' needs an OllamaChatClient reference!");
                return;
            }
        }

        // Set NPC name in UI
        if (npcNameLabel != null && npcProfile != null)
        {
            npcNameLabel.text = npcProfile.npcName;
        }
        
        // Setup AudioSource for TTS
        if (npcProfile != null && npcProfile.enableTTS)
        {
            if (npcProfile.audioSource == null)
            {
                npcProfile.audioSource = GetComponent<AudioSource>();
                if (npcProfile.audioSource == null)
                {
                    npcProfile.audioSource = gameObject.AddComponent<AudioSource>();
                    npcProfile.audioSource.playOnAwake = false;
                }
            }
        }
        
        // Register with manager
        if (NPCManager.Instance != null)
        {
            if (!NPCManager.Instance.npcInstances.Contains(this))
            {
                NPCManager.Instance.npcInstances.Add(this);
            }
        }

        // Add enter key support for input field
        if (userInput != null)
        {
            userInput.onSubmit.AddListener((string text) => { Send(); });
        }
    }

    public async void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null || ollamaClient == null) return;

        // Clear input
        if (userInput != null) userInput.text = "";

        // Cancel any ongoing request
        cts?.Cancel();
        cts = new CancellationTokenSource();

        // Build system prompt including external messages
        string fullSystemPrompt = npcProfile.GetFullSystemPrompt();
        if (externalMessages.Count > 0)
        {
            fullSystemPrompt += "\n\nRecent messages from other characters:\n" + string.Join("\n", externalMessages);
        }

        // Create single conversation - no history, just current user message
        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = fullSystemPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = userText }
        };

        // Clear output
        if (outputText) outputText.text = "";

        try
        {
            var response = await ollamaClient.SendChatAsync(
                messages,
                npcProfile.temperature,
                npcProfile.repeatPenalty,
                (streamContent) => {
                    // Update UI during streaming
                    if (outputText) outputText.text = streamContent;
                }, 
                cts.Token
            );

            if (!string.IsNullOrEmpty(response.error))
            {
                if (outputText) outputText.text = "Error: " + response.error;
                return;
            }

            // Broadcast response to other NPCs
            if (broadcastResponses && NPCManager.Instance != null)
            {
                NPCManager.Instance.BroadcastMessage(this, response.content);
            }
            
            // Speak the response with TTS
            if (npcProfile.enableTTS && !string.IsNullOrEmpty(response.content))
            {
                StartCoroutine(SpeakText(response.content));
            }
        }
        catch (OperationCanceledException)
        {
            if (outputText) outputText.text = "Cancelled";
        }
        catch (Exception ex)
        {
            if (outputText) outputText.text = "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// Receive a message from another NPC
    /// </summary>
    public void ReceiveExternalMessage(string senderName, string message)
    {
        string formattedMessage = $"{senderName}: {message}";
        externalMessages.Add(formattedMessage);
        
        // Keep only last few external messages to avoid context overflow
        while (externalMessages.Count > 3)
        {
            externalMessages.RemoveAt(0);
        }
        
        // Update UI to show external messages
        if (externalMessagesText != null)
        {
            externalMessagesText.text = "Messages from others:\n" + string.Join("\n", externalMessages);
        }
    }
    
    private IEnumerator SpeakText(string text)
    {
        if (string.IsNullOrEmpty(text) || npcProfile?.audioSource == null) yield break;
        
        // Create TTS request with proper JSON format
        string json = $@"{{
            ""input"": ""{text.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}"",
            ""voice"": ""{npcProfile.voiceName}""
        }}";
        
        Debug.Log($"TTS Request: {json}");
        
        var request = new HttpRequestMessage(HttpMethod.Post, TTS_ENDPOINT)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        // Send request
        var responseTask = httpClient.SendAsync(request);
        while (!responseTask.IsCompleted)
            yield return new WaitForSeconds(0.01f);
            
        var response = responseTask.Result;
        if (!response.IsSuccessStatusCode)
        {
            Debug.LogError($"TTS failed for {npcProfile.npcName}: {response.StatusCode}");
            yield break;
        }
        
        // Get audio data
        var audioTask = response.Content.ReadAsByteArrayAsync();
        while (!audioTask.IsCompleted)
            yield return new WaitForSeconds(0.01f);
            
        var audioData = audioTask.Result;
        
        // Save temp file and play
        string tempFile = System.IO.Path.GetTempFileName() + ".mp3";
        System.IO.File.WriteAllBytes(tempFile, audioData);
        
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + tempFile, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    npcProfile.audioSource.clip = clip;
                    npcProfile.audioSource.Play();
                    Debug.Log($"{npcProfile.npcName} speaking: {text}");
                    
                    // Wait for audio to finish
                    while (npcProfile.audioSource.isPlaying)
                        yield return new WaitForSeconds(0.1f);
                }
            }
        }
        
        // Cleanup
        if (System.IO.File.Exists(tempFile))
            System.IO.File.Delete(tempFile);
    }

    void OnDestroy()
    {
        cts?.Cancel();
        
        // Unregister from NPCManager
        if (NPCManager.Instance != null && NPCManager.Instance.npcInstances.Contains(this))
        {
            NPCManager.Instance.npcInstances.Remove(this);
        }
    }
}
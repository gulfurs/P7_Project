using System;
using System.Collections.Generic;
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
    public int maxHistory = 6;
    public bool broadcastResponses = true;
    
    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text externalMessagesText;
    
    private readonly List<OllamaChatClient.ChatMessage> history = new List<OllamaChatClient.ChatMessage>();
    private readonly List<string> externalMessages = new List<string>();
    private CancellationTokenSource cts;

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

        // Update conversation history
        history.Add(new OllamaChatClient.ChatMessage { role = "user", content = userText });
        TrimHistory();

        // Build system prompt including external messages
        string systemPrompt = npcProfile.GetFullSystemPrompt();
        if (externalMessages.Count > 0)
        {
            systemPrompt += "\n\nRecent messages from other characters:\n" + string.Join("\n", externalMessages);
        }

        // Build message list for request
        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = systemPrompt }
        };
        messages.AddRange(history);

        // Clear output and start streaming
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

            // Save assistant reply to history
            history.Add(new OllamaChatClient.ChatMessage { role = "assistant", content = response.content });
            TrimHistory();
            
            // Broadcast response to other NPCs
            if (broadcastResponses && NPCManager.Instance != null)
            {
                NPCManager.Instance.BroadcastMessage(this, response.content);
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled - this is normal
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

    public void CancelStream()
    {
        cts?.Cancel();
    }

    private void TrimHistory()
    {
        int pairsToKeep = Mathf.Max(0, maxHistory);
        var filtered = new List<OllamaChatClient.ChatMessage>();
        
        // Remove system messages from history (we rebuild them each time)
        foreach (var m in history) 
            if (m.role != "system") 
                filtered.Add(m);

        // Keep only the last N conversation pairs
        int messagesToKeep = pairsToKeep * 2; // user + assistant pairs
        if (filtered.Count > messagesToKeep)
        {
            int start = filtered.Count - messagesToKeep;
            history.Clear();
            for (int i = start; i < filtered.Count; i++) 
                history.Add(filtered[i]);
        }
        else
        {
            history.Clear();
            history.AddRange(filtered);
        }
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
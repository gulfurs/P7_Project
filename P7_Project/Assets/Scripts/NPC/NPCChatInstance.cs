using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

/// <summary>
/// Main NPC chat instance - now much cleaner with delegated responsibilities
/// </summary>
public class NPCChatInstance : MonoBehaviour
{
    [Header("NPC Configuration")]
    public NPCProfile npcProfile;

    [Header("Component References")]
    public OllamaChatClient ollamaClient;
    public NPCTTSHandler ttsHandler;
    
    [Header("Memory")]
    public NPCMemory memory = new NPCMemory();

    [Header("Chat Settings")]
    public bool broadcastResponses = true;
    public bool enableAutoResponse = true;
    public float responseDelay = 2f;
    public float responseChance = 0.8f;

    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text memoryDisplayText;

    private CancellationTokenSource cts;
    private bool isCurrentlySpeaking = false;
    
    // Metadata parsing state
    private NPCMetadata currentMetadata;
    private string metadataBuffer = "";
    private bool isParsingMetadata = false;

    void Start()
    {
        InitializeComponents();
        RegisterWithManagers();
        SetupUI();
    }

    private void InitializeComponents()
    {
        // Auto-find OllamaClient
        if (ollamaClient == null)
        {
            ollamaClient = FindObjectOfType<OllamaChatClient>();
            if (ollamaClient == null)
            {
                Debug.LogError($"NPCChatInstance '{gameObject.name}' needs an OllamaChatClient!");
                return;
            }
        }

        // Setup TTS Handler
        if (ttsHandler == null)
        {
            ttsHandler = gameObject.AddComponent<NPCTTSHandler>();
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
            
            // Initialize TTS handler with audio source and voice
            ttsHandler.Initialize(npcProfile.audioSource, npcProfile.voiceName);
        }
    }

    private void RegisterWithManagers()
    {
        // Register with NPC Manager
        if (NPCManager.Instance != null)
        {
            if (!NPCManager.Instance.npcInstances.Contains(this))
                NPCManager.Instance.npcInstances.Add(this);
        }
    }

    private void SetupUI()
    {
        if (npcNameLabel != null && npcProfile != null)
            npcNameLabel.text = npcProfile.npcName;

        if (userInput != null)
            userInput.onSubmit.AddListener((string text) => { Send(); });
    }

    /// <summary>
    /// Send message from UI input
    /// </summary>
    public async void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null || ollamaClient == null) return;

        SendMessage(userText);
    }

    /// <summary>
    /// Send a message and get NPC response
    /// </summary>
    public async void SendMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || npcProfile == null || ollamaClient == null)
            return;

        // Request turn from dialogue manager
        if (DialogueManager.Instance != null)
        {
            bool turnGranted = DialogueManager.Instance.RequestTurn(npcProfile.npcName, this, messageText);
            if (!turnGranted)
            {
                // Queued for later, will be called via ExecuteQueuedMessage
                return;
            }
        }

        // Turn granted, execute immediately
        await ExecuteSpeech(messageText);
    }
    
    /// <summary>
    /// Execute a queued message (called by DialogueManager)
    /// </summary>
    public async void ExecuteQueuedMessage(string messageText)
    {
        await ExecuteSpeech(messageText);
    }
    
    /// <summary>
    /// Internal method to execute the actual speech
    /// </summary>
    private async System.Threading.Tasks.Task ExecuteSpeech(string messageText)
    {
        if (isCurrentlySpeaking) return;
        
        isCurrentlySpeaking = true;

        // Clear input
        if (userInput != null && userInput.text == messageText)
            userInput.text = "";

        // Cancel any ongoing request
        cts?.Cancel();
        cts = new CancellationTokenSource();

        // Build prompt with memory
        string fullSystemPrompt = BuildPromptWithMemory();

        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = fullSystemPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = messageText }
        };

        // Clear output and reset metadata parsing
        if (outputText) outputText.text = "";
        ResetMetadataParsing();

        // Stream response
        var ttsBuffer = new StringBuilder();
        var displayBuffer = new StringBuilder();
        
        var response = await ollamaClient.SendChatAsync(
            messages,
            npcProfile.temperature,
            npcProfile.repeatPenalty,
            null,
            (token) => ProcessToken(token, ttsBuffer, displayBuffer),
            cts.Token
        );

        // Handle errors
        if (!string.IsNullOrEmpty(response.error))
        {
            if (outputText) outputText.text = "Error: " + response.error;
            FinishSpeaking();
            return;
        }

        // Process remaining TTS buffer
        ProcessRemainingTTS(ttsBuffer);

        // Wait for TTS to finish before releasing turn
        if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled)
        {
            // Wait for TTS handler to finish speaking
            while (ttsHandler.IsSpeaking())
            {
                await System.Threading.Tasks.Task.Delay(50);
            }
        }

        // Store in memory
        memory.AddDialogueTurn("User", messageText);
        memory.AddDialogueTurn(npcProfile.npcName, response.content);
        UpdateMemoryDisplay();

        // Broadcast to other NPCs
        if (broadcastResponses && NPCManager.Instance != null)
            NPCManager.Instance.BroadcastMessage(this, response.content);
        
        FinishSpeaking();
    }

    /// <summary>
    /// Build system prompt with memory context
    /// </summary>
    private string BuildPromptWithMemory()
    {
        string prompt = npcProfile.GetFullSystemPrompt();
        
        // Add medium-term memory (facts)
        string mediumTermContext = memory.GetMediumTermContext();
        if (!string.IsNullOrEmpty(mediumTermContext))
            prompt += "\n\n" + mediumTermContext;
        
        // Add short-term memory (recent dialogue)
        string shortTermContext = memory.GetShortTermContext();
        if (!string.IsNullOrEmpty(shortTermContext))
            prompt += "\n\n" + shortTermContext;
        
        return prompt;
    }

    /// <summary>
    /// Process incoming tokens for metadata and TTS
    /// </summary>
    private void ProcessToken(string token, StringBuilder ttsBuffer, StringBuilder displayBuffer)
    {
        foreach (char c in token)
        {
            // Building potential [META] tag
            if (!isParsingMetadata && metadataBuffer.Length > 0 && metadataBuffer.Length < 6)
            {
                metadataBuffer += c;
                
                if (metadataBuffer == "[META]")
                {
                    // Found complete tag
                    isParsingMetadata = true;
                    metadataBuffer = "";
                }
                else if (!"[META]".StartsWith(metadataBuffer))
                {
                    // Not a metadata tag - flush buffer and current char
                    string bufferContent = metadataBuffer;
                    metadataBuffer = "";
                    
                    // Add all buffered content to output
                    displayBuffer.Append(bufferContent);
                    if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled)
                        ttsBuffer.Append(bufferContent);
                }
                // Else: still building [META], wait for more chars
                continue;
            }
            
            // Check if this char starts a potential [META] tag
            if (!isParsingMetadata && metadataBuffer.Length == 0 && c == '[')
            {
                metadataBuffer = "[";
                continue;
            }
            
            // Parsing metadata content
            if (isParsingMetadata)
            {
                metadataBuffer += c;
                
                if (metadataBuffer.EndsWith("[/META]"))
                {
                    string jsonContent = metadataBuffer.Substring(0, metadataBuffer.Length - 7);
                    ExecuteMetadata(NPCMetadata.ParseFromJson(jsonContent));
                    
                    isParsingMetadata = false;
                    metadataBuffer = "";
                }
                continue;
            }
            
            // Normal dialogue text
            displayBuffer.Append(c);
            
            // TTS processing
            if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled)
            {
                ttsBuffer.Append(c);
                
                // Process on sentence endings
                if (c == '.' || c == '!' || c == '?' || (ttsBuffer.Length > 60 && c == ','))
                {
                    string chunk = ttsBuffer.ToString().Trim();
                    if (chunk.Length > 0)
                    {
                        ttsHandler.EnqueueSpeech(chunk);
                        ttsBuffer.Clear();
                    }
                }
            }
        }
        
        // Update UI
        if (outputText) outputText.text = displayBuffer.ToString();
    }

    /// <summary>
    /// Execute metadata actions
    /// </summary>
    private void ExecuteMetadata(NPCMetadata metadata)
    {
        if (metadata == null) return;
        
        currentMetadata = metadata;
        
        // Trigger animation
        if (!string.IsNullOrEmpty(metadata.animatorTrigger) && npcProfile.animatorConfig != null)
            npcProfile.animatorConfig.TriggerAnimation(metadata.animatorTrigger);
        
        // Handle interruption
        if (metadata.shouldInterrupt && DialogueManager.Instance != null)
            DialogueManager.Instance.RequestInterruption(npcProfile.npcName, "Urgent interjection");
    }

    /// <summary>
    /// Process remaining TTS buffer after response completes
    /// </summary>
    private void ProcessRemainingTTS(StringBuilder ttsBuffer)
    {
        if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled && ttsBuffer.Length > 0)
        {
            string chunk = ttsBuffer.ToString().Trim();
            if (chunk.Length > 0)
                ttsHandler.EnqueueSpeech(chunk);
        }
    }

    /// <summary>
    /// Receive message from another NPC
    /// </summary>
    public void ReceiveExternalMessage(string senderName, string message)
    {
        memory.AddDialogueTurn(senderName, message);
        UpdateMemoryDisplay();
        
        // Auto-respond logic
        if (enableAutoResponse && !isCurrentlySpeaking && UnityEngine.Random.value < responseChance)
        {
            // Don't respond if spoke recently
            if (DialogueManager.Instance != null && DialogueManager.Instance.HasSpokeRecently(npcProfile.npcName))
                return;
            
            Invoke(nameof(AutoRespond), responseDelay + UnityEngine.Random.Range(-0.5f, 1f));
        }
    }
    
    private void AutoRespond()
    {
        if (!isCurrentlySpeaking)
        {
            var lastTurn = memory.GetLastTurn();
            if (lastTurn != null)
                SendMessage($"Respond to {lastTurn.speaker}: {lastTurn.message}");
        }
    }

    /// <summary>
    /// Add a fact to NPC's medium-term memory
    /// </summary>
    public void LearnFact(string fact)
    {
        memory.AddFact(fact);
        UpdateMemoryDisplay();
    }

    private void ResetMetadataParsing()
    {
        currentMetadata = null;
        metadataBuffer = "";
        isParsingMetadata = false;
    }

    private void FinishSpeaking()
    {
        isCurrentlySpeaking = false;
        
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ReleaseTurn(npcProfile.npcName);
    }

    private void UpdateMemoryDisplay()
    {
        if (memoryDisplayText == null) return;
        
        string display = memory.GetShortTermContext();
        if (!string.IsNullOrEmpty(display))
            memoryDisplayText.text = display;
    }

    [ContextMenu("Clear Memory")]
    public void ClearMemory()
    {
        memory.ClearAll();
        UpdateMemoryDisplay();
    }
}

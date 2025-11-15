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
    [Tooltip("Optional: Only used if LLMConfig is set to OllamaHTTP mode")]
    public OllamaChatClient ollamaClient;
    public NPCTTSHandler ttsHandler;
    
    [Header("LLM Routing")]
    [Tooltip("If null, will auto-find LLMConfig.Instance")]
    public LLMConfig llmConfig;
    
    [Header("Memory")]
    public NPCMemory memory = new NPCMemory();

    [Header("Chat Settings")]
    public bool enableAutoResponse = true;

    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text memoryDisplayText;

    private CancellationTokenSource cts;
    private bool isCurrentlySpeaking;

    private readonly StringBuilder metadataBuffer = new StringBuilder();
    private bool isParsingMetadata;

    private const string MetadataOpenTag = "[META]";
    private const string MetadataCloseTag = "[/META]";

    private bool IsTTSEnabled =>
        npcProfile != null &&
        npcProfile.enableTTS &&
        NPCManager.Instance != null &&
        NPCManager.Instance.globalTTSEnabled;

    void Start()
    {
        InitializeComponents();
        RegisterWithManagers();
        SetupUI();
    }

    void Update()
    {
        if (npcProfile?.animatorConfig != null)
            npcProfile.animatorConfig.TickGaze(Time.deltaTime);
    }

    void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void InitializeComponents()
    {
        // Auto-find LLMConfig
        if (llmConfig == null)
        {
            llmConfig = LLMConfig.Instance;
            if (llmConfig == null)
            {
                Debug.LogError($"NPCChatInstance '{gameObject.name}' needs LLMConfig in the scene!");
                return;
            }
        }

        // Auto-find OllamaClient only if needed for HTTP mode
        if (ollamaClient == null && llmConfig.IsOllamaMode)
        {
            ollamaClient = FindObjectOfType<OllamaChatClient>();
            if (ollamaClient == null)
            {
                Debug.LogWarning($"NPCChatInstance '{gameObject.name}' in OllamaHTTP mode but no OllamaChatClient found!");
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
        var manager = NPCManager.Instance;
        if (manager != null && !manager.npcInstances.Contains(this))
            manager.npcInstances.Add(this);
    }

    private void SetupUI()
    {
        if (npcNameLabel != null && npcProfile != null)
            npcNameLabel.text = npcProfile.npcName;

        // ONLY the first NPC instance should listen to InputField submissions to avoid duplicate events.
        // The input is then broadcast to all NPCs from the listener.
        if (userInput != null && NPCManager.Instance != null)
        {
            if (NPCManager.Instance.npcInstances.Count > 0 && NPCManager.Instance.npcInstances[0] == this)
            {
                userInput.onSubmit.RemoveAllListeners(); // Clear any existing listeners
                userInput.onSubmit.AddListener((string text) => { Send(); });
                Debug.Log($"[NPCChatInstance] {npcProfile.npcName} registered as primary input listener.");
            }
        }
    }

    /// <summary>
    /// Send message from UI input (text mode - tutorial)
    /// </summary>
    public void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null) return;

        ProcessUserAnswer(userText);

        if (userInput != null)
            userInput.text = "";
    }

    /// <summary>
    /// Process user answer from voice or text
    /// Central method - avoids duplication
    /// </summary>
    public void ProcessUserAnswer(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null) 
            return;

        // Notify DialogueManager
        // This is now handled by the central ProcessUserAnswer method
        // if (DialogueManager.Instance != null)
        //     DialogueManager.Instance.OnUserAnswered(userText);

        // Broadcast user answer to ALL interviewers
        var manager = NPCManager.Instance;
        if (manager != null)
        {
            Debug.Log($"📢 User answered: \"{userText}\"");
            
            foreach (var npc in manager.npcInstances)
            {
                if (npc != null)
                {
                    npc.memory.AddDialogueTurn("User", userText);
                    // Each NPC asks LLM if it should respond
                    npc.AskLLMIfShouldRespond(userText);
                }
            }
        }
    }
    
    /// <summary>
    /// Ask the LLM itself whether this NPC should ask a follow-up
    /// </summary>
    public async void AskLLMIfShouldRespond(string userAnswer)
    {
        if (!enableAutoResponse || isCurrentlySpeaking)
            return;
        
        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = BuildTurnDecisionPrompt(userAnswer) },
            new OllamaChatClient.ChatMessage { role = "user", content = userAnswer }
        };
        
        // Ask LLM for decision
        string response;
        if (llmConfig.IsLocalMode)
        {
            var controller = llmConfig.GetLlamaController();
            response = controller?.GenerateReply(messages);
        }
        else
        {
            if (ollamaClient == null) return;
            var result = await ollamaClient.SendChatAsync(messages, 0.3f, 1.0f, null, null, default).ConfigureAwait(true);
            response = result.content;
        }

        if (string.IsNullOrEmpty(response)) return;
        
        bool shouldRespond = response.ToLower().Contains("yes");
        Debug.Log($"🤖 {npcProfile.npcName} decision: {shouldRespond}");
        
        if (DialogueManager.Instance?.currentPhase == DialogueManager.InterviewPhase.Main)
            shouldRespond = DialogueManager.Instance.RecordDecision(npcProfile.npcName, shouldRespond);
        
        if (shouldRespond && DialogueManager.Instance?.RequestTurn(npcProfile.npcName) == true)
        {
            string instruction = DialogueManager.Instance.currentPhase == DialogueManager.InterviewPhase.Conclusion
                ? "It's time to conclude the interview. Ask a final question or give some closing remarks."
                : $"Ask a relevant follow-up question to: {userAnswer}";
            await ExecuteSpeech(instruction);
        }
    }
    
    /// <summary>
    /// Forces this NPC to introduce themselves, bypassing normal turn-taking logic.
    /// This is called by the DialogueManager to start the interview.
    /// </summary>
    public async void InitiateIntroduction()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.RequestTurn(npcProfile.npcName))
        {
            Debug.Log($"[NPCChatInstance] {npcProfile.npcName} is initiating the introduction.");
            await ExecuteSpeech("Introduce yourself and welcome the candidate to the interview.");
        }
    }

    /// <summary>
    /// Build prompt for LLM to decide whether to respond
    /// </summary>
    private string BuildTurnDecisionPrompt(string userAnswer)
    {
        if (DialogueManager.Instance == null)
            return $"You are {npcProfile.npcName}. {npcProfile.systemPrompt}\n\nCandidate said: \"{userAnswer}\"\nDo you have a relevant follow-up? YES or NO only.";

        switch (DialogueManager.Instance.currentPhase)
        {
            case DialogueManager.InterviewPhase.Introduction:
                return $"You are {npcProfile.npcName}. The interview is just starting. The first speaker has just introduced themselves. Is it your turn to introduce yourself now? Respond YES or NO.";
            case DialogueManager.InterviewPhase.Conclusion:
                return $"You are {npcProfile.npcName}. The interview has reached its conclusion phase. Should you be the one to deliver the closing remarks or ask a final question? Respond YES or NO.";
            default:
                return $"You are {npcProfile.npcName}. {npcProfile.systemPrompt}\n\nCandidate said: \"{userAnswer}\"\nDo you have a relevant follow-up? YES or NO only.";
        }
    }

    /// <summary>
    /// Internal method to execute the actual speech
    /// </summary>
    private async System.Threading.Tasks.Task ExecuteSpeech(string messageText)
    {
        if (isCurrentlySpeaking) return;
        
        isCurrentlySpeaking = true;

    cts?.Cancel();
    cts?.Dispose();
    cts = new CancellationTokenSource();

        // Build messages with memory
        var messages = new List<OllamaChatClient.ChatMessage>();
        
        // Add system prompt
        messages.Add(new OllamaChatClient.ChatMessage 
        { 
            role = "system", 
            content = npcProfile.GetFullSystemPrompt() 
        });
        
        // Add conversation history from memory
        messages.AddRange(memory.GetConversationHistory());
        
        // Add current user message
        messages.Add(new OllamaChatClient.ChatMessage 
        { 
            role = "user", 
            content = messageText 
        });

        // Clear output and reset metadata parsing
        if (outputText) outputText.text = "";
        ResetMetadataParsing();

        // Stream response with TTS buffering
        var ttsBuffer = new StringBuilder();
        var displayBuffer = new StringBuilder();
        bool ttsActive = IsTTSEnabled;
        
        // Route to appropriate LLM
        string fullResponse;
        if (llmConfig.IsLocalMode)
        {
            fullResponse = await System.Threading.Tasks.Task.Run(() => 
                llmConfig.GetLlamaController()?.GenerateReply(messages, 
                    token => UnityMainThreadDispatcher.Enqueue(() => 
                        ProcessToken(token, ttsBuffer, displayBuffer, ttsActive, !ttsActive)), 
                    cts.Token), 
                cts.Token).ConfigureAwait(true);
        }
        else
        {
            var result = await ollamaClient.SendChatAsync(messages, 
                npcProfile.GetEffectiveTemperature(), 
                npcProfile.GetEffectiveRepeatPenalty(), 
                null,
                token => ProcessToken(token, ttsBuffer, displayBuffer, ttsActive, !ttsActive),
                cts.Token).ConfigureAwait(true);
            
            if (!string.IsNullOrEmpty(result.error))
            {
                if (outputText) outputText.text = "Error: " + result.error;
                FinishSpeaking();
                return;
            }
            fullResponse = result.content;
        }

        // Process remaining TTS and store response
        ProcessRemainingTTS(ttsBuffer, displayBuffer.ToString(), ttsActive);
        memory.AddDialogueTurn(npcProfile.npcName, fullResponse);
        LogMemoryState();
        FinishSpeaking();
    }

    private void ProcessToken(string token, StringBuilder ttsBuffer, StringBuilder displayBuffer, bool ttsActive, bool shouldStreamDisplay)
    {
        foreach (char c in token)
        {
            if (HandleMetadataChar(c, displayBuffer, ttsBuffer, ttsActive)) continue;
            displayBuffer.Append(c);
            if (ttsActive) HandleTTSChar(c, ttsBuffer, displayBuffer);
        }
        if (shouldStreamDisplay && outputText) outputText.text = displayBuffer.ToString();
    }

    private bool HandleMetadataChar(char c, StringBuilder displayBuffer, StringBuilder ttsBuffer, bool ttsActive)
    {
        if (!isParsingMetadata)
        {
            if (metadataBuffer.Length > 0)
            {
                if (metadataBuffer.Length < MetadataOpenTag.Length && MetadataOpenTag[metadataBuffer.Length] == c)
                {
                    metadataBuffer.Append(c);
                    if (metadataBuffer.Length == MetadataOpenTag.Length)
                    {
                        isParsingMetadata = true;
                        metadataBuffer.Clear();
                    }
                    return true;
                }
                FlushMetadataBuffer(displayBuffer, ttsBuffer, ttsActive);
            }
            if (c == MetadataOpenTag[0])
            {
                metadataBuffer.Append(c);
                return true;
            }
        }
        else
        {
            metadataBuffer.Append(c);
            if (metadataBuffer.Length >= MetadataCloseTag.Length && 
                metadataBuffer.ToString(metadataBuffer.Length - MetadataCloseTag.Length, MetadataCloseTag.Length) == MetadataCloseTag)
            {
                string json = metadataBuffer.ToString(0, metadataBuffer.Length - MetadataCloseTag.Length);
                ExecuteMetadata(NPCMetadata.ParseFromJson(json));
                metadataBuffer.Clear();
                isParsingMetadata = false;
            }
            return true;
        }
        return false;
    }

    private void FlushMetadataBuffer(StringBuilder displayBuffer, StringBuilder ttsBuffer, bool ttsActive)
    {
        for (int i = 0; i < metadataBuffer.Length; i++)
        {
            char c = metadataBuffer[i];
            displayBuffer.Append(c);
            if (ttsActive) HandleTTSChar(c, ttsBuffer, displayBuffer);
        }
        metadataBuffer.Clear();
    }

    private void HandleTTSChar(char c, StringBuilder ttsBuffer, StringBuilder displayBuffer)
    {
        ttsBuffer.Append(c);
        if ((c == '.' || c == '!' || c == '?') || (c == ',' && ttsBuffer.Length > 60))
        {
            string chunk = ttsBuffer.ToString().Trim();
            if (chunk.Length > 0)
            {
                if (outputText) outputText.text = displayBuffer.ToString();
                ttsHandler.EnqueueSpeech(chunk, null);
                ttsBuffer.Clear();
            }
        }
    }

    private void ExecuteMetadata(NPCMetadata metadata)
    {
        if (metadata == null || npcProfile.animatorConfig == null) return;
        
        if (!string.IsNullOrEmpty(metadata.animatorTrigger))
            npcProfile.animatorConfig.TriggerAnimation(metadata.animatorTrigger);

        bool isSelfSpeaking = DialogueManager.Instance?.currentSpeaker == npcProfile.npcName;
        Transform target = NPCManager.Instance?.GetLookTargetForSpeaker(isSelfSpeaking ? "User" : DialogueManager.Instance?.currentSpeaker) 
            ?? npcProfile.animatorConfig.neutralLookTarget;
        
        npcProfile.animatorConfig.ApplyMetadata(metadata, target);
    }

    private void ProcessRemainingTTS(StringBuilder ttsBuffer, string fullDisplayText, bool ttsActive)
    {
        if (ttsActive && ttsBuffer.Length > 0)
            ttsHandler.EnqueueSpeech(ttsBuffer.ToString().Trim(), null);
        if (outputText) outputText.text = fullDisplayText;
    }

    private void LogMemoryState()
    {
        Debug.Log($"🧠 [{npcProfile.npcName}] Memory: {memory.GetCount()} turns");
        if (memoryDisplayText != null) memoryDisplayText.text = memory.GetShortTermContext();
    }

    [ContextMenu("Clear Memory")]
    public void ClearMemory()
    {
        memory.ClearAll();
        if (memoryDisplayText != null) memoryDisplayText.text = string.Empty;
    }

    private void ResetMetadataParsing()
    {
        metadataBuffer.Clear();
        isParsingMetadata = false;
    }

    private void FinishSpeaking()
    {
        isCurrentlySpeaking = false;
        DialogueManager.Instance?.ReleaseTurn(npcProfile.npcName);
    }

    public void OnSpeakerChanged(string speakerName)
    {
        if (npcProfile?.animatorConfig == null)
            return;

        Transform target = null;
        var manager = NPCManager.Instance;
        if (manager != null)
        {
            if (string.IsNullOrEmpty(speakerName))
                target = npcProfile?.animatorConfig?.neutralLookTarget;
            else if (speakerName == npcProfile.npcName)
                target = manager.GetLookTargetForSpeaker("User");
            else
                target = manager.GetLookTargetForSpeaker(speakerName);
        }

        if (target == null && npcProfile?.animatorConfig != null)
            target = npcProfile.animatorConfig.neutralLookTarget;

        bool immediate = !string.IsNullOrEmpty(speakerName) && speakerName == npcProfile.npcName;
        npcProfile.animatorConfig.SetSpeakerTarget(target, immediate);
    }
}

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
        var manager = NPCManager.Instance;
        if (manager != null && !manager.npcInstances.Contains(this))
            manager.npcInstances.Add(this);
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
    public void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null || ollamaClient == null) return;

        // Notify DialogueManager
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnUserAnswered(userText);
        }

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

        if (userInput != null)
            userInput.text = "";
    }
    
    /// <summary>
    /// Ask the LLM itself whether this NPC should ask a follow-up
    /// </summary>
    public async void AskLLMIfShouldRespond(string userAnswer)
    {
        if (!enableAutoResponse || isCurrentlySpeaking)
            return;
        
        // Build decision prompt
        string decisionPrompt = BuildTurnDecisionPrompt(userAnswer);
        
        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = decisionPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = userAnswer }
        };
        
        // Ask LLM for decision (should be quick, low temp)
        var response = await ollamaClient.SendChatAsync(
            messages,
            temperature: 0.3f, // Low temp for consistent decisions
            repeatPenalty: 1.0f,
            null,
            null,
            default
        );
        
        if (!string.IsNullOrEmpty(response.error))
            return;
        
        // Parse LLM decision
        string decision = response.content.Trim().ToLower();
        bool shouldRespond = decision.Contains("yes") || decision.Contains("respond");
        
        Debug.Log($"🤖 {npcProfile.npcName} LLM decision: {decision.Substring(0, Mathf.Min(50, decision.Length))}... → {(shouldRespond ? "RESPOND" : "PASS")}");
        
        // Check if we should force a response (second NPC if first passed)
        if (DialogueManager.Instance != null)
        {
            shouldRespond = DialogueManager.Instance.RecordDecision(npcProfile.npcName, shouldRespond);
        }
        
        if (shouldRespond)
        {
            // Request turn and generate actual response
            if (DialogueManager.Instance != null && DialogueManager.Instance.RequestTurn(npcProfile.npcName))
            {
                await ExecuteSpeech($"Ask a relevant follow-up question to: {userAnswer}");
            }
        }
    }
    
    /// <summary>
    /// Build prompt for LLM to decide whether to respond
    /// </summary>
    private string BuildTurnDecisionPrompt(string userAnswer)
    {
        var promptBuilder = new StringBuilder()
            .Append("You are ")
            .Append(npcProfile.npcName)
            .Append(". ")
            .Append(npcProfile.systemPrompt)
            .Append("\n\n=== TURN-TAKING DECISION ===\n")
            .Append("The candidate just answered. Decide if YOU should ask the next follow-up question.\n\n")
            .Append("Consider:\n")
            .Append("- Your role and expertise: ")
            .Append(npcProfile.personalityTraits)
            .Append('\n')
            .Append("- Is this answer related to YOUR area of interviewing?\n")
            .Append("- Do you have a relevant follow-up question?\n")
            .Append("- Should you let your co-interviewer ask instead?\n\n");

        if (DialogueManager.Instance != null)
        {
            bool wasLast = DialogueManager.Instance.WasLastSpeaker(npcProfile.npcName);
            string turnHistory = DialogueManager.Instance.GetTurnHistory();
            
            if (!string.IsNullOrEmpty(turnHistory))
                promptBuilder.Append("Recent turn order: ").Append(turnHistory).Append('\n');
            
            if (wasLast)
                promptBuilder.Append("⚠️ You just asked the last question - consider letting your co-interviewer take a turn.\n\n");
        }
        
        // Add conversation memory
        string recentConvo = memory.GetShortTermContext();
        if (!string.IsNullOrEmpty(recentConvo))
            promptBuilder.Append("Recent conversation:\n").Append(recentConvo).Append("\n\n");

        promptBuilder
            .Append("Respond with ONLY:\n")
            .Append("'YES' if you should ask a follow-up question\n")
            .Append("'NO' if your co-interviewer should ask instead\n")
            .Append("\nBe brief - just YES or NO with short reason.");

        return promptBuilder.ToString();
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

        // Stream response with TTS buffering
        var ttsBuffer = new StringBuilder();
        var displayBuffer = new StringBuilder();
        bool ttsActive = IsTTSEnabled;
        bool shouldStreamDisplay = !ttsActive;
        
        var response = await ollamaClient.SendChatAsync(
            messages,
            npcProfile.temperature,
            npcProfile.repeatPenalty,
            null,
            token => ProcessToken(token, ttsBuffer, displayBuffer, ttsActive, shouldStreamDisplay),
            cts.Token
        );

        if (!string.IsNullOrEmpty(response.error))
        {
            if (outputText) outputText.text = "Error: " + response.error;
            FinishSpeaking();
            return;
        }

        // Process remaining TTS buffer
        string fullDisplayText = displayBuffer.ToString();
        ProcessRemainingTTS(ttsBuffer, fullDisplayText, ttsActive);

        // Wait for TTS to finish before releasing turn
        if (ttsActive)
        {
            while (ttsHandler.IsSpeaking())
                await System.Threading.Tasks.Task.Delay(50);
        }

        // Store response - DON'T broadcast to other NPCs (they don't respond to each other)
        memory.AddDialogueTurn(npcProfile.npcName, response.content);
        
        LogMemoryState();
        FinishSpeaking();
    }

    /// <summary>
    /// Build system prompt with memory context
    /// </summary>
    private string BuildPromptWithMemory()
    {
        var promptBuilder = new StringBuilder(npcProfile.GetFullSystemPrompt());

        string mediumTermContext = memory.GetMediumTermContext();
        if (!string.IsNullOrEmpty(mediumTermContext))
            promptBuilder.Append("\n\n").Append(mediumTermContext);

        string shortTermContext = memory.GetShortTermContext();
        if (!string.IsNullOrEmpty(shortTermContext))
            promptBuilder.Append("\n\n").Append(shortTermContext);

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Process incoming tokens for metadata and TTS
    /// </summary>
    private void ProcessToken(
        string token,
        StringBuilder ttsBuffer,
        StringBuilder displayBuffer,
        bool ttsActive,
        bool shouldStreamDisplay)
    {
        foreach (char c in token)
        {
            if (HandleMetadataChar(c, displayBuffer, ttsBuffer, ttsActive))
                continue;

            displayBuffer.Append(c);

            if (ttsActive)
                HandleTTSChar(c, ttsBuffer, displayBuffer);
        }

        if (shouldStreamDisplay && outputText)
            outputText.text = displayBuffer.ToString();
    }

    private bool HandleMetadataChar(
        char c,
        StringBuilder displayBuffer,
        StringBuilder ttsBuffer,
        bool ttsActive)
    {
        if (!isParsingMetadata)
        {
            if (metadataBuffer.Length > 0)
            {
                int matchIndex = metadataBuffer.Length;
                if (matchIndex < MetadataOpenTag.Length && MetadataOpenTag[matchIndex] == c)
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
                metadataBuffer.Clear();
                // Allow current char to be processed normally
            }

            if (c == MetadataOpenTag[0])
            {
                metadataBuffer.Append(c);
                return true;
            }
        }

        if (isParsingMetadata)
        {
            metadataBuffer.Append(c);

            if (EndsWith(metadataBuffer, MetadataCloseTag))
            {
                int jsonLength = metadataBuffer.Length - MetadataCloseTag.Length;
                string jsonContent = metadataBuffer.ToString(0, jsonLength);
                ExecuteMetadata(NPCMetadata.ParseFromJson(jsonContent));

                metadataBuffer.Clear();
                isParsingMetadata = false;
            }
            return true;
        }

        return false;
    }

    private void FlushMetadataBuffer(
        StringBuilder displayBuffer,
        StringBuilder ttsBuffer,
        bool ttsActive)
    {
        if (metadataBuffer.Length == 0)
            return;

        int length = metadataBuffer.Length;
        for (int i = 0; i < length; i++)
        {
            char bufferedChar = metadataBuffer[i];
            displayBuffer.Append(bufferedChar);

            if (ttsActive)
                HandleTTSChar(bufferedChar, ttsBuffer, displayBuffer);
        }
    }

    private void HandleTTSChar(
        char c,
        StringBuilder ttsBuffer,
        StringBuilder displayBuffer)
    {
        ttsBuffer.Append(c);

        bool isSentenceEnding = c == '.' || c == '!' || c == '?';
        bool isLongClauseBreak = c == ',' && ttsBuffer.Length > 60;

        if (!isSentenceEnding && !isLongClauseBreak)
            return;

        string chunk = ttsBuffer.ToString().Trim();
        if (chunk.Length == 0)
            return;

        string displaySnapshot = displayBuffer.ToString();
        EnqueueTTSChunk(chunk, displaySnapshot);
        ttsBuffer.Clear();
    }

    private void EnqueueTTSChunk(string chunk, string displaySnapshot)
    {
        ttsHandler.EnqueueSpeech(chunk, () =>
        {
            if (outputText)
                outputText.text = displaySnapshot;
        });
    }

    private static bool EndsWith(StringBuilder builder, string value)
    {
        if (builder.Length < value.Length)
            return false;

        int start = builder.Length - value.Length;
        for (int i = 0; i < value.Length; i++)
        {
            if (builder[start + i] != value[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Execute metadata actions
    /// </summary>
    private void ExecuteMetadata(NPCMetadata metadata)
    {
        if (metadata == null || npcProfile.animatorConfig == null)
            return;
        
        if (!string.IsNullOrEmpty(metadata.animatorTrigger))
            npcProfile.animatorConfig.TriggerAnimation(metadata.animatorTrigger);

        string currentSpeaker = DialogueManager.Instance != null ? DialogueManager.Instance.currentSpeaker : string.Empty;
        bool isSelfSpeaking = !string.IsNullOrEmpty(currentSpeaker) && currentSpeaker == npcProfile.npcName;
        Transform focusTarget = null;

        var manager = NPCManager.Instance;
        if (manager != null)
        {
            focusTarget = isSelfSpeaking
                ? manager.GetLookTargetForSpeaker("User")
                : manager.GetLookTargetForSpeaker(currentSpeaker);

            if (focusTarget == null && npcProfile?.animatorConfig != null)
                focusTarget = npcProfile.animatorConfig.neutralLookTarget;
        }

        npcProfile.animatorConfig.ApplyMetadata(metadata, focusTarget);
    }

    /// <summary>
    /// Process remaining TTS buffer after response completes
    /// </summary>
    private void ProcessRemainingTTS(StringBuilder ttsBuffer, string fullDisplayText, bool ttsActive)
    {
        if (ttsActive && ttsBuffer.Length > 0)
        {
            string chunk = ttsBuffer.ToString().Trim();
            if (chunk.Length > 0)
            {
                EnqueueTTSChunk(chunk, fullDisplayText);
                ttsBuffer.Clear();
            }
        }
        else if (!ttsActive && outputText)
        {
            outputText.text = fullDisplayText;
        }
    }

    /// <summary>
    /// Receive message from another NPC - INTERVIEWERS DON'T RESPOND TO EACH OTHER
    /// </summary>
    public void ReceiveExternalMessage(string senderName, string message)
    {
        memory.AddDialogueTurn(senderName, message);
        Debug.Log($"📨 [{npcProfile.npcName}] Heard {senderName} (not responding - interviewer mode)");
        // Interviewers don't respond to each other, only to user
    }

    /// <summary>
    /// Add a fact to NPC's medium-term memory
    /// </summary>
    public void LearnFact(string fact)
    {
        memory.AddFact(fact);
    }

    private void ResetMetadataParsing()
    {
        metadataBuffer.Clear();
        isParsingMetadata = false;
    }

    private void FinishSpeaking()
    {
        isCurrentlySpeaking = false;
        
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ReleaseTurn(npcProfile.npcName);
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

    private void LogMemoryState()
    {
        int factCount = memory.GetFactCount();
        Debug.Log($"🧠 [{npcProfile.npcName}] Memory updated (short-term cap {memory.shortTermCapacity}, facts {factCount}).");

        // Update UI if available
        if (memoryDisplayText != null)
        {
            string display = memory.GetShortTermContext();
            memoryDisplayText.text = string.IsNullOrEmpty(display) ? string.Empty : display;
        }
    }

    [ContextMenu("Clear Memory")]
    public void ClearMemory()
    {
        memory.ClearAll();
        Debug.Log($"🔄 [{npcProfile.npcName}] Memory cleared");

        if (memoryDisplayText != null)
            memoryDisplayText.text = string.Empty;
    }
    
    [ContextMenu("Show Memory")]
    public void ShowMemory()
    {
        LogMemoryState();
    }
}

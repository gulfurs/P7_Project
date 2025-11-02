using System;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// NPC Chat Instance - Job Interview Agent
/// Handles conversation, turn-taking, and non-verbal behaviors
/// Uses LLMConfig for core roleplaying instructions
/// </summary>
public class NPCChatInstance : MonoBehaviour
{
    [Header("NPC Configuration")]
    public NPCProfile npcProfile;

    [Header("Component References")]
    public LlamaBridge llamaBridge;
    public NPCTTSHandler ttsHandler;
    
    [Header("Memory")]
    public LlamaMemory llamaMemory;

    [Header("Chat Settings")]
    public bool enableAutoResponse = true;

    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text memoryDisplayText;

    private bool isCurrentlySpeaking;

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

    private void InitializeComponents()
    {
        // Auto-find LlamaBridge if not assigned
        if (llamaBridge == null)
        {
            llamaBridge = FindObjectOfType<LlamaBridge>();
            if (llamaBridge == null)
            {
                Debug.LogError($"[NPCChat] '{gameObject.name}' needs a LlamaBridge in the scene!");
                return;
            }
        }

        // Get shared LlamaMemory
        if (llamaMemory == null)
        {
            llamaMemory = LlamaMemory.Instance;
        }

        // Register this NPC's system prompt using LLMConfig
        if (npcProfile != null)
        {
            var config = LLMConfig.Instance;
            string fullSystemPrompt = config.GetSystemPromptForNPC(npcProfile);
            llamaMemory.RegisterNPCPrompt(npcProfile.npcName, fullSystemPrompt);
            Debug.Log($"[NPCChat] Registered {npcProfile.npcName} with full system prompt");
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
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null || llamaBridge == null) return;

        // Get LlamaMemory
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;

        // Notify DialogueManager
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnUserAnswered(userText);
        }

        // Add user message to shared memory (everyone hears it)
        llamaMemory.AddDialogueTurn("User", userText);

        // Broadcast user answer to ALL interviewers
        var manager = NPCManager.Instance;
        if (manager != null)
        {
            Debug.Log($"📢 User answered: \"{userText}\"");
            
            foreach (var npc in manager.npcInstances)
            {
                if (npc != null)
                {
                    // Each NPC asks LLM if it should respond
                    npc.AskLLMIfShouldRespond(userText);
                }
            }
        }

        if (userInput != null)
            userInput.text = "";
    }
    
    /// <summary>
    /// Ask the LLM whether this NPC should respond to the candidate's answer
    /// Uses a simple decision prompt for turn-taking
    /// </summary>
    public void AskLLMIfShouldRespond(string userAnswer)
    {
        if (!enableAutoResponse || isCurrentlySpeaking)
            return;

        StartCoroutine(AskLLMIfShouldRespondRoutine(userAnswer));
    }

    private System.Collections.IEnumerator AskLLMIfShouldRespondRoutine(string userAnswer)
    {
        // Stagger responses to make conversation feel natural
        float delay = UnityEngine.Random.Range(0.05f, 0.35f);
        yield return new WaitForSeconds(delay);

        if (!enableAutoResponse || isCurrentlySpeaking)
            yield break;

        if (llamaBridge == null)
        {
            Debug.LogError("[NPCChat] LlamaBridge missing for decision call.");
            yield break;
        }

        // Don't interrupt if someone already has the turn
        if (DialogueManager.Instance != null && !string.IsNullOrEmpty(DialogueManager.Instance.currentSpeaker))
            yield break;

        // Simple decision prompt - should this NPC respond?
        string decisionPrompt = $"Should {npcProfile.npcName} ask a follow-up question about: \"{userAnswer}\"?\nAnswer YES or NO: ";

        // Queue the decision through the bridge
        string decisionRaw = string.Empty;
        bool decisionDone = false;
        
        llamaBridge.EnqueueGenerate(decisionPrompt, 0.5f, 1.0f, 16,
            (res) =>
            {
                decisionRaw = (res ?? string.Empty).Trim();
                decisionDone = true;
            });

        yield return new WaitUntil(() => decisionDone);

        string response = decisionRaw.ToLower();

        // Error handling - treat errors as 'no'
        if (response.Contains("error") || response.Contains("llama_decode") || response.Length == 0)
        {
            Debug.LogWarning($"[NPCChat] Decision error for {npcProfile.npcName}: {response}");
            yield break;
        }

        // Parse YES/NO from response
        bool wantsToSpeak = false;
        
        if (System.Text.RegularExpressions.Regex.IsMatch(response, @"\byes\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            wantsToSpeak = true;
        }
        else if (response.Contains("```") || response.Contains("def ") || response.Contains("python"))
        {
            // Model output garbage - treat as NO
            Debug.LogWarning($"[NPCChat] Garbage output from {npcProfile.npcName}, treating as NO");
            wantsToSpeak = false;
        }

        Debug.Log($"🤖 {npcProfile.npcName} decision: {response.Substring(0, Mathf.Min(80, response.Length))}... → {(wantsToSpeak ? "RESPOND" : "PASS")}");

        // Check with DialogueManager for forcing rules
        bool shouldRespond = wantsToSpeak;
        if (DialogueManager.Instance != null)
        {
            shouldRespond = DialogueManager.Instance.RecordDecision(npcProfile.npcName, shouldRespond);
        }

        if (shouldRespond)
        {
            // Request turn and generate actual interview question
            if (DialogueManager.Instance != null && DialogueManager.Instance.RequestTurn(npcProfile.npcName))
            {
                StartCoroutine(ExecuteSpeechRoutine(userAnswer));
            }
        }
    }
    
    /// <summary>
    /// Execute speech generation with metadata extraction
    /// Generates interview question with non-verbal behaviors
    /// </summary>
    private System.Collections.IEnumerator ExecuteSpeechRoutine(string messageText)
    {
        if (isCurrentlySpeaking) yield break;

        isCurrentlySpeaking = true;

        if (npcProfile == null || llamaBridge == null)
        {
            Debug.LogError($"[NPCChat] Missing npcProfile or llamaBridge!");
            FinishSpeaking();
            yield break;
        }

        // Get shared memory
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
        
        // Get recent conversation context (last 2 turns)
        string conversationHistory = llamaMemory.GetShortTermContext(2);

        string response = string.Empty;
        bool done = false;

        // Build minimal prompt: Short system + recent history + NPC name
        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.Append(npcProfile.GetShortSystemPrompt()).Append("\n\n");
        
        if (!string.IsNullOrEmpty(conversationHistory))
            promptBuilder.Append(conversationHistory).Append("\n");

        promptBuilder.Append(npcProfile.npcName).Append(": ");

        string fullPrompt = promptBuilder.ToString();

        // Enqueue generation via bridge (thread-safe)
        llamaBridge.EnqueueGenerate(
            fullPrompt, 
            npcProfile.temperature, 
            npcProfile.repeatPenalty, 
            64, 
            (res) => 
            { 
                response = (res ?? string.Empty).Trim(); 
                done = true; 
            });

        // Wait for completion
        yield return new WaitUntil(() => done);

        if (string.IsNullOrEmpty(response))
        {
            if (outputText) outputText.text = "No response";
            FinishSpeaking();
            yield break;
        }

        // Extract metadata (non-verbal actions) and spoken text
        var (metadata, displayText) = NPCMetadata.ProcessResponse(response);

        // Validate metadata - if missing, use defaults
        if (!response.TrimStart().StartsWith("[META]", StringComparison.Ordinal))
        {
            Debug.LogWarning($"[NPCChat] Response missing metadata block for {npcProfile.npcName}. Using defaults.");
            metadata = new NPCMetadata { animatorTrigger = "idle", isFocused = true, isIgnoring = false };
        }

        // Clean spoken text - remove role labels, code fences, etc.
        if (!string.IsNullOrEmpty(displayText))
        {
            // Remove role prefixes
            displayText = System.Text.RegularExpressions.Regex.Replace(
                displayText, 
                @"(?m)^(assistant\s*:|assistant:|user\s*:|user:)\s*", 
                "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove code fences
            displayText = displayText.Replace("```", "");
            
            // Strip NPC name prefix if present
            if (displayText.StartsWith(npcProfile.npcName + ":", StringComparison.OrdinalIgnoreCase))
            {
                int colon = displayText.IndexOf(":");
                if (colon >= 0 && colon + 1 < displayText.Length)
                    displayText = displayText.Substring(colon + 1).TrimStart();
            }
            
            // Collapse multiple newlines
            displayText = System.Text.RegularExpressions.Regex.Replace(displayText, "\n{2,}", "\n").Trim();
        }

        // Update UI with clean spoken text only
        if (outputText)
            outputText.text = displayText;

        // Execute metadata (animations, attention state, gaze)
        // IMPORTANT: These are internal behaviors, NOT spoken
        ExecuteMetadata(metadata);

        // Check if output is an error (don't TTS errors)
        bool isErrorOutput = false;
        if (!string.IsNullOrEmpty(displayText))
        {
            var low = displayText.ToLowerInvariant();
            if (low.Contains("error") || low.Contains("llama_decode") || low.StartsWith("[error:"))
                isErrorOutput = true;
        }

        // Process TTS if enabled and not an error
        if (IsTTSEnabled && ttsHandler != null && !isErrorOutput)
        {
            try
            {
                // Limit TTS to first sentence or 300 chars
                string ttsText = displayText ?? string.Empty;
                if (ttsText.Length > 300)
                {
                    int idx = ttsText.IndexOf('.', 200);
                    if (idx > 0)
                        ttsText = ttsText.Substring(0, idx + 1);
                    else
                        ttsText = ttsText.Substring(0, 300);
                }

                ttsHandler.ProcessResponseForTTS(ttsText);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NPCChat] TTS error: {e.Message}");
            }
        }

        // Store spoken text in shared memory
        string memoryText = isErrorOutput ? "[LLM ERROR]" : (displayText ?? string.Empty);
        llamaMemory.AddDialogueTurn(npcProfile.npcName, memoryText);
        
        Debug.Log($"[NPCChat] {npcProfile.npcName} spoke: \"{displayText}\"");
        LogMemoryState();

        FinishSpeaking();
        yield break;
    }

    /// <summary>
    /// Build system prompt with memory context
    /// </summary>
    private string BuildPromptWithMemory()
    {
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;

        return llamaMemory.BuildPromptForGeneration(npcProfile.npcName, 4);
    }

    /// <summary>
    /// Execute metadata actions (animations, attention, gaze)
    /// IMPORTANT: These are internal non-verbal behaviors, not spoken
    /// </summary>
    private void ExecuteMetadata(NPCMetadata metadata)
    {
        if (metadata == null || npcProfile.animatorConfig == null)
            return;
        
        // Trigger animation if specified
        if (!string.IsNullOrEmpty(metadata.animatorTrigger))
        {
            npcProfile.animatorConfig.TriggerAnimation(metadata.animatorTrigger);
            Debug.Log($"🎭 {npcProfile.npcName} non-verbal: {metadata.animatorTrigger}");
        }

        // Determine gaze target based on attention state
        string currentSpeaker = DialogueManager.Instance != null 
            ? DialogueManager.Instance.currentSpeaker 
            : string.Empty;
            
        bool isSelfSpeaking = !string.IsNullOrEmpty(currentSpeaker) && currentSpeaker == npcProfile.npcName;
        Transform focusTarget = null;

        var manager = NPCManager.Instance;
        if (manager != null)
        {
            // If speaking, look at user; otherwise look at current speaker
            focusTarget = isSelfSpeaking
                ? manager.GetLookTargetForSpeaker("User")
                : manager.GetLookTargetForSpeaker(currentSpeaker);

            // Fallback to neutral look target
            if (focusTarget == null && npcProfile?.animatorConfig != null)
                focusTarget = npcProfile.animatorConfig.neutralLookTarget;
        }

        // Apply metadata to animator config (sets attention state and gaze)
        npcProfile.animatorConfig.ApplyMetadata(metadata, focusTarget);
        
        Debug.Log($"👁️ {npcProfile.npcName} attention: focused={metadata.isFocused}, ignoring={metadata.isIgnoring}");
    }

    /// <summary>
    /// Receive message from another NPC
    /// In interview mode, interviewers don't respond to each other - only to candidates
    /// </summary>
    public void ReceiveExternalMessage(string senderName, string message)
    {
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
            
        llamaMemory.AddDialogueTurn(senderName, message);
        Debug.Log($"📨 [{npcProfile.npcName}] Heard {senderName} (interview mode - no cross-talk)");
        // Interviewers listen but don't respond to each other
    }

    /// <summary>
    /// Add a fact to NPC's medium-term memory
    /// </summary>
    public void LearnFact(string fact)
    {
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
        llamaMemory.AddFact(npcProfile.npcName, fact);
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
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;

        int factCount = llamaMemory.GetFactCount(npcProfile.npcName);
        Debug.Log($"🧠 [{npcProfile.npcName}] Shared memory updated (facts {factCount}).");

        // Update UI if available
        if (memoryDisplayText != null)
        {
            string display = llamaMemory.GetShortTermContext();
            memoryDisplayText.text = string.IsNullOrEmpty(display) ? string.Empty : display;
        }
    }

    [ContextMenu("Clear Memory")]
    public void ClearMemory()
    {
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
        llamaMemory.ClearAll();
        Debug.Log($"🔄 [{npcProfile.npcName}] Shared memory cleared");

        if (memoryDisplayText != null)
            memoryDisplayText.text = string.Empty;
    }
    
    [ContextMenu("Show Memory")]
    public void ShowMemory()
    {
        LogMemoryState();
    }
}

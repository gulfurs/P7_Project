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
            Debug.Log($"📢 User answered: {userText}");
            
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
    /// IMPORTANT: This decision prompt is NEVER added to shared memory
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

        // CRITICAL: Build a CLEAN, ISOLATED decision prompt that won't leak into memory
        // Use ONLY the character role and the user's answer - NO conversation history
        string decisionPrompt = $"You are {npcProfile.npcName}. The candidate said: \"{userAnswer}\"\n\nShould you ask a follow-up question? Reply with ONLY 'YES' or 'NO': ";

        // Queue the decision through the bridge
        string decisionRaw = string.Empty;
        bool decisionDone = false;
        
        llamaBridge.EnqueueGenerate(decisionPrompt, 0.3f, 1.0f, 8,
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
                // CRITICAL: Add small delay to ensure decision context is fully cleared from LLM
                yield return new WaitForSeconds(0.1f);
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
        
        // Get recent conversation context (last 3 turns)
        string conversationHistory = llamaMemory.GetShortTermContext(3);

        string response = string.Empty;
        bool done = false;

        // Build simple prompt
        var promptBuilder = new System.Text.StringBuilder();
        
        if (!string.IsNullOrEmpty(conversationHistory))
            promptBuilder.AppendLine(conversationHistory);

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

        // Debug: Log raw LLM output
        Debug.Log($"[NPCChat] 🔍 RAW LLM OUTPUT for {npcProfile.npcName}: {response}");

        if (string.IsNullOrEmpty(response))
        {
            if (outputText) outputText.text = "No response";
            FinishSpeaking();
            yield break;
        }

        // Clean and extract
        string cleanedResponse = CleanRawResponse(response);
        Debug.Log($"[NPCChat] 🧹 CLEANED: {cleanedResponse}");

        // If no valid response, use simple fallback
        if (string.IsNullOrEmpty(cleanedResponse))
        {
            cleanedResponse = "Tell me more about your experience.";
            Debug.LogWarning($"[NPCChat] Using fallback response");
        }

        // Extract metadata - if missing, treat entire response as text
        var (metadata, displayText) = NPCMetadata.ProcessResponse(cleanedResponse);
        
        // No metadata? Use defaults and treat response as plain text
        if (!cleanedResponse.TrimStart().StartsWith("[META]"))
        {
            metadata = new NPCMetadata { animatorTrigger = "idle", isFocused = true, isIgnoring = false };
            displayText = cleanedResponse; // Use entire cleaned response as speech
        }
        
        Debug.Log($"[NPCChat] 📝 DISPLAY: {displayText}");

        // MINIMAL cleaning - just remove NPC name prefix if present
        if (!string.IsNullOrEmpty(displayText))
        {
            if (displayText.StartsWith(npcProfile.npcName + ":", StringComparison.OrdinalIgnoreCase))
            {
                int colon = displayText.IndexOf(":");
                if (colon >= 0) displayText = displayText.Substring(colon + 1).Trim();
            }
        }

        // If empty, use fallback
        if (string.IsNullOrWhiteSpace(displayText))
        {
            displayText = "Can you tell me more?";
        }

        // Update UI with clean spoken text only
        if (outputText)
            outputText.text = displayText;

        // Execute metadata (animations, attention state, gaze)
        // IMPORTANT: These are internal behaviors, NOT spoken
        ExecuteMetadata(metadata);

        // Simple validation - just check it's not empty
        bool isValid = !string.IsNullOrWhiteSpace(displayText);

        // TTS
        if (IsTTSEnabled && ttsHandler != null && isValid)
        {
            ttsHandler.ProcessResponseForTTS(displayText);
        }

        // Add to memory
        if (isValid)
        {
            llamaMemory.AddDialogueTurn(npcProfile.npcName, displayText);
            Debug.Log($"[NPCChat] {npcProfile.npcName} spoke: {displayText}");
        }
        
        LogMemoryState();

        FinishSpeaking();
        yield break;
    }

    /// <summary>
    /// Clean raw LLM response - minimal cleaning only
    /// </summary>
    private string CleanRawResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        // Remove "Do not use..." instructions that the LLM echoed back
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"^Do not use [^.]+\.\s*",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove multiple "Do not..." statements
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"Do not use [^,]+,\s*",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove "just keep it simple" instructions
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"just keep it simple[^.]+\.\s*",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove decision prompt if it appears at the END
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"Should\s+\w+\s+ask.*?Answer\s+YES\s+or\s+NO.*$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        // Remove "yes" or "no" on its own line at the start
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"^\s*(yes|no)\s*\n",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove "User:" lines (conversation echoes)
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"User:\s*[^\n]+\n?",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove "Assistant:" or NPC name prefixes mid-text
        response = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"\n\s*(Assistant|superdude|epiclonewolf):\s*\n?",
            "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return response.Trim();
    }

    /// <summary>
    /// Check if string contains actual readable text (not just punctuation/whitespace)
    /// </summary>
    private bool ContainsActualText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Count alphanumeric characters
        int alphaCount = 0;
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
                alphaCount++;
        }

        // Need at least 3 alphanumeric characters to be valid text
        return alphaCount >= 3;
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

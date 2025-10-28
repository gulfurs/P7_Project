using System;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Main NPC chat instance - cleaner with delegated responsibilities
/// Directly references NPCProfile for system prompts
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
        // Auto-find LlamaBridge
        if (llamaBridge == null)
        {
            llamaBridge = FindObjectOfType<LlamaBridge>();
            if (llamaBridge == null)
            {
                Debug.LogError($"NPCChatInstance '{gameObject.name}' needs a LlamaBridge!");
                return;
            }
        }

        // Get LlamaMemory
        if (llamaMemory == null)
        {
            llamaMemory = LlamaMemory.Instance;
        }

        // Register this NPC's system prompt
        if (npcProfile != null)
        {
            llamaMemory.RegisterNPCPrompt(npcProfile.npcName, npcProfile.GetFullSystemPrompt());
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
    /// Ask the LLM itself whether this NPC should ask a follow-up
    /// </summary>
    public void AskLLMIfShouldRespond(string userAnswer)
    {
        // Run the decision process in a coroutine to stagger NPCs and avoid simultaneous model calls
        if (!enableAutoResponse || isCurrentlySpeaking)
            return;

        StartCoroutine(AskLLMIfShouldRespondRoutine(userAnswer));
    }

    private System.Collections.IEnumerator AskLLMIfShouldRespondRoutine(string userAnswer)
    {
        // Small random delay to stagger multiple NPCs and make conversation natural
        float delay = UnityEngine.Random.Range(0.05f, 0.35f);
        yield return new WaitForSeconds(delay);

        if (!enableAutoResponse || isCurrentlySpeaking)
            yield break;

        if (llamaBridge == null)
        {
            Debug.LogError("[NPCChat] LlamaBridge reference missing for decision call.");
            yield break;
        }

        // If someone already has the turn, don't bother
        if (DialogueManager.Instance != null && !string.IsNullOrEmpty(DialogueManager.Instance.currentSpeaker))
            yield break;

        // Build decision prompt inline - ULTRA simple with explicit instruction
        string decisionPrompt = $"Question: Should {npcProfile.npcName} respond to the candidate saying \"{userAnswer}\"?\nAnswer with only YES or NO: ";

        // Queue the decision request to avoid overlapping native calls
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

        // If the model produced an error or empty output, treat as 'no'
        if (response.Contains("error") || response.Contains("llama_decode") || response.Contains("[error:") || response.Length == 0)
        {
            Debug.LogWarning($"[NPCChat] Decision call produced error for {npcProfile.npcName}: {response}");
            yield break;
        }

        // Look for explicit yes/no in the response
        bool wantsToSpeak = false;
        
        // Check for YES first (more specific)
        if (System.Text.RegularExpressions.Regex.IsMatch(response, @"\byes\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            wantsToSpeak = true;
        }
        // If model outputs garbage (code, etc), default to NO
        else if (response.Contains("```") || response.Contains("def ") || response.Contains("python") || response.Contains("function"))
        {
            Debug.LogWarning($"[NPCChat] Model outputted code/garbage for {npcProfile.npcName}, treating as NO");
            wantsToSpeak = false;
        }
        // Explicit NO
        else if (System.Text.RegularExpressions.Regex.IsMatch(response, @"\bno\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            wantsToSpeak = false;
        }
        // Anything unclear defaults to NO
        else
        {
            Debug.LogWarning($"[NPCChat] Unclear response from {npcProfile.npcName}: {response.Substring(0, Mathf.Min(50, response.Length))}");
            wantsToSpeak = false;
        }

        Debug.Log($"🤖 {npcProfile.npcName} LLM decision: {response.Substring(0, Mathf.Min(80, response.Length))}... → {(wantsToSpeak ? "RESPOND" : "PASS")}");

        // Check with DialogueManager for forcing rules
        bool shouldRespond = wantsToSpeak;
        if (DialogueManager.Instance != null)
        {
            shouldRespond = DialogueManager.Instance.RecordDecision(npcProfile.npcName, shouldRespond);
        }

        if (shouldRespond)
        {
            // Request turn and generate actual response
            if (DialogueManager.Instance != null && DialogueManager.Instance.RequestTurn(npcProfile.npcName))
            {
                // Start the speech coroutine which will enqueue the prompt through the bridge
                StartCoroutine(ExecuteSpeechRoutine(userAnswer));
            }
        }
    }
    
    /// <summary>
    /// Internal method to execute the actual speech
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

        // Get the NPC's system prompt directly from their profile
        string systemPrompt = npcProfile.GetFullSystemPrompt();

        // Get conversation history from shared memory
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
        
        string conversationHistory = llamaMemory.GetShortTermContext(2); // Only last 2 turns

        string response = string.Empty;
        bool done = false;

        // Build MINIMAL prompt: Short system + recent history + NPC name
        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.Append(npcProfile.GetShortSystemPrompt()).Append("\n\n");
        
        // Add recent conversation
        if (!string.IsNullOrEmpty(conversationHistory))
            promptBuilder.Append(conversationHistory).Append("\n");

        // NPC continues
        promptBuilder.Append(npcProfile.npcName).Append(": ");

        string fullPrompt = promptBuilder.ToString();

        // Enqueue prompt via bridge
        llamaBridge.EnqueueGenerate(fullPrompt, npcProfile.temperature, npcProfile.repeatPenalty, 64, (res) => { response = (res ?? string.Empty).Trim(); done = true; });

        // Wait until the request completes
        yield return new WaitUntil(() => done);

        if (string.IsNullOrEmpty(response))
        {
            if (outputText) outputText.text = "No response";
            FinishSpeaking();
            yield break;
        }

        // Extract metadata and clean display text
        string originalResponse = response;
        var (metadata, displayText) = NPCMetadata.ProcessResponse(response);

        // If the model didn't include metadata at the start, give it one retry with a short format reminder
        if (!response.TrimStart().StartsWith("[META]", StringComparison.Ordinal))
        {
            Debug.LogWarning($"[NPCChat] Response missing metadata block for {npcProfile.npcName}. Using defaults.");
            // Use default metadata instead of retry
            metadata = new NPCMetadata { animatorTrigger = "idle", isFocused = true, isIgnoring = false };
        }

        // Clean spoken text: remove assistant/user labels, code fences, and collapse repeated whitespace
        if (!string.IsNullOrEmpty(displayText))
        {
            // Remove common role prefixes like 'Assistant:' or 'User:' at line starts
            displayText = System.Text.RegularExpressions.Regex.Replace(displayText, @"(?m)^(assistant\s*:|assistant:|user\s*:|user:)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove any remaining code fences
            displayText = displayText.Replace("```", "");
            // Strip a leading "Name:" prefix if present
            if (displayText.StartsWith(npcProfile.npcName + ":", StringComparison.OrdinalIgnoreCase))
            {
                int colon = displayText.IndexOf(":");
                if (colon >= 0 && colon + 1 < displayText.Length)
                    displayText = displayText.Substring(colon + 1).TrimStart();
            }
            // Collapse multiple newlines and trim
            displayText = System.Text.RegularExpressions.Regex.Replace(displayText, "\n{2,}", "\n").Trim();
        }

        // Update UI with clean text only
        if (outputText)
            outputText.text = displayText;

        // Execute metadata (animations, attention state, gaze)
        ExecuteMetadata(metadata);

        // Decide whether the output is an error (don't TTS error strings)
        bool isErrorOutput = false;
        if (!string.IsNullOrEmpty(displayText))
        {
            var low = displayText.ToLowerInvariant();
            if (low.Contains("error") || low.Contains("llama_decode") || low.StartsWith("[error:") )
                isErrorOutput = true;
        }

        // Process TTS if enabled and not an error
        if (IsTTSEnabled && ttsHandler != null && !isErrorOutput)
        {
            try
            {
                // Limit TTS length to first sentence/300 chars to improve responsiveness
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

        // Store the spoken text in memory (or a short error message)
        string memoryText = isErrorOutput ? "[LLM ERROR]" : (displayText ?? string.Empty);
        llamaMemory.AddDialogueTurn(npcProfile.npcName, memoryText);
        Debug.Log($"[NPCChat] Raw LLM response: {response}");
        
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
    /// Receive message from another NPC - INTERVIEWERS DON'T RESPOND TO EACH OTHER
    /// </summary>
    public void ReceiveExternalMessage(string senderName, string message)
    {
        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;
        llamaMemory.AddDialogueTurn(senderName, message);
        Debug.Log($"📨 [{npcProfile.npcName}] Heard {senderName} (not responding - interviewer mode)");
        // Interviewers don't respond to each other, only to user
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

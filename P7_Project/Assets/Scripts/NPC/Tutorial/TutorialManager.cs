using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

/// <summary>
/// Tutorial Manager - simplified NPC for tutorial
/// - Has its own Send method
/// - No animator/context/personality prompts
/// - Doesn't broadcast to other NPCs
/// - Shows "now loading..." until TTS output
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Configuration")]
    public NPCProfile npcProfile;
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text instructionText;
    public GameObject objectToHideOnComplete; // Drag the Canvas/UI here
    
    [Header("Interview Transition")]
    public Canvas interviewCanvas;           // Interview UI to show after tutorial
    public InteractManager interactManager;  // To freeze/unfreeze interaction
    
    [Header("Component References")]
    public OllamaChatClient ollamaClient;
    public NPCTTSHandler ttsHandler;
    
    [Header("Instruction Messages")]
    public List<string> instructionMessages = new List<string>();
    
    private bool isCurrentlySpeaking = false;
    private CancellationTokenSource cts;

    void Start()
    {
        InitializeComponents();
        
        // Show tutorial welcome message
        if (outputText)
        {
            outputText.text = @"Welcome to Job Gauntlet!


In this simulation, you will talk to 2 virtual job interviewers. 
See them as real job interviewers and this as a real interview. 
So talk as you naturally would.


Here are the steps to get started:

Step 1: When you are ready to talk, just start speaking into the microphone.

Step 2: The system will naturally catch your voice, and your answer will be picked up by the job interviewers.

Step 3: When you are ready, type in the field below to start the experience.";
        }
        
        // Warm up system with silent call
        WarmUpSystem();
        
        // Hook into input submit for continue
        if (userInput != null)
        {
            userInput.onSubmit.AddListener((string text) => { ContinueToInterview(); });
        }
    }
    
    private async void ContinueToInterview()
    {
        // Hide tutorial UI and start interview
        if (objectToHideOnComplete != null)
            objectToHideOnComplete.SetActive(false);
        
        await System.Threading.Tasks.Task.Delay(500);
        TransitionToInterview();
    }
    
    private async void WarmUpSystem()
    {
        // Silent warmup call to initialize LLM
        if (ollamaClient == null || npcProfile == null) return;
        
        string systemPrompt = npcProfile.systemPrompt;
        if (string.IsNullOrEmpty(systemPrompt))
        {
            systemPrompt = "You are a friendly and helpful AI assistant.";
        }

        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = systemPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = "Hi" }
        };

        cts?.Cancel();
        cts = new CancellationTokenSource();
        
        // Silent call - no UI updates
        await ollamaClient.SendChatAsync(
            messages,
            0f,
            1.1f,
            null,
            (token) => { }, // Ignore output
            cts.Token
        ).ConfigureAwait(true);
        
        Debug.Log("[Tutorial] System warmed up - ready for interview");
    }

    private void InitializeComponents()
    {
        // Auto-find OllamaClient
        if (ollamaClient == null)
        {
            ollamaClient = FindObjectOfType<OllamaChatClient>();
            if (ollamaClient == null)
            {
                Debug.LogError("TutorialManager: OllamaChatClient not found!");
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
            
            ttsHandler.Initialize(npcProfile.audioSource, npcProfile.voiceName);
        }
    }

    /// <summary>
    /// Send message from UI input
    /// </summary>
    public async void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null || ollamaClient == null) 
            return;

        // Clear input
        if (userInput != null)
            userInput.text = "";
        
        // Show loading state (index 1 in instructionMessages)
        if (instructionText && instructionMessages.Count > 1)
            instructionText.text = instructionMessages[1];
        
        await ExecuteSpeech(userText);
    }

    /// <summary>
    /// Execute speech response
    /// </summary>
    private async System.Threading.Tasks.Task ExecuteSpeech(string messageText)
    {
        if (isCurrentlySpeaking) return;
        
        isCurrentlySpeaking = true;
        
        cts?.Cancel();
        cts = new CancellationTokenSource();

        // Build simple prompt - ONLY system prompt, no context/personality/animator
        string systemPrompt = npcProfile.systemPrompt;
        if (string.IsNullOrEmpty(systemPrompt))
        {
            systemPrompt = "You are a friendly and helpful AI assistant. Your role is to welcome the user to this job interview simulation and give them a brief, warm introduction. Keep your response to one or two short sentences.";
        }

        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = systemPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = messageText }
        };

        // Clear output
        if (outputText) outputText.text = "";

        // Stream response with TTS buffering
        var ttsBuffer = new StringBuilder();
        var displayBuffer = new StringBuilder();
        bool shouldStreamDisplay = !npcProfile.enableTTS;
        
        // Use higher temperature for variety in tutorial responses
        float tutorialTemperature = 0f;  // Higher than default for variety
        float tutorialRepeatPenalty = 1.1f;
        
        var response = await ollamaClient.SendChatAsync(
            messages,
            tutorialTemperature,
            tutorialRepeatPenalty,
            null,
            (token) => ProcessToken(token, ttsBuffer, displayBuffer, shouldStreamDisplay),
            cts.Token
        ).ConfigureAwait(true);

        if (!string.IsNullOrEmpty(response.error))
        {
            if (outputText) outputText.text = "Error: " + response.error;
            FinishSpeaking();
            return;
        }

        // Process remaining TTS buffer
        string fullDisplayText = displayBuffer.ToString();
        ProcessRemainingTTS(ttsBuffer, fullDisplayText);

        // Show empty state (index 2) while TTS plays
        if (instructionText && instructionMessages.Count > 2)
            instructionText.text = instructionMessages[2];

        // Wait for TTS to finish
        if (npcProfile.enableTTS && ttsHandler != null)
        {
            int waitCount = 0;
            while (ttsHandler.IsSpeaking())
            {
                waitCount++;
                await System.Threading.Tasks.Task.Delay(50, cts.Token);
                
                // Safety timeout: 60 seconds max
                if (waitCount > 1200)
                {
                    Debug.LogWarning("[Tutorial] TTS timeout - proceeding anyway");
                    break;
                }
            }
            Debug.Log($"[Tutorial] Waited {waitCount * 50}ms for TTS to complete");
        }
        else
        {
            Debug.LogWarning($"[Tutorial] TTS disabled or handler null. enableTTS={npcProfile?.enableTTS}, handler={ttsHandler != null}");
        }

        // AFTER TTS finishes, show final message (index 3)
        if (instructionText && instructionMessages.Count > 3)
        {
            instructionText.text = instructionMessages[3];
        }
        
        // Wait briefly before transition (reduced from 2000)
        await System.Threading.Tasks.Task.Delay(500);
        
        // SMOOTH TRANSITION: Hide tutorial, start interview
        if (objectToHideOnComplete != null)
            objectToHideOnComplete.SetActive(false);
        
        // Start interview seamlessly
        await System.Threading.Tasks.Task.Delay(200); // Brief pause (reduced from 500)
        TransitionToInterview();
        
        FinishSpeaking();
    }

    /// <summary>
    /// Process incoming tokens for TTS
    /// </summary>
    private void ProcessToken(string token, StringBuilder ttsBuffer, StringBuilder displayBuffer, bool shouldStreamDisplay = true)
    {
        foreach (char c in token)
        {
            displayBuffer.Append(c);
            
            // TTS processing
            if (npcProfile.enableTTS && ttsHandler != null)
            {
                ttsBuffer.Append(c);
                
                // Process on sentence endings
                if (c == '.' || c == '!' || c == '?' || (ttsBuffer.Length > 60 && c == ','))
                {
                    string chunk = ttsBuffer.ToString().Trim();
                    if (chunk.Length > 0)
                    {
                        string currentDisplayText = displayBuffer.ToString();
                        ttsHandler.EnqueueSpeech(chunk, () => 
                        {
                            if (outputText && !shouldStreamDisplay)
                            {
                                outputText.text = currentDisplayText;
                            }
                        });
                        ttsBuffer.Clear();
                    }
                }
            }
        }
        
        // Update UI if streaming display
        if (shouldStreamDisplay && outputText) 
            outputText.text = displayBuffer.ToString();
    }

    /// <summary>
    /// Process remaining TTS buffer
    /// </summary>
    private void ProcessRemainingTTS(StringBuilder ttsBuffer, string fullDisplayText)
    {
        if (npcProfile.enableTTS && ttsHandler != null && ttsBuffer.Length > 0)
        {
            string chunk = ttsBuffer.ToString().Trim();
            if (chunk.Length > 0)
            {
                ttsHandler.EnqueueSpeech(chunk, () => 
                {
                    if (outputText) 
                    {
                        outputText.text = fullDisplayText;
                    }
                });
            }
        }
        else if (!npcProfile.enableTTS || ttsHandler == null)
        {
            if (outputText) outputText.text = fullDisplayText;
        }
    }

    /// <summary>
    /// Finish speaking
    /// </summary>
    private void FinishSpeaking()
    {
        isCurrentlySpeaking = false;
    }

    /// <summary>
    /// Transition from tutorial to interview (seamless)
    /// </summary>
    private void TransitionToInterview()
    {
        Debug.Log("🎬 Tutorial complete → Interview starting");
        
        // Clear interview state
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ClearHistory();
        
        foreach (var npc in NPCManager.Instance.npcInstances)
            npc?.ClearMemory();
        
        // Show interview UI
        if (interviewCanvas != null)
            interviewCanvas.gameObject.SetActive(true);
        
        // NOTE: WhisperContinuous is enabled by Interview controller
        // Not here - tutorial is purely text-input
        
        // Freeze world interaction
        if (interactManager != null)
            interactManager.UnlockInteract(false);
        
        Debug.Log("📢 Ready for interview. Kicking off introductions...");
        
        // Kick off the interview introductions
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartInterview();
        }
    }
}
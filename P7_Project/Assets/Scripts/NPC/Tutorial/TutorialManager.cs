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
        
        // Show initial instruction (index 0)
        if (instructionText && instructionMessages.Count > 0)
            instructionText.text = instructionMessages[0];
        
        // Hook into input submit
        if (userInput != null)
        {
            userInput.onSubmit.AddListener((string text) => { Send(); });
        }
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
            systemPrompt = "You are a helpful tutorial guide.";
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
        
        var response = await ollamaClient.SendChatAsync(
            messages,
            npcProfile.temperature,
            npcProfile.repeatPenalty,
            null,
            (token) => ProcessToken(token, ttsBuffer, displayBuffer, shouldStreamDisplay),
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
        ProcessRemainingTTS(ttsBuffer, fullDisplayText);

        // Show empty state (index 2) while TTS plays
        if (instructionText && instructionMessages.Count > 2)
            instructionText.text = instructionMessages[2];

        // Wait for TTS to finish
        if (npcProfile.enableTTS && ttsHandler != null)
        {
            while (ttsHandler.IsSpeaking())
                await System.Threading.Tasks.Task.Delay(50, cts.Token);
        }

        // AFTER TTS finishes, show final message (index 3)
        if (instructionText && instructionMessages.Count > 3)
        {
            instructionText.text = instructionMessages[3];
        }
        
        // Hide the object after brief pause
        await System.Threading.Tasks.Task.Delay(2000);
        if (objectToHideOnComplete != null)
        {
            objectToHideOnComplete.SetActive(false);
        }
        
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
}

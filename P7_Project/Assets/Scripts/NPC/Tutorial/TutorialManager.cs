using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Tutorial Manager - simplified NPC for tutorial
/// - Has its own Send method
/// - Uses LlamaMemory for LLM responses
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
    public NPCTTSHandler ttsHandler;
    
    [Header("Instruction Messages")]
    public List<string> instructionMessages = new List<string>();
    
    private bool isCurrentlySpeaking = false;
    private LlamaMemory llamaMemory;

    void Start()
    {
        llamaMemory = LlamaMemory.Instance;
        InitializeComponents();
        
        // Register tutorial NPC profile system prompt
        if (npcProfile != null)
        {
            string systemPrompt = npcProfile.GetFullSystemPrompt();
            llamaMemory.RegisterNPCPrompt("Tutorial", systemPrompt);
        }
        
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
    public void Send()
    {
        var userText = userInput != null ? userInput.text : "";
        if (string.IsNullOrWhiteSpace(userText) || npcProfile == null) 
            return;

        // Clear input
        if (userInput != null)
            userInput.text = "";
        
        // Show loading state (index 1 in instructionMessages)
        if (instructionText && instructionMessages.Count > 1)
            instructionText.text = instructionMessages[1];
        
        ExecuteSpeech(userText);
    }

    /// <summary>
    /// Execute speech response
    /// </summary>
    private void ExecuteSpeech(string messageText)
    {
        if (isCurrentlySpeaking) return;
        
        isCurrentlySpeaking = true;

        // Add user message to shared memory
        llamaMemory.AddDialogueTurn("User", messageText);

        // Clear output
        if (outputText) outputText.text = "";

        // Get LLM response from LlamaBridge
        string fullPrompt = llamaMemory.BuildPromptForGeneration("Tutorial", 4);
        string response = LlamaBridge.Instance.GenerateText(
            fullPrompt,
            npcProfile.temperature,
            npcProfile.repeatPenalty,
            maxTokens: 256
        );

        if (string.IsNullOrEmpty(response))
        {
            if (outputText) outputText.text = "Error: No response from LLM";
            FinishSpeaking();
            return;
        }

        // Add response to shared memory
        llamaMemory.AddDialogueTurn("Tutorial", response);

        // Parse metadata and display text
        var (metadata, displayText) = NPCMetadata.ProcessResponse(response);
        
        // Apply metadata if any
        if (metadata != null)
        {
            // Tutorial is simple, so minimal metadata usage
        }

        // Display response
        if (outputText) outputText.text = displayText;

        // Queue TTS if enabled
        if (npcProfile.enableTTS && ttsHandler != null)
        {
            ttsHandler.ProcessResponseForTTS(displayText);
            
            // Show empty state (index 2) while TTS plays
            if (instructionText && instructionMessages.Count > 2)
                instructionText.text = instructionMessages[2];
            
            // Wait for TTS to finish, then show final state
            StartCoroutine(WaitForTTSAndFinish());
        }
        else
        {
            // No TTS, show final message immediately
            if (instructionText && instructionMessages.Count > 3)
            {
                instructionText.text = instructionMessages[3];
            }
            StartCoroutine(HideObjectAndFinish());
        }
    }

    /// <summary>
    /// Wait for TTS to finish speaking
    /// </summary>
    private System.Collections.IEnumerator WaitForTTSAndFinish()
    {
        while (ttsHandler != null && ttsHandler.IsSpeaking())
            yield return new WaitForSeconds(0.05f);

        // AFTER TTS finishes, show final message (index 3)
        if (instructionText && instructionMessages.Count > 3)
        {
            instructionText.text = instructionMessages[3];
        }
        
        yield return StartCoroutine(HideObjectAndFinish());
    }

    /// <summary>
    /// Hide object after brief pause and finish speaking
    /// </summary>
    private System.Collections.IEnumerator HideObjectAndFinish()
    {
        yield return new WaitForSeconds(2f);
        if (objectToHideOnComplete != null)
        {
            objectToHideOnComplete.SetActive(false);
        }
        FinishSpeaking();
    }

    /// <summary>
    /// Finish speaking
    /// </summary>
    private void FinishSpeaking()
    {
        isCurrentlySpeaking = false;
    }
}

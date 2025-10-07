using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;
/*

public class NPCChatInstance : MonoBehaviour
{
    [Header("NPC Configuration")]
    public NPCProfile npcProfile;

    [Header("Ollama Client Reference")]
    public OllamaChatClient ollamaClient;

    [Header("Chat Settings")]
    public bool broadcastResponses = true;
    public bool enableAutoResponse = true; // NPCs respond automatically to messages
    public float responseDelay = 2f; // Delay before responding (seconds)
    public float responseChance = 0.8f; // Chance to respond to a message (0-1)

    [Header("UI")]
    public TMP_InputField userInput;
    public TMP_Text outputText;
    public TMP_Text npcNameLabel;
    public TMP_Text externalMessagesText;

    private readonly List<string> externalMessages = new List<string>();
    private CancellationTokenSource cts;
    private bool isCurrentlySpeaking = false;
    
    // TTS Queue System
    private readonly Queue<string> ttsQueue = new Queue<string>();
    private bool isProcessingTTS = false;
    
    // Non-verbal metadata tracking
    private NPCMetadata currentMetadata;
    private string metadataBuffer = "";
    private bool isParsingMetadata = false;

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

        SendMessage(userText);
    }

    /// <summary>
    /// Send a message with direct token streaming to TTS
    /// </summary>
    public async void SendMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || npcProfile == null || ollamaClient == null || isCurrentlySpeaking) return;

        isCurrentlySpeaking = true;

        // Clear input if it was from UI
        if (userInput != null && userInput.text == messageText) userInput.text = "";

        // Cancel any ongoing request
        cts?.Cancel();
        cts = new CancellationTokenSource();

        // Build system prompt including external messages
        string fullSystemPrompt = npcProfile.GetFullSystemPrompt();
        if (externalMessages.Count > 0)
        {
            fullSystemPrompt += "\n\nRecent messages from other characters:\n" + string.Join("\n", externalMessages);
        }

        // Create single conversation - no history, just current user message
        var messages = new List<OllamaChatClient.ChatMessage>
        {
            new OllamaChatClient.ChatMessage { role = "system", content = fullSystemPrompt },
            new OllamaChatClient.ChatMessage { role = "user", content = messageText }
        };

        // Clear output
        if (outputText) outputText.text = "";

        // Reset metadata tracking
        currentMetadata = null;
        metadataBuffer = "";
        isParsingMetadata = false;

        // Direct token-based TTS streaming
        var ttsBuffer = new StringBuilder();
        var displayBuffer = new StringBuilder(); // For display without metadata tags
        
        var response = await ollamaClient.SendChatAsync(
            messages,
            npcProfile.temperature,
            npcProfile.repeatPenalty,
            (streamContent) => {
                // Display is handled per-token now
            },
            (token) => {
                // Parse metadata tags and extract dialogue
                ProcessToken(token, ttsBuffer, displayBuffer);
            },
            cts.Token
        );

        if (!string.IsNullOrEmpty(response.error))
        {
            if (outputText) outputText.text = "Error: " + response.error;
            isCurrentlySpeaking = false;
            return;
        }

        // Process any remaining text in buffer for TTS
        if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled && ttsBuffer.Length > 0)
        {
            string chunk = ttsBuffer.ToString().Trim();
            if (chunk.Length > 0)
            {
                ttsQueue.Enqueue(chunk);
                if (!isProcessingTTS) StartCoroutine(ProcessTTSQueue());
            }
        }

        // Broadcast response to other NPCs
        if (broadcastResponses && NPCManager.Instance != null)
        {
            NPCManager.Instance.BroadcastMessage(this, response.content);
        }
        
        isCurrentlySpeaking = false;
    }

    public void ReceiveExternalMessage(string senderName, string message)
    {
        externalMessages.Add($"{senderName}: {message}");
        if (externalMessages.Count > 3) externalMessages.RemoveAt(0);
        
        if (externalMessagesText != null)
            externalMessagesText.text = "Messages from others:\n" + string.Join("\n", externalMessages);
        
        // Simple auto-respond
        if (enableAutoResponse && !isCurrentlySpeaking && UnityEngine.Random.value < responseChance)
            Invoke(nameof(AutoRespond), responseDelay + UnityEngine.Random.Range(-0.5f, 1f));
    }
    
    private void AutoRespond()
    {
        if (!isCurrentlySpeaking && externalMessages.Count > 0)
        {
            string lastMessage = externalMessages[externalMessages.Count - 1];
            SendMessage($"Respond to: {lastMessage}");
        }
    }

    /// <summary>
    /// Process incoming tokens, extract metadata, and separate dialogue for TTS
    /// </summary>
    private void ProcessToken(string token, StringBuilder ttsBuffer, StringBuilder displayBuffer)
    {
        foreach (char c in token)
        {
            // Parsing metadata content (already inside [META]...)
            if (isParsingMetadata)
            {
                metadataBuffer += c;
                
                // Check for metadata end tag
                if (metadataBuffer.EndsWith("[/META]"))
                {
                    // Extract JSON and execute inline
                    string jsonContent = metadataBuffer.Substring(0, metadataBuffer.Length - 7);
                    currentMetadata = NPCMetadata.ParseFromJson(jsonContent);
                    
                    // Execute animator trigger immediately
                    if (!string.IsNullOrEmpty(currentMetadata.animatorTrigger) && npcProfile.animatorConfig != null)
                        npcProfile.animatorConfig.TriggerAnimation(currentMetadata.animatorTrigger);
                    
                    // Apply attention states
                    if (currentMetadata.isFocused && npcProfile.animatorConfig != null)
                        npcProfile.animatorConfig.SetAttentionState(AttentionState.Focused);
                    else if (currentMetadata.isIgnoring && npcProfile.animatorConfig != null)
                        npcProfile.animatorConfig.SetAttentionState(AttentionState.Ignoring);
                    else if (npcProfile.animatorConfig != null)
                        npcProfile.animatorConfig.SetAttentionState(AttentionState.Idle);
                    
                    // Reset
                    isParsingMetadata = false;
                    metadataBuffer = "";
                    continue;
                }
                // Still collecting metadata, don't add to display
                continue;
            }
            
            // Check for metadata start tag
            if (metadataBuffer.Length < 6)
            {
                metadataBuffer += c;
                
                // Found complete [META] tag
                if (metadataBuffer == "[META]")
                {
                    isParsingMetadata = true;
                    metadataBuffer = "";
                    continue;
                }
                
                // Check if we're still building [META]
                if ("[META]".StartsWith(metadataBuffer))
                {
                    // Still possibly building [META], wait for more chars
                    continue;
                }
                else
                {
                    // Not a [META] tag, flush accumulated buffer to display
                    displayBuffer.Append(metadataBuffer);
                    
                    // Also add to TTS if enabled
                    if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled)
                    {
                        ttsBuffer.Append(metadataBuffer);
                    }
                    
                    metadataBuffer = "";
                    continue;
                }
            }
            
            // Normal dialogue text (not in metadata, not building [META] tag)
            displayBuffer.Append(c);
            
            // TTS processing - larger chunks for performance
            if (npcProfile.enableTTS && NPCManager.Instance.globalTTSEnabled)
            {
                ttsBuffer.Append(c);
                
                // Process on sentence endings or long phrases
                if (c == '.' || c == '!' || c == '?' || (ttsBuffer.Length > 60 && c == ','))
                {
                    string chunk = ttsBuffer.ToString().Trim();
                    if (chunk.Length > 0)
                    {
                        ttsQueue.Enqueue(chunk);
                        if (!isProcessingTTS) StartCoroutine(ProcessTTSQueue());
                        ttsBuffer.Clear();
                    }
                }
            }
        }
        
        // Update UI with clean dialogue (no metadata tags)
        if (outputText) outputText.text = displayBuffer.ToString();
    }

    /// <summary>
    /// Process TTS queue sequentially to maintain order
    /// </summary>
    private IEnumerator ProcessTTSQueue()
    {
        isProcessingTTS = true;
        
        while (ttsQueue.Count > 0)
        {
            string textChunk = ttsQueue.Dequeue();
            yield return StartCoroutine(ProcessSingleTTSChunk(textChunk));
        }
        
        isProcessingTTS = false;
    }
    
    /// <summary>
    /// Process a single TTS chunk and play it immediately
    /// </summary>
    private IEnumerator ProcessSingleTTSChunk(string textChunk)
    {
        if (string.IsNullOrEmpty(textChunk) || npcProfile?.audioSource == null) yield break;

        // Generate TTS in background thread
        var ttsTask = System.Threading.Tasks.Task.Run(() => GenerateAudioData(textChunk));
        
        // Wait for TTS generation - reduced polling interval
        while (!ttsTask.IsCompleted)
            yield return null; // Wait one frame instead of fixed time
        
        var audioBytes = ttsTask.Result;
        if (audioBytes != null && audioBytes.Length > 0)
        {
            // Convert to Unity AudioClip - optimized with BitConverter
            int sampleCount = audioBytes.Length / 2;
            float[] audioData = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                audioData[i] = System.BitConverter.ToInt16(audioBytes, i * 2) / 32768f;
            
            AudioClip clip = AudioClip.Create($"TTS_{Time.time}", sampleCount, 1, 22050, false);
            clip.SetData(audioData, 0);
            
            // Wait for current audio to finish
            while (npcProfile.audioSource.isPlaying)
                yield return null;
            
            npcProfile.audioSource.clip = clip;
            npcProfile.audioSource.Play();
            
            // Wait for this chunk to finish
            while (npcProfile.audioSource.isPlaying)
                yield return null;
        }
    }
    
    /// <summary>
    /// Generate audio data in background thread
    /// </summary>
    private byte[] GenerateAudioData(string text)
    {
        string tempScript = System.IO.Path.GetTempFileName() + ".py";
        string cleanText = text.Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", " ").Trim();
        
        if (string.IsNullOrEmpty(cleanText)) return new byte[0];
        
        string script = $@"
from piper import PiperVoice
import sys

voice = PiperVoice.load('{npcProfile.voiceName}.onnx')
text = '{cleanText}'

audio_data = []
for chunk in voice.synthesize(text):
    audio_data.extend(chunk.audio_int16_bytes)

sys.stdout.buffer.write(bytes(audio_data))
";

        System.IO.File.WriteAllText(tempScript, script);
        
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "python";
        process.StartInfo.Arguments = $"\"{tempScript}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        
        process.Start();
        
        var audioBytes = new List<byte>();
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                audioBytes.Add(buffer[i]);
        }
        
        process.WaitForExit();
        process?.Dispose();
        System.IO.File.Delete(tempScript);
        
        return audioBytes.ToArray();
    }




}

*/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Passive latency monitoring system that hooks into the live pipeline
/// Measures real-time performance during actual gameplay
/// </summary>
public class PipelineTester : MonoBehaviour
{
    [Header("Monitoring Targets")]
    public NPCChatInstance targetNPC;
    public WindowsDictation windowsDictation;  // Use WindowsDictation for STT
    
    [Header("Settings")]
    [Tooltip("Automatically start monitoring on game start")]
    public bool startMonitoringOnStart = true;
    
    [Tooltip("Maximum number of interactions to log (0 = unlimited)")]
    public int maxInteractionsToLog = 0;

    private bool isMonitoring = false;
    private int interactionCount = 0;
    private bool waitingForUserInput = false;
    private bool waitingForLLMResponse = false;
    private string currentUserInput = "";

    void Start()
    {
        if (startMonitoringOnStart)
            StartMonitoring();
    }

    [ContextMenu("Start Monitoring")]
    public void StartMonitoring()
    {
        if (isMonitoring)
        {
            Debug.LogWarning("[PipelineTester] Already monitoring!");
            return;
        }

        // Ensure LatencyEvaluator exists
        if (LatencyEvaluator.Instance == null)
        {
            GameObject go = new GameObject("LatencyEvaluator");
            go.AddComponent<LatencyEvaluator>();
        }

        // Auto-find components if not assigned
        if (targetNPC == null)
            targetNPC = FindObjectOfType<NPCChatInstance>();
        
        if (windowsDictation == null)
            windowsDictation = FindObjectOfType<WindowsDictation>();

        if (targetNPC == null)
        {
            Debug.LogError("[PipelineTester] No NPCChatInstance found!");
            return;
        }

        isMonitoring = true;
        Debug.Log("[PipelineTester] ✅ Started passive monitoring of pipeline");
        Debug.Log($"[PipelineTester] Results will be saved to: {Application.persistentDataPath}/latency_results.csv");
    }

    [ContextMenu("Stop Monitoring")]
    public void StopMonitoring()
    {
        isMonitoring = false;
        Debug.Log("[PipelineTester] ⏹️ Stopped monitoring");
    }

    void Update()
    {
        if (!isMonitoring || targetNPC == null)
            return;

        // Check if we should stop monitoring based on max interactions
        if (maxInteractionsToLog > 0 && interactionCount >= maxInteractionsToLog)
        {
            StopMonitoring();
            return;
        }

        // Monitor for new user input via WindowsDictation
        if (windowsDictation != null && !waitingForUserInput)
        {
            // Check if user input field has text (indicating speech was captured)
            if (targetNPC.userInput != null && !string.IsNullOrWhiteSpace(targetNPC.userInput.text))
            {
                string userText = targetNPC.userInput.text;
                
                // New user input detected - start tracking STT (already complete by this point)
                if (userText != currentUserInput)
                {
                    currentUserInput = userText;
                    Debug.Log($"[PipelineTester] 📝 User input detected: {userText}");
                    
                    // STT happened before we could measure it (it's synchronous in WindowsDictation)
                    // We'll mark it as 0 or estimate based on speech duration
                    LatencyEvaluator.Instance.StartTimer("STT");
                    LatencyEvaluator.Instance.StopTimer("STT"); // Immediate for WindowsDictation
                    
                    waitingForUserInput = true;
                }
            }
        }

        // Monitor for NPC response (LLM + TTS pipeline)
        if (waitingForUserInput && !waitingForLLMResponse)
        {
            // User input was captured, now waiting for NPC to start responding
            // This happens when ProcessUserAnswer is called
            StartCoroutine(MonitorNPCResponse());
            waitingForLLMResponse = true;
        }
    }

    private IEnumerator MonitorNPCResponse()
    {
        interactionCount++;
        string testName = $"Interaction_{interactionCount}_{System.DateTime.Now:HHmmss}";
        
        Debug.Log($"[PipelineTester] 🔍 Monitoring interaction #{interactionCount}");

        bool firstTokenReceived = false;
        bool llmComplete = false;
        bool ttsStarted = false;
        bool ttsEnded = false;
        bool firstTTSGenerated = false;

        // Hook into TTS events
        System.Action onTTSStart = () => 
        { 
            ttsStarted = true;
            LatencyEvaluator.Instance.StopTimer("TTS_PlaybackDelay");
            Debug.Log("[PipelineTester] 🔊 TTS playback started");
        };
        
        System.Action onTTSEnd = () => 
        { 
            ttsEnded = true;
            Debug.Log("[PipelineTester] ✅ TTS playback ended");
        };
        
        targetNPC.ttsHandler.OnGlobalPlaybackStart += onTTSStart;
        targetNPC.ttsHandler.OnGlobalPlaybackEnd += onTTSEnd;

        // Start LLM and TTS timers
        LatencyEvaluator.Instance.StartTimer("LLM_FirstToken");
        LatencyEvaluator.Instance.StartTimer("LLM_Total");
        LatencyEvaluator.Instance.StartTimer("TTS_PlaybackDelay");
        LatencyEvaluator.Instance.StartTimer("TTS_Generation");

        // Wait a frame for the NPC to start processing
        yield return null;

        // Monitor the OllamaClient's SendChatAsync via reflection or polling
        // Since we can't intercept the existing call, we'll poll for changes
        float timeout = 60f;
        float timer = 0;
        string previousOutputText = "";

        while (timer < timeout)
        {
            // Detect first token by monitoring output text changes
            if (targetNPC.outputText != null && !firstTokenReceived)
            {
                string currentOutput = targetNPC.outputText.text;
                if (!string.IsNullOrEmpty(currentOutput) && currentOutput != previousOutputText)
                {
                    if (!firstTokenReceived)
                    {
                        firstTokenReceived = true;
                        LatencyEvaluator.Instance.StopTimer("LLM_FirstToken");
                        Debug.Log("[PipelineTester] 🎯 First LLM token received");
                    }
                    previousOutputText = currentOutput;
                }
            }

            // Detect first TTS chunk generation
            if (!firstTTSGenerated && targetNPC.ttsHandler != null)
            {
                // Check if TTS has started generating (queue has items)
                if (ttsStarted || targetNPC.ttsHandler.IsSpeaking())
                {
                    if (!firstTTSGenerated)
                    {
                        firstTTSGenerated = true;
                        LatencyEvaluator.Instance.StopTimer("TTS_Generation");
                        Debug.Log("[PipelineTester] 🎵 First TTS chunk generated");
                    }
                }
            }

            // Check if everything is complete
            if (ttsEnded)
            {
                llmComplete = true;
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Stop LLM timer when TTS ends (indicates full response received)
        LatencyEvaluator.Instance.StopTimer("LLM_Total");

        // Cleanup
        targetNPC.ttsHandler.OnGlobalPlaybackStart -= onTTSStart;
        targetNPC.ttsHandler.OnGlobalPlaybackEnd -= onTTSEnd;

        // Log results
        LatencyEvaluator.Instance.EndTest(testName);
        Debug.Log($"[PipelineTester] 📊 Interaction #{interactionCount} logged to CSV");

        // Reset for next interaction
        waitingForUserInput = false;
        waitingForLLMResponse = false;
        currentUserInput = "";

        yield return new WaitForSeconds(1f); // Brief delay before accepting next input
    }

}
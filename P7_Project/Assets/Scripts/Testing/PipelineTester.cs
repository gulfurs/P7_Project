using UnityEngine;
using System;

/// <summary>
/// Pipeline latency profiler - extracts timing from debug log events.
/// Measures: ProcessUserAnswer → first token → first audio → audio end
/// </summary>
public class PipelineTester : MonoBehaviour
{
    [Header("Settings")]
    public bool startMonitoringOnStart = true;
    public int maxInteractionsToLog = 0;

    private bool isMonitoring = false;
    private int interactionCount = 0;
    
    // Current interaction state
    private bool isTrackingInteraction = false;
    private DateTime userInputLogTime; // When "📢 User answered" was logged
    private DateTime firstTokenLogTime; // When first LLM output appeared
    private DateTime firstAudioLogTime; // When "[TTS] Audio playback starting" was logged
    private DateTime lastAudioEndLogTime; // When "[TTS] ✅ Audio playback completed" was logged
    private string currentUserInput = "";
    private int tokenCount = 0;

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

        isMonitoring = true;
        Debug.Log("[PipelineTester] ✅ Started monitoring pipeline latency from debug logs");
    }

    [ContextMenu("Stop Monitoring")]
    public void StopMonitoring()
    {
        isMonitoring = false;
        Debug.Log("[PipelineTester] ⏹️ Stopped monitoring");
        LatencyEvaluator.Instance.PrintStatistics();
    }

    /// <summary>
    /// Call this from NPCChatInstance:ProcessUserAnswer when user input is detected
    /// </summary>
    public void OnUserInputDetected(string userText)
    {
        if (!isMonitoring) return;

        isTrackingInteraction = true;
        userInputLogTime = DateTime.Now;
        currentUserInput = userText;
        firstTokenLogTime = default;
        firstAudioLogTime = default;
        lastAudioEndLogTime = default;
        tokenCount = 0;

        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkInputSent();

        Debug.Log($"[PipelineTester] 🔴 START interaction: '{userText.Substring(0, Math.Min(30, userText.Length))}'");
    }

    /// <summary>
    /// Call this from NPCChatInstance when first LLM token appears
    /// </summary>
    public void OnFirstTokenAppeared(string tokenText)
    {
        if (!isMonitoring || !isTrackingInteraction) return;

        firstTokenLogTime = DateTime.Now;
        
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkFirstToken();

        double latencyMs = (firstTokenLogTime - userInputLogTime).TotalMilliseconds;
        Debug.Log($"[PipelineTester] 🎯 TTFT (first token): {latencyMs:F2}ms");
    }

    /// <summary>
    /// Call this from NPCTTSHandler when audio playback starts
    /// </summary>
    public void OnAudioPlaybackStarted()
    {
        if (!isMonitoring || !isTrackingInteraction) return;

        firstAudioLogTime = DateTime.Now;
        
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkFirstAudio();

        double latencyMs = (firstAudioLogTime - userInputLogTime).TotalMilliseconds;
        Debug.Log($"[PipelineTester] 🔊 TTFB (first audio): {latencyMs:F2}ms");
    }

    /// <summary>
    /// Call this from NPCTTSHandler when all audio playback completes
    /// </summary>
    public void OnAllAudioPlaybackEnded(int tokenCountInResponse, double totalAudioDurationMs)
    {
        if (!isMonitoring || !isTrackingInteraction) return;

        lastAudioEndLogTime = DateTime.Now;
        
        // Forward to new LatencyEvaluator system if available
        if (LatencyEvaluator.Instance != null)
        {
            LatencyEvaluator.Instance.MarkInteractionEnd(tokenCountInResponse);
        }

        Debug.Log($"[PipelineTester] Interaction Ended. Tokens: {tokenCountInResponse}");

        // Reset
        isTrackingInteraction = false;

        // Check if we should stop
        if (maxInteractionsToLog > 0 && interactionCount >= maxInteractionsToLog)
        {
            StopMonitoring();
        }
    }

    [ContextMenu("Print Statistics")]
    public void PrintStatistics()
    {
        LatencyEvaluator.Instance.PrintStatistics();
    }
}
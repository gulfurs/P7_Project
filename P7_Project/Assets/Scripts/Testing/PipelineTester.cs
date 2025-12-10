using UnityEngine;
using System;

/// <summary>
/// Pipeline latency profiler - extracts timing from debug log events.
/// Measures: ProcessUserAnswer → first token → first audio → audio end
/// Supports both WindowsDictation and Whisper STT modes.
/// </summary>
public class PipelineTester : MonoBehaviour
{
    // STT Configuration
    [Tooltip("STT mode: Windows Dictation or Whisper")]
    public enum STTMode { WindowsDictation, Whisper }
    public STTMode sttMode = STTMode.WindowsDictation;

    [Header("Monitoring Settings")]
    public bool startMonitoringOnStart = true;
    public int maxInteractionsToLog = 0;
    [Tooltip("Enable debug log-based fallback for missed measurements")]
    public bool enableLogFallback = true;

    [Header("Component References")]
    public WindowsDictation windowsDictation;
    public WhisperContinuous whisperContinuous;

    private bool isMonitoring = false;
    private int interactionCount = 0;

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
    }

    [ContextMenu("Analyze Debug Logs (Fallback)")]
    public void RecoverFromDebugLogs()
    {
        if (!enableLogFallback)
        {
            Debug.LogWarning("[PipelineTester] Log fallback is disabled!");
            return;
        }

        Debug.Log("[PipelineTester] 📋 Running debug log analysis...");
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.AnalyzeDebugLogs();
        Debug.Log("[PipelineTester] ✅ Analysis complete. Check PrintStatistics for results.");
    }

    /// <summary>
    /// Call this from NPCChatInstance:ProcessUserAnswer when user input is detected
    /// </summary>
    public void OnUserInputDetected(string userText)
    {
        if (!isMonitoring) return;

        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkInputSent();

        Debug.Log($"[PipelineTester] 🔴 START interaction: '{userText.Substring(0, Math.Min(30, userText.Length))}'");
    }

    /// <summary>
    /// Call this from NPCChatInstance when first LLM token appears
    /// </summary>
    public void OnFirstTokenAppeared(string tokenText)
    {
        if (!isMonitoring) return;
        
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkFirstToken();
    }

    /// <summary>
    /// Call this from NPCTTSHandler when audio playback starts
    /// </summary>
    public void OnAudioPlaybackStarted()
    {
        if (!isMonitoring) return;
        
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkFirstAudio();
    }

    /// <summary>
    /// Call this from NPCTTSHandler when all audio playback completes
    /// </summary>
    public void OnAllAudioPlaybackEnded(int tokenCountInResponse, double totalAudioDurationMs)
    {
        if (!isMonitoring) return;
        
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkInteractionEnd(tokenCountInResponse);

        interactionCount++;
        Debug.Log($"[PipelineTester] Interaction #{interactionCount} ended. Tokens: {tokenCountInResponse}");

        // Check if we should stop
        if (maxInteractionsToLog > 0 && interactionCount >= maxInteractionsToLog)
        {
            StopMonitoring();
        }
    }
}
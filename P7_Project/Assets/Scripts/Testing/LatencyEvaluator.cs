using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Academic-grade latency profiler - measures pipeline timing via direct hooks + debug log fallback.
/// Uses both event-based measurement and debug log analysis for safety net.
/// </summary>
public class LatencyEvaluator : MonoBehaviour
{
    public static LatencyEvaluator Instance { get; private set; }

    [System.Serializable]
    private class LatencyMeasurement
    {
        public string testId;
        public long timestamp;
        
        // Timestamps (ticks)
        public long speechStartTick;
        public long inputSentTick;
        public long firstTokenTick;
        public long firstAudioTick;
        public long audioEndTick;

        // Metrics (ms)
        public double sttLatencyMs;      // InputSent - SpeechStart
        public double ttftMs;            // FirstToken - InputSent
        public double ttfbAudioMs;       // FirstAudio - InputSent
        public double e2eLatencyMs;      // AudioEnd - InputSent
        
        public int tokenCount;
        public double tokensPerSec;
        public string source;            // "hook" or "log"
    }

    private string csvFilePath;
    private string debugLogPath;
    private List<LatencyMeasurement> allMeasurements = new List<LatencyMeasurement>();
    private Queue<string> debugLogBuffer = new Queue<string>();
    
    // Current interaction state
    private LatencyMeasurement currentMeasurement;
    private bool isMeasuring = false;
    private bool useDebugLogFallback = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeOutputFiles();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeOutputFiles()
    {
        string persistentPath = Application.persistentDataPath;
        string timestamp = System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
        csvFilePath = Path.Combine(persistentPath, $"latency_results_{timestamp}.csv");
        debugLogPath = Path.Combine(persistentPath, $"debug_log_{timestamp}.txt");

        var header = new StringBuilder();
        header.AppendLine("TestID,Timestamp,STT_Latency_ms,TTFT_ms,TTFB_Audio_ms,E2E_Latency_ms,TokenCount,TokensPerSec,Source");
        File.WriteAllText(csvFilePath, header.ToString());
        File.WriteAllText(debugLogPath, "[Debug Log Buffer]\n");
        
        // Capture Unity debug logs
        Application.logMessageReceived += OnLogMessageReceived;
        
        Debug.Log($"[LatencyEvaluator] ✅ Initialized. Results: {csvFilePath}");
        Debug.Log($"[LatencyEvaluator] 📋 Debug logs: {debugLogPath}");
    }

    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        // Buffer key latency-related logs
        if (logString.Contains("[Latency]") || logString.Contains("[PipelineTester]") || 
            logString.Contains("📢 User answered") || logString.Contains("[TTS] Audio playback"))
        {
            string timestampedLog = $"[{System.DateTime.Now:HH:mm:ss.fff}] {logString}";
            debugLogBuffer.Enqueue(timestampedLog);
            
            // Write to file in real-time
            File.AppendAllText(debugLogPath, timestampedLog + Environment.NewLine);
        }
    }

    // --- HOOKS ---

    public void MarkSpeechStart()
    {
        if (isMeasuring && currentMeasurement.speechStartTick > 0) return; // Already started

        currentMeasurement = new LatencyMeasurement();
        currentMeasurement.testId = $"Test_{System.DateTime.Now:HHmmss}";
        currentMeasurement.timestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        currentMeasurement.speechStartTick = System.DateTime.Now.Ticks;
        isMeasuring = true;
    }

    public void MarkInputSent()
    {
        if (!isMeasuring) MarkSpeechStart();
        
        if (currentMeasurement.inputSentTick == 0)
        {
            currentMeasurement.inputSentTick = System.DateTime.Now.Ticks;
            Debug.Log("[Latency] Input Sent");
        }
    }

    public void MarkFirstToken()
    {
        if (!isMeasuring || currentMeasurement.firstTokenTick > 0) return;

        currentMeasurement.firstTokenTick = System.DateTime.Now.Ticks;
        currentMeasurement.source = "hook";
        Debug.Log("[Latency] First Token Received");
    }

    public void MarkFirstAudio()
    {
        if (!isMeasuring || currentMeasurement.firstAudioTick > 0) return;

        currentMeasurement.firstAudioTick = System.DateTime.Now.Ticks;
        currentMeasurement.source = "hook";
        Debug.Log("[Latency] First Audio Playback");
    }

    public void MarkInteractionEnd(int tokenCount)
    {
        if (!isMeasuring) return;

        currentMeasurement.audioEndTick = System.DateTime.Now.Ticks;
        currentMeasurement.tokenCount = tokenCount;

        // Calculate Metrics
        long freq = System.TimeSpan.TicksPerMillisecond;
        
        // STT Latency: InputSent - SpeechStart
        if (currentMeasurement.inputSentTick > currentMeasurement.speechStartTick)
            currentMeasurement.sttLatencyMs = (currentMeasurement.inputSentTick - currentMeasurement.speechStartTick) / (double)freq;

        // TTFT: FirstToken - InputSent
        if (currentMeasurement.firstTokenTick > currentMeasurement.inputSentTick)
            currentMeasurement.ttftMs = (currentMeasurement.firstTokenTick - currentMeasurement.inputSentTick) / (double)freq;

        // TTFB Audio: FirstAudio - InputSent
        if (currentMeasurement.firstAudioTick > currentMeasurement.inputSentTick)
            currentMeasurement.ttfbAudioMs = (currentMeasurement.firstAudioTick - currentMeasurement.inputSentTick) / (double)freq;

        // E2E: AudioEnd - InputSent
        if (currentMeasurement.audioEndTick > currentMeasurement.inputSentTick)
            currentMeasurement.e2eLatencyMs = (currentMeasurement.audioEndTick - currentMeasurement.inputSentTick) / (double)freq;

        // Throughput
        double generationTimeMs = (currentMeasurement.audioEndTick - currentMeasurement.firstTokenTick) / (double)freq;
        if (generationTimeMs > 0 && tokenCount > 0)
            currentMeasurement.tokensPerSec = (tokenCount / generationTimeMs) * 1000.0;

        SaveMeasurement(currentMeasurement);
        isMeasuring = false;
    }

    private void SaveMeasurement(LatencyMeasurement m)
    {
        if (string.IsNullOrEmpty(m.source))
            m.source = "hook";
        
        allMeasurements.Add(m);

        var sb = new StringBuilder();
        sb.Append($"{m.testId},");
        sb.Append($"{m.timestamp},");
        sb.Append($"{m.sttLatencyMs:F2},");
        sb.Append($"{m.ttftMs:F2},");
        sb.Append($"{m.ttfbAudioMs:F2},");
        sb.Append($"{m.e2eLatencyMs:F2},");
        sb.Append($"{m.tokenCount},");
        sb.Append($"{m.tokensPerSec:F2},");
        sb.AppendLine($"{m.source}");

        File.AppendAllText(csvFilePath, sb.ToString());
        
        string sourceStr = m.source == "hook" ? "🤖" : "📋";
        Debug.Log($"[Latency] {sourceStr} Saved: STT={m.sttLatencyMs:F0}ms, TTFT={m.ttftMs:F0}ms, TTFB={m.ttfbAudioMs:F0}ms, E2E={m.e2eLatencyMs:F0}ms");
    }

    /// <summary>
    /// Fallback: Extract measurements from debug log buffer when hooks fail
    /// </summary>
    [ContextMenu("Analyze Debug Logs")]
    public void AnalyzeDebugLogs()
    {
        if (!useDebugLogFallback)
        {
            Debug.Log("[LatencyEvaluator] Debug log fallback disabled.");
            return;
        }

        Debug.Log("[LatencyEvaluator] 📋 Analyzing debug logs for missed measurements...");
        
        if (!File.Exists(debugLogPath))
        {
            Debug.LogWarning("[LatencyEvaluator] Debug log file not found!");
            return;
        }

        string[] lines = File.ReadAllLines(debugLogPath);
        int recovered = 0;

        DateTime? userAnsweredTime = null;
        DateTime? firstTokenTime = null;
        DateTime? audioPlaybackTime = null;
        DateTime? audioEndTime = null;
        string testId = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[Debug Log Buffer]"))
                continue;

            // Extract timestamp [HH:mm:ss.fff]
            var timeMatch = Regex.Match(line, @"\[(\d{2}:\d{2}:\d{2}\.\d{3})\]");
            if (!timeMatch.Success) continue;

            string timeStr = timeMatch.Groups[1].Value;
            DateTime time = DateTime.ParseExact(timeStr, "HH:mm:ss.fff", null);

            // Detect events
            if (line.Contains("User answered"))
            {
                if (userAnsweredTime.HasValue && firstTokenTime.HasValue)
                {
                    recovered += CreateMeasurementFromLogs(testId, userAnsweredTime.Value, firstTokenTime.Value, audioPlaybackTime, audioEndTime);
                }
                userAnsweredTime = time;
                firstTokenTime = null;
                audioPlaybackTime = null;
                audioEndTime = null;
                testId = $"LogRecover_{time:HHmmss_fff}";
            }
            else if (line.Contains("First Token") && !firstTokenTime.HasValue)
            {
                firstTokenTime = time;
            }
            else if (line.Contains("Audio playback") && !audioPlaybackTime.HasValue)
            {
                audioPlaybackTime = time;
            }
            else if (line.Contains("Audio playback completed"))
            {
                audioEndTime = time;
            }
        }

        // Save last measurement
        if (userAnsweredTime.HasValue && firstTokenTime.HasValue)
            recovered += CreateMeasurementFromLogs(testId, userAnsweredTime.Value, firstTokenTime.Value, audioPlaybackTime, audioEndTime);

        Debug.Log($"[LatencyEvaluator] ✅ Recovered {recovered} measurements from debug logs");
    }

    private int CreateMeasurementFromLogs(string testId, DateTime inputTime, DateTime tokenTime, DateTime? audioTime, DateTime? endTime)
    {
        var m = new LatencyMeasurement()
        {
            testId = testId,
            timestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            source = "log"
        };

        m.ttftMs = (tokenTime - inputTime).TotalMilliseconds;
        
        if (audioTime.HasValue)
            m.ttfbAudioMs = (audioTime.Value - inputTime).TotalMilliseconds;
        
        if (endTime.HasValue)
            m.e2eLatencyMs = (endTime.Value - inputTime).TotalMilliseconds;

        m.tokenCount = 0;
        m.tokensPerSec = 0;

        SaveMeasurement(m);
        return 1;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
    }
}

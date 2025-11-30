using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;

/// <summary>
/// Academic-grade latency profiler - measures pipeline timing via direct hooks.
/// Quantitative metrics only - suitable for peer-reviewed publication.
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
    }

    private string csvFilePath;
    private List<LatencyMeasurement> allMeasurements = new List<LatencyMeasurement>();
    
    // Current interaction state
    private LatencyMeasurement currentMeasurement;
    private bool isMeasuring = false;

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

        var header = new StringBuilder();
        header.AppendLine("TestID,Timestamp,STT_Latency_ms,TTFT_ms,TTFB_Audio_ms,E2E_Latency_ms,TokenCount,TokensPerSec");
        File.WriteAllText(csvFilePath, header.ToString());
        
        Debug.Log($"[LatencyEvaluator] Initialized. Saving to: {csvFilePath}");
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
        
        // Debug.Log("[LatencyEvaluator] Speech Start Detected");
    }

    public void MarkInputSent()
    {
        if (!isMeasuring) MarkSpeechStart(); // Fallback if speech start wasn't caught
        
        if (currentMeasurement.inputSentTick == 0)
        {
            currentMeasurement.inputSentTick = System.DateTime.Now.Ticks;
            // Debug.Log("[LatencyEvaluator] Input Sent");
        }
    }

    public void MarkFirstToken()
    {
        if (!isMeasuring || currentMeasurement.firstTokenTick > 0) return;

        currentMeasurement.firstTokenTick = System.DateTime.Now.Ticks;
        // Debug.Log("[LatencyEvaluator] First Token Received");
    }

    public void MarkFirstAudio()
    {
        if (!isMeasuring || currentMeasurement.firstAudioTick > 0) return;

        currentMeasurement.firstAudioTick = System.DateTime.Now.Ticks;
        // Debug.Log("[LatencyEvaluator] First Audio Playback");
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
        allMeasurements.Add(m);

        var sb = new StringBuilder();
        sb.Append($"{m.testId},");
        sb.Append($"{m.timestamp},");
        sb.Append($"{m.sttLatencyMs:F2},");
        sb.Append($"{m.ttftMs:F2},");
        sb.Append($"{m.ttfbAudioMs:F2},");
        sb.Append($"{m.e2eLatencyMs:F2},");
        sb.Append($"{m.tokenCount},");
        sb.Append($"{m.tokensPerSec:F2}");

        File.AppendAllText(csvFilePath, sb.ToString() + Environment.NewLine);
        
        Debug.Log($"[LatencyEvaluator] Saved: STT={m.sttLatencyMs:F0}ms, TTFT={m.ttftMs:F0}ms, TTFB={m.ttfbAudioMs:F0}ms, E2E={m.e2eLatencyMs:F0}ms");
    }

    [ContextMenu("Print Stats")]
    public void PrintStatistics()
    {
        if (allMeasurements.Count == 0)
        {
            Debug.Log("[LatencyEvaluator] No measurements recorded yet.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("\n========== LATENCY EVALUATION SUMMARY ==========");
        sb.AppendLine($"Total Interactions: {allMeasurements.Count}\n");

        // TTFT - Time To First Token
        var ttfts = allMeasurements.Where(m => m.ttftMs > 0).Select(m => m.ttftMs).ToList();
        if (ttfts.Count > 0)
        {
            sb.AppendLine("TTFT - User Input to First Token (ms):");
            sb.AppendLine($"  Mean: {ttfts.Average():F2}");
            sb.AppendLine($"  Median: {GetMedian(ttfts):F2}");
            sb.AppendLine($"  StdDev: {GetStdDev(ttfts):F2}");
            sb.AppendLine($"  Range: [{ttfts.Min():F2}, {ttfts.Max():F2}]\n");
        }

        // TTFB - Time To First Audio
        var ttfbs = allMeasurements.Where(m => m.ttfbAudioMs > 0).Select(m => m.ttfbAudioMs).ToList();
        if (ttfbs.Count > 0)
        {
            sb.AppendLine("TTFB - User Input to First Audio (ms):");
            sb.AppendLine($"  Mean: {ttfbs.Average():F2}");
            sb.AppendLine($"  Median: {GetMedian(ttfbs):F2}");
            sb.AppendLine($"  StdDev: {GetStdDev(ttfbs):F2}");
            sb.AppendLine($"  Range: [{ttfbs.Min():F2}, {ttfbs.Max():F2}]\n");
        }

        // E2E Latency
        var e2es = allMeasurements.Where(m => m.e2eLatencyMs > 0).Select(m => m.e2eLatencyMs).ToList();
        if (e2es.Count > 0)
        {
            sb.AppendLine("E2E - User Input to Audio End (ms):");
            sb.AppendLine($"  Mean: {e2es.Average():F2}");
            sb.AppendLine($"  Median: {GetMedian(e2es):F2}");
            sb.AppendLine($"  StdDev: {GetStdDev(e2es):F2}");
            sb.AppendLine($"  Range: [{e2es.Min():F2}, {e2es.Max():F2}]\n");
        }

        sb.AppendLine($"CSV Results: {csvFilePath}");
        sb.AppendLine("================================================\n");

        Debug.Log(sb.ToString());
    }

    private double GetMedian(List<double> values)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        int count = values.Count;
        return count % 2 == 0 ? (values[count / 2 - 1] + values[count / 2]) / 2 : values[count / 2];
    }

    private double GetStdDev(List<double> values)
    {
        if (values.Count == 0) return 0;
        double avg = values.Average();
        double sumSquareDiff = values.Sum(x => (x - avg) * (x - avg));
        return System.Math.Sqrt(sumSquareDiff / values.Count);
    }
}
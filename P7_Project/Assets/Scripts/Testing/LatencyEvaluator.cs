using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System;

public class LatencyEvaluator : MonoBehaviour
{
    public static LatencyEvaluator Instance { get; private set; }

    private Dictionary<string, Stopwatch> timers = new Dictionary<string, Stopwatch>();
    private Dictionary<string, double> results = new Dictionary<string, double>();
    private StringBuilder csvContent = new StringBuilder();
    private string filePath;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        filePath = Path.Combine(Application.persistentDataPath, "latency_results.csv");
        // Initialize CSV header if file doesn't exist
        if (!File.Exists(filePath))
        {
            csvContent.AppendLine("TestName,STT_Latency,LLM_FirstToken,LLM_Total,TTS_Generation,TTS_PlaybackDelay");
            File.WriteAllText(filePath, csvContent.ToString());
            csvContent.Clear();
        }
        UnityEngine.Debug.Log($"[LatencyEvaluator] Saving results to: {filePath}");
    }

    public void StartTimer(string id)
    {
        if (!timers.ContainsKey(id)) timers[id] = new Stopwatch();
        timers[id].Restart();
    }

    public void StopTimer(string id)
    {
        if (timers.ContainsKey(id) && timers[id].IsRunning)
        {
            timers[id].Stop();
            results[id] = timers[id].Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log($"[Latency] {id}: {results[id]}ms");
        }
    }

    public void EndTest(string testName)
    {
        // Collect results
        double stt = results.ContainsKey("STT") ? results["STT"] : 0;
        double llmFirst = results.ContainsKey("LLM_FirstToken") ? results["LLM_FirstToken"] : 0;
        double llmTotal = results.ContainsKey("LLM_Total") ? results["LLM_Total"] : 0;
        double ttsGen = results.ContainsKey("TTS_Generation") ? results["TTS_Generation"] : 0;
        double ttsPlay = results.ContainsKey("TTS_PlaybackDelay") ? results["TTS_PlaybackDelay"] : 0;
        
        string line = $"{testName},{stt},{llmFirst},{llmTotal},{ttsGen},{ttsPlay}";
        File.AppendAllText(filePath, line + Environment.NewLine);
        
        UnityEngine.Debug.Log($"[Latency] Test '{testName}' saved to {filePath}");
        results.Clear();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Handles TTS audio generation and playback for NPCs
/// Separated from main NPC chat logic for clarity
/// Pre-generates audio while waiting for turn to speak
/// </summary>
public class NPCTTSHandler : MonoBehaviour
{
    private AudioSource audioSource;
    private string voiceName;
    [Header("Python Execution")]
    [Tooltip("Optional explicit path to python executable for TTS generation. Leave empty to use system python.")]
    public string pythonExecutablePath = "";
    
    private readonly Queue<TTSRequest> ttsQueue = new Queue<TTSRequest>();
    private readonly Queue<TTSRequest> preRenderedQueue = new Queue<TTSRequest>();
    private bool isGenerating = false;
    private bool isCurrentlyPlaying = false;
    private Coroutine playbackCoroutine = null;
    
    [Serializable]
    private class TTSRequest
    {
        public string text;
        public AudioClip clip;
        public Action onStartPlayback; // Callback when this clip starts playing
    }
    
    /// <summary>
    /// Split response into TTS chunks at sentence boundaries
    /// </summary>
    public void ProcessResponseForTTS(string displayText)
    {
        if (string.IsNullOrEmpty(displayText)) return;
        
        var chunks = new System.Collections.Generic.List<string>();
        var currentChunk = new System.Text.StringBuilder();
        
        foreach (char c in displayText)
        {
            currentChunk.Append(c);
            
            bool isSentenceEnding = c == '.' || c == '!' || c == '?';
            bool isLongClause = c == ',' && currentChunk.Length > 60;
            
            if (isSentenceEnding || isLongClause)
            {
                string chunk = currentChunk.ToString().Trim();
                if (chunk.Length > 0)
                    chunks.Add(chunk);
                currentChunk.Clear();
            }
        }
        
        // Add remaining text
        if (currentChunk.Length > 0)
        {
            string chunk = currentChunk.ToString().Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);
        }
        
        // Enqueue all chunks
        foreach (var chunk in chunks)
            EnqueueSpeech(chunk, null);
    }
    
    public void Initialize(AudioSource source, string voice)
    {
        audioSource = source;
        voiceName = voice;
    }
    
    /// <summary>
    /// Check if TTS is currently speaking or has queued speech
    /// </summary>
    public bool IsSpeaking()
    {
        return isGenerating || isCurrentlyPlaying || ttsQueue.Count > 0 || preRenderedQueue.Count > 0;
    }
    
    /// <summary>
    /// Add text chunk to TTS queue and start processing
    /// </summary>
    public void EnqueueSpeech(string text, Action onStartPlayback = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        var request = new TTSRequest 
        { 
            text = text,
            onStartPlayback = onStartPlayback
        };
        
        ttsQueue.Enqueue(request);
        
        // Start generation immediately
        if (!isGenerating)
        {
            if (LatencyEvaluator.Instance != null)
                LatencyEvaluator.Instance.StartTimer("TTS_Generation");

            StartCoroutine(GenerateAudioInBackground());
        }
        
        // Start playback if not already playing
        if (playbackCoroutine == null)
            playbackCoroutine = StartCoroutine(PlayPreRenderedQueue());
    }
    
    /// <summary>
    /// Generate audio clips in background, independent of playback
    /// </summary>
    private IEnumerator GenerateAudioInBackground()
    {
        isGenerating = true;
        
        while (ttsQueue.Count > 0)
        {
            var request = ttsQueue.Dequeue();
            
            Debug.Log($"🎵 Generating TTS audio for: \"{request.text.Substring(0, Math.Min(30, request.text.Length))}...\"");
            
            // Generate audio in background thread
            var ttsTask = System.Threading.Tasks.Task.Run(() => GenerateAudioData(request.text));
            
            // Wait for generation
            while (!ttsTask.IsCompleted)
                yield return null;
            
            var audioBytes = ttsTask.Result;
            if (audioBytes != null && audioBytes.Length > 0)
            {
                Debug.Log($"[TTS] Generated {audioBytes.Length} bytes");
                // Convert to AudioClip
                request.clip = CreateAudioClip(audioBytes);
                
                if (request.clip != null)
                {
                    // Add to pre-rendered queue (ready to play)
                    preRenderedQueue.Enqueue(request);
                    Debug.Log($"✅ TTS audio ready, queue size: {preRenderedQueue.Count}");

                    if (LatencyEvaluator.Instance != null)
                    {
                        // Only stop if it was running (captures first chunk of batch)
                        LatencyEvaluator.Instance.StopTimer("TTS_Generation");
                        if (preRenderedQueue.Count == 1)
                        {
                             LatencyEvaluator.Instance.StartTimer("TTS_PlaybackDelay");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[TTS] Failed to create audio clip!");
                }
            }
            else
            {
                Debug.LogError($"[TTS] Audio generation failed! audioBytes={audioBytes}, length={audioBytes?.Length}");
            }
        }
        
        isGenerating = false;
    }
    
    /// <summary>
    /// Process TTS queue sequentially
    /// </summary>
    private IEnumerator PlayPreRenderedQueue()
    {
        while (true)
        {
            // Wait for pre-rendered clips to be available
            while (preRenderedQueue.Count == 0)
            {
                if (!isGenerating && ttsQueue.Count == 0)
                {
                    playbackCoroutine = null;
                    yield break;
                }
                yield return null;
            }
            
            var request = preRenderedQueue.Dequeue();
            
            if (request.clip != null)
            {
                Debug.Log($"[TTS] Queued clip ready for playback: {request.clip.name}");
                
                if (LatencyEvaluator.Instance != null)
                    LatencyEvaluator.Instance.StopTimer("TTS_PlaybackDelay");

                request.onStartPlayback?.Invoke();
                yield return StartCoroutine(PlayAudioClip(request.clip));
            }
            else
            {
                Debug.LogWarning("[TTS] Clip is null in queue!");
            }
        }
    }
    
    /// <summary>
    /// Create AudioClip from byte array
    /// </summary>
    private AudioClip CreateAudioClip(byte[] audioBytes)
    {
        if (audioBytes == null || audioBytes.Length == 0)
        {
            Debug.LogError("[TTS] AudioBytes are empty or null!");
            return null;
        }
        
        int sampleCount = audioBytes.Length / 2;
        float[] audioData = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            audioData[i] = System.BitConverter.ToInt16(audioBytes, i * 2) / 32768f;
        
        AudioClip clip = AudioClip.Create($"TTS_{Time.time}", sampleCount, 1, 22050, false);
        clip.SetData(audioData, 0);
        Debug.Log($"[TTS] 🎵 Created audio clip: {sampleCount} samples ({sampleCount / 22050.0f}s)");
        return clip;
    }
    
    /// <summary>
    /// Play an audio clip and wait for completion
    /// </summary>
    private IEnumerator PlayAudioClip(AudioClip clip)
    {
        if (audioSource == null)
        {
            Debug.LogError("[TTS] AudioSource is NULL! Cannot play audio.");
            yield break;
        }
        
        if (clip == null)
        {
            Debug.LogError("[TTS] AudioClip is NULL! Cannot play audio.");
            yield break;
        }
        
        Debug.Log($"[TTS] 🔊 Playing audio clip: {clip.name} (length: {clip.length}s, samples: {clip.samples})");
        
        isCurrentlyPlaying = true;
        audioSource.clip = clip;
        
        if (!audioSource.gameObject.activeInHierarchy)
            Debug.LogError("[TTS] AudioSource GameObject is inactive!");
        
        if (!audioSource.enabled)
            Debug.LogError("[TTS] AudioSource component is disabled!");
        
        audioSource.Play();
        Debug.Log($"[TTS] Audio playback started. isPlaying={audioSource.isPlaying}");
        
        int frames = 0;
        while (audioSource.isPlaying)
        {
            frames++;
            yield return null;
        }
        
        Debug.Log($"[TTS] ✅ Audio playback completed (waited {frames} frames)");
        isCurrentlyPlaying = false;
    }
    
    /// <summary>
    /// Generate audio data in background thread
    /// </summary>
    private byte[] GenerateAudioData(string text)
    {
        string tempScript = System.IO.Path.GetTempFileName() + ".py";
        string cleanText = text.Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", " ").Trim();
        
        if (string.IsNullOrEmpty(cleanText)) return new byte[0];

        // --- Start of Fix: Use absolute path for TTS model from project root ---
        // Get project root by going one level up from the Assets folder
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string modelPath = System.IO.Path.Combine(projectRoot, $"{voiceName}.onnx");
        // Python requires forward slashes, even on Windows
        modelPath = modelPath.Replace("\\", "/"); 
        // --- End of Fix ---

        // Try Piper first (high quality), fallback to pyttsx3 (basic TTS)
        string script = $@"
import sys
import os
import tempfile

# Try Piper first (preferred method - high quality)
try:
    from piper import PiperVoice
    
    model_path = r'{modelPath}' # Use raw string for path
    if not os.path.exists(model_path):
        sys.stderr.write(f'Piper Error: Model file not found at {{model_path}}')
        sys.exit(1)

    voice = PiperVoice.load(model_path)
    text = '{cleanText}'
    
    audio_data = []
    for chunk in voice.synthesize(text):
        audio_data.extend(chunk.audio_int16_bytes)
    
    sys.stdout.buffer.write(bytes(audio_data))
    sys.exit(0)
except Exception as e:
    sys.stderr.write(f'Piper failed: {{str(e)}}. Check if model exists and piper-tts is installed.')

# Fallback to pyttsx3 (basic but reliable)
try:
    import pyttsx3
    
    engine = pyttsx3.init()
    engine.setProperty('rate', 150)  # Speed
    engine.setProperty('volume', 1.0)  # Volume
    
    # Generate to temp WAV file
    temp_audio = tempfile.mktemp(suffix='.wav')
    engine.save_to_file('{cleanText}', temp_audio)
    engine.runAndWait()
    
    # Read and output WAV bytes
    if os.path.exists(temp_audio):
        with open(temp_audio, 'rb') as f:
            audio_data = f.read()
        os.remove(temp_audio)
        
        sys.stdout.buffer.write(audio_data)
        sys.exit(0)
    else:
        sys.stderr.write('pyttsx3: WAV file not created')
        sys.exit(1)
        
except Exception as e:
    sys.stderr.write(f'pyttsx3 failed: {{str(e)}}')
    sys.exit(1)
";

        System.IO.File.WriteAllText(tempScript, script);
        
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = GetPythonExecutablePath();
        process.StartInfo.Arguments = $"\"{tempScript}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        
        process.Start();
        
        var audioBytes = new List<byte>();
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
                audioBytes.Add(buffer[i]);
        }
        
        // Read error output if any
        string errorOutput = process.StandardError.ReadToEnd();
        
        process.WaitForExit();
        
        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrEmpty(errorOutput))
                Debug.LogError($"[TTS] Error: {errorOutput}");
            else
                Debug.LogError("[TTS] TTS generation failed with no error message");
        }
        
        process?.Dispose();
        System.IO.File.Delete(tempScript);
        
        return audioBytes.ToArray();
    }

    private string GetPythonExecutablePath()
    {
        if (!string.IsNullOrEmpty(pythonExecutablePath))
            return pythonExecutablePath;

        return "python";
    }
    
    /// <summary>
    /// Clear TTS queue
    /// </summary>
    public void ClearQueue()
    {
        ttsQueue.Clear();
        preRenderedQueue.Clear();
        if (audioSource != null)
            audioSource.Stop();
        
        isCurrentlyPlaying = false;
        
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
    }
}

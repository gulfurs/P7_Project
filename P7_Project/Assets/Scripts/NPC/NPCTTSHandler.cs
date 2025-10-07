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
            StartCoroutine(GenerateAudioInBackground());
        
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
                // Convert to AudioClip
                request.clip = CreateAudioClip(audioBytes);
                
                // Add to pre-rendered queue (ready to play)
                preRenderedQueue.Enqueue(request);
                
                Debug.Log($"✅ TTS audio ready, queue size: {preRenderedQueue.Count}");
            }
        }
        
        isGenerating = false;
    }
    
    /// <summary>
    /// Play pre-rendered audio clips sequentially
    /// </summary>
    private IEnumerator PlayPreRenderedQueue()
    {
        while (true)
        {
            // Wait for pre-rendered clips to be available
            while (preRenderedQueue.Count == 0)
            {
                // Exit if nothing is generating and queue is empty
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
                // Trigger callback (this will show the text in UI)
                request.onStartPlayback?.Invoke();
                
                // Play the audio
                yield return StartCoroutine(PlayAudioClip(request.clip));
            }
        }
    }
    
    /// <summary>
    /// Process TTS queue sequentially
    /// </summary>
    private IEnumerator ProcessTTSQueue()
    {
        // DEPRECATED: Old sequential approach
        // Now using GenerateAudioInBackground + PlayPreRenderedQueue
        yield break;
    }
    
    /// <summary>
    /// Create AudioClip from byte array
    /// </summary>
    private AudioClip CreateAudioClip(byte[] audioBytes)
    {
        int sampleCount = audioBytes.Length / 2;
        float[] audioData = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            audioData[i] = System.BitConverter.ToInt16(audioBytes, i * 2) / 32768f;
        
        AudioClip clip = AudioClip.Create($"TTS_{Time.time}", sampleCount, 1, 22050, false);
        clip.SetData(audioData, 0);
        return clip;
    }
    
    /// <summary>
    /// Play an audio clip and wait for completion
    /// </summary>
    private IEnumerator PlayAudioClip(AudioClip clip)
    {
        if (audioSource == null || clip == null) yield break;
        
        isCurrentlyPlaying = true;
        
        audioSource.clip = clip;
        audioSource.Play();
        
        // Wait for audio to finish
        while (audioSource.isPlaying)
            yield return null;
        
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
        
        string script = $@"
from piper import PiperVoice
import sys

voice = PiperVoice.load('{voiceName}.onnx')
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
    
    /// <summary>
    /// Clear TTS queue (useful for interruptions)
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

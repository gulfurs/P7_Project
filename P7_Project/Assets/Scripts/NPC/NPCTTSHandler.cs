using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Handles TTS audio generation and playback for NPCs
/// Separated from main NPC chat logic for clarity
/// </summary>
public class NPCTTSHandler : MonoBehaviour
{
    private AudioSource audioSource;
    private string voiceName;
    
    private readonly Queue<string> ttsQueue = new Queue<string>();
    private readonly Queue<AudioClip> preRenderedClips = new Queue<AudioClip>();
    private bool isProcessingTTS = false;
    private bool isCurrentlyPlaying = false;
    
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
        return isProcessingTTS || isCurrentlyPlaying || ttsQueue.Count > 0 || preRenderedClips.Count > 0;
    }
    
    /// <summary>
    /// Add text chunk to TTS queue and start processing
    /// </summary>
    public void EnqueueSpeech(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        ttsQueue.Enqueue(text);
        
        if (!isProcessingTTS)
            StartCoroutine(ProcessTTSQueue());
    }
    
    /// <summary>
    /// Process TTS queue sequentially
    /// </summary>
    private IEnumerator ProcessTTSQueue()
    {
        isProcessingTTS = true;
        
        while (ttsQueue.Count > 0)
        {
            string textChunk = ttsQueue.Dequeue();
            
            // Pre-render the audio clip in background
            var ttsTask = System.Threading.Tasks.Task.Run(() => GenerateAudioData(textChunk));
            
            // Wait for generation
            while (!ttsTask.IsCompleted)
                yield return null;
            
            var audioBytes = ttsTask.Result;
            if (audioBytes != null && audioBytes.Length > 0)
            {
                // Convert to AudioClip
                AudioClip clip = CreateAudioClip(audioBytes);
                
                // Wait for current audio to finish
                while (isCurrentlyPlaying)
                    yield return null;
                
                // Play this clip
                yield return StartCoroutine(PlayAudioClip(clip));
            }
        }
        
        isProcessingTTS = false;
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
        preRenderedClips.Clear();
        if (audioSource != null)
            audioSource.Stop();
        
        isCurrentlyPlaying = false;
    }
}

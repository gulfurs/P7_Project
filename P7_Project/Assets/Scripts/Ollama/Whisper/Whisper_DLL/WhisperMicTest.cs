using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class WhisperContinuous2 : MonoBehaviour
{
    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_init_from_file(string modelPath);

    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe(IntPtr ctx, string audioPath);

    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_whisper_unload_model();

    private IntPtr whisperCtx = IntPtr.Zero;
    private AudioClip micClip;
    private string micDevice;
    private float[] audioBuffer;
    private ConcurrentQueue<float[]> chunkQueue = new();
    private Thread workerThread;
    private bool running = true;
    private int chunkSize = 16000; // 1 second @ 16kHz

    void Start()
    {
        string modelPath = Application.streamingAssetsPath + "/Whisper/ggml-tiny.bin";
        whisperCtx = unity_whisper_init_from_file(modelPath);

        if (whisperCtx == IntPtr.Zero)
        {
            Debug.LogError("[Whisper] Failed to load model!");
            return;
        }

        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (micDevice == null)
        {
            Debug.LogError("[Whisper] No microphone found!");
            return;
        }

        Debug.Log($"[Whisper] Starting continuous recognition on: {micDevice}");
        micClip = Microphone.Start(micDevice, true, 10, 16000); // 10s rolling buffer
        audioBuffer = new float[chunkSize];

        workerThread = new Thread(ProcessChunks);
        workerThread.Start();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!Microphone.IsRecording(micDevice)) return;

        // Copy audio chunk (mono only)
        int length = Mathf.Min(chunkSize, data.Length / channels);
        float[] chunk = new float[length];
        for (int i = 0; i < length; i++)
            chunk[i] = data[i * channels];

        chunkQueue.Enqueue(chunk);
    }

    void ProcessChunks()
    {
        while (running)
        {
            if (chunkQueue.TryDequeue(out var chunk))
            {
                string tempPath = Path.Combine(Application.persistentDataPath, "mic_chunk.wav");
                SaveWav(tempPath, chunk, 16000);

                IntPtr resultPtr = unity_whisper_transcribe(whisperCtx, tempPath);
                string result = Marshal.PtrToStringAnsi(resultPtr);
                if (!string.IsNullOrEmpty(result))
                    Debug.Log($"[Whisper Live] {result}");
            }
            else
            {
                Thread.Sleep(50);
            }
        }
    }

    void SaveWav(string path, float[] samples, int sampleRate)
    {
        using (var mem = new MemoryStream())
        using (var writer = new BinaryWriter(mem))
        {
            int sampleCount = samples.Length;
            int channels = 1;
            int byteRate = sampleRate * channels * 2;

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(sampleCount * 2);

            foreach (float s in samples)
            {
                short val = (short)(Mathf.Clamp(s, -1f, 1f) * 32767);
                writer.Write(val);
            }

            writer.Flush();
            File.WriteAllBytes(path, mem.ToArray());
        }
    }

    void OnDestroy()
    {
        running = false;
        workerThread?.Join();
        unity_whisper_unload_model();
        Debug.Log("[Whisper] Continuous recognition stopped.");
    }
}

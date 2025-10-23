using System;
using System.Collections;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WhisperManager1 : MonoBehaviour
{
    [Header("?? Microphone Settings")]
    public int sampleRate = 16000;
    public int recordSeconds = 5;
    private AudioClip micClip;
    private string micDevice;

    [Header("?? Whisper Settings")]
    public string modelPath = "Assets/StreamingAssets/models/whisper-tiny.en.gguf";
    public LlamaManager llamaManager;  // link this in Inspector
    private bool isInitialized = false;

    // Native plugin interface (adjust to your DLL)
    [System.Runtime.InteropServices.DllImport("whisper_unity")]
    private static extern IntPtr whisper_init(string modelPath);

    [System.Runtime.InteropServices.DllImport("whisper_unity")]
    private static extern IntPtr whisper_transcribe(IntPtr ctx, float[] samples, int numSamples);

    [System.Runtime.InteropServices.DllImport("whisper_unity")]
    private static extern void whisper_free(IntPtr ctx);

    private IntPtr ctx = IntPtr.Zero;

    void Start()
    {
        InitializeWhisper();
    }

    void InitializeWhisper()
    {
        if (File.Exists(modelPath))
        {
            ctx = whisper_init(modelPath);
            if (ctx != IntPtr.Zero)
            {
                Debug.Log($"[Whisper] Model loaded: {modelPath}");
                isInitialized = true;
            }
            else Debug.LogError("[Whisper] Failed to initialize model!");
        }
        else Debug.LogError($"[Whisper] Model not found at path: {modelPath}");
    }

    public void StartRecording()
    {
        if (!Microphone.IsRecording(null))
        {
            micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            micClip = Microphone.Start(micDevice, false, recordSeconds, sampleRate);
            Debug.Log("[Whisper] Recording started...");
            StartCoroutine(WaitAndTranscribe());
        }
        else Debug.LogWarning("[Whisper] Microphone is already recording!");
    }

    IEnumerator WaitAndTranscribe()
    {
        yield return new WaitForSeconds(recordSeconds);
        Microphone.End(micDevice);
        Debug.Log("[Whisper] Recording finished, transcribing...");

        float[] samples = new float[micClip.samples * micClip.channels];
        micClip.GetData(samples, 0);
        string transcription = Transcribe(samples);
        OnTranscriptionReady(transcription);
    }

    string Transcribe(float[] samples)
    {
        if (!isInitialized)
        {
            Debug.LogError("[Whisper] Model not initialized!");
            return "";
        }

        IntPtr ptr = whisper_transcribe(ctx, samples, samples.Length);
        string text = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
        Debug.Log($"[Whisper] Transcription: {text}");
        return text;
    }

    public void OnTranscriptionReady(string text)
    {
        Debug.Log($"[Whisper] Final transcription: {text}");

        if (llamaManager != null)
        {
            llamaManager.SendPrompt(text);
        }
        else
        {
            Debug.LogWarning("[Whisper] No LLaMA manager connected!");
        }
    }

    void OnDestroy()
    {
        if (ctx != IntPtr.Zero)
        {
            whisper_free(ctx);
            ctx = IntPtr.Zero;
        }
    }
}

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class PluginTest : MonoBehaviour
{
    [DllImport("llama_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr llama_generate(IntPtr ctx, string prompt);

    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_init_from_file(string modelPath);

    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe(IntPtr ctx, string audioPath);

    [DllImport("whisper_unity", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_whisper_unload_model();

    private IntPtr whisperCtx = IntPtr.Zero;

    private void Start()
    {
        Debug.Log("=== Plugin test start ===");

        // --- LLaMA test ---
        Debug.Log("[LLaMA Response] Hello from Unity test!");

        // --- Whisper test ---
        string modelPath = Application.streamingAssetsPath + "/Whisper/ggml-tiny.bin";
        string wavPath = Application.streamingAssetsPath + "/samples/test.wav";

        whisperCtx = unity_whisper_init_from_file(modelPath);
        if (whisperCtx == IntPtr.Zero)
        {
            Debug.LogError("[Whisper] Failed to load model!");
            return;
        }

        IntPtr resultPtr = unity_whisper_transcribe(whisperCtx, wavPath);
        string result = Marshal.PtrToStringAnsi(resultPtr);

        Debug.Log($"[Whisper Transcription] {result}");

        unity_whisper_unload_model();
        Debug.Log("=== Plugin test complete ===");
    }
}

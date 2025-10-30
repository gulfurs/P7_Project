using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WWhisperManager
{
    private const string DLL_NAME = "whisper_unity";

    // --- Native functions from whisper_unity.cpp ---
    [DllImport(DLL_NAME, EntryPoint = "unity_whisper_init_from_file", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool unity_whisper_init_from_file(string modelPath);

    [DllImport(DLL_NAME, EntryPoint = "unity_whisper_unload_model", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_whisper_unload_model();

    [DllImport(DLL_NAME, EntryPoint = "unity_whisper_transcribe_partial", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe_partial(string wavPath);

    [DllImport(DLL_NAME, EntryPoint = "unity_whisper_transcribe", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe(string wavPath);

    // --- Context handle ---
    private static bool modelLoaded = false;

    // --- API used by Unity ---
    public static bool InitModel(string modelPath)
    {
        if (!unity_whisper_init_from_file(modelPath))
        {
            Debug.LogError("[WhisperManager] Failed to initialize model!");
            return false;
        }

        modelLoaded = true;
        Debug.Log("[WhisperManager] Model loaded successfully!");
        return true;
    }

    public static string TranscribePartial(string audioPath)
    {
        if (!modelLoaded)
        {
            Debug.LogError("[WhisperManager] No model loaded.");
            return "[NO_MODEL]";
        }

        IntPtr resultPtr = unity_whisper_transcribe_partial(audioPath);
        if (resultPtr == IntPtr.Zero)
            return "[BLANK_AUDIO]";

        return Marshal.PtrToStringAnsi(resultPtr);
    }

    public static string Transcribe(string audioPath)
    {
        if (!modelLoaded)
        {
            Debug.LogError("[WhisperManager] No model loaded.");
            return "[NO_MODEL]";
        }

        IntPtr resultPtr = unity_whisper_transcribe(audioPath);
        if (resultPtr == IntPtr.Zero)
            return "[BLANK_AUDIO]";

        return Marshal.PtrToStringAnsi(resultPtr);
    }

    public static void Unload()
    {
        if (modelLoaded)
        {
            unity_whisper_unload_model();
            modelLoaded = false;
            Debug.Log("[WhisperManager] Freed model context.");
        }
    }
}

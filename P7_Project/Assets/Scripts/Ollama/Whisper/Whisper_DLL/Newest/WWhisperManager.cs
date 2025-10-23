using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WWhisperManager
{
    private const string DLL_NAME = "whisper_unity";
    private static bool modelLoaded = false;

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool unity_whisper_init_from_file(string modelPath);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe(string wavPath);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_whisper_transcribe_partial(string wavPath);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_whisper_unload_model();

    private static string PtrToString(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(ptr);
    }

    public static bool InitModel(string modelPath)
    {
        if (modelLoaded) return true;
        modelLoaded = unity_whisper_init_from_file(modelPath);
        Debug.Log(modelLoaded ? "[Whisper] Model initialized" : "[Whisper] Failed to initialize model");
        return modelLoaded;
    }

    public static string Transcribe(string wavPath)
    {
        if (!modelLoaded) return "[Error] Model not loaded";
        IntPtr result = unity_whisper_transcribe(wavPath);
        return PtrToString(result);
    }

    public static string TranscribePartial(string wavPath)
    {
        if (!modelLoaded) return "[Error] Model not loaded";
        IntPtr result = unity_whisper_transcribe_partial(wavPath);
        return PtrToString(result);
    }

    public static void Unload()
    {
        if (!modelLoaded) return;
        unity_whisper_unload_model();
        modelLoaded = false;
        Debug.Log("[Whisper] Model unloaded");
    }
}

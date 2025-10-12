using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WhisperBridge
{
    [DllImport("whisper")]
    public static extern int whisper_init_from_file(string modelPath);

    [DllImport("whisper")]
    public static extern IntPtr whisper_transcribe(string audioFile);

    public static string Transcribe(string audioPath)
    {
        IntPtr ptr = whisper_transcribe(audioPath);
        return Marshal.PtrToStringAnsi(ptr);
    }
}

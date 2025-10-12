using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class LlamaBridge
{
    [DllImport("llama")]
    public static extern int llama_init_from_file(string modelPath);

    [DllImport("llama")]
    public static extern IntPtr llama_generate(string prompt);

    public static string GenerateText(string prompt)
    {
        IntPtr ptr = llama_generate(prompt);
        return Marshal.PtrToStringAnsi(ptr);
    }
}

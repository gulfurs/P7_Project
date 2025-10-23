using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    const int HEADER_SIZE = 44;

    public static byte[] FromAudioClip(AudioClip clip)
    {
        MemoryStream stream = new MemoryStream();
        int length = clip.samples * clip.channels;
        float[] samples = new float[length];
        clip.GetData(samples, 0);

        ushort bitDepth = 16;
        int byteRate = clip.frequency * clip.channels * (bitDepth / 8);

        stream.Seek(0, SeekOrigin.Begin);
        WriteHeader(stream, clip, length, byteRate, bitDepth);
        WriteSamples(stream, samples, bitDepth);
        return stream.ToArray();
    }

    static void WriteHeader(Stream stream, AudioClip clip, int length, int byteRate, ushort bitDepth)
    {
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
        writer.Write((int)(HEADER_SIZE + length * (bitDepth / 8) - 8));
        writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)clip.channels);
        writer.Write(clip.frequency);
        writer.Write(byteRate);
        writer.Write((ushort)(clip.channels * (bitDepth / 8)));
        writer.Write(bitDepth);
        writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
        writer.Write(length * (bitDepth / 8));
    }

    static void WriteSamples(Stream stream, float[] samples, ushort bitDepth)
    {
        BinaryWriter writer = new BinaryWriter(stream);
        float rescaleFactor = 32767f; // to convert float [-1.0,1.0] to Int16
        foreach (float f in samples)
        {
            short val = (short)(Mathf.Clamp(f, -1f, 1f) * rescaleFactor);
            writer.Write(val);
        }
    }

    public static void SaveToFile(AudioClip clip, string filePath)
    {
        byte[] wavBytes = FromAudioClip(clip);
        File.WriteAllBytes(filePath, wavBytes);
        Debug.Log("Saved WAV file to: " + filePath);
    }
}

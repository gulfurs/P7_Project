using UnityEngine;
using System.Collections;

public class WhisperContinuous : MonoBehaviour
{
    public string modelFileName = "ggml-large.bin";
    public float chunkDuration = 3f; 
    public AudioSource audioSource;

    private string modelPath;
    private string micDevice;

    [Header("Connections")]
    public LlamaController llamaController; 

    void Start()
    {
        modelPath = Application.streamingAssetsPath + "/Whisper/" + modelFileName;
        Debug.Log("[Whisper] Loading model: " + modelPath);

        if (!WWhisperManager.InitModel(modelPath))
        {
            Debug.LogError("[Whisper] Model failed to load!");
            return;
        }

        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (micDevice == null)
        {
            Debug.LogError("[Whisper] No microphone found!");
            return;
        }

        Debug.Log("[Whisper] Starting continuous recognition on: " + micDevice);
        StartCoroutine(CaptureMicrophone());
    }

    private IEnumerator CaptureMicrophone()
    {
        while (true)
        {
            // Record a short chunk
            AudioClip clip = Microphone.Start(micDevice, false, (int)chunkDuration, 16000);
            yield return new WaitForSeconds(chunkDuration);
            Microphone.End(micDevice);

            string path = Application.persistentDataPath + "/mic_chunk.wav";
            SaveWav(path, clip);

            // Run Whisper on this chunk
            string result = WWhisperManager.TranscribePartial(path);

            if (!string.IsNullOrWhiteSpace(result) && result != "[BLANK_AUDIO]")
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    Debug.Log($"[Whisper Result] {result}");

                    if (llamaController != null)
                    {
                        //  Send recognized speech directly to Llama
                        llamaController.GenerateReply(result);
                    }
                    else
                    {
                        Debug.LogWarning("[Whisper?LLaMA] No LlamaController linked!");
                    }
                });
            }
        }
    }

    private void SaveWav(string path, AudioClip clip)
    {
        var data = WavUtility.FromAudioClip(clip);
        System.IO.File.WriteAllBytes(path, data);
    }

    private void OnApplicationQuit()
    {
        WWhisperManager.Unload();
    }
}

using UnityEngine;
using System.Collections;
using TMPro;

public class WhisperContinuous : MonoBehaviour
{
    [Header("Whisper Settings")]
    public string modelFileName = "ggml-tiny.bin";
    public float chunkDuration = 3f; 
    public AudioSource audioSource;

    [Header("Input Field")]
    public TMP_InputField inputField;

    private string modelPath;
    private string micDevice;

    void Start()
    {
        modelPath = Application.streamingAssetsPath + "/Whisper/" + modelFileName;
        Debug.Log("[Whisper] Loading model: " + modelPath);

        if (!WhisperManager.InitModel(modelPath))
        {
            Debug.LogError("[Whisper] Model failed to load!");
            return;
        }

        if (inputField == null)
        {
            var tutorialCanvas = FindObjectOfType<Canvas>();
            if (tutorialCanvas != null)
                inputField = tutorialCanvas.GetComponentInChildren<TMP_InputField>();
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
            AudioClip clip = Microphone.Start(micDevice, false, (int)chunkDuration, 16000);
            yield return new WaitForSeconds(chunkDuration);
            Microphone.End(micDevice);

            string path = Application.persistentDataPath + "/mic_chunk.wav";
            SaveWav(path, clip);

            // Run Whisper on this chunk
            string result = WhisperManager.Transcribe(path);

            if (!string.IsNullOrWhiteSpace(result) && result != "[BLANK_AUDIO]")
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    Debug.Log($"[Whisper] Transcribed: {result}");

                    if (inputField != null)
                        inputField.text = result;
                        // In continuous mode, we might want to auto-submit or just display
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
        WhisperManager.Unload();
    }
}
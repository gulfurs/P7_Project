using UnityEngine;
using System.Collections;
using TMPro;

public class WhisperContinuous : MonoBehaviour
{
    public string modelFileName = "ggml-tiny.bin";
    public float chunkDuration = 3f;
    public AudioSource audioSource;

    [Header("Input Field")]
    public TMP_InputField inputField;

    private string modelPath;
    private string micDevice;

    // ? NEW ? — reference to LLaMA
    private LlamaController llama;

    void Start()
    {
        modelPath = Application.streamingAssetsPath + "/Whisper/" + modelFileName;
        Debug.Log("[Whisper] Loading model: " + modelPath);

        if (!WWhisperManager.InitModel(modelPath))
        {
            Debug.LogError("[Whisper] Model failed to load!");
            return;
        }

        // ? NEW ? — find LLaMA in scene
        llama = FindObjectOfType<LlamaController>();
        if (llama == null)
            Debug.LogWarning("[Whisper] No LlamaController found in scene!");

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

            string result = WWhisperManager.TranscribePartial(path);

            if (!string.IsNullOrWhiteSpace(result) && result != "[BLANK_AUDIO]")
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    Debug.Log($"[Whisper] Transcribed: {result}");

                    if (inputField != null)
                        inputField.text = result;

                    // ? NEW ? — send result to LLaMA
                    if (llama != null)
                    {
                        llama.GenerateReply(result);
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

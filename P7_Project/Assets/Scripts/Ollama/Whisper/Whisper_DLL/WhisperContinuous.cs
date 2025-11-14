using UnityEngine;
using System.Collections;
using TMPro;

public enum RecordingMode
{
    Continuous,
    PushToTalk
}

public class WhisperContinuous : MonoBehaviour
{
    [Header("Whisper Settings")]
    public RecordingMode mode = RecordingMode.Continuous;
    public string modelFileName = "ggml-tiny.bin";
    public float chunkDuration = 3f; 
    public AudioSource audioSource;

    [Header("Input Field")]
    public TMP_InputField inputField;

    private string modelPath;
    private string micDevice;
    private AudioClip pushToTalkClip;
    private bool isRecordingForPushToTalk = false;

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

        // Only start continuous capture if in the correct mode
        if (mode == RecordingMode.Continuous)
        {
            Debug.Log("[Whisper] Starting continuous recognition on: " + micDevice);
            StartCoroutine(CaptureMicrophone());
        }
        else
        {
            Debug.Log("[Whisper] Push-to-talk mode enabled. Waiting for input.");
        }
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

    /// <summary>
    /// Starts recording audio for push-to-talk.
    /// </summary>
    public void StartPushToTalkRecording()
    {
        if (mode != RecordingMode.PushToTalk || isRecordingForPushToTalk || micDevice == null)
        {
            return;
        }

        isRecordingForPushToTalk = true;
        // Record for a longer duration, as we'll stop it manually
        pushToTalkClip = Microphone.Start(micDevice, false, 300, 16000); 
        Debug.Log("[Whisper] Started push-to-talk recording.");
    }

    /// <summary>
    /// Stops recording audio for push-to-talk and transcribes the result.
    /// </summary>
    public void StopPushToTalkRecordingAndTranscribe()
    {
        if (mode != RecordingMode.PushToTalk || !isRecordingForPushToTalk)
        {
            return;
        }

        Microphone.End(micDevice);
        isRecordingForPushToTalk = false;
        Debug.Log("[Whisper] Stopped push-to-talk recording. Transcribing...");

        string path = Application.persistentDataPath + "/ptt_clip.wav";
        SaveWav(path, pushToTalkClip);

        // Run Whisper on the saved clip
        string result = WhisperManager.Transcribe(path);

        if (!string.IsNullOrWhiteSpace(result) && result != "[BLANK_AUDIO]")
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                Debug.Log($"[Whisper] Transcribed PTT: {result}");

                if (inputField != null)
                    inputField.text = result;

                // Directly notify NPCs instead of relying on inputField.onSubmit
                var npcManager = NPCManager.Instance;
                if (npcManager != null && npcManager.npcInstances.Count > 0)
                {
                    var firstNPC = npcManager.npcInstances[0];
                    if (firstNPC != null)
                    {
                        firstNPC.ProcessUserAnswer(result);
                    }
                }
                else
                {
                    Debug.LogWarning("[Whisper] No NPCs found to process transcription.");
                }
            });
        }
        else
        {
            Debug.Log("[Whisper] PTT transcription was blank.");
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
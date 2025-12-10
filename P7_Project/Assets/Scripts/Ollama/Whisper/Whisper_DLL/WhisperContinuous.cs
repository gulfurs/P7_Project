using UnityEngine;
using System.Collections;
using TMPro;

public class WhisperContinuous : MonoBehaviour
{
    [Header("Whisper Settings")]
    public string modelFileName = "ggml-tiny.bin";
    public float chunkDuration = 3f; // keep this small (2�3s) for responsive STT
    public AudioSource audioSource;

    [Header("Input Field")]
    public TMP_InputField inputField;

    [Header("NPC Chat")]
    public NPCChatInstance npcChatInstance;    // assign in Inspector

    [Header("Sentence Detection")]
    [Tooltip("Seconds after last speech before we send the final text to NPCChatInstance.Send()")]
    public float sentenceEndDelay = 3f;        // 3�4 seconds as requested

    [Tooltip("Hard limit for how long a single answer can last (seconds). 0 = no limit.")]
    public float maxUtteranceDuration = 60f;   // e.g. 60 seconds total per answer

    [Header("Control")]
    [Tooltip("If false, you must call EnableSending(true) / SetSendingEnabled(true) before the mic input will ever call NPCChatInstance.Send().")]
    public bool sendingEnabledAtStart = false;

    private string modelPath;
    private string micDevice;

    // state
    private string currentTranscript = "";
    private float lastSpeechTime = -1f;
    private float utteranceStartTime = -1f;
    private bool hasPendingUtterance = false;
    private bool sendingEnabled;

    /// <summary>
    /// For external scripts to check whether we currently allow sending.
    /// </summary>
    public bool IsSendingEnabled => sendingEnabled;

    void Start()
    {
        sendingEnabled = sendingEnabledAtStart;

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

    /// <summary>
    /// PUBLIC: Recommended external API for other scripts (like TTS handlers)
    /// to enable/disable sending.
    /// 
    /// Example usage:
    ///  - SetSendingEnabled(false) when TTS starts speaking.
    ///  - SetSendingEnabled(true) when TTS finishes.
    /// </summary>
    public void SetSendingEnabled(bool enable)
    {
        EnableSending(enable);
    }

    /// <summary>
    /// Call this from your game / dialogue logic.
    /// Example: set true when it's the player's turn to answer.
    /// </summary>
    public void EnableSending(bool enable)
    {
        sendingEnabled = enable;
        Debug.Log("[Whisper] Sending enabled = " + sendingEnabled);

        // When we turn sending off, clear any half-finished utterance
        if (!sendingEnabled)
        {
            currentTranscript = "";
            hasPendingUtterance = false;
            lastSpeechTime = -1f;
            utteranceStartTime = -1f;

            if (inputField != null)
                inputField.text = "";
        }
    }

    void Update()
    {
        // Only try to commit utterances when sending is actually allowed.
        if (!sendingEnabled)
            return;

        if (!hasPendingUtterance || lastSpeechTime <= 0f)
            return;

        bool silenceLongEnough =
            Time.time - lastSpeechTime >= sentenceEndDelay;

        bool utteranceTooLong =
            maxUtteranceDuration > 0f &&
            utteranceStartTime > 0f &&
            Time.time - utteranceStartTime >= maxUtteranceDuration;

        if (silenceLongEnough || utteranceTooLong)
        {
            CommitUtterance();
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
            string trimmedResult = result == null ? "" : result.Trim();

            // ----- IMPORTANT FILTERS -----
            // 1) Skip empty / whitespace
            // 2) Skip things like [BLANK_AUDIO], [Chirping], [Noise], etc.
            if (string.IsNullOrWhiteSpace(trimmedResult) || IsBracketOnlyToken(trimmedResult))
            {
                // Do NOT update lastSpeechTime here � this counts as silence.
                continue;
            }

            // If sending isn't enabled yet (intro, instructions, NPC talking), ignore content.
            if (!sendingEnabled)
            {
                Debug.Log($"[Whisper] Ignoring transcript while sending disabled: {trimmedResult}");
                continue;
            }

            // ----- REAL SPEECH -----
            Debug.Log($"[Whisper] Transcribed chunk: {trimmedResult}");

            // If this is the first speech in this answer, mark start time
            if (!hasPendingUtterance)
            {
                utteranceStartTime = Time.time;
                currentTranscript = trimmedResult;
                
                // Hook for latency profiler - Speech Start (STT)
                if (LatencyEvaluator.Instance != null)
                    LatencyEvaluator.Instance.MarkSpeechStart();
            }
            else
            {
                currentTranscript = (currentTranscript + " " + trimmedResult).Trim();
            }

            // Update UI live as the user speaks
            if (inputField != null)
            {
                inputField.text = currentTranscript;
            }

            // Mark the time of this last speech chunk
            lastSpeechTime = Time.time;
            hasPendingUtterance = true;
        }
    }

    private bool IsBracketOnlyToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        // e.g. "[Chirping]", "[BLANK_AUDIO]", "[Noise]"
        return text.StartsWith("[") && text.EndsWith("]");
    }

    private void CommitUtterance()
    {
        hasPendingUtterance = false;

        if (string.IsNullOrWhiteSpace(currentTranscript))
        {
            lastSpeechTime = -1f;
            utteranceStartTime = -1f;
            return;
        }

        // Extra safety: if sending was turned off between capture and commit, drop it.
        if (!sendingEnabled)
        {
            Debug.Log($"[Whisper] Utterance ready but sending disabled, discarding: {currentTranscript}");
            currentTranscript = "";
            lastSpeechTime = -1f;
            utteranceStartTime = -1f;

            if (inputField != null)
                inputField.text = "";
            return;
        }

        Debug.Log($"[Whisper] Sentence end detected. Final transcript: {currentTranscript}");

        // Hook for latency profiler - Input Sent (STT complete)
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkInputSent();

        // Put final text into NPCChatInstance's input and call Send()
        if (npcChatInstance != null && npcChatInstance.userInput != null)
        {
            npcChatInstance.userInput.text = currentTranscript;
            npcChatInstance.Send();       // <- still the key requirement
        }
        else
        {
            if (inputField != null)
                inputField.text = currentTranscript;
        }

        // Reset for next utterance
        currentTranscript = "";
        lastSpeechTime = -1f;
        utteranceStartTime = -1f;

        if (inputField != null)
        {
            inputField.text = "";
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

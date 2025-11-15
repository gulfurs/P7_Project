using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;

public class WhisperContinuous : MonoBehaviour
{
    [Header("Whisper Settings")]
    public string modelFileName = "ggml-tiny.bin";
    public float chunkDuration = 3f; 
    public AudioSource audioSource;

    [Header("Input Field")]
    public TMP_InputField inputField;

    [Header("Sentence Accumulation Settings")]
    [Tooltip("Time in seconds of silence before considering sentence complete")]
    public float silenceThreshold = 2.0f;

    private string modelPath;
    private string micDevice;

    // Sentence accumulation for continuous mode
    private string accumulatedSentence = "";
    private float lastSpeechTime = 0f;
    private bool isSpeaking = false;

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

            // Clean the transcription
            string cleanedResult = CleanTranscription(result);

            if (!string.IsNullOrWhiteSpace(cleanedResult))
            {
                // User is speaking, accumulate the text
                isSpeaking = true;
                lastSpeechTime = Time.time;
                
                // Add to accumulated sentence with space
                if (!string.IsNullOrEmpty(accumulatedSentence))
                {
                    accumulatedSentence += " " + cleanedResult;
                }
                else
                {
                    accumulatedSentence = cleanedResult;
                }

                Debug.Log($"[Whisper] Accumulating: {cleanedResult} | Full: {accumulatedSentence}");
            }
            else
            {
                // Check if we should finalize the accumulated sentence
                if (isSpeaking && !string.IsNullOrEmpty(accumulatedSentence))
                {
                    float silenceDuration = Time.time - lastSpeechTime;
                    
                    if (silenceDuration >= silenceThreshold)
                    {
                        // Sentence complete - finalize it
                        string finalSentence = accumulatedSentence.Trim();
                        accumulatedSentence = "";
                        isSpeaking = false;

                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            Debug.Log($"[Whisper] Finalized sentence: {finalSentence}");

                            if (inputField != null)
                                inputField.text = finalSentence;

                            // Auto-submit to NPC
                            NotifyNPCs(finalSentence);
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cleans transcription by removing unwanted markers and normalizing text.
    /// </summary>
    private string CleanTranscription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Trim whitespace
        text = text.Trim();

        // Remove anything inside brackets [] or parentheses ()
        text = Regex.Replace(text, @"\[.*?\]", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\(.*?\)", "", RegexOptions.IgnoreCase);

        // Remove excessive punctuation (e.g., "..." or single ".")
        text = Regex.Replace(text, @"^\.+$", "");

        // Normalize multiple spaces
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
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
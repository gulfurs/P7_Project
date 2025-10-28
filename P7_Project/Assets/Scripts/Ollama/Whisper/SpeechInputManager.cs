using System.IO;
using UnityEngine;

/// <summary>
/// Integrates Whisper speech-to-text with NPC interview system
/// Routes: Audio → Whisper transcription → Shared memory → NPC decisions
/// </summary>
public class SpeechInputManager : MonoBehaviour
{
    [Header("Microphone Settings")]
    public int sampleRate = 16000;
    public int recordSeconds = 5;
    
    [Header("Whisper Settings")]
    public string whisperModelPath = "Assets/StreamingAssets/models/whisper-tiny.en.gguf";
    
    [Header("Audio Output")]
    public string wavOutputPath = "Assets/StreamingAssets/audio/temp_input.wav";

    private AudioClip micClip;
    private string micDevice;
    private bool isRecording = false;
    private bool whisperInitialized = false;

    private NPCChatInstance npcChatInstance;
    private LlamaMemory llamaMemory;

    private static SpeechInputManager instance;

    public static SpeechInputManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SpeechInputManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("SpeechInputManager");
                    instance = obj.AddComponent<SpeechInputManager>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void Start()
    {
        // Get references
        npcChatInstance = FindObjectOfType<NPCChatInstance>();
        llamaMemory = LlamaMemory.Instance;

        // Create output directory if needed
        string dir = Path.GetDirectoryName(wavOutputPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Initialize Whisper model
        whisperInitialized = WWhisperManager.InitModel(whisperModelPath);
        if (whisperInitialized)
        {
            Debug.Log("[SpeechInputManager] Initialized - ready to capture voice input");
        }
        else
        {
            Debug.LogError("[SpeechInputManager] Failed to initialize Whisper model!");
        }
    }

    /// <summary>
    /// Start recording microphone input
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("[SpeechInputManager] Already recording!");
            return;
        }

        if (!Microphone.IsRecording(null))
        {
            micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            micClip = Microphone.Start(micDevice, false, recordSeconds, sampleRate);
            isRecording = true;
            Debug.Log($"[SpeechInputManager] Recording started on device: {micDevice}");
        }
    }

    /// <summary>
    /// Stop recording and transcribe
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording || !Microphone.IsRecording(null))
        {
            Debug.LogWarning("[SpeechInputManager] Not currently recording!");
            return;
        }

        if (!whisperInitialized)
        {
            Debug.LogError("[SpeechInputManager] Whisper model not initialized!");
            Microphone.End(micDevice);
            isRecording = false;
            return;
        }

        Microphone.End(micDevice);
        isRecording = false;
        Debug.Log("[SpeechInputManager] Recording stopped, transcribing...");

        // Save to WAV file
        SaveWavFile(micClip);

        // Transcribe using Whisper
        TranscribeAudio();
    }

    /// <summary>
    /// Save audio clip to WAV file
    /// </summary>
    private void SaveWavFile(AudioClip clip)
    {
        byte[] wavData = WavUtility.FromAudioClip(clip);
        
        string fullPath = Path.Combine(Application.persistentDataPath, wavOutputPath);
        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(fullPath, wavData);
        Debug.Log($"[SpeechInputManager] Saved audio to: {fullPath}");
    }

    /// <summary>
    /// Transcribe audio using Whisper
    /// </summary>
    private void TranscribeAudio()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, wavOutputPath);

        // Use WWhisperManager for transcription
        string transcription = WWhisperManager.Transcribe(fullPath);

        if (string.IsNullOrEmpty(transcription) || transcription.Contains("Error"))
        {
            Debug.LogError($"[SpeechInputManager] Transcription failed: {transcription}");
            return;
        }

        Debug.Log($"[SpeechInputManager] Transcribed: {transcription}");
        OnTranscriptionReady(transcription);
    }

    /// <summary>
    /// Called when transcription is ready - insert into NPC pipeline
    /// </summary>
    private void OnTranscriptionReady(string userSpeech)
    {
        if (npcChatInstance == null)
            npcChatInstance = FindObjectOfType<NPCChatInstance>();

        if (llamaMemory == null)
            llamaMemory = LlamaMemory.Instance;

        Debug.Log($"[SpeechInputManager] Processing transcribed speech: \"{userSpeech}\"");

        // Add to shared memory and trigger NPC decisions (same as Send() does)
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnUserAnswered(userSpeech);
        }

        llamaMemory.AddDialogueTurn("User", userSpeech);

        var manager = NPCManager.Instance;
        if (manager != null)
        {
            foreach (var npc in manager.npcInstances)
            {
                if (npc != null)
                {
                    npc.AskLLMIfShouldRespond(userSpeech);
                }
            }
        }
    }

    public bool IsRecording => isRecording;
}

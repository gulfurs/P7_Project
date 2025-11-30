using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections;
using TMPro;

/// <summary>
/// Windows Dictation STT - copied structure from WhisperContinuous but using Windows native engine
/// </summary>
public class WindowsDictation : MonoBehaviour
{
    [Header("Input Field")]
    public TMP_InputField inputField;

    [Header("NPC Chat")]
    public NPCChatInstance npcChatInstance;    // assign in Inspector

    [Header("Sentence Detection")]
    [Tooltip("Seconds after last speech before we send the final text to NPCChatInstance.Send()")]
    public float sentenceEndDelay = 3f;        // 3–4 seconds as requested

    [Tooltip("Hard limit for how long a single answer can last (seconds). 0 = no limit.")]
    public float maxUtteranceDuration = 60f;   // e.g. 60 seconds total per answer

    [Header("Control")]
    [Tooltip("If false, you must call EnableSending(true) / SetSendingEnabled(true) before the mic input will ever call NPCChatInstance.Send().")]
    public bool sendingEnabledAtStart = false;

    [Tooltip("Cooldown period after sending before accepting new input (in seconds).")]
    public float sendCooldownDuration = 10f;

    private DictationRecognizer dictationRecognizer;

    // state
    private string currentTranscript = "";
    private float lastSpeechTime = -1f;
    private float utteranceStartTime = -1f;
    private bool hasPendingUtterance = false;
    private bool sendingEnabled;
    private float lastSendTime = -1f;
    private bool isInCooldown = false;

    /// <summary>
    /// For external scripts to check whether we currently allow sending.
    /// </summary>
    public bool IsSendingEnabled => sendingEnabled;

    void Start()
    {
        sendingEnabled = sendingEnabledAtStart;

        if (inputField == null)
        {
            var tutorialCanvas = FindObjectOfType<Canvas>();
            if (tutorialCanvas != null)
                inputField = tutorialCanvas.GetComponentInChildren<TMP_InputField>();
        }

        if (npcChatInstance == null)
            npcChatInstance = FindObjectOfType<NPCChatInstance>();

        Debug.Log("[WindowsDictation] Starting continuous recognition.");

        try
        {
            dictationRecognizer = new DictationRecognizer();
            dictationRecognizer.DictationResult += OnDictationResult;
            dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
            dictationRecognizer.DictationComplete += OnDictationComplete;
            dictationRecognizer.DictationError += OnDictationError;
            StartDictation();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WindowsDictation] Initialization Failed!\n" +
                $"Make sure Windows Speech Recognition is enabled in Settings > Time & Language > Speech\n" +
                $"Error: {e.Message}");
        }
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
        Debug.Log("[WindowsDictation] Sending enabled = " + sendingEnabled);

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
        if (!sendingEnabled || isInCooldown)
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

    void LateUpdate()
    {
        // Check if cooldown has expired
        if (isInCooldown && Time.time - lastSendTime >= sendCooldownDuration)
        {
            isInCooldown = false;
            Debug.Log("[WindowsDictation] Cooldown expired. Ready to accept new input.");
        }
    }

    private void StartDictation()
    {
        if (dictationRecognizer != null && dictationRecognizer.Status != SpeechSystemStatus.Running)
        {
            dictationRecognizer.Start();
            Debug.Log("[WindowsDictation] Dictation started.");
        }
    }

    private void OnDictationHypothesis(string text)
    {
        if (!sendingEnabled || isInCooldown) return;

        // Hook for latency profiler - Speech Start
        if (LatencyEvaluator.Instance != null)
            LatencyEvaluator.Instance.MarkSpeechStart();

        Debug.Log($"[WindowsDictation] Hypothesis (ignored): {text}");
        // Don't accumulate hypothesis - only use final results
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (!sendingEnabled || isInCooldown) return;

        Debug.Log($"[WindowsDictation] Result (confidence: {confidence}): {text}");

        if (string.IsNullOrWhiteSpace(text))
            return;

        // Replace the entire transcript with the final result (don't append)
        currentTranscript = text;
        utteranceStartTime = Time.time;

        // Update UI with the final recognized text
        if (inputField != null)
        {
            inputField.text = currentTranscript;
        }

        // Mark the time of this final result
        lastSpeechTime = Time.time;
        hasPendingUtterance = true;
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        Debug.Log($"[WindowsDictation] Engine stopped: {cause}. Restarting...");
        // Always restart the engine to keep listening continuously
        StartCoroutine(RestartDictationDelayed());
    }

    private IEnumerator RestartDictationDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Dispose();
        }
        
        try
        {
            dictationRecognizer = new DictationRecognizer();
            dictationRecognizer.DictationResult += OnDictationResult;
            dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
            dictationRecognizer.DictationComplete += OnDictationComplete;
            dictationRecognizer.DictationError += OnDictationError;
            StartDictation();
            Debug.Log("[WindowsDictation] Dictation restarted.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WindowsDictation] Failed to restart: {e.Message}");
        }
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError($"[WindowsDictation] Error: {error}");
        StartCoroutine(RestartDictationDelayed());
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
            Debug.Log($"[WindowsDictation] Utterance ready but sending disabled, discarding: {currentTranscript}");
            currentTranscript = "";
            lastSpeechTime = -1f;
            utteranceStartTime = -1f;

            if (inputField != null)
                inputField.text = "";
            return;
        }

        Debug.Log($"[WindowsDictation] Sentence end detected. Final transcript: {currentTranscript}");

        // Put final text into NPCChatInstance's input and call Send()
        if (npcChatInstance != null && npcChatInstance.userInput != null)
        {
            npcChatInstance.userInput.text = currentTranscript;

            // Hook for latency profiler - Input Sent
            if (LatencyEvaluator.Instance != null)
                LatencyEvaluator.Instance.MarkInputSent();

            npcChatInstance.Send();       // <- still the key requirement
        }
        else
        {
            if (inputField != null)
                inputField.text = currentTranscript;
        }

        // Start cooldown to prevent overlapping inputs
        isInCooldown = true;
        lastSendTime = Time.time;
        Debug.Log($"[WindowsDictation] Input sent. Cooldown started ({sendCooldownDuration}s). No new input until then.");

        // Reset for next utterance
        currentTranscript = "";
        lastSpeechTime = -1f;
        utteranceStartTime = -1f;
        hasPendingUtterance = false;

        if (inputField != null)
        {
            inputField.text = "";
        }
    }

    void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Dispose();
            dictationRecognizer = null;
        }
    }
}

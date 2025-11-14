using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interview UI - Just a recording indicator (red/green)
/// Everything else is handled by existing systems
/// </summary>
public class InterviewUIManager : MonoBehaviour
{
    [Header("Recording Indicator")]
    public Image recordingIndicator;
    public Color recordingColor = Color.red;
    public Color idleColor = Color.green;
    [Tooltip("Color to show when the system is listening via push-to-talk.")]
    public Color listeningColor = Color.yellow;

    [Header("Push-to-Talk Settings")]
    [Tooltip("Enable push-to-talk functionality.")]
    public bool enablePushToTalk = true;
    [Tooltip("The key to press to talk.")]
    public KeyCode pushToTalkKey = KeyCode.V;
    [Tooltip("Reference to the WhisperContinuous script for controlling recording.")]
    public WhisperContinuous whisperController;

    private bool isListening = false;

    private void Start()
    {
        if (enablePushToTalk && whisperController == null)
        {
            // Try to find it if not assigned
            whisperController = FindObjectOfType<WhisperContinuous>();
            if (whisperController == null)
            {
                Debug.LogError("[InterviewUIManager] Push-to-talk is enabled, but WhisperContinuous controller is not found!");
                enablePushToTalk = false; // Disable to prevent errors
            }
        }
    }

    private void Update()
    {
        HandlePushToTalk();
        UpdateRecordingIndicator();
    }

    private void HandlePushToTalk()
    {
        if (!enablePushToTalk || whisperController == null)
        {
            return;
        }

        // When key is pressed down
        if (Input.GetKeyDown(pushToTalkKey))
        {
            isListening = true;
            Debug.Log("[PushToTalk] Key pressed. Starting recording...");
            whisperController.StartPushToTalkRecording();
        }

        // When key is released
        if (Input.GetKeyUp(pushToTalkKey))
        {
            isListening = false;
            Debug.Log("[PushToTalk] Key released. Stopping recording and transcribing...");
            whisperController.StopPushToTalkRecordingAndTranscribe();
        }
    }

    private void UpdateRecordingIndicator()
    {
        if (recordingIndicator == null) return;

        if (enablePushToTalk)
        {
            // In push-to-talk mode, the indicator shows if you are currently speaking
            recordingIndicator.color = isListening ? listeningColor : idleColor;
        }
        else
        {
            // In continuous mode, the indicator shows if an NPC is speaking
            if (DialogueManager.Instance != null)
            {
                string currentSpeaker = DialogueManager.Instance.currentSpeaker;
                recordingIndicator.color = !string.IsNullOrEmpty(currentSpeaker) ? recordingColor : idleColor;
            }
        }
    }
}

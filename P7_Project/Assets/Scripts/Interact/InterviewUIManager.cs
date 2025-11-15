using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interview UI - Just a recording indicator (red/green)
/// Shows when NPCs are speaking (continuous recording mode only)
/// </summary>
public class InterviewUIManager : MonoBehaviour
{
    [Header("Recording Indicator")]
    public Image recordingIndicator;
    public Color recordingColor = Color.red;
    public Color idleColor = Color.green;

    private void Start()
    {
        // Initialize recording indicator
        UpdateRecordingIndicator();
    }

    private void Update()
    {
        UpdateRecordingIndicator();
    }

    private void UpdateRecordingIndicator()
    {
        if (recordingIndicator == null) return;

        // In continuous mode, the indicator shows if an NPC is speaking
        if (DialogueManager.Instance != null)
        {
            string currentSpeaker = DialogueManager.Instance.currentSpeaker;
            recordingIndicator.color = !string.IsNullOrEmpty(currentSpeaker) ? recordingColor : idleColor;
        }
    }
}

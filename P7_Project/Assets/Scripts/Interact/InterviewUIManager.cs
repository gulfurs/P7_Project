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

    private void Update()
    {
        // Optional: animate indicator based on dialogue state
        if (DialogueManager.Instance != null)
        {
            string currentSpeaker = DialogueManager.Instance.currentSpeaker;
            
            if (recordingIndicator != null)
            {
                recordingIndicator.color = !string.IsNullOrEmpty(currentSpeaker) ? recordingColor : idleColor;
            }
        }
    }
}

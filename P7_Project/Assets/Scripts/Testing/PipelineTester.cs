using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class PipelineTester : MonoBehaviour
{
    public List<AudioClip> testClips;
    public NPCChatInstance targetNPC;
    public float cooldownSeconds = 3.0f;

    private bool isRunning = false;

    [ContextMenu("Start Test Suite")]
    public void StartTestSuite()
    {
        if (isRunning) return;
        StartCoroutine(RunTests());
    }

    private IEnumerator RunTests()
    {
        isRunning = true;
        
        // Ensure LatencyEvaluator exists
        if (LatencyEvaluator.Instance == null)
        {
            GameObject go = new GameObject("LatencyEvaluator");
            go.AddComponent<LatencyEvaluator>();
        }

        for (int i = 0; i < testClips.Count; i++)
        {
            AudioClip clip = testClips[i];
            string testName = $"Clip_{i}_{clip.name}";
            Debug.Log($"--- Starting Test: {testName} ---");

            // 1. Save Clip to WAV
            string tempPath = Path.Combine(Application.persistentDataPath, "temp_test.wav");
            SaveWav(tempPath, clip);

            // 2. STT
            LatencyEvaluator.Instance.StartTimer("STT");
            string transcription = WhisperManager.Transcribe(tempPath);
            LatencyEvaluator.Instance.StopTimer("STT");

            Debug.Log($"[Tester] Transcription: {transcription}");

            if (string.IsNullOrEmpty(transcription) || transcription == "[BLANK_AUDIO]")
            {
                Debug.LogWarning("Skipping empty transcription.");
                continue;
            }

            // 3. Inject into NPC
            bool interactionComplete = false;
            
            // Subscribe to completion event
            System.Action onComplete = () => { interactionComplete = true; };
            if (targetNPC != null)
            {
                targetNPC.OnInteractionComplete += onComplete;
                targetNPC.ProcessUserAnswer(transcription);
            }
            else
            {
                Debug.LogError("Target NPC is null!");
                break;
            }

            // Wait for completion (LLM done AND TTS done)
            float timeout = 60f; // Increased timeout for full speech
            float timer = 0;
            while (timer < timeout)
            {
                bool llmDone = interactionComplete;
                bool ttsDone = !targetNPC.ttsHandler.IsSpeaking();

                if (llmDone && ttsDone)
                    break;

                timer += Time.deltaTime;
                yield return null;
            }

            if (targetNPC != null)
                targetNPC.OnInteractionComplete -= onComplete;

            // Log results
            LatencyEvaluator.Instance.EndTest(testName);

            // Cooldown
            yield return new WaitForSeconds(cooldownSeconds);
        }

        isRunning = false;
        Debug.Log("--- Test Suite Complete ---");
    }

    private void SaveWav(string path, AudioClip clip)
    {
        byte[] wavData = WavUtility.FromAudioClip(clip);
        File.WriteAllBytes(path, wavData);
    }
}

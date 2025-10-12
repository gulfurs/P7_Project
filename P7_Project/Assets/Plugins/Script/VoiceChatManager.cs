using UnityEngine;

public class VoiceChatManager : MonoBehaviour
{
    public string whisperModel = "Assets/StreamingAssets/models/whisper-base.en.bin";
    public string llamaModel = "Assets/StreamingAssets/models/llama2-7b-q4_0";

    void Start()
    {
        Debug.Log("Initializing models...");
        WhisperBridge.whisper_init_from_file(whisperModel);
        LlamaBridge.llama_init_from_file(llamaModel);
    }

    public void ProcessAudio(string audioPath)
    {
        string text = WhisperBridge.Transcribe(audioPath);
        Debug.Log("Whisper output: " + text);

        string reply = LlamaBridge.GenerateText(text);
        Debug.Log("Llama reply: " + reply);
    }
}

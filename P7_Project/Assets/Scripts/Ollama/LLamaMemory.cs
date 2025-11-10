using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Shared conversation memory for job interview simulation
/// Used by all NPCs - maintains dialogue history and system prompts
/// </summary>
public class LlamaMemory : MonoBehaviour
{
    private StringBuilder memory = new StringBuilder();
    private const int MAX_LENGTH = 8000;
    private Dictionary<string, string> npcSystemPrompts = new Dictionary<string, string>();
    private Dictionary<string, List<string>> npcFacts = new Dictionary<string, List<string>>();
    
    public static LlamaMemory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterNPCPrompt(string npcName, string systemPrompt)
    {
        npcSystemPrompts[npcName] = systemPrompt;
    }

    public void AddDialogueTurn(string speaker, string message)
    {
        memory.AppendLine($"{speaker}: {message}");
        
        if (memory.Length > MAX_LENGTH)
            memory.Remove(0, memory.Length / 2);
    }

    public void AddFact(string npcName, string fact)
    {
        if (!npcFacts.ContainsKey(npcName))
            npcFacts[npcName] = new List<string>();
        
        if (!npcFacts[npcName].Contains(fact))
            npcFacts[npcName].Add(fact);
    }

    public string BuildPromptForGeneration(string npcName, int lastNTurns = 4)
    {
        return GetShortTermContext(lastNTurns);
    }

    public string GetShortTermContext(int lastNTurns = 6)
    {
        string[] lines = memory.ToString().Split('\n');
        int start = Mathf.Max(0, lines.Length - lastNTurns);
        var sb = new StringBuilder();

        for (int i = start; i < lines.Length; i++)
            if (!string.IsNullOrEmpty(lines[i]))
                sb.AppendLine(lines[i]);

        return sb.ToString().Trim();
    }

    public string GetFullConversation() => memory.ToString();
    
    public int GetFactCount(string npcName) => npcFacts.ContainsKey(npcName) ? npcFacts[npcName].Count : 0;

    public void ClearAll()
    {
        memory.Clear();
        npcFacts.Clear();
    }
}

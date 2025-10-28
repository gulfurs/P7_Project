using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Unified memory for LLaMA - shared by all NPCs, holds conversation and prompts
/// </summary>
public class LlamaMemory : MonoBehaviour
{
    [Header("Conversation Memory")]
    [TextArea(5, 10)]
    public string conversationPreview;

    private StringBuilder memory = new StringBuilder();
    private const int maxMemoryLength = 8000;

    // Per-NPC system prompts (replaces NPCProfile.GetFullSystemPrompt)
    private Dictionary<string, string> npcSystemPrompts = new Dictionary<string, string>();
    
    // Per-NPC facts
    private Dictionary<string, List<string>> npcFacts = new Dictionary<string, List<string>>();

    private static LlamaMemory instance;

    public static LlamaMemory Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LlamaMemory>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("LlamaMemory");
                    instance = obj.AddComponent<LlamaMemory>();
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
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Register NPC's system prompt (from NPCProfile)
    /// </summary>
    public void RegisterNPCPrompt(string npcName, string systemPrompt)
    {
        npcSystemPrompts[npcName] = systemPrompt;
        Debug.Log($"[LlamaMemory] Registered prompt for {npcName}");
    }

    /// <summary>
    /// Add a dialogue turn (everyone hears it)
    /// </summary>
    public void AddDialogueTurn(string speaker, string message)
    {
        memory.AppendLine($"{speaker}: {message}");
        UpdatePreview();

        if (memory.Length > maxMemoryLength)
        {
            int cutIndex = memory.Length / 2;
            memory.Remove(0, cutIndex);
        }
    }

    /// <summary>
    /// Add a fact to specific NPC
    /// </summary>
    public void AddFact(string npcName, string fact)
    {
        if (!npcFacts.ContainsKey(npcName))
            npcFacts[npcName] = new List<string>();

        if (!npcFacts[npcName].Contains(fact))
            npcFacts[npcName].Add(fact);
    }

    /// <summary>
    /// Build full prompt for generation - includes ONLY recent conversation
    /// System prompts are sent once during registration and assumed to be "baked in"
    /// </summary>
    public string BuildPromptForGeneration(string npcName, int lastNTurns = 4)
    {
        // Just return recent conversation history
        return GetShortTermContext(lastNTurns);
    }

    /// <summary>
    /// Get recent dialogue turns
    /// </summary>
    public string GetShortTermContext(int lastNTurns = 6)
    {
        string[] lines = memory.ToString().Split('\n');
        int start = Mathf.Max(0, lines.Length - lastNTurns);
        var sb = new StringBuilder();

        for (int i = start; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
                sb.AppendLine(lines[i]);
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Get full conversation
    /// </summary>
    public string GetFullConversation()
    {
        return memory.ToString();
    }

    public int GetFactCount(string npcName)
    {
        return npcFacts.ContainsKey(npcName) ? npcFacts[npcName].Count : 0;
    }

    private void UpdatePreview()
    {
        conversationPreview = memory.ToString();
    }

    public void ClearAll()
    {
        memory.Clear();
        npcFacts.Clear();
        UpdatePreview();
        Debug.Log("[LlamaMemory] Cleared all history and facts");
    }
}

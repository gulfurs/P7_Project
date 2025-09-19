using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [Header("NPC Instances")]
    public List<NPCChatInstance> npcInstances = new List<NPCChatInstance>();
    
    public static NPCManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Register all NPC instances
        var foundNPCs = FindObjectsOfType<NPCChatInstance>();
        foreach (var npc in foundNPCs)
        {
            if (!npcInstances.Contains(npc))
            {
                npcInstances.Add(npc);
            }
        }
    }
    
    /// <summary>
    /// Broadcast a message from one NPC to all other NPCs
    /// </summary>
    public void BroadcastMessage(NPCChatInstance sender, string message)
    {
        foreach (var npc in npcInstances)
        {
            if (npc != sender && npc != null)
            {
                npc.ReceiveExternalMessage(sender.npcProfile.npcName, message);
            }
        }
    }
    
    /// <summary>
    /// Send a message from one specific NPC to another specific NPC
    /// </summary>
    public void SendMessageToNPC(NPCChatInstance sender, string targetNPCName, string message)
    {
        foreach (var npc in npcInstances)
        {
            if (npc != sender && npc.npcProfile.npcName == targetNPCName)
            {
                npc.ReceiveExternalMessage(sender.npcProfile.npcName, message);
                break;
            }
        }
    }
    
    /// <summary>
    /// Get all NPC names for reference
    /// </summary>
    public List<string> GetAllNPCNames()
    {
        List<string> names = new List<string>();
        foreach (var npc in npcInstances)
        {
            if (npc != null && !string.IsNullOrEmpty(npc.npcProfile.npcName))
            {
                names.Add(npc.npcProfile.npcName);
            }
        }
        return names;
    }
}
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [Header("NPC Instances")]
    public List<NPCChatInstance> npcInstances = new List<NPCChatInstance>();
    
    [Header("Global Settings")]
    public bool globalTTSEnabled = true; // Master TTS switch for debugging

    [Header("Shared Gaze Reference")]
    public Transform userTransform;
    
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
        // Find and register any NPC instances not already registered
        var foundNPCs = FindObjectsOfType<NPCChatInstance>();
        foreach (var npc in foundNPCs)
        {
            if (!npcInstances.Contains(npc))
                npcInstances.Add(npc);
        }
    }

    public Transform GetLookTargetForSpeaker(string speakerName)
    {
        if (speakerName.Equals("User", System.StringComparison.OrdinalIgnoreCase))
            return userTransform;

        foreach (var npc in npcInstances)
        {
            if (npc == null || npc.npcProfile == null)
                continue;

            if (npc.npcProfile.npcName == speakerName)
            {
                if (npc.npcProfile.npcGameObject != null)
                    return npc.npcProfile.npcGameObject.transform;

                return npc.transform;
            }
        }

        return null;
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

    public void NotifySpeakerChanged(string speakerName)
    {
        foreach (var npc in npcInstances)
        {
            npc?.OnSpeakerChanged(speakerName);
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
        var names = new List<string>();
        foreach (var npc in npcInstances)
        {
            if (npc != null && npc.npcProfile != null && !string.IsNullOrEmpty(npc.npcProfile.npcName))
                names.Add(npc.npcProfile.npcName);
        }
        return names;
    }
    
    /// <summary>
    /// Get count of active NPCs (for forcing response logic)
    /// </summary>
    public int GetActiveNPCCount()
    {
        int count = 0;
        foreach (var npc in npcInstances)
        {
            if (npc != null && npc.npcProfile != null && !string.IsNullOrEmpty(npc.npcProfile.npcName))
                count++;
        }
        return count;
    }
    

    
    /// <summary>
    /// Quick TTS control for debugging
    /// </summary>
    [ContextMenu("Toggle Global TTS")]
    public void ToggleGlobalTTS()
    {
        globalTTSEnabled = !globalTTSEnabled;
        Debug.Log($"Global TTS is now: {(globalTTSEnabled ? "ENABLED" : "DISABLED")}");
    }
}


using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [Header("NPC Instances")]
    public List<NPCChatInstance> npcInstances = new List<NPCChatInstance>();
    
    [Header("Global Settings")]
    public bool globalTTSEnabled = true; // Master TTS switch for debugging
    
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
        // If asking for "User" target, try to find by tag
        if (speakerName.Equals("User", System.StringComparison.OrdinalIgnoreCase))
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) return player.transform;
            
            // Fallback: look for camera
            if (Camera.main != null) return Camera.main.transform;
            
            return null; // No user found
        }

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

    public void NotifySpeakerChanged(string speakerName)
    {
        foreach (var npc in npcInstances)
        {
            npc?.OnSpeakerChanged(speakerName);
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
    /// Quick TTS control for debugging
    /// </summary>
    [ContextMenu("Toggle Global TTS")]
    public void ToggleGlobalTTS()
    {
        globalTTSEnabled = !globalTTSEnabled;
        Debug.Log($"Global TTS is now: {(globalTTSEnabled ? "ENABLED" : "DISABLED")}");
    }
}


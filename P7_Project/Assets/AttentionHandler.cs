using UnityEngine;
using UnityEngine.Animations.Rigging;
using uLipSync;

public class AttentionHandler : MonoBehaviour
{
    public MultiAimConstraint aimConstraint;
    public uLipSync.uLipSync npcLipSync;

    public float speakingThreshold = 0.01f;

    public float lookAtSpeed = 6f;       
    public float lookAwaySpeed = 1.5f;    
    public float silenceHoldTime = 0.25f;

    WeightedTransformArray sources;

    float playerWeight = 1f;
    float npcWeight = 0f;

    float silenceTimer = 0f;

    void Start()
    {
        sources = aimConstraint.data.sourceObjects;
    }

    void Update()
    {
        bool speaking = npcLipSync.result.rawVolume > speakingThreshold;

        if (speaking)
        {
            silenceTimer = 0f; 
        }
        else
        {
            silenceTimer += Time.deltaTime;
        }

        bool shouldLookAtNPC = speaking || silenceTimer < silenceHoldTime;

        float targetPlayer = shouldLookAtNPC ? 0f : 1f;
        float targetNPC = shouldLookAtNPC ? 1f : 0f;

        float speed = shouldLookAtNPC ? lookAtSpeed : lookAwaySpeed;

        playerWeight = Mathf.Lerp(playerWeight, targetPlayer, Time.deltaTime * speed);
        npcWeight = Mathf.Lerp(npcWeight, targetNPC, Time.deltaTime * speed);

        sources.SetWeight(0, playerWeight);
        sources.SetWeight(1, npcWeight);

        aimConstraint.data.sourceObjects = sources;
    }
}

using UnityEngine;

public class CharacterLayerSetup : MonoBehaviour
{
    [Header("Animator Source")]
    [Tooltip("If left empty, the script will search child objects for an Animator.")]
    public Animator animator;

    [Header("Layer Weights")]
    [Range(0f, 1f)] public float baseLayerWeight = 1f;
    [Range(0f, 1f)] public float headLayerWeight = 1f;
    [Range(0f, 1f)] public float legsLayerWeight = 1f;
    [Range(0f, 1f)] public float handsLayerWeight = 1f;

    void Awake()
    {
        // Auto-find animator if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("CharacterLayerSetup: No Animator found in children.");
                return;
            }
        }

        ApplyLayerWeights();
    }

    void ApplyLayerWeights()
    {
        SetLayerWeight("Base Layer", baseLayerWeight);
        SetLayerWeight("Head", headLayerWeight);
        SetLayerWeight("Legs", legsLayerWeight);
        SetLayerWeight("Hands", handsLayerWeight);
    }

    void SetLayerWeight(string layerName, float weight)
    {
        int index = animator.GetLayerIndex(layerName);
        if (index == -1)
        {
            Debug.LogWarning($"CharacterLayerSetup: Layer '{layerName}' not found on Animator '{animator.name}'.");
            return;
        }

        animator.SetLayerWeight(index, weight);
    }
}

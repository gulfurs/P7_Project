using UnityEngine;
using System;
using System.Collections.Generic;

public class ObjectInteract : InteractHandler
{
    [Header("Blend Shape Settings")]
    public SkinnedMeshRenderer skinnedMesh; 
    public int blendShapeIndex = 0;         
    public float lerpSpeed = 5f;            

    private bool isOpen = false;            
    private float targetWeight = 0f;        

    private void Update()
    {
        if (skinnedMesh)
        {
            float currentWeight = skinnedMesh.GetBlendShapeWeight(blendShapeIndex);
            float newWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * lerpSpeed);
            skinnedMesh.SetBlendShapeWeight(blendShapeIndex, newWeight);
        }
    }

    public override void InteractLogic()
    {
        base.InteractLogic();
        interactable = true;
        isOpen = !isOpen;
        targetWeight = isOpen ? 100f : 0f;
    }
}


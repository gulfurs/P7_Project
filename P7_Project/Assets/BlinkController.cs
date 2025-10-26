using UnityEngine;
using System.Collections;

public class BlinkController : MonoBehaviour
{
    public SkinnedMeshRenderer faceRenderer;
    private int blinkIndex;

    [Header("Blink Settings")]
    public float blinkSpeed = 12f; // How fast eyelids move
    public Vector2 blinkInterval = new Vector2(3f, 6f); // Random time between blinks
    public float closedHoldTime = 0.07f; // How long eyes stay closed

    private float nextBlinkTime;
    private bool isBlinking;
    private float weight;

    void Start()
    {
        blinkIndex = faceRenderer.sharedMesh.GetBlendShapeIndex("Eyes_Closed");
        ScheduleNextBlink();
    }

    void Update()
    {
        if (!isBlinking && Time.time >= nextBlinkTime)
            StartCoroutine(Blink());
    }

    IEnumerator Blink()
    {
        isBlinking = true;

        // Close eyes
        while (weight < 100f)
        {
            weight += Time.deltaTime * blinkSpeed * 100f;
            faceRenderer.SetBlendShapeWeight(blinkIndex, weight);
            yield return null;
        }

        // Pause closed
        yield return new WaitForSeconds(closedHoldTime);

        // Open eyes
        while (weight > 0f)
        {
            weight -= Time.deltaTime * blinkSpeed * 100f;
            faceRenderer.SetBlendShapeWeight(blinkIndex, weight);
            yield return null;
        }

        weight = 0f;
        faceRenderer.SetBlendShapeWeight(blinkIndex, weight);

        isBlinking = false;
        ScheduleNextBlink();
    }

    void ScheduleNextBlink()
    {
        nextBlinkTime = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
    }
}

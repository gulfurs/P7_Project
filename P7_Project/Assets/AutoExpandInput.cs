using UnityEngine;
using TMPro;

public class AutoExpandInput : MonoBehaviour
{
    public TMP_InputField input;
    public RectTransform textArea;
    public float padding = 10f;

    private void Update()
    {
        float preferredHeight = input.textComponent.preferredHeight;
        Vector2 size = textArea.sizeDelta;
        size.y = preferredHeight + padding;
        textArea.sizeDelta = size;
    }
}
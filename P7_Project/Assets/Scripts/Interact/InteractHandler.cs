using UnityEngine;

public class InteractHandler : MonoBehaviour
{
    public bool interactable = true;
    [TextArea] public string tooltipText = "Press E to interact";

    public virtual void InteractLogic()
    {
        Debug.Log("Interacted with: " + gameObject.name);
        interactable = false;
    }
}

using UnityEngine;
using TMPro;

public class InteractManager : MonoBehaviour
{
    private InputHandler _input;
    public TextMeshProUGUI tooltip;

    private InteractHandler currentHoverTarget;
    [SerializeField] private bool canInteract = false;

    private void Start()
    {
        _input = GetComponent<InputHandler>();
        HideToolTip();

        if (_input != null)
            _input.OnInteractPerformed += HandleInteract; // subscribe to event
    }

    private void OnDestroy()
    {
        if (_input != null)
            _input.OnInteractPerformed -= HandleInteract; // unsubscribe
    }

    private void Update()
    {
        if (!canInteract)
        {
            HideToolTip();
            currentHoverTarget = null;
            return;
        }

        UpdateHoverTarget();
    }

    private void UpdateHoverTarget()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            InteractHandler interactObject = hit.collider.GetComponent<InteractHandler>();
            if (interactObject != null && interactObject.interactable)
            {
                if (interactObject != currentHoverTarget)
                {
                    currentHoverTarget = interactObject;
                    ShowToolTip(interactObject.tooltipText);
                }
                return;
            }
        }

        // Clear hover if nothing hit
        if (currentHoverTarget != null)
        {
            currentHoverTarget = null;
            HideToolTip();
        }
    }

    private void HandleInteract()
    {
        if (!canInteract || currentHoverTarget == null)
            return;

        currentHoverTarget.InteractLogic(); 
    }

    public void ShowToolTip(string message)
    {
        tooltip.text = message;
        tooltip.gameObject.SetActive(true);
    }

    public void HideToolTip()
    {
        tooltip.gameObject.SetActive(false);
    }

    public void UnlockInteract(bool unlock)
    {
        canInteract = unlock;
    }
}

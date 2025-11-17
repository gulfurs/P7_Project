using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public class TeleportInteract : MonoBehaviour
{
    [Header("Teleport Settings")]
    public GameObject playerToTeleport; // Assign the player GameObject in the Inspector
    public Vector3 teleportPosition = Vector3.zero;

    private void Start()
    {
        // Ensure the box collider is set as a trigger
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the player we want to teleport
        if (other.gameObject == playerToTeleport)
        {
            StartCoroutine(TeleportPlayer());
        }
    }

    private IEnumerator TeleportPlayer()
    {
        // Wait one frame to ensure we're outside of physics calculation
        yield return new WaitForFixedUpdate();

        CharacterController cc = playerToTeleport.GetComponent<CharacterController>();
        if (cc != null)
        {
            // Disable the character controller temporarily
            cc.enabled = false;
            
            // Set position directly
            playerToTeleport.transform.position = teleportPosition;
            
            // Re-enable the character controller
            cc.enabled = true;
        }
        else
        {
            // Fall back to direct position change if no CharacterController
            playerToTeleport.transform.position = teleportPosition;
        }

        Debug.Log($"Teleported '{playerToTeleport.name}' to: {teleportPosition}");
    }

    private void OnDrawGizmos()
    {
        // Draw a sphere at the teleport destination to make it visible in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(teleportPosition, 1f);
    }
}
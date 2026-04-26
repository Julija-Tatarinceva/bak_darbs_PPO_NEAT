using UnityEngine;

namespace CarExperiment
{
    /// <summary>
    /// Teleports any object that collides with this goal piece back to the initial road piece's position and rotation.
    /// </summary>
    public class GoalTeleport : MonoBehaviour
    {
        public Transform teleportTarget; // The transform to teleport to (usually the first road piece)

        private void OnTriggerEnter(Collider other)
        {
            // Only teleport objects with Rigidbody (e.g., cars)
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                // Teleport the object to the target position and rotation
                rb.position = teleportTarget.position;
                rb.rotation = teleportTarget.rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                // Fallback: move the object directly
                other.transform.position = teleportTarget.position;
                other.transform.rotation = teleportTarget.rotation;
            }
        }
    }
}
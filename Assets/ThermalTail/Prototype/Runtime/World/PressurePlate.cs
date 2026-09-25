using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>A floor hazard that immediately captures the player through the normal checkpoint flow.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PressurePlate : MonoBehaviour
    {
        [Tooltip("Message shown when the player steps on this plate.")]
        public string CaptureMessage = "Pressure plate triggered";

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnTriggerEnter(Collider other) { Capture(other); }
        void OnTriggerStay(Collider other) { Capture(other); }

        void Capture(Collider other)
        {
            if (!isActiveAndEnabled) return;
            var player = other.GetComponentInParent<SurfaceMotor>();
            if (player == null || player.Session == null) return;
            // Temperature, shelters and respawn grace never disarm a physical floor trap.
            player.Session.TryCapture(CaptureMessage, ignoreRespawnGrace: true);
        }
    }
}

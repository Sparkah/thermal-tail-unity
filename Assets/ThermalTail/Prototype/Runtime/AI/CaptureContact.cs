using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CaptureContact : MonoBehaviour
    {
        public LayerMask ObstructionMask = ~0;
        void Reset() { GetComponent<SphereCollider>().isTrigger = true; }
        void OnTriggerStay(Collider other)
        {
            var player = other.GetComponentInParent<SurfaceMotor>();
            if (player == null) return;
            // Safe cover and thermal matching never excuse actual reachable capture contact.
            var warden = GetComponentInParent<WardenBrain>();
            if (warden != null && warden.TryGetComponent<WardenTakedown>(out var takedown) && takedown.IsTakenDown) return;
            if (warden != null && VisionSensor.Unobstructed(transform.position, player.transform.position, ObstructionMask,
                warden.transform, player.transform))
                player.Session.TryCapture("Caught by a warden");
        }
    }
}

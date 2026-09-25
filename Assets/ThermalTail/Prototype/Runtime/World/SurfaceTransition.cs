using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SurfaceTransition : MonoBehaviour
    {
        [Tooltip("Markers are player BODY CENTRES, with up pointing away from the surface.")]
        public Transform Entry, Exit, Control;
        [Min(0.1f)] public float Duration = 0.7f;
        [System.NonSerialized] public Vector3 BeginPosition;
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnTriggerStay(Collider other)
        {
            var motor = other.GetComponentInParent<SurfaceMotor>();
            if (motor == null || Entry == null || Exit == null || motor.MoveInput.sqrMagnitude < 0.1f) return;
            if (Vector3.Dot(motor.SurfaceNormal, Entry.up) < 0.8f) return;
            if (Vector3.Dot(motor.Velocity, Entry.forward) <= 0.05f) return;
            motor.BeginTransition(this);
        }
        void OnDrawGizmos()
        {
            if (Entry == null || Exit == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Entry.position, Control != null ? Control.position : Exit.position);
            if (Control != null) Gizmos.DrawLine(Control.position, Exit.position);
            Gizmos.DrawRay(Exit.position, Exit.up * 0.5f);
        }
    }
}

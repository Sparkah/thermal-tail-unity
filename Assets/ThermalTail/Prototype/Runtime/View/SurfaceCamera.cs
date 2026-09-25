using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class SurfaceCamera : MonoBehaviour
    {
        public SurfaceMotor Player;
        public LayerMask ObstructionMask = ~0;
        [Min(0.2f), Tooltip("Desired distance from the player. Walls can push the camera closer.")]
        public float Distance = 5f;
        public float Pitch = 35f;
        [Range(-85f, 85f), Tooltip("Negative pitch allows looking upward relative to the attached surface.")]
        public float MinimumPitch = -80f;
        [Range(-85f, 85f)] public float MaximumPitch = 80f;
        public float Smoothing = 8f;
        Vector3 up = Vector3.up;
        bool snap = true;
        void Start() { if (Player != null) Player.Session.ResetOccurred += Snap; }
        void OnDestroy() { if (Player != null && Player.Session != null) Player.Session.ResetOccurred -= Snap; }
        void Snap() { snap = true; }
        public void AdjustPitch(float degrees)
        {
            Pitch = Mathf.Clamp(Pitch + degrees, Mathf.Min(MinimumPitch, MaximumPitch), Mathf.Max(MinimumPitch, MaximumPitch));
        }
        void LateUpdate()
        {
            if (Player == null) return;
            float ease = snap ? 1f : 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            up = Vector3.Slerp(up, Player.SurfaceNormal, ease).normalized;
            Vector3 forward = Player.ViewForward;
            Vector3 offset = (-forward * Mathf.Cos(Pitch * Mathf.Deg2Rad) + Player.SurfaceNormal * Mathf.Sin(Pitch * Mathf.Deg2Rad)) * Distance;
            Vector3 target = Player.transform.position;
            float length = offset.magnitude;
            if (Physics.SphereCast(target, 0.15f, offset.normalized, out var hit, length, ObstructionMask, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(0.2f, hit.distance - 0.1f);
            Vector3 desired = target + offset.normalized * length;
            // Resolve obstruction every frame, including the interpolation path.
            Vector3 next = Vector3.Lerp(transform.position, desired, ease);
            Vector3 toNext = next - target;
            if (Physics.SphereCast(target, 0.15f, toNext.normalized, out hit, toNext.magnitude, ObstructionMask, QueryTriggerInteraction.Ignore))
                next = target + toNext.normalized * Mathf.Max(0.2f, hit.distance - 0.1f);
            transform.position = next;
            transform.rotation = Quaternion.LookRotation(target - next, up);
            snap = false;
        }
    }
}

using UnityEngine;

namespace ThermalTail
{
    /// <summary>Sits behind and above the target and smooths toward it. Nothing else.</summary>
    public class TTChaseCam : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 9f;
        public float Height = 6.5f;
        public float LookAhead = 3f;
        [Tooltip("Higher follows tighter.")]
        public float Damping = 6f;
        [Tooltip("Swing round behind the target as it turns. Off keeps a fixed heading.")]
        public bool FollowHeading;

        Vector3 _forward = Vector3.forward;

        void LateUpdate()
        {
            if (Target == null) return;
            float k = 1f - Mathf.Exp(-Damping * Time.deltaTime);

            if (FollowHeading)
            {
                Vector3 want = Target.forward; want.y = 0f;
                if (want.sqrMagnitude > 0.001f) _forward = Vector3.Slerp(_forward, want.normalized, k);
            }

            Vector3 eye = Target.position - _forward * Distance + Vector3.up * Height;
            Vector3 aim = Target.position + _forward * LookAhead;

            transform.position = Vector3.Lerp(transform.position, eye, k);
            transform.rotation = Quaternion.LookRotation((aim - transform.position).normalized, Vector3.up);
        }
    }
}

using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class VisionSensor : MonoBehaviour
    {
        public PrototypeSession Session;
        public Transform Eye;
        [Min(0.1f)] public float Range = 9f;
        [Range(1, 179)] public float FieldOfView = 80f;
        [Min(0f), Tooltip("Additional close-range vision within this distance from the eye. Zero disables it. Walls and temperature rules still apply.")]
        public float CloseRange = 0f;
        [Range(1f, 360f), Tooltip("Full angle of the close-range zone in degrees; 180 covers the forward hemisphere.")]
        public float CloseFieldOfView = 180f;
        public LayerMask ObstructionMask = ~0;
        public bool Active = true;
        public float Suspicion { get; private set; }
        public bool SeesPlayer { get; private set; }
        public bool TrackingMismatch { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public Transform Origin => Eye != null ? Eye : transform;
        void Awake() { if (Session == null) Session = FindFirstObjectByType<PrototypeSession>(); }
        void OnEnable() { if (Session != null) Session.ResetOccurred += Clear; }
        void OnDisable() { if (Session != null) Session.ResetOccurred -= Clear; }
        void Update()
        {
            if (Session == null || Session.Player == null || Session.Thermal == null || Session.IsComplete || Session.Resetting) return;
            SeesPlayer = Active && Session.GraceRemaining <= 0f && !Session.Thermal.IsSafe && CanSee(Session.Player);
            TrackingMismatch = SeesPlayer && !Session.Thermal.Matched;
            if (TrackingMismatch) LastSeenPosition = Session.Player.transform.position;
            if (TryGetComponent<SecurityCamera>(out var camera))
                Suspicion = StealthRules.IntegrateSuspicion(Suspicion, SeesPlayer, Session.Thermal.Mismatch,
                    Time.deltaTime, Session.Settings.MatchTolerance, 1f / Mathf.Max(.1f, camera.BaseDetectionTime),
                    Mathf.Max(0f, camera.TemperatureSensitivity), 1f / Mathf.Max(.1f, camera.SuspicionRecoveryTime));
            else
                Suspicion = StealthRules.IntegrateSuspicion(Suspicion, SeesPlayer, Session.Thermal.Mismatch, Time.deltaTime, Session.Settings);
        }
        public bool CanSee(SurfaceMotor target, float awarenessRadius = 0f)
        {
            Vector3 center = target.transform.position;
            // A body centre and two shoulder samples make small cover edges less binary.
            return VisiblePoint(center, target.transform, awarenessRadius) ||
                VisiblePoint(center + target.transform.right * target.Radius * 0.6f, target.transform, awarenessRadius) ||
                VisiblePoint(center - target.transform.right * target.Radius * 0.6f, target.transform, awarenessRadius);
        }
        bool VisiblePoint(Vector3 point, Transform target, float awarenessRadius)
        {
            Vector3 offset = point - Origin.position;
            if (offset.sqrMagnitude < 0.0001f) return false;
            bool close = CloseRange > 0f && offset.sqrMagnitude <= CloseRange * CloseRange &&
                Vector3.Angle(Origin.forward, offset) <= CloseFieldOfView * 0.5f;
            if (!close)
            {
                if (offset.sqrMagnitude > Range * Range) return false;
                if (offset.sqrMagnitude > awarenessRadius * awarenessRadius &&
                    Vector3.Angle(Origin.forward, offset) > FieldOfView * 0.5f) return false;
            }
            return Unobstructed(Origin.position, point, ObstructionMask, transform, target);
        }
        public static bool Unobstructed(Vector3 from, Vector3 to, LayerMask mask, Transform self, Transform target)
        {
            Vector3 offset = to - from;
            foreach (var hit in Physics.RaycastAll(from, offset.normalized, offset.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                if (self != null && hit.transform.IsChildOf(self)) continue;
                if (target != null && hit.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
        public void RaiseAlarm(Vector3 position) { Suspicion = Mathf.Max(Suspicion, 0.8f); LastSeenPosition = position; }
        public void Clear() { Suspicion = 0f; SeesPlayer = TrackingMismatch = false; }
    }
}

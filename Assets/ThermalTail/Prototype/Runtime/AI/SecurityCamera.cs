using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(VisionSensor))]
    [DefaultExecutionOrder(-10)]
    public sealed class SecurityCamera : MonoBehaviour
    {
        public Transform Pivot;
        [Range(0, 90)] public float SweepDegrees = 35f;
        [Min(0.1f), Tooltip("Seconds per sweep/on-off cycle. Does not control detection time.")]
        public float Period = 8f;
        [Range(0, 1), Tooltip("Fraction of each cycle spent active. Set to 1 to keep the camera on continuously.")]
        public float ActiveFraction = 0.75f;
        public float PhaseOffset;
        [Header("Detection timing")]
        [Min(0.1f), Tooltip("Seconds from zero suspicion to capture during continuous visible temperature mismatch, before the temperature bonus. Higher is slower.")]
        public float BaseDetectionTime = 3.333333f;
        [Min(0f), Tooltip("Extra suspicion per second for each degree beyond Match Tolerance. Zero gives a fixed detection time regardless of mismatch size.")]
        public float TemperatureSensitivity = 0.045f;
        [Min(0.1f), Tooltip("Seconds to recover from full suspicion to zero when the player is hidden, thermally matched, safe, or the camera is inactive. Higher keeps suspicion longer.")]
        public float SuspicionRecoveryTime = 2.222222f;
        VisionSensor sensor;
        Quaternion baseRotation;
        void Awake()
        {
            sensor = GetComponent<VisionSensor>();
            if (Pivot == null) Pivot = transform;
            baseRotation = Pivot.localRotation;
        }
        void Update()
        {
            var session = sensor.Session;
            if (session == null || session.IsComplete || session.Resetting) return;
            float phase = Mathf.Repeat(session.AttemptTime / Period + PhaseOffset, 1f);
            sensor.Active = phase < ActiveFraction;
            Pivot.localRotation = baseRotation * Quaternion.Euler(0f, Mathf.Sin(phase * Mathf.PI * 2f) * SweepDegrees, 0f);
        }
        void LateUpdate()
        {
            if (sensor.Session != null && sensor.Suspicion >= 1f) sensor.Session.TryCapture("Camera alarm");
        }
    }
}

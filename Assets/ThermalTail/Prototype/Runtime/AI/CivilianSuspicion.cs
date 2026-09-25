using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class CivilianSuspicion : MonoBehaviour
    {
        public PrototypeSession Session;
        public CrowdZone Zone;
        public LayerMask ObstructionMask = ~0;
        [Min(.1f), Tooltip("Seconds of wrong temperature inside this civilian's core ring before they call nearby wardens.")]
        public float DetectionTime = .75f;
        [Min(.1f), Tooltip("Seconds to clear a full meter after leaving the core ring, matching temperature, or breaking visibility.")]
        public float RecoveryTime = .5f;
        public float Suspicion { get; private set; }
        public Vector3 EyePosition => transform.position + transform.up * .9f;

        void Awake()
        {
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
            if (Zone == null) Zone = GetComponentInChildren<CrowdZone>();
        }
        void OnEnable() { if (Session != null) Session.ResetOccurred += Clear; }
        void OnDisable()
        {
            if (Session != null) Session.ResetOccurred -= Clear;
            Clear();
        }
        void Update()
        {
            if (Session == null || Session.Settings == null || Session.Player == null || Session.Thermal == null ||
                Zone == null || Session.Resetting || Session.IsComplete) return;
            var player = Session.Player;
            bool suspicious = Zone.isActiveAndEnabled && Session.GraceRemaining <= 0 && !Session.Thermal.IsSafe &&
                Vector3.Distance(player.transform.position, Zone.transform.position) <= Zone.WorldRadius &&
                Mathf.Abs(Session.Thermal.Temperature - Zone.Target(Session)) > Session.Settings.MatchTolerance &&
                VisionSensor.Unobstructed(EyePosition, player.transform.position, ObstructionMask, transform, player.transform);
            Suspicion = Mathf.Clamp01(Suspicion + Time.deltaTime * (suspicious
                ? 1f / Mathf.Max(.1f, DetectionTime) : -1f / Mathf.Max(.1f, RecoveryTime)));
            // The session throttles repeated calls while a player remains exposed.
            if (suspicious && Suspicion >= 1f) Session.ReportCivilianThermalAlert(player.transform.position);
        }
        void Clear() { Suspicion = 0; }
    }
}

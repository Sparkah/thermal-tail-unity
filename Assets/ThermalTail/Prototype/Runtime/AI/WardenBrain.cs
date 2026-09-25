using UnityEngine;

namespace ThermalTail.Prototype
{
    public enum WardenMode { Patrol, Chase, Search, Suspicious }
    [RequireComponent(typeof(NpcMotor), typeof(VisionSensor))]
    [DefaultExecutionOrder(20)]
    public sealed class WardenBrain : MonoBehaviour
    {
        [Min(0f), Tooltip("Degrees per second to turn toward a visible thermal mismatch before committing to pursuit.")]
        public float SuspiciousTurnSpeed = 150f;
        [Min(1f), Tooltip("Multiplies NpcMotor's Chase Speed during pursuit, including civilian alerts.")]
        public float RunSpeedMultiplier = 1.6f;
        [Min(0f)] public float RunAcceleration = 16f;
        [Min(0f)] public float RunTurnSpeed = 540f;
        [Min(0f), Tooltip("360-degree sight during pursuit only; walls still block it and sensor range is the maximum.")]
        public float ChaseAwarenessRadius = 4f;
        [Min(0f)] public float LostSightGrace = 1.5f;
        [Min(0f), Tooltip("How far ahead in seconds to investigate using the last observed velocity, never hidden movement.")]
        public float PredictionSeconds = 0.5f;
        public WardenMode Mode { get; private set; }
        public Vector3 PursuitDestination => destination;
        NpcMotor motor;
        VisionSensor sensor;
        PrototypeSession session;
        Vector3 destination;
        float alertUntil;
        float lastSeenTime;
        Vector3 lastSeenPosition, lastSeenVelocity;
        void Awake()
        {
            motor = GetComponent<NpcMotor>(); sensor = GetComponent<VisionSensor>();
            session = motor.Session != null ? motor.Session : FindFirstObjectByType<PrototypeSession>();
        }
        void OnEnable()
        {
            if (session == null) return;
            session.CivilianAlert += HearCivilian; session.ResetOccurred += ResetBrain;
        }
        void OnDisable()
        {
            if (motor != null && motor.Agent != null) motor.ResumePatrol();
            if (session == null) return;
            session.CivilianAlert -= HearCivilian; session.ResetOccurred -= ResetBrain;
        }
        void Update()
        {
            if (session == null || session.IsComplete || session.Resetting) return;
            bool canObserve = sensor.isActiveAndEnabled && sensor.Active && session.GraceRemaining <= 0f;
            bool noticed = canObserve && sensor.TrackingMismatch;
            bool tracking = canObserve && (Mode == WardenMode.Chase
                ? sensor.CanSee(session.Player, Mathf.Max(0f, ChaseAwarenessRadius))
                : noticed && sensor.Suspicion >= session.Settings.ChaseThreshold);
            if (tracking)
            {
                destination = lastSeenPosition = session.Player.transform.position;
                lastSeenVelocity = session.Player.Velocity;
                lastSeenTime = Time.time;
                Mode = WardenMode.Chase;
            }
            else if (noticed && Mode != WardenMode.Chase)
            {
                destination = sensor.LastSeenPosition;
                Mode = WardenMode.Suspicious;
            }
            if (Mode == WardenMode.Suspicious)
            {
                if (sensor.Suspicion <= 0f || !sensor.isActiveAndEnabled) Mode = WardenMode.Patrol;
                else
                {
                    // Only visible mismatches update this point; cover leaves the last observation intact.
                    motor.HoldAndFace(destination, SuspiciousTurnSpeed);
                    return;
                }
            }
            if (Mode == WardenMode.Chase)
            {
                if (!tracking)
                {
                    float lostFor = Time.time - lastSeenTime;
                    destination = lastSeenPosition + lastSeenVelocity * Mathf.Min(lostFor, Mathf.Max(0f, PredictionSeconds));
                    if (lostFor >= LostSightGrace)
                    {
                        Mode = WardenMode.Search;
                        alertUntil = Time.time + session.Settings.InvestigationSeconds;
                    }
                }
            }
            if (Mode == WardenMode.Search && Time.time >= alertUntil) Mode = WardenMode.Patrol;
            if (Mode == WardenMode.Patrol) { motor.ResumePatrol(); return; }
            if (Mode == WardenMode.Chase)
                motor.Pursue(destination, Mathf.Max(1f, RunSpeedMultiplier), RunAcceleration, RunTurnSpeed);
            else
            {
                motor.Pursue(destination);
                if (Vector3.Distance(transform.position, destination) < 1f) transform.Rotate(0f, 50f * Time.deltaTime, 0f);
            }
        }
        void HearCivilian(Vector3 position)
        {
            if (Vector3.Distance(transform.position, position) > session.Settings.CivilianAlertRadius) return;
            // Repeated contact reports must not replace a currently visible chase target.
            if (Mode == WardenMode.Chase && Time.time - lastSeenTime < 0.1f) return;
            destination = lastSeenPosition = position; lastSeenVelocity = Vector3.zero; lastSeenTime = Time.time;
            Mode = WardenMode.Chase; sensor.RaiseAlarm(position);
        }
        public void ResetBrain()
        {
            Mode = WardenMode.Patrol; alertUntil = lastSeenTime = 0f;
            destination = lastSeenPosition = lastSeenVelocity = Vector3.zero;
        }
    }
}

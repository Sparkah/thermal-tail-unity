using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(SurfaceMotor))]
    public sealed class PlayerThermal : MonoBehaviour
    {
        public PrototypeSession Session;
        public LayerMask VolumeMask = ~0;
        public float Temperature { get; private set; } = 22f;
        public float Ambient { get; private set; }
        public float TargetTemperature { get; private set; }
        public float Mismatch { get; private set; }
        public bool InCrowd { get; private set; }
        public bool InTransitionBand { get; private set; }
        public bool IsSafe { get; private set; }
        public bool Matched => Mismatch <= Session.Settings.MatchTolerance;
        public string Context => IsSafe ? "SAFE SHELTER" : InCrowd ? "CROWD" : InTransitionBand ? "CROWD EDGE — EITHER TARGET" : "AMBIENT";
        SurfaceMotor motor;
        ThermalVolume source;
        void Awake()
        {
            motor = GetComponent<SurfaceMotor>();
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
        }
        void FixedUpdate()
        {
            if (Session == null || Session.Resetting || Session.IsComplete) return;
            RefreshEnvironment();
            float dt = Time.fixedDeltaTime;
            float heat = motor.Velocity.magnitude * Session.Settings.HeatPerMetre * dt;
            Temperature += heat;
            if (source != null)
                Temperature = Mathf.MoveTowards(Temperature, source.TargetTemperature, source.DegreesPerSecond * dt);
            else Temperature = Mathf.MoveTowards(Temperature, Ambient, Session.Settings.PassiveCooling * dt);
            Temperature = Mathf.Clamp(Temperature, 0f, 100f);
            RefreshEnvironment();
        }
        public void SetTemperature(float value) { Temperature = Mathf.Clamp(value, 0f, 100f); RefreshEnvironment(); }

        public void RefreshEnvironment()
        {
            if (Session == null || Session.Settings == null) return;
            Ambient = Session.Settings.Ambient;
            IsSafe = InCrowd = InTransitionBand = false;
            source = null;
            int ambientPriority = int.MinValue, sourcePriority = int.MinValue;
            float coreGap = float.PositiveInfinity, bandGap = float.PositiveInfinity;
            float coreTarget = Ambient, bandTarget = Ambient;
            // Physics performs the spatial query. No trigger-enter bookkeeping to go stale after a warp.
            foreach (Collider c in Physics.OverlapSphere(transform.position, motor.Radius,
                VolumeMask, QueryTriggerInteraction.Collide))
            {
                if (!c.isTrigger) continue;
                if (c.TryGetComponent<SafeZone>(out _)) IsSafe = true;
                if (c.TryGetComponent<ThermalVolume>(out var zone))
                {
                    if (zone.OverridesAmbient && zone.Priority > ambientPriority)
                    { Ambient = zone.Ambient; ambientPriority = zone.Priority; }
                    if (zone.ChangesBodyTemperature && zone.Priority > sourcePriority)
                    { source = zone; sourcePriority = zone.Priority; }
                }
                if (!c.TryGetComponent<CrowdZone>(out var crowd)) continue;
                float distance = Vector3.Distance(transform.position, crowd.transform.position);
                float target = crowd.Target(Session);
                float gap = Mathf.Abs(Temperature - target);
                if (distance <= crowd.WorldRadius)
                {
                    InCrowd = true;
                    if (gap < coreGap) { coreGap = gap; coreTarget = target; }
                }
                else if (distance <= crowd.WorldRadius + crowd.WorldBand)
                {
                    InTransitionBand = true;
                    if (gap < bandGap) { bandGap = gap; bandTarget = target; }
                }
            }
            // A core takes precedence over another civilian's edge band. Overlaps never flicker by query order.
            if (InCrowd) { TargetTemperature = coreTarget; Mismatch = coreGap; InTransitionBand = false; }
            else if (InTransitionBand && bandGap < Mathf.Abs(Temperature - Ambient))
            { TargetTemperature = bandTarget; Mismatch = bandGap; }
            else { TargetTemperature = Ambient; Mismatch = Mathf.Abs(Temperature - Ambient); }
        }
    }
}

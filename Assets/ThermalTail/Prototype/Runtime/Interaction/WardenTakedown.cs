using System;
using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(WardenBrain))]
    public sealed class WardenTakedown : MonoBehaviour
    {
        [Min(0.1f)] public float InteractionRange = 1.5f;
        [Range(1f, 180f), Tooltip("Full approach angle centred behind the warden.")]
        public float RearAngle = 120f;
        public bool AllowAlertedTakedowns = false;
        public bool RestoreOnCheckpoint = true;
        public bool IsTakenDown { get; private set; }
        public event Action<bool> StateChanged;
        WardenBrain brain;
        NpcMotor motor;
        VisionSensor sensor;
        PrototypeSession session;
        Collider[] colliders;
        bool[] colliderEnabled;
        bool brainEnabled, motorEnabled, sensorEnabled, agentEnabled;

        void Awake()
        {
            brain = GetComponent<WardenBrain>(); motor = GetComponent<NpcMotor>(); sensor = GetComponent<VisionSensor>();
            session = motor.Session != null ? motor.Session : FindFirstObjectByType<PrototypeSession>();
        }
        void OnEnable() { if (session != null) session.ResetOccurred += OnCheckpoint; }
        void OnDisable() { if (session != null) session.ResetOccurred -= OnCheckpoint; }

        public bool CanTakedown(SurfaceMotor player)
        {
            if (!isActiveAndEnabled || IsTakenDown || session == null || player == null || player.Session != session ||
                session.PlayerInputBlocked || player.InTransition || !brain.isActiveAndEnabled) return false;
            if (!AllowAlertedTakedowns && (brain.Mode != WardenMode.Patrol || sensor.Suspicion > 0f)) return false;
            Vector3 offset = player.transform.position - transform.position;
            if (offset.sqrMagnitude > InteractionRange * InteractionRange) return false;
            Vector3 approach = Vector3.ProjectOnPlane(offset, transform.up);
            if (approach.sqrMagnitude < 0.0001f || Vector3.Angle(-transform.forward, approach) > RearAngle * 0.5f) return false;
            return VisionSensor.Unobstructed(player.transform.position, transform.position + transform.up * 0.5f,
                sensor.ObstructionMask, player.transform, transform);
        }

        public bool TryTakedown(SurfaceMotor player)
        {
            if (!CanTakedown(player)) return false;
            brainEnabled = brain.enabled; motorEnabled = motor.enabled;
            sensorEnabled = sensor.enabled; agentEnabled = motor.Agent.enabled;
            colliders = GetComponentsInChildren<Collider>(true);
            colliderEnabled = new bool[colliders.Length];
            IsTakenDown = true;
            brain.enabled = false;
            motor.enabled = false;
            motor.Agent.enabled = false;
            sensor.Clear(); sensor.enabled = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderEnabled[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
            StateChanged?.Invoke(true);
            session.Notify("Warden incapacitated");
            return true;
        }

        void OnCheckpoint()
        {
            if (!IsTakenDown || !RestoreOnCheckpoint) return;
            IsTakenDown = false;
            motor.Agent.enabled = agentEnabled;
            motor.enabled = motorEnabled;
            motor.ResetActor();
            brain.ResetBrain(); brain.enabled = brainEnabled;
            sensor.Clear(); sensor.enabled = sensorEnabled;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderEnabled[i];
            StateChanged?.Invoke(false);
        }
    }
}

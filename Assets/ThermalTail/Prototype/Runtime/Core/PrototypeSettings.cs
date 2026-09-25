using UnityEngine;

namespace ThermalTail.Prototype
{
    [CreateAssetMenu(menuName = "Thermal Tail/3D Prototype Settings")]
    public sealed class PrototypeSettings : ScriptableObject
    {
        [Header("Temperature (game units)")]
        public float Ambient = 22f;
        public float CivilianTemperature = 36f;
        [Min(0.1f)] public float MatchTolerance = 2f;
        [Min(0)] public float HeatPerMetre = 0.3f;
        [Min(0)] public float PassiveCooling = 0.08f;
        [Header("Player")]
        [Min(0.1f)] public float MoveSpeed = 2.6f;
        [Min(0.1f)] public float ClimbSpeed = 1.8f;
        [Min(0)] public float RespawnGrace = 2f;
        [Header("Perception")]
        [Min(0)] public float SuspicionGain = 0.3f;
        [Min(0)] public float GainPerDegree = 0.045f;
        [Min(0)] public float SuspicionDecay = 0.45f;
        [Range(0, 1)] public float ChaseThreshold = 0.65f;
        [Min(0)] public float InvestigationSeconds = 5f;
        [Min(0)] public float CivilianAlertRadius = 14f;
    }
}

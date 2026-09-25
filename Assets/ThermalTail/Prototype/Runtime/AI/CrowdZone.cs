using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CrowdZone : MonoBehaviour
    {
        [Min(0.1f)] public float Radius = 2.2f;
        [Min(0)] public float TransitionBand = 0.55f;
        public bool UseSessionTemperature = true;
        public float Temperature = 36f;
        public float WorldRadius => Radius * Mathf.Abs(transform.lossyScale.x);
        public float WorldBand => TransitionBand * Mathf.Abs(transform.lossyScale.x);
        public float Target(PrototypeSession session) => UseSessionTemperature ? session.Settings.CivilianTemperature : Temperature;
        void Awake() { ConfigureCollider(); }
        void OnValidate() { ConfigureCollider(); }
        void ConfigureCollider()
        {
            var c = GetComponent<SphereCollider>();
            c.isTrigger = true;
            c.radius = Radius + TransitionBand;
        }
    }
}

using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(Collider))]
    public sealed class ThermalVolume : MonoBehaviour
    {
        public bool OverridesAmbient;
        public float Ambient = 22f;
        public bool ChangesBodyTemperature = true;
        public float TargetTemperature = 22f;
        [Min(0)] public float DegreesPerSecond = 5f;
        [Tooltip("Highest priority wins when sources overlap.")]
        public int Priority;
        void Reset() { GetComponent<Collider>().isTrigger = true; }
    }
}

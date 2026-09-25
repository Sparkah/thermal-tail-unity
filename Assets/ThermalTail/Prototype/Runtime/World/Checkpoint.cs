using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        public Transform Spawn;
        public string SectionName = "Vents";
        public float Temperature = 22f;
        bool activated;
        void Reset() { GetComponent<Collider>().isTrigger = true; }
        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<SurfaceMotor>();
            if (player == null || activated || Spawn == null) return;
            activated = true;
            player.Session.SetCheckpoint(Spawn.position, Spawn.rotation, Temperature, SectionName);
        }
    }
}

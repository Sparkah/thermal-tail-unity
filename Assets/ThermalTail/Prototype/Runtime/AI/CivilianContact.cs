using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CivilianContact : MonoBehaviour
    {
        [Min(0.1f)] public float PersonalRadius = 0.5f;
        void Awake() { Configure(); }
        void OnValidate() { Configure(); }
        void Configure() { var c = GetComponent<SphereCollider>(); c.isTrigger = true; c.radius = PersonalRadius; }
        void OnTriggerStay(Collider other)
        {
            var motor = other.GetComponentInParent<SurfaceMotor>();
            if (motor != null) motor.Session.ReportCivilianContact(motor.transform.position);
        }
    }
}

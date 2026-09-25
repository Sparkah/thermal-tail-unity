using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Explicit security blind spot. Prevents new sight, not contact capture or ongoing pursuit.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SafeZone : MonoBehaviour
    {
        void Reset() { GetComponent<Collider>().isTrigger = true; }
    }
}

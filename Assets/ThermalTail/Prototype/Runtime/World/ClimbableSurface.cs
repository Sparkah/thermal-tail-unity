using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Opt-in attachment surface. Use simple solid colliders and unit-scale actor roots.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbableSurface : MonoBehaviour { }
}

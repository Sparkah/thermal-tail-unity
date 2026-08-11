using UnityEngine;

namespace ThermalTail
{
    /// <summary>Tints a renderer by the player's body temperature, through the ported ramp.</summary>
    public class ThermalTint : MonoBehaviour
    {
        public ThermalDirector Director;
        Renderer _rend;
        MaterialPropertyBlock _block;

        void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            if (Director == null) Director = FindFirstObjectByType<ThermalDirector>();
        }

        void LateUpdate()
        {
            if (_rend == null || Director == null) return;
            _rend.GetPropertyBlock(_block);
            var c = ThermalPalette.Sample(Director.PTemp);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            _rend.SetPropertyBlock(_block);
        }
    }
}

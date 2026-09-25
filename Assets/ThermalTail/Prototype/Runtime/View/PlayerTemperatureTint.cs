using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(PlayerThermal))]
    public sealed class PlayerTemperatureTint : MonoBehaviour
    {
        PlayerThermal thermal;
        Renderer[] renderers;
        MaterialPropertyBlock properties;
        void Start()
        {
            thermal = GetComponent<PlayerThermal>();
            renderers = GetComponentsInChildren<Renderer>();
            properties = new MaterialPropertyBlock();
        }
        void LateUpdate()
        {
            Color color = Color.Lerp(new Color(0.15f, 0.65f, 1f), new Color(1f, 0.35f, 0.12f), Mathf.InverseLerp(18, 40, thermal.Temperature));
            foreach (Renderer view in renderers)
            {
                view.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                view.SetPropertyBlock(properties);
            }
        }
    }
}

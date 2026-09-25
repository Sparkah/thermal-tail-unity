using UnityEngine;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(WardenTakedown))]
    public sealed class WardenTakedownView : MonoBehaviour
    {
        public Transform BodyVisual;
        public Vector3 DownOffset = new Vector3(0f, -0.48f, 0f);
        public Vector3 DownRotation = new Vector3(0f, 0f, 90f);
        WardenTakedown takedown;
        StealthVisuals stealth;
        Vector3 position;
        Quaternion rotation;
        Renderer[] renderers;
        bool[] rendererEnabled;
        bool stealthEnabled;

        void Awake()
        {
            takedown = GetComponent<WardenTakedown>(); stealth = GetComponent<StealthVisuals>();
            if (BodyVisual == null) BodyVisual = transform.Find("Body");
        }
        void OnEnable() { takedown.StateChanged += Show; }
        void OnDisable() { if (takedown != null) takedown.StateChanged -= Show; }
        void Show(bool down)
        {
            if (down)
            {
                if (BodyVisual != null)
                {
                    position = BodyVisual.localPosition; rotation = BodyVisual.localRotation;
                    BodyVisual.localPosition += DownOffset;
                    BodyVisual.localRotation *= Quaternion.Euler(DownRotation);
                }
                renderers = GetComponentsInChildren<Renderer>(true);
                rendererEnabled = new bool[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    rendererEnabled[i] = renderers[i].enabled;
                    if (BodyVisual == null || !renderers[i].transform.IsChildOf(BodyVisual)) renderers[i].enabled = false;
                }
                if (stealth != null) { stealthEnabled = stealth.enabled; stealth.enabled = false; }
            }
            else
            {
                if (BodyVisual != null) { BodyVisual.localPosition = position; BodyVisual.localRotation = rotation; }
                if (renderers != null)
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) renderers[i].enabled = rendererEnabled[i];
                if (stealth != null) stealth.enabled = stealthEnabled;
            }
        }
    }
}

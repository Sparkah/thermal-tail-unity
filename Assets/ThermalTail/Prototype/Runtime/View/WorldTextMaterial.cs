using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Uses a depth-tested material while keeping TextMesh's dynamic font atlas bound.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class WorldTextMaterial : MonoBehaviour
    {
        public Material DepthTestedMaterial;
        TextMesh label;
        MeshRenderer view;
        MaterialPropertyBlock properties;

        void OnEnable()
        {
            Font.textureRebuilt += FontRebuilt;
            Refresh();
        }
        void OnDisable() { Font.textureRebuilt -= FontRebuilt; }
        void LateUpdate() { Refresh(); }
        void FontRebuilt(Font font) { if (label != null && label.font == font) Refresh(); }

        public void Refresh()
        {
            if (DepthTestedMaterial == null) return;
            if (label == null) label = GetComponent<TextMesh>();
            if (view == null) view = GetComponent<MeshRenderer>();
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (view.sharedMaterial != DepthTestedMaterial) view.sharedMaterial = DepthTestedMaterial;
            if (properties == null) properties = new MaterialPropertyBlock();
            // Font atlases can be replaced when Unity adds glyphs. Do not bake an atlas
            // texture into the asset or modify the font's shared overlay material.
            view.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", label.font.material.mainTexture);
            view.SetPropertyBlock(properties);
        }
    }
}

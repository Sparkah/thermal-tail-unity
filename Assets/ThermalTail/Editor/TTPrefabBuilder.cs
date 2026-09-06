using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Builds the greybox prefab palette: one prefab per type the game actually simulates,
    /// named the way Pit talks rather than the way the JSON does. Section 3.1 of the plan.
    ///
    /// Every prefab is a unit cube; the importer scales it to the authored source rect, so a
    /// prefab carries type, colour, depth and collider shape, never dimensions.
    /// </summary>
    public static class TTPrefabBuilder
    {
        public const string Root = "Assets/ThermalTail";
        public const string PrefabDir = Root + "/Prefabs";
        public const string MaterialDir = Root + "/Materials";
        public const string ResourceDir = Root + "/Resources";
        public const string LevelDir = Root + "/Levels";

        public class Spec
        {
            public TTObjectType Type;
            public string PrefabName;
            public Color Colour;
            public float Depth;
            public bool Trigger;
            public PrimitiveType Primitive = PrimitiveType.Cube;
        }

        /// <summary>
        /// Depths place the greybox in the same planes the browser build faked with a
        /// perspective divide: the player plane at z=0, platform slabs extruded behind it,
        /// lenses further back still.
        /// </summary>
        public static readonly List<Spec> Specs = new List<Spec>
        {
            new Spec { Type = TTObjectType.Platform,       PrefabName = "Ledge",             Colour = new Color(0.36f, 0.42f, 0.47f), Depth = 7.1f, Trigger = false },
            new Spec { Type = TTObjectType.MovingPlatform, PrefabName = "Moving Ledge",      Colour = new Color(0.45f, 0.62f, 0.58f), Depth = 5.0f, Trigger = false },
            new Spec { Type = TTObjectType.Moth,           PrefabName = "Glowmoth",          Colour = new Color(1.00f, 0.88f, 0.44f), Depth = 0.4f, Trigger = true, Primitive = PrimitiveType.Sphere },
            new Spec { Type = TTObjectType.Camera,         PrefabName = "Surveillance Lens", Colour = new Color(0.30f, 0.80f, 0.95f), Depth = 1.2f, Trigger = true },
            new Spec { Type = TTObjectType.Guard,          PrefabName = "Warden",            Colour = new Color(0.95f, 0.45f, 0.32f), Depth = 0.6f, Trigger = true, Primitive = PrimitiveType.Capsule },
            new Spec { Type = TTObjectType.Shelter,        PrefabName = "Blind Spot",        Colour = new Color(0.24f, 0.30f, 0.40f), Depth = 2.0f, Trigger = true },
            new Spec { Type = TTObjectType.CoolRock,       PrefabName = "Cool Rock",         Colour = new Color(0.34f, 0.66f, 1.00f), Depth = 1.4f, Trigger = true },
            new Spec { Type = TTObjectType.IceMist,        PrefabName = "Ice Mist",          Colour = new Color(0.62f, 0.86f, 1.00f), Depth = 1.4f, Trigger = true },
            new Spec { Type = TTObjectType.WarmVent,       PrefabName = "Warm Vent",         Colour = new Color(1.00f, 0.55f, 0.30f), Depth = 1.4f, Trigger = true },
            new Spec { Type = TTObjectType.SunPatch,       PrefabName = "Sun Patch",         Colour = new Color(1.00f, 0.78f, 0.42f), Depth = 1.4f, Trigger = true },
            new Spec { Type = TTObjectType.Thorn,          PrefabName = "Thorn",             Colour = new Color(0.85f, 0.20f, 0.28f), Depth = 1.0f, Trigger = true },
            new Spec { Type = TTObjectType.Checkpoint,     PrefabName = "Scent Mark",        Colour = new Color(0.49f, 1.00f, 0.89f), Depth = 0.8f, Trigger = true },
            new Spec { Type = TTObjectType.ThermalGate,    PrefabName = "Thermal Gate",      Colour = new Color(0.80f, 0.60f, 1.00f), Depth = 3.0f, Trigger = false },
            new Spec { Type = TTObjectType.Wind,           PrefabName = "Wind Zone",         Colour = new Color(0.70f, 0.90f, 0.85f), Depth = 2.0f, Trigger = true },
            new Spec { Type = TTObjectType.DryAir,         PrefabName = "Dry Air",           Colour = new Color(0.85f, 0.80f, 0.62f), Depth = 2.0f, Trigger = true },
            new Spec { Type = TTObjectType.AmbientZone,    PrefabName = "Climate Zone",      Colour = new Color(0.55f, 0.55f, 0.75f), Depth = 2.4f, Trigger = true },
            new Spec { Type = TTObjectType.Ambient,        PrefabName = "Marker Ambient",    Colour = new Color(0.5f, 0.5f, 0.5f),    Depth = 0.2f, Trigger = true },
            new Spec { Type = TTObjectType.StartTemp,      PrefabName = "Marker Start Temp", Colour = new Color(0.5f, 0.5f, 0.5f),    Depth = 0.2f, Trigger = true },
            new Spec { Type = TTObjectType.Quota,          PrefabName = "Marker Quota",      Colour = new Color(0.5f, 0.5f, 0.5f),    Depth = 0.2f, Trigger = true },
            new Spec { Type = TTObjectType.ClimbWall,      PrefabName = "Climb Wall",        Colour = new Color(0.4f, 0.45f, 0.5f),   Depth = 0.2f, Trigger = true }
        };

        [MenuItem("Tools/Thermal Tail/Rebuild Greybox Prefabs")]
        public static void RebuildAll()
        {
            EnsureDirs();
            foreach (var s in Specs) BuildOne(s);
            BuildPlayer();
            BuildGoalMarker();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ThermalTail] Built " + (Specs.Count + 2) + " greybox prefabs in " + PrefabDir);
        }

        public static void EnsureDirs()
        {
            foreach (var d in new[] { Root, PrefabDir, MaterialDir, ResourceDir, LevelDir })
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        public static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        public static Material MakeMaterial(string name, Color colour, bool transparent)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = LitShader();
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (transparent)
            {
                // URP Lit transparent setup, so volumes read as volumes in the greybox.
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildOne(Spec spec)
        {
            bool volume = spec.Trigger && spec.Type != TTObjectType.Moth && spec.Type != TTObjectType.Guard;
            var colour = spec.Colour;
            if (volume) colour.a = 0.32f;

            var go = GameObject.CreatePrimitive(spec.Primitive);
            go.name = spec.PrefabName;

            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = MakeMaterial(spec.PrefabName.Replace(" ", "_"), colour, volume);

            // Colliders exist as geometry data for later queries; the simulation does its own AABB tests.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = spec.Trigger;

            var tt = go.AddComponent<TTObject>();
            tt.Type = spec.Type;
            tt.DisplayName = spec.PrefabName;
            tt.ViewDepth = spec.Depth;

            string path = PrefabDir + "/" + spec.PrefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void BuildPlayer()
        {
            var go = new GameObject("Lizard");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            // 72 x 48 source pixels, lying along x like the sprite does.
            body.transform.localScale = new Vector3(0.48f, 0.36f, 0.48f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var col = body.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            body.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Lizard", new Color(0.3f, 0.85f, 0.75f), false);
            go.AddComponent<ThermalTint>();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Lizard.prefab");
            Object.DestroyImmediate(go);
        }

        static void BuildGoalMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Exit Den";
            go.transform.localScale = new Vector3(1.2f, 1.6f, 1.2f);
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Exit_Den", new Color(1f, 0.84f, 0.35f, 0.55f), true);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Exit Den.prefab");
            Object.DestroyImmediate(go);
        }
    }
}

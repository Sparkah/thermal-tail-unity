using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Imports a game-factory-generic-levels-v1 payload into one Unity scene per level.
    ///
    /// The one transformation that matters is decoding the overloaded label. In the browser
    /// build "L slow" meant "aim left, 6.2 second cycle" and renaming the lens silently
    /// changed the puzzle. Here that is decoded once, at import, into typed fields, and the
    /// label survives as an inert display name.
    /// </summary>
    public static class TTLevelImporter
    {
        public const string DefaultPayload =
            "/Users/timmarkin/Agents/output/pit-thermal-tail-pin-20260806/kv_creation-levels_28b2b59cc5d384aec11374b6b2f2244a";

        // ---------------------------------------------------------------- DTOs

        [Serializable] public class JVec { public float x; public float y; }

        [Serializable]
        public class JObj
        {
            public string id;
            public string type;
            public float x, y, w, h, value;
            public string label;
        }

        [Serializable]
        public class JLevel
        {
            public string name;
            public float width, height;
            public JVec player;
            public JVec goal;
            public JObj[] objects;
            public string notes;
        }

        [Serializable]
        public class JPayload
        {
            public string schema;
            public JLevel[] levels;
            public string source;
        }

        // ---------------------------------------------------------------- menu

        [MenuItem("Tools/Thermal Tail/Import Levels JSON (pinned production bytes)")]
        public static void ImportPinned() => Import(DefaultPayload);

        [MenuItem("Tools/Thermal Tail/Import Levels JSON...")]
        public static void ImportChosen()
        {
            string path = EditorUtility.OpenFilePanel("Thermal Tail levels payload", "/Users/timmarkin/Agents/output", "");
            if (!string.IsNullOrEmpty(path)) Import(path);
        }

        public static void Import(string payloadPath)
        {
            if (!File.Exists(payloadPath))
            {
                Debug.LogError("[ThermalTail] Payload not found: " + payloadPath);
                return;
            }

            var payload = JsonUtility.FromJson<JPayload>(File.ReadAllText(payloadPath));
            if (payload == null || payload.levels == null || payload.levels.Length == 0)
            {
                Debug.LogError("[ThermalTail] Could not parse levels from " + payloadPath);
                return;
            }

            TTPrefabBuilder.EnsureDirs();
            if (!File.Exists(TTPrefabBuilder.PrefabDir + "/Ledge.prefab"))
                TTPrefabBuilder.RebuildAll();

            EnsureTuning();
            var prefabs = LoadPrefabs();

            // Collect into plain C# first. Loading a scene single can unload an asset that
            // nothing in the new scene references, which destroys a held ScriptableObject.
            var built = new List<LevelCatalogAsset.Entry>();
            var report = new List<string>();
            for (int i = 0; i < payload.levels.Length; i++)
            {
                string sceneName = BuildScene(payload.levels[i], i, prefabs);
                built.Add(new LevelCatalogAsset.Entry { SceneName = sceneName, DisplayName = payload.levels[i].name });
                report.Add((i + 1) + ". " + payload.levels[i].name + " -> " + sceneName +
                           " (" + (payload.levels[i].objects != null ? payload.levels[i].objects.Length : 0) + " objects)");
            }

            WriteCatalog(built);

            Debug.Log("[ThermalTail] Imported " + payload.levels.Length + " levels from " + payloadPath +
                      "\n" + string.Join("\n", report));
        }

        /// <summary>Writes the level order after all scenes exist, then registers them for builds.</summary>
        static void WriteCatalog(List<LevelCatalogAsset.Entry> entries)
        {
            var catalog = EnsureCatalog();
            catalog.Levels.Clear();
            catalog.Levels.AddRange(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RegisterBuildScenes(entries);
        }

        // ---------------------------------------------------------------- assets

        public static ThermalTuning EnsureTuning()
        {
            string path = TTPrefabBuilder.ResourceDir + "/ThermalTuning.asset";
            var t = AssetDatabase.LoadAssetAtPath<ThermalTuning>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<ThermalTuning>();
                AssetDatabase.CreateAsset(t, path);
                AssetDatabase.SaveAssets();
            }
            return t;
        }

        static LevelCatalogAsset EnsureCatalog()
        {
            string path = TTPrefabBuilder.ResourceDir + "/LevelCatalog.asset";
            var c = AssetDatabase.LoadAssetAtPath<LevelCatalogAsset>(path);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<LevelCatalogAsset>();
                AssetDatabase.CreateAsset(c, path);
                AssetDatabase.SaveAssets();
            }
            return c;
        }

        static Dictionary<TTObjectType, GameObject> LoadPrefabs()
        {
            var map = new Dictionary<TTObjectType, GameObject>();
            foreach (var s in TTPrefabBuilder.Specs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(TTPrefabBuilder.PrefabDir + "/" + s.PrefabName + ".prefab");
                if (go != null) map[s.Type] = go;
            }
            return map;
        }

        static void RegisterBuildScenes(List<LevelCatalogAsset.Entry> entries)
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var e in entries)
            {
                string p = TTPrefabBuilder.LevelDir + "/" + e.SceneName + ".unity";
                if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---------------------------------------------------------------- label decoding

        public static TTObjectType ParseType(string type)
        {
            switch ((type ?? "").Trim())
            {
                case "platform": return TTObjectType.Platform;
                case "movingPlatform": return TTObjectType.MovingPlatform;
                case "moth": return TTObjectType.Moth;
                case "camera": return TTObjectType.Camera;
                case "guard": return TTObjectType.Guard;
                case "shelter": return TTObjectType.Shelter;
                case "coolRock": return TTObjectType.CoolRock;
                case "iceMist": return TTObjectType.IceMist;
                case "warmVent": return TTObjectType.WarmVent;
                case "sunPatch": return TTObjectType.SunPatch;
                case "thorn": return TTObjectType.Thorn;
                case "checkpoint": return TTObjectType.Checkpoint;
                case "thermalGate": return TTObjectType.ThermalGate;
                case "wind": return TTObjectType.Wind;
                case "dryAir": return TTObjectType.DryAir;
                case "ambientZone": return TTObjectType.AmbientZone;
                case "ambient": return TTObjectType.Ambient;
                case "startTemp": return TTObjectType.StartTemp;
                case "quota": return TTObjectType.Quota;
                case "climbWall": return TTObjectType.ClimbWall;
                default: return TTObjectType.Unknown;
            }
        }

        /// <summary>
        /// Transcription of the substring matching in cameraInfo(), climbCameraInfo(),
        /// buildEnemies() and objectRect(). Run once, at import, so the flags stop being fragile.
        /// </summary>
        public static void DecodeLabel(TTPiece t, string rawLabel)
        {
            string label = (rawLabel ?? "").ToLowerInvariant();
            bool fast = label.Contains("fast");
            bool slow = label.Contains("slow");

            switch (t.Type)
            {
                case TTObjectType.Camera:
                    // direction: first character 'r', or the substring "right", else left.
                    t.Facing = (label.Length > 0 && label[0] == 'r') || label.Contains("right")
                        ? LensFacing.Right : LensFacing.Left;
                    t.Sweep = fast ? SweepRate.Fast : (slow ? SweepRate.Slow : SweepRate.Normal);
                    t.AlwaysOn = label.Contains("always");
                    t.Pulse = label.Contains("pulse");
                    if (label.Contains("up")) t.ClimbBaseAim = ClimbAim.Up;
                    else if (label.Contains("left")) t.ClimbBaseAim = ClimbAim.Left;
                    else if (label.Contains("right")) t.ClimbBaseAim = ClimbAim.Right;
                    else t.ClimbBaseAim = ClimbAim.Down;
                    t.Arc = label.Contains("wide") ? SweepArc.Wide
                          : (label.Contains("narrow") ? SweepArc.Narrow : SweepArc.Normal);
                    break;

                case TTObjectType.Guard:
                    t.Speed = fast ? GuardSpeed.Fast : (slow ? GuardSpeed.Slow : GuardSpeed.Normal);
                    t.Patrol = label.Contains("vertical") ? PatrolAxis.Vertical : PatrolAxis.Horizontal;
                    break;

                case TTObjectType.MovingPlatform:
                    t.LedgeFast = fast;
                    t.LedgeAxis = label.Contains("vertical") ? PatrolAxis.Vertical : PatrolAxis.Horizontal;
                    break;
            }
        }

        // ---------------------------------------------------------------- scene build

        static string SceneFileName(JLevel level, int index)
        {
            var safe = new System.Text.StringBuilder();
            foreach (char c in level.name ?? ("Level " + (index + 1)))
                safe.Append(char.IsLetterOrDigit(c) ? c : '_');
            return string.Format("{0:00}_{1}", index + 1, safe.ToString());
        }

        static float Meta(JLevel level, string type, float fallback)
        {
            if (level.objects == null) return fallback;
            foreach (var o in level.objects)
                if (o != null && o.type == type) return o.value;
            return fallback;
        }

        static bool HasType(JLevel level, string type)
        {
            if (level.objects == null) return false;
            foreach (var o in level.objects)
                if (o != null && o.type == type) return true;
            return false;
        }

        /// <summary>How deep each kind of box is. Presentation only; scale Z to change it.</summary>
        static float DefaultDepth(TTObjectType t)
        {
            foreach (var spec in TTPrefabBuilder.Specs)
                if (spec.Type == t) return Mathf.Max(0.05f, spec.Depth);
            return 1f;
        }

        /// <summary>
        /// One material per object type, taken from the same spec table the old prefabs were
        /// generated from. Volumes render translucent so a climate zone the size of the level
        /// does not paint over everything behind it - which is what an opaque one did.
        /// </summary>
        static Dictionary<TTObjectType, Material> SpecMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var d = new Dictionary<TTObjectType, Material>();
            if (shader == null) return d;

            foreach (var spec in TTPrefabBuilder.Specs)
            {
                if (d.ContainsKey(spec.Type)) continue;
                var m = new Material(shader);
                var c = spec.Colour;
                bool volume = spec.Trigger && spec.Type != TTObjectType.Moth && spec.Type != TTObjectType.Guard;
                if (volume)
                {
                    c.a = 0.28f;
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_ZWrite", 0f);
                    m.renderQueue = 3000 + (int)spec.Type;   // stable order, no frame-to-frame swapping
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                m.SetColor("_BaseColor", c);
                m.SetColor("_Color", c);
                d[spec.Type] = m;
            }
            return d;
        }

        /// <summary>
        /// Where each type's front face sits, so no two types share a plane. Small numbers,
        /// front to back: the things you look at nearest the camera, volumes furthest away
        /// so their transparency never washes over the level.
        /// </summary>
        static float ZFront(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Moth: return -0.45f;
                case TTObjectType.Guard: return -0.30f;
                case TTObjectType.Checkpoint: return -0.22f;
                case TTObjectType.Thorn: return -0.14f;
                case TTObjectType.ThermalGate: return 0.06f;
                case TTObjectType.Platform: return 0.14f;
                case TTObjectType.MovingPlatform: return 0.10f;
                case TTObjectType.Camera: return 0.22f;
                case TTObjectType.Shelter: return 0.30f;
                case TTObjectType.CoolRock: return 0.38f;
                case TTObjectType.WarmVent: return 0.46f;
                case TTObjectType.SunPatch: return 0.54f;
                case TTObjectType.IceMist: return 0.62f;
                case TTObjectType.DryAir: return 0.70f;
                case TTObjectType.Wind: return 0.78f;
                case TTObjectType.AmbientZone: return 0.86f;
                default: return 0.5f;
            }
        }

        static PrimitiveType ShapeFor(TTObjectType t)
        {
            foreach (var spec in TTPrefabBuilder.Specs)
                if (spec.Type == t) return spec.Primitive;
            return PrimitiveType.Cube;
        }

        static string BuildScene(JLevel level, int index, Dictionary<TTObjectType, GameObject> prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Re-resolve per scene: the previous scene's unload can have dropped it.
            var tuning = AssetDatabase.LoadAssetAtPath<ThermalTuning>(TTPrefabBuilder.ResourceDir + "/ThermalTuning.asset");

            // ---- lighting ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(0.92f, 0.95f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

            // ---- settings ----
            var settingsGo = new GameObject("Level Settings");
            var settings = settingsGo.AddComponent<LevelSettings>();
            settings.LevelName = string.IsNullOrEmpty(level.name) ? ("Operation " + (index + 1)) : level.name;
            settings.LevelIndex = index;
            settings.Width = TTMath.Clamp(level.width == 0 ? 2800f : level.width, 64f, 10000f);
            settings.Height = TTMath.Clamp(level.height == 0 ? 1200f : level.height, 64f, 10000f);
            settings.Ambient = TTMath.Clamp(Meta(level, "ambient", 28f), 0f, 100f);
            settings.IsClimb = HasType(level, "climbWall");
            settings.PlayerStart = level.player != null ? new Vector2(level.player.x, level.player.y) : new Vector2(100f, settings.Height * 0.7f);
            settings.Goal = level.goal != null ? new Vector2(level.goal.x, level.goal.y) : new Vector2(settings.Width - 100f, settings.Height * 0.7f);
            settings.DesignerNotes = level.notes ?? "";

            int mothCount = 0;
            if (level.objects != null)
                foreach (var o in level.objects) if (o != null && o.type == "moth") mothCount++;
            settings.Quota = (int)TTMath.Clamp(Mathf.Round(Meta(level, "quota", Mathf.Min(3, mothCount))), 0, mothCount);

            // startTemp default in the browser build is ambient + 12 when the marker is absent.
            float ambientAtSpawn = settings.Ambient;
            if (level.objects != null)
            {
                foreach (var o in level.objects)
                {
                    if (o == null || o.type != "ambientZone") continue;
                    float rx = o.x, ry = o.y, rw = o.w, rh = o.h;
                    if (rw < 0) { rx += rw; rw = -rw; }
                    if (rh < 0) { ry += rh; rh = -rh; }
                    if (settings.PlayerStart.x >= rx && settings.PlayerStart.x <= rx + rw &&
                        settings.PlayerStart.y >= ry && settings.PlayerStart.y <= ry + rh)
                        ambientAtSpawn = TTMath.Clamp(o.value, 0f, 100f);
                }
            }
            settings.StartTemp = TTMath.Clamp(Meta(level, "startTemp", ambientAtSpawn + 12f), 0f, 100f);

            // ---- objects ----
            var parent = new GameObject("Objects");
            var mats = SpecMaterials();
            int created = 0, unknown = 0;
            if (level.objects != null)
            {
                for (int i = 0; i < level.objects.Length; i++)
                {
                    var o = level.objects[i];
                    if (o == null) continue;
                    var type = ParseType(o.type);
                    if (type == TTObjectType.Unknown) unknown++;

                    float rx = o.x, ry = o.y, rw = o.w, rh = o.h;
                    if (rw < 0) { rx += rw; rw = -rw; }
                    if (rh < 0) { ry += rh; rh = -rh; }

                    var inst = GameObject.CreatePrimitive(ShapeFor(type));
                    inst.transform.SetParent(parent.transform, false);
                    UnityEngine.Object.DestroyImmediate(inst.GetComponent<Collider>());

                    // Side elevation, exactly as the original: the rect is the box, and the
                    // only new axis is depth toward the camera, which nothing simulates.
                    inst.transform.localScale = new Vector3(Mathf.Max(0.01f, rw / TTCoord.PixelsPerUnit),
                                                            Mathf.Max(0.01f, rh / TTCoord.PixelsPerUnit),
                                                            DefaultDepth(type));
                    // Every box used to start its front face on the same plane at z = 0.
                    // Seen dead on that is invisible; seen at any angle, two coplanar faces
                    // flicker against each other - the "texture inside texture" blinking.
                    // Each type now gets its own slab of depth so nothing is ever coplanar.
                    inst.transform.position = TTCoord.RectCenter(rx, ry, rw, rh,
                        ZFront(type) + DefaultDepth(type) * 0.5f);

                    var tt = inst.AddComponent<TTPiece>();
                    tt.Type = type;
                    tt.SourceIndex = i;
                    tt.Value = o.value;
                    DecodeLabel(tt, o.label);
                    if (mats.TryGetValue(type, out var m) && m != null)
                        inst.GetComponent<Renderer>().sharedMaterial = m;
                    inst.name = string.Format("{0:000} {1} [{2}]", i, string.IsNullOrEmpty(o.label) ? o.type : o.label, o.type);
                    created++;
                }
            }

            // ---- player, goal ----
            var lizard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            lizard.name = "Lizard";
            UnityEngine.Object.DestroyImmediate(lizard.GetComponent<Collider>());
            lizard.transform.localScale = new Vector3(0.5f, 0.32f, 0.5f);
            lizard.transform.position = TTCoord.Point(settings.PlayerStart.x, settings.PlayerStart.y);


            var goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goal.name = "Exit Den";
            UnityEngine.Object.DestroyImmediate(goal.GetComponent<Collider>());
            goal.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
            goal.transform.position = TTCoord.Point(settings.Goal.x, settings.Goal.y, 0.5f);


            // ---- camera ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.07f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<FollowCameraRig>();

            // ---- director ----
            var dirGo = new GameObject("Director");
            var director = dirGo.AddComponent<ThermalDirector>();
            director.Tuning = tuning;
            director.Settings = settings;
            director.PlayerView = lizard.transform;
            director.CameraRig = rig;
            rig.Director = director;
            var tint = lizard.GetComponent<ThermalTint>();
            if (tint != null) tint.Director = director;
            var hud = dirGo.AddComponent<ThermalHUD>();
            hud.Director = director;

            string sceneName = SceneFileName(level, index);
            string path = TTPrefabBuilder.LevelDir + "/" + sceneName + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[ThermalTail] " + sceneName + ": " + created + " objects" +
                      (unknown > 0 ? ", " + unknown + " unknown types substituted" : "") +
                      (settings.IsClimb ? ", CLIMB" : ""));
            return sceneName;
        }
    }
}

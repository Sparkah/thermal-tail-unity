using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Turns an imported level into a playable 3D one, in a single pass.
    ///
    /// The imported scenes are a side elevation: their boxes stand up in the XY plane like a
    /// cutaway drawing. Wrapping that in colliders and dropping a player into it gives a
    /// camera buried inside geometry and a player wedged between boxes, which is exactly
    /// what happens if you only add colliders. The layout has to be turned into a floor plan
    /// first, and that is what this does before anything else.
    ///
    /// Order matters: rebuild the geometry, lay a floor, then place the player on top of
    /// something, then the camera, then take the old director out of the loop.
    /// </summary>
    public static class TTFixLevel
    {
        static readonly Regex NamePattern = new Regex(@"^(\d+)\s+(.*?)\s*\[(\w+)\]\s*$");

        [MenuItem("Tools/Thermal Tail/Fix This Level %#f")]
        public static void FixOpen()
        {
            var report = new StringBuilder();
            bool ok = Fix(report);
            Debug.Log("[ThermalTail] " + report);
            if (!ok) Debug.LogWarning("[ThermalTail] level did not pass its checks - see above.");
        }

        [MenuItem("Tools/Thermal Tail/Fix ALL Levels")]
        public static void FixAll()
        {
            var report = new StringBuilder();
            int pass = 0, fail = 0;
            foreach (var path in Directory.GetFiles("Assets/ThermalTail/Levels", "*.unity"))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.AppendLine("--- " + Path.GetFileNameWithoutExtension(path));
                if (Object.FindFirstObjectByType<LevelSettings>() == null)
                {
                    report.AppendLine("  not a level scene, skipped");
                    continue;
                }
                if (Fix(report)) pass++; else fail++;
                EditorSceneManager.SaveOpenScenes();
            }
            report.AppendLine($"{pass} playable, {fail} not");
            Directory.CreateDirectory("_probe");
            File.WriteAllText("_probe/levels.txt", report.ToString());
            Debug.Log("[ThermalTail]\n" + report);
        }

        static bool Fix(StringBuilder report)
        {
            var settings = Object.FindFirstObjectByType<LevelSettings>();
            if (settings == null) { report.AppendLine("  no LevelSettings"); return false; }

            // ---- 1. rebuild the geometry as a floor plan ----
            var root = GameObject.Find("Objects");
            int rebuilt = 0;
            var platforms = new List<Bounds>();
            if (root != null)
            {
                var olds = new List<Transform>();
                foreach (Transform c in root.transform) olds.Add(c);

                foreach (var old in olds)
                {
                    var m = NamePattern.Match(old.name);
                    // Already converted pieces keep their transform; only untouched
                    // elevation objects get measured and rebuilt.
                    if (old.GetComponent<TTPiece>() != null) { Measure(old, platforms); continue; }
                    if (!m.Success) { Object.DestroyImmediate(old.gameObject); continue; }

                    var type = TTLevelImporter.ParseType(m.Groups[3].Value);
                    if (type == TTObjectType.Unknown) { Object.DestroyImmediate(old.gameObject); continue; }

                    var p = old.localPosition;
                    var s = old.localScale;
                    float w = Mathf.Abs(s.x) * 100f, h = Mathf.Abs(s.y) * 100f;
                    float x = p.x * 100f - w * 0.5f, y = -p.y * 100f - h * 0.5f;

                    var piece = Rebuild(root.transform, type, int.Parse(m.Groups[1].Value), old.name, x, y, w, h);
                    Object.DestroyImmediate(old.gameObject);
                    Measure(piece.transform, platforms);
                    rebuilt++;
                }
            }
            report.AppendLine($"  rebuilt {rebuilt} objects as primitives on the ground plane");

            // ---- 2. a floor, so nothing can fall out of the world ----
            var floor = GameObject.Find("Floor") ?? GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            float fw = Mathf.Max(20f, settings.Width / 100f) + 20f;
            float fh = Mathf.Max(20f, settings.Height / 100f) + 20f;
            floor.transform.position = new Vector3(settings.Width / 200f, -0.5f, -settings.Height / 200f);
            floor.transform.localScale = new Vector3(fw, 1f, fh);
            EnsureCollider(floor);
            Paint(floor, new Color(0.24f, 0.27f, 0.31f));

            // ---- 3. colliders on everything the player can touch ----
            int solid = 0, ghosted = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.GetComponent<CharacterController>() != null) continue;
                var piece = mf.GetComponent<TTPiece>();
                bool wantSolid = piece == null || IsSolid(piece.Type);   // floor has no piece
                var col = mf.GetComponent<Collider>();

                if (wantSolid)
                {
                    if (col == null) { mf.gameObject.AddComponent<BoxCollider>(); }
                    solid++;
                }
                else
                {
                    if (col != null) Object.DestroyImmediate(col);
                    Ghost(mf.gameObject);
                    ghosted++;
                }
            }
            report.AppendLine($"  {solid} solid, {ghosted} see-through markers");

            // ---- 4. leftovers from the runtime dressing, which we no longer build ----
            foreach (var w in Object.FindObjectsByType<ThermalWorld3D>(FindObjectsSortMode.None))
                Object.DestroyImmediate(w.gameObject);

            // ---- 5. player, standing on the biggest platform ----
            // Clear every player already in the scene, however it got there - a previous
            // run of this, a leftover from the old prefab, or one dragged in by hand. Finding
            // only the object literally named "Lizard" left strays behind that then won the
            // FindFirstObjectByType lottery and put the camera somewhere empty.
            foreach (var stray in Object.FindObjectsByType<TTPlayer>(FindObjectsSortMode.None))
                Object.DestroyImmediate(stray.gameObject);
            foreach (var stray in Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                Object.DestroyImmediate(stray.gameObject);
            var oldLizard = GameObject.Find("Lizard");
            if (oldLizard != null) Object.DestroyImmediate(oldLizard);

            Vector3 stand = StandingSpot(platforms, floor);
            var player = MakePlayer(stand);

            // ---- 6. camera ----
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Object.DestroyImmediate(c.gameObject);
            var cam = MakeCamera(player.transform);

            // ---- 7. the director runs the game: wardens, lenses, heat, moths, den, losing.
            // Only the lizard's movement is not its job any more.
            var director = Object.FindFirstObjectByType<ThermalDirector>();
            if (director == null)
            {
                var go = new GameObject("Director");
                director = go.AddComponent<ThermalDirector>();
                go.AddComponent<ThermalHUD>().Director = director;
            }
            director.gameObject.SetActive(true);
            director.enabled = true;
            director.Settings = settings;
            director.Tuning = director.Tuning != null ? director.Tuning : Resources.Load<ThermalTuning>("ThermalTuning");
            director.PlayerView = player.transform;
            director.GroundPlay = true;
            director.ExternalPlayer = true;
            director.BuildWorldDressing = false;
            director.CameraRig = null;                      // TTChaseCam owns the camera now
            var denObj = GameObject.Find("Exit Den");
            if (denObj == null)
            {
                denObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                denObj.name = "Exit Den";
                denObj.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
                Paint(denObj, new Color(0.45f, 1f, 0.65f));
                Object.DestroyImmediate(denObj.GetComponent<Collider>());
            }
            denObj.transform.position = DenSpot(platforms, player.transform.position);
            director.Den = denObj.transform;
            foreach (var hud in Object.FindObjectsByType<ThermalHUD>(FindObjectsSortMode.None))
            {
                hud.gameObject.SetActive(true);
                hud.Director = director;
            }
            report.AppendLine($"  director wired: {Object.FindObjectsByType<TTPiece>(FindObjectsSortMode.None).Length} pieces, den at {denObj.transform.position}");

            // ---- 8. check it ----
            bool clear = !InsideAnything(stand + Vector3.up * 0.5f, 0.45f);
            bool camClear = !InsideAnything(cam.transform.position, 0.3f);
            bool hasGround = platforms.Count > 0 || floor != null;
            report.AppendLine($"  player at {stand} clear={clear}, camera clear={camClear}, surfaces={platforms.Count}");

            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            return clear && camClear && hasGround;
        }

        static void Measure(Transform t, List<Bounds> into)
        {
            var piece = t.GetComponent<TTPiece>();
            if (piece == null) return;
            if (piece.Type != TTObjectType.Platform && piece.Type != TTObjectType.MovingPlatform) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) into.Add(r.bounds);
        }

        /// <summary>Furthest platform from the player, so the level is a journey.</summary>
        static Vector3 DenSpot(List<Bounds> platforms, Vector3 from)
        {
            Bounds best = default; float far = -1f; bool any = false;
            foreach (var b in platforms)
            {
                float d = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(from.x, from.z));
                if (d > far) { far = d; best = b; any = true; }
            }
            return any ? new Vector3(best.center.x, best.max.y + 0.4f, best.center.z)
                       : from + Vector3.forward * 6f;
        }

        /// <summary>Middle of the largest platform, or the floor if the level has none.</summary>
        static Vector3 StandingSpot(List<Bounds> platforms, GameObject floor)
        {
            Bounds best = default;
            float bestArea = -1f;
            foreach (var b in platforms)
            {
                float area = b.size.x * b.size.z;
                if (area > bestArea) { bestArea = area; best = b; }
            }
            if (bestArea > 0f) return new Vector3(best.center.x, best.max.y + 1.1f, best.center.z);
            return new Vector3(floor.transform.position.x, 1.6f, floor.transform.position.z);
        }

        /// <summary>Solid means the player collides with it. Everything else is a marker.</summary>
        static bool IsSolid(TTObjectType t) =>
            t == TTObjectType.Platform || t == TTObjectType.MovingPlatform || t == TTObjectType.ThermalGate;

        /// <summary>Markers stay visible but stop blocking the camera.</summary>
        static void Ghost(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            var m = new Material(r.sharedMaterial);
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            c.a = 0.35f;
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Surface", 1f);                 // URP transparent
            m.SetFloat("_Blend", 0f);
            m.renderQueue = 3000;
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            r.sharedMaterial = m;
        }

        static bool InsideAnything(Vector3 p, float radius)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponent<TTPlayer>() != null) continue;
                if (r.GetComponent<Collider>() == null) continue;   // only solid things block
                var b = r.bounds;
                b.Expand(-0.02f);
                if (b.Contains(p)) return true;
                if (b.SqrDistance(p) < radius * radius) return true;
            }
            return false;
        }

        static TTPiece Rebuild(Transform parent, TTObjectType type, int index, string name,
                               float x, float y, float w, float h)
        {
            float height = DefaultHeight(type);
            var go = GameObject.CreatePrimitive(
                type == TTObjectType.Moth ? PrimitiveType.Sphere :
                type == TTObjectType.Guard ? PrimitiveType.Capsule : PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Mathf.Max(0.05f, w / 100f), height, Mathf.Max(0.05f, h / 100f));
            go.transform.position = new Vector3((x + w * 0.5f) / 100f, height * 0.5f, -(y + h * 0.5f) / 100f);

            // Only the things you are meant to bump into are solid. Giving a climate zone or
            // a glowmoth a collider is what wedged the player inside scenery and put an
            // invisible box between the camera and the level.
            if (IsSolid(type)) EnsureCollider(go);

            var piece = go.AddComponent<TTPiece>();
            piece.Type = type;
            piece.SourceIndex = index;
            Paint(go, TintFor(type));
            return piece;
        }

        static void EnsureCollider(GameObject go)
        {
            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        }

        static GameObject MakePlayer(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Lizard";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            Paint(go, new Color(1f, 0.82f, 0.35f));

            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.4f; cc.center = Vector3.zero;
            cc.slopeLimit = 50f; cc.stepOffset = 0.4f;
            go.AddComponent<TTPlayer>();
            return go;
        }

        static GameObject MakeCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            var chase = go.AddComponent<TTChaseCam>();
            chase.Target = target;
            go.transform.position = target.position - Vector3.forward * 9f + Vector3.up * 6.5f;
            go.transform.rotation = Quaternion.LookRotation((target.position - go.transform.position).normalized);
            target.GetComponent<TTPlayer>().CameraTransform = go.transform;
            return go;
        }

        static Color TintFor(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Platform:
                case TTObjectType.MovingPlatform: return new Color(0.42f, 0.52f, 0.62f);
                case TTObjectType.Moth: return new Color(1f, 0.88f, 0.44f);
                case TTObjectType.Guard: return new Color(0.95f, 0.45f, 0.32f);
                case TTObjectType.Thorn: return new Color(0.85f, 0.20f, 0.28f);
                case TTObjectType.Camera: return new Color(0.30f, 0.80f, 0.95f);
                case TTObjectType.ThermalGate: return new Color(0.80f, 0.60f, 1f);
                case TTObjectType.Checkpoint: return new Color(0.49f, 1f, 0.89f);
                default: return new Color(0.5f, 0.55f, 0.62f);
            }
        }

        static float DefaultHeight(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Platform:
                case TTObjectType.MovingPlatform: return 0.5f;
                case TTObjectType.ThermalGate: return 1.9f;
                case TTObjectType.Shelter: return 0.85f;
                case TTObjectType.CoolRock: return 0.55f;
                case TTObjectType.WarmVent: return 0.22f;
                case TTObjectType.SunPatch: return 0.05f;
                case TTObjectType.IceMist:
                case TTObjectType.DryAir:
                case TTObjectType.Wind: return 1.25f;
                case TTObjectType.AmbientZone: return 0.04f;
                case TTObjectType.Thorn: return 0.45f;
                case TTObjectType.Moth: return 0.25f;
                case TTObjectType.Camera: return 2.1f;
                case TTObjectType.Guard: return 0.95f;
                case TTObjectType.Checkpoint: return 0.3f;
                default: return 0.5f;
            }
        }

        static void Paint(GameObject go, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var m = new Material(shader);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }
    }
}

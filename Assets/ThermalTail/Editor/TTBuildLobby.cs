using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Builds "Level 1 - Lobby" from Pit's floor plan.
    ///
    /// The drawing is a room seen from above: outer walls with a goal alcove at the top, a
    /// reception desk with three NPCs behind it, an enemy with a vision cone at each side, a
    /// cold unit on the left wall and a hot one on the right with their zones bleeding into
    /// the room, seating as cover, and the player entering at the bottom.
    ///
    /// It is built as a flat climb level because that is exactly the controller the plan
    /// implies - 8-way movement, no gravity, cone-shaped lenses - so nothing new had to be
    /// invented to play it.
    /// </summary>
    public static class TTBuildLobby
    {
        const string ScenePath = "Assets/ThermalTail/Levels/08_Lobby.unity";

        // Room in source pixels, y growing down the plan.
        const float W = 2400f, H = 1600f, Wall = 60f;

        [MenuItem("Tools/Thermal Tail/Build Pit's Lobby Level")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TTCoord.Flat = true;

            var settings = new GameObject("Level Settings").AddComponent<LevelSettings>();
            settings.LevelName = "Level 1 - Lobby";
            settings.LevelIndex = 7;
            settings.Width = W;
            settings.Height = H;
            settings.LiesFlat = true;
            settings.IsClimb = true;
            settings.Ambient = 27f;
            settings.StartTemp = 36f;
            settings.Quota = 3;
            settings.PlayerStart = new Vector2(1010f, 1300f);
            settings.Goal = new Vector2(1150f, 200f);
            settings.DesignerNotes = "From Pit's floor plan: lobby with reception, two patrolling " +
                                     "enemies, cold wall left, hot wall right, exit through the top alcove.";

            var objects = new GameObject("Objects");
            int index = 0;

            // ---- outer walls, with the alcove opening at the top ----
            Add(objects, ref index, TTObjectType.Platform, "wall top left", 0f, 300f, 890f, Wall);
            Add(objects, ref index, TTObjectType.Platform, "wall top right", 1410f, 300f, 990f, Wall);
            Add(objects, ref index, TTObjectType.Platform, "alcove left", 890f, 120f, Wall, 240f);
            Add(objects, ref index, TTObjectType.Platform, "alcove right", 1350f, 120f, Wall, 240f);
            Add(objects, ref index, TTObjectType.Platform, "alcove back", 890f, 60f, 520f, Wall);
            Add(objects, ref index, TTObjectType.Platform, "wall left", 0f, 300f, Wall, 1300f);
            Add(objects, ref index, TTObjectType.Platform, "wall right", W - Wall, 300f, Wall, 1300f);
            Add(objects, ref index, TTObjectType.Platform, "wall bottom left", 0f, H - Wall, 1000f, Wall);
            Add(objects, ref index, TTObjectType.Platform, "wall bottom right", 1250f, H - Wall, 1150f, Wall);

            // ---- reception desk, open toward the player ----
            Add(objects, ref index, TTObjectType.Platform, "desk left", 620f, 700f, Wall, 340f);
            Add(objects, ref index, TTObjectType.Platform, "desk right", 1700f, 700f, Wall, 340f);
            Add(objects, ref index, TTObjectType.Platform, "desk front", 620f, 980f, 1140f, Wall);

            // ---- the three NPCs behind it: a crowd to lose yourself in ----
            Add(objects, ref index, TTObjectType.Shelter, "npc left", 880f, 770f, 110f, 110f);
            Add(objects, ref index, TTObjectType.Shelter, "npc middle", 1120f, 770f, 110f, 110f);
            Add(objects, ref index, TTObjectType.Shelter, "npc right", 1360f, 770f, 110f, 110f);

            // ---- cold wall unit on the left, its chill spilling into the room ----
            Add(objects, ref index, TTObjectType.CoolRock, "chiller", 70f, 800f, 90f, 260f);
            Add(objects, ref index, TTObjectType.IceMist, "cold draught", 160f, 880f, 520f, 430f, -14f);

            // ---- hot wall unit on the right ----
            Add(objects, ref index, TTObjectType.WarmVent, "radiator", W - 150f, 700f, 90f, 250f);
            Add(objects, ref index, TTObjectType.SunPatch, "warm side", 1740f, 1130f, 520f, 380f, 16f);

            // ---- seating and pillars, the cover you move between ----
            Add(objects, ref index, TTObjectType.Platform, "seat a", 200f, 1290f, 170f, 160f);
            Add(objects, ref index, TTObjectType.Platform, "seat b", 430f, 1310f, 180f, 140f);
            Add(objects, ref index, TTObjectType.Platform, "seat c", 1690f, 1270f, 200f, 150f);
            Add(objects, ref index, TTObjectType.Platform, "seat d", 2000f, 1320f, 190f, 140f);
            Add(objects, ref index, TTObjectType.Platform, "pillar a", 830f, 1120f, 110f, 110f);
            Add(objects, ref index, TTObjectType.Platform, "pillar b", 1080f, 1150f, 110f, 110f);
            Add(objects, ref index, TTObjectType.Platform, "pillar c", 1480f, 1110f, 120f, 110f);

            // ---- the two enemies, each watching a cone of the room ----
            Add(objects, ref index, TTObjectType.Guard, "patrol left", 300f, 1080f, 70f, 90f, 320f,
                g => { g.Speed = GuardSpeed.Normal; g.Patrol = PatrolAxis.Horizontal; });
            Add(objects, ref index, TTObjectType.Guard, "patrol right", 2080f, 1060f, 70f, 90f, 300f,
                g => { g.Speed = GuardSpeed.Slow; g.Patrol = PatrolAxis.Vertical; });

            Add(objects, ref index, TTObjectType.Camera, "cone left", 470f, 1060f, 520f, 120f, 7f,
                g => { g.ClimbBaseAim = ClimbAim.Right; g.Sweep = SweepRate.Slow; g.Arc = SweepArc.Wide; });
            Add(objects, ref index, TTObjectType.Camera, "cone right", 1960f, 1040f, 520f, 120f, 7f,
                g => { g.ClimbBaseAim = ClimbAim.Left; g.Sweep = SweepRate.Normal; g.Arc = SweepArc.Normal; });

            // ---- what you came for ----
            Add(objects, ref index, TTObjectType.Moth, "glowmoth desk", 1150f, 880f, 50f, 50f);
            Add(objects, ref index, TTObjectType.Moth, "glowmoth cold", 380f, 1000f, 50f, 50f);
            Add(objects, ref index, TTObjectType.Moth, "glowmoth warm", 1980f, 1180f, 50f, 50f);
            Add(objects, ref index, TTObjectType.Checkpoint, "scent mark", 1010f, 1420f, 80f, 80f);

            Floor(settings);
            Lighting();

            var lizard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            lizard.name = "Lizard";
            Object.DestroyImmediate(lizard.GetComponent<Collider>());
            lizard.transform.localScale = new Vector3(0.55f, 0.3f, 0.55f);
            lizard.transform.position = TTCoord.Point(settings.PlayerStart.x, settings.PlayerStart.y, 0.3f);
            Paint(lizard, new Color(1f, 0.82f, 0.35f));
            lizard.AddComponent<ThermalTint>();

            var den = GameObject.CreatePrimitive(PrimitiveType.Cube);
            den.name = "Exit Den";
            Object.DestroyImmediate(den.GetComponent<Collider>());
            den.transform.localScale = new Vector3(1.1f, 0.1f, 1.1f);
            den.transform.position = TTCoord.Point(settings.Goal.x, settings.Goal.y, 0.05f);
            Paint(den, new Color(0.95f, 0.78f, 0.2f));

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.fieldOfView = 46f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<FollowCameraRig>();
            rig.ClimbDistance = 15f;
            rig.Pitch = 52f;      // look down into the room
            rig.Yaw = 0f;

            var dirGo = new GameObject("Director");
            var director = dirGo.AddComponent<ThermalDirector>();
            director.Settings = settings;
            director.Tuning = Resources.Load<ThermalTuning>("ThermalTuning");
            director.PlayerView = lizard.transform;
            director.CameraRig = rig;
            rig.Director = director;
            dirGo.AddComponent<ThermalHUD>().Director = director;

            Directory.CreateDirectory("Assets/ThermalTail/Levels");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ThermalTail] Built {ScenePath} from Pit's plan: {index} objects.");
        }

        static void Add(GameObject parent, ref int index, TTObjectType type, string name,
                        float x, float y, float w, float h, float value = 0f,
                        System.Action<TTPiece> tweak = null)
        {
            var go = GameObject.CreatePrimitive(
                type == TTObjectType.Moth ? PrimitiveType.Sphere :
                type == TTObjectType.Guard || type == TTObjectType.Shelter ? PrimitiveType.Capsule
                                                                           : PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = $"{index:000} {name} [{type}]";
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = TTCoord.RectScale(w, h, Height(type));
            go.transform.position = TTCoord.Point(x + w * 0.5f, y + h * 0.5f, Height(type) * 0.5f);

            var piece = go.AddComponent<TTPiece>();
            piece.Type = type;
            piece.SourceIndex = index++;
            piece.Value = value;
            tweak?.Invoke(piece);
            Paint(go, Tint(type));
        }

        static float Height(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Platform: return 1.1f;      // walls and furniture stand up
                case TTObjectType.Guard: return 1.0f;
                case TTObjectType.Shelter: return 0.9f;
                case TTObjectType.CoolRock: return 0.9f;
                case TTObjectType.WarmVent: return 0.9f;
                case TTObjectType.Camera: return 0.06f;       // the cone is drawn on the floor
                case TTObjectType.IceMist:
                case TTObjectType.SunPatch: return 0.05f;
                case TTObjectType.Moth: return 0.3f;
                case TTObjectType.Checkpoint: return 0.08f;
                default: return 0.4f;
            }
        }

        static Color Tint(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Platform: return new Color(0.40f, 0.44f, 0.50f);
                case TTObjectType.Guard: return new Color(0.90f, 0.32f, 0.28f);
                case TTObjectType.Shelter: return new Color(0.55f, 0.60f, 0.70f);
                case TTObjectType.CoolRock: return new Color(0.34f, 0.72f, 1.00f);
                case TTObjectType.IceMist: return new Color(0.40f, 0.85f, 1.00f);
                case TTObjectType.WarmVent: return new Color(1.00f, 0.52f, 0.26f);
                case TTObjectType.SunPatch: return new Color(1.00f, 0.66f, 0.32f);
                case TTObjectType.Camera: return new Color(0.95f, 0.25f, 0.25f);
                case TTObjectType.Moth: return new Color(1.00f, 0.90f, 0.45f);
                case TTObjectType.Checkpoint: return new Color(0.45f, 1.00f, 0.85f);
                default: return Color.grey;
            }
        }

        static void Floor(LevelSettings s)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Floor";
            go.transform.localScale = new Vector3(W / 100f, 0.4f, H / 100f);
            go.transform.position = TTCoord.Point(W * 0.5f, H * 0.5f, -0.2f);
            Paint(go, new Color(0.20f, 0.22f, 0.26f));
        }

        static void Lighting()
        {
            var go = new GameObject("Directional Light");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.25f;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(58f, 140f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.42f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.29f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.15f, 0.18f);
        }

        static void Paint(GameObject go, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var m = new Material(shader);
            if (c.a < 0.99f)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = 3000;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }
    }
}

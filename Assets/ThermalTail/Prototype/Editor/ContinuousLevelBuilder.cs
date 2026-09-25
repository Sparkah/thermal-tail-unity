using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ThermalTail.Prototype.Editor
{
    /// <summary>Authors one editable scene; gameplay actors stay connected to shared prefabs.</summary>
    public static class ContinuousLevelBuilder
    {
        public const string ScenePath = PrototypeSceneBuilder.Root + "/Scenes/ThermalTailLevel.unity";
        public const string DataRoot = PrototypeSceneBuilder.Root + "/Data/ContinuousLevel";
        public const string PrefabRoot = PrototypeSceneBuilder.Root + "/Prefabs/ContinuousLevel";
        const string Root = PrototypeSceneBuilder.Root;
        static readonly Vector3 VaultCenter = new Vector3(84, 0, -24);
        static PrototypeSession session;
        static Transform section, geometry, actors, details;
        static Material floor, wall, trim, warm, cold, gold, dark;
        static int world, volume;

        [MenuItem("Tools/Thermal Tail/3D Prototype/Open Continuous Level")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Build();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Thermal Tail/3D Prototype/Rebuild Continuous Level")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild continuous level?",
                "This replaces generated geometry and scene edits. Shared prefab tuning, level settings, materials and edited note assets are preserved.",
                "Rebuild", "Cancel")) return;
            Build();
        }

        // Explicit batch entry point. Never regenerates automatically on imports or Play Mode.
        public static void Build()
        {
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(PrefabRoot);
            AssetDatabase.Refresh();
            world = LayerMask.NameToLayer("TT World"); volume = LayerMask.NameToLayer("TT Volumes");
            if (world < 0 || volume < 0) throw new InvalidOperationException("Create the 3D prototype layers first.");
            foreach (string prefab in new[] { "Player3D", "Warden", "Civilian", "HeatingShelter", "CoolingShelter", "NoteActor", "SecurityCamera", "LockActor" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + prefab + ".prefab") == null)
                    throw new InvalidOperationException("Missing shared prefab: " + prefab);
            floor = Material("Deck", new Color(.22f, .29f, .34f));
            wall = Material("Duct", new Color(.34f, .43f, .47f));
            trim = Material("Route", new Color(.17f, .8f, .68f), true);
            warm = Material("Warm", new Color(1f, .39f, .12f), true);
            cold = Material("Cold", new Color(.13f, .65f, 1f), true);
            gold = Material("Note", new Color(1f, .78f, .18f), true);
            dark = Material("Machinery", new Color(.09f, .13f, .17f));
            EnsureCameraVariant("DuctCamera", 11, 32);
            EnsureCameraVariant("VaultCamera", 17, 30);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .68f, .73f);
            var sun = new GameObject("Facility light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            session = new GameObject("Level Session").AddComponent<PrototypeSession>();
            session.Settings = Settings();
            MakeSection("01 · Thermal concourse"); BuildLobby();
            MakeSection("02 · Service ventilation"); BuildVents();
            MakeSection("03 · Vault descent"); BuildVault();
            CreatePlayerAndUI();
            PrototypeSceneBuilder.RecordSceneOverrides(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Thermal Tail] Built continuous concourse / vents / vault level: " + ScenePath);
        }

        static void MakeSection(string name)
        {
            section = new GameObject(name).transform;
            geometry = Group("Architecture", section);
            actors = Group("Gameplay · prefab instances", section);
            details = Group("Signs and lighting", section);
        }

        static void BuildLobby()
        {
            Box("Concourse deck", new Vector3(-20, -.3f, 0), new Vector3(40, .6f, 32), floor);
            Box("Concourse ceiling", new Vector3(-20, 7.3f, 0), new Vector3(40, .6f, 32));
            Box("West boundary", new Vector3(-40.3f, 3.5f, 0), new Vector3(.6f, 7, 32.6f));
            Box("North boundary", new Vector3(-20, 3.5f, 16.3f), new Vector3(40, 7, .6f));
            Box("South boundary", new Vector3(-20, 3.5f, -16.3f), new Vector3(40, 7, .6f));
            Box("East boundary north", new Vector3(.3f, 3.5f, 10.15f), new Vector3(.6f, 7, 12.3f));
            Box("East boundary south", new Vector3(.3f, 3.5f, -10.15f), new Vector3(.6f, 7, 12.3f));
            Box("Central thermal divider", new Vector3(-15, 1.6f, 0), new Vector3(1, 3.2f, 12), dark);
            Box("North cover island", new Vector3(-25, 1.3f, 8), new Vector3(6, 2.6f, 2), dark);
            Box("South cover island", new Vector3(-26, 1.3f, -8), new Vector3(7, 2.6f, 2), dark);
            Box("Exit sight break", new Vector3(-6, 1.5f, 5), new Vector3(5, 3, 1), dark);
            Box("Maintenance table top", new Vector3(-25, 2, 0), new Vector3(5, .4f, 4), wall);
            foreach (float x in new[] { -27f, -23f }) foreach (float z in new[] { -1.4f, 1.4f })
                Box("Table leg", new Vector3(x, .9f, z), new Vector3(.5f, 1.8f, .5f), dark);
            Shelter("Arrival cooler · 22", new Vector3(-37, 0, 0), 22);
            Shelter("Crowd preparation · 36", new Vector3(-33, 0, 4), 36);
            Box("Arrival heating baffle", new Vector3(-34.8f, 1.3f, 5), new Vector3(.5f, 2.6f, 4), dark);
            Shelter("North exchange · 22", new Vector3(-20, 0, 9), 22);
            Shelter("Divider warm pocket · 36", new Vector3(-18.8f, 0, -4), 36);
            Shelter("Divider cool pocket · 22", new Vector3(-12.5f, 0, -4), 22);
            Shelter("South refuge · 22", new Vector3(-21, 0, -9.5f), 22);
            Shelter("Vent approach cooler · 22", new Vector3(-4, 0, 0), 22);
            Npc("Civilian · west circuit", false, true, new[] {
                new Vector3(-31, 0, -4), new Vector3(-31, 0, 4), new Vector3(-20, 0, 4), new Vector3(-20, 0, -4) });
            Npc("Civilian · divider shuttle", false, false, new[] { new Vector3(-17, 0, -7), new Vector3(-17, 0, 7) });
            Npc("Warden · north shuttle", true, false, new[] { new Vector3(-35, 0, 12), new Vector3(-5, 0, 12) });
            Npc("Warden · south shuttle", true, false, new[] { new Vector3(-5, 0, -12), new Vector3(-35, 0, -12) });
            Npc("Warden · exit triangle", true, true, new[] {
                new Vector3(-9, 0, -6), new Vector3(-3, 0, -3), new Vector3(-10, 0, 3) });
            Sign("01 / THERMAL CONCOURSE\nOrange shelters: 36  /  Blue shelters: 22\nMatch civilians inside their rings. Keep clear of their bodies.",
                new Vector3(-38, 2.4f, 4), Vector3.right, .13f);
            Sign("SERVICE VENTS  >\nCool to 22 before leaving the crowd", new Vector3(-1, 2.3f, 3.5f), Vector3.left, .15f);
            LightAt(new Vector3(-28, 5, 0), 15, Color.white, 3);
            LightAt(new Vector3(-9, 5, 0), 15, Color.white, 3);
            BakeLobbyNavigation();
        }

        static Vector3 Cell(int x, int row) => new Vector3(4 + 8 * x, 0, 16 - 8 * row);
        static void BuildVents()
        {
            // Eight metre grid, seven metre headroom. Blank cells are sealed machinery blocks.
            string[] map = { ".#######", "##.#...#", "#.##.###", "...#...#", "...#####", "...#.#.#" };
            var cells = new HashSet<Vector2Int>();
            for (int r = 0; r < map.Length; r++) for (int c = 0; c < map[r].Length; c++)
                if (map[r][c] == '#') cells.Add(new Vector2Int(c, r));
            foreach (var cell in cells)
            {
                Vector3 p = Cell(cell.x, cell.y);
                string id = cell.x + "," + cell.y;
                Box("Duct floor " + id, p + Vector3.down * .3f, new Vector3(8, .6f, 8), floor);
                Box("Duct ceiling " + id, p + Vector3.up * 7.3f, new Vector3(8, .6f, 8));
                for (int side = 0; side < 4; side++)
                {
                    Vector2Int step = side == 0 ? Vector2Int.right : side == 1 ? Vector2Int.left : side == 2 ? Vector2Int.up : Vector2Int.down;
                    if (cells.Contains(cell + step)) continue;
                    if (cell == new Vector2Int(0, 2) && side == 1) continue;
                    if (cell == new Vector2Int(7, 5) && side == 0) continue;
                    Vector3 direction = new Vector3(step.x, 0, -step.y);
                    var panel = Box("Duct wall " + id + " / " + side, p + direction * 4 + Vector3.up * 3.5f,
                        new Vector3(8.4f, 7, .4f));
                    panel.transform.rotation = Quaternion.LookRotation(direction);
                }
                Decoration("Duct seam " + id, p + new Vector3(0, .015f, -3.7f), new Vector3(7.4f, .02f, .07f), dark);
                Decoration("Ceiling light " + id, p + new Vector3(0, 6.96f, 0), new Vector3(2.3f, .03f, .25f), trim);
                LightAt(p + Vector3.up * 5.7f, 9, new Color(.7f, .86f, 1f), 2);
            }
            CheckpointAt("Vents checkpoint", new Vector3(2, 3.5f, 0), new Vector3(1, .34f, 0),
                Quaternion.LookRotation(Vector3.right), "Service ventilation", 22, new Vector3(1, 7, 7.5f));
            Shelter("Vent entry cooler · 22", new Vector3(3, 0, 1), 22);
            Shelter("West elbow cooler · 22", Cell(1, 1), 22);
            Shelter("Upper junction cooler · 22", Cell(3, 0) + new Vector3(-1, 0, -1), 22);
            Shelter("East elbow cooler · 22", Cell(7, 0), 22);
            Shelter("East return cooler · 22", Cell(7, 2) + new Vector3(1, 0, -1), 22);
            Shelter("Search branch cooler · 22", Cell(3, 4), 22);
            Shelter("Lower junction cooler · 22", Cell(5, 4), 22);
            // Full-width low baffles require climbing; >4 metres remain clear above the tops.
            Box("Climb baffle · upper duct", Cell(4, 0) + Vector3.up * 1.1f, new Vector3(.8f, 2.2f, 8), dark);
            Box("Climb baffle · lower duct", Cell(7, 3) + Vector3.up * 1.1f, new Vector3(8, 2.2f, .8f), dark);
            StripeOnBaffle(Cell(4, 0) + new Vector3(-.415f, 1.1f, 0), Quaternion.Euler(0, 90, 0));
            StripeOnBaffle(Cell(7, 3) + new Vector3(0, 1.1f, .415f), Quaternion.identity);
            Note(1, Cell(0, 2) + new Vector3(1.6f, .45f, -2.5f));
            Note(2, Cell(2, 2) + new Vector3(-2, .45f, 0));
            Note(3, Cell(5, 2) + new Vector3(-2, .45f, 0));
            Note(4, Cell(3, 5) + new Vector3(0, .45f, -2));
            Note(5, Cell(5, 5) + new Vector3(0, .45f, -2));
            CameraAt("Duct camera · entry junction", Cell(1, 0) + new Vector3(0, 2.7f, 2.5f), new Vector3(0, -.2f, -1), 0);
            CameraAt("Duct camera · east spine", Cell(7, 1) + new Vector3(2.7f, 2.7f, 0), new Vector3(-.35f, -.2f, -1), .3f);
            CameraAt("Duct camera · search branch", Cell(3, 3) + new Vector3(-2.7f, 2.7f, 0), new Vector3(.3f, -.2f, -1), .55f);
            CameraAt("Duct camera · lower return", Cell(6, 4) + new Vector3(0, 2.7f, -2.7f), new Vector3(-1, -.2f, .3f), .8f);
            Sign("02 / SERVICE VENTILATION\nExplore the side ducts for notes\nJ: reread notes  /  Climb striped obstructions",
                new Vector3(5, 2.5f, -3.65f), Vector3.forward, .14f);
            Sign("VAULT ACCESS  >", Cell(7, 5) + new Vector3(0, 2.2f, -3.65f), Vector3.forward, .2f);
            Box("Vault access deck", new Vector3(67.15f, -.3f, -24), new Vector3(6.3f, .6f, 8), floor);
            Box("Vault access ceiling", new Vector3(67, 7.3f, -24), new Vector3(6, .6f, 8));
            foreach (float z in new[] { -28f, -20f })
                Box("Vault access side", new Vector3(67, 3.5f, z), new Vector3(6, 7, .4f));
        }

        static void BuildVault()
        {
            // West panel opens at floor level; its inner face x=70.3 is flush with the entrance rim.
            float width = 28 * Mathf.Tan(Mathf.PI / 8) + .25f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 outward = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                var panel = Box("Vault climb wall " + i, VaultCenter + outward * 14 + Vector3.down * 12,
                    new Vector3(width, 24, .6f));
                panel.transform.rotation = Quaternion.LookRotation(outward);
                if (i != 6)
                {
                    var parapet = Box("Vault upper wall " + i, VaultCenter + outward * 14 + Vector3.up * 3.5f,
                        new Vector3(width, 7, .6f));
                    parapet.transform.rotation = panel.transform.rotation;
                }
                else foreach (float z in new[] { -29f, -19f })
                    Box("Vault entrance jamb", new Vector3(70, 3.5f, z), new Vector3(.6f, 7, 2));
                foreach (float y in new[] { -8f, -16f })
                {
                    var stripe = Decoration(y == -8 ? "Warm tier boundary" : "Cold tier boundary",
                        VaultCenter + outward * 13.685f + Vector3.up * y, new Vector3(width - .25f, .12f, .025f), y == -8 ? warm : cold);
                    stripe.transform.rotation = panel.transform.rotation;
                }
            }
            Box("Vault bottom deck", VaultCenter + Vector3.down * 24.3f, new Vector3(29, .6f, 29), floor);
            Box("Vault roof", VaultCenter + Vector3.up * 7.3f, new Vector3(29, .6f, 29), dark);
            Box("Central camera column", VaultCenter + Vector3.down * 11.5f, new Vector3(1.8f, 25, 1.8f), dark, false);
            AmbientBand("Warm exhaust tier · 30", -12, 8, 30);
            AmbientBand("Cold storage tier · 18", -20, 8, 18);
            Shelter("Vault rim cooler · 22", new Vector3(67, 0, -24), 22);
            CheckpointAt("Vault checkpoint", new Vector3(65, 3.5f, -24), new Vector3(67, .34f, -24),
                Quaternion.LookRotation(Vector3.right), "Vault descent", 22, new Vector3(1, 7, 7.5f));
            Sign("03 / VAULT DESCENT\nClimb over the rim and follow the wall shelters\nUpper: 22  /  Orange tier: 30  /  Blue tier: 18\nThe keypad is at the bottom",
                new Vector3(67.5f, 2.3f, -27.7f), Vector3.forward, .14f);
            WallShelter(6, -2, 22, "Descent entry");
            WallShelter(7, -5, 22, "Upper cooling station");
            WallShelter(0, -7.8f, 30, "Prepare for warm exhaust");
            WallShelter(1, -10.5f, 30, "Warm tier refuge");
            WallShelter(2, -13, 30, "Warm tier exchange");
            WallShelter(3, -15.8f, 18, "Prepare for cold storage");
            WallShelter(4, -18.5f, 18, "Cold tier refuge");
            WallShelter(5, -21, 18, "Lower cooling station");
            for (int i = 0; i < 12; i++)
            {
                Vector3 facing = Quaternion.Euler(0, 270 + i * 105, 0) * Vector3.forward;
                CameraAt("Column camera " + (i + 1).ToString("00"), VaultCenter + facing * 1.4f + Vector3.down * (2.5f + i * 1.65f),
                    facing, i * .137f, true);
            }
            Shelter("Keypad preparation · 18", VaultCenter + new Vector3(0, -24, -8), 18);
            Box("Vault door", VaultCenter + new Vector3(0, -22, -11.8f), new Vector3(5, 4, .65f), dark);
            var keypad = Prefab("LockActor", "Final vault keypad", VaultCenter + new Vector3(0, -23.25f, -11.35f));
            keypad.GetComponent<LockActor>().Session = session;
            Sign("INNER VAULT\nE: enter four-digit code", VaultCenter + new Vector3(0, -21.4f, -11.4f), Vector3.forward, .18f);
            for (int i = 0; i < 4; i++)
                LightAt(VaultCenter + new Vector3(0, -3 - i * 6, 0), 22, i < 2 ? new Color(1, .8f, .62f) : new Color(.5f, .8f, 1), 4);
        }

        static void WallShelter(int side, float y, float temperature, string name)
        {
            Vector3 outward = Quaternion.Euler(0, side * 45, 0) * Vector3.forward;
            Vector3 p = VaultCenter + outward * 13.7f + Vector3.up * y;
            Quaternion rotation = Quaternion.LookRotation(Vector3.down, -outward);
            var shelter = Shelter(name + " · " + temperature, p, temperature, rotation, false);
            // Canopy conceals the shelter from central cameras, with access from all four edges.
            var hood = Box(name + " camera baffle", p - outward * 2.2f, new Vector3(3.5f, .25f, 3.5f), dark);
            hood.transform.rotation = rotation;
            shelter.GetComponent<ThermalVolume>().Priority = 20;
            Sign(temperature + " / SAFE", p - outward * .04f + Vector3.up * .95f, -outward, .17f);
        }

        static void AmbientBand(string name, float y, float height, float temperature)
        {
            var go = new GameObject(name); go.transform.SetParent(actors); go.layer = volume;
            go.transform.position = VaultCenter + Vector3.up * y;
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(28, height, 28);
            var region = go.AddComponent<ThermalVolume>(); region.OverridesAmbient = true; region.Ambient = temperature;
            region.ChangesBodyTemperature = false; region.Priority = temperature == 18 ? 2 : 1;
        }

        static void CreatePlayerAndUI()
        {
            var go = Prefab("Player3D", "Player 3D", new Vector3(-37, .34f, 0));
            go.transform.SetParent(null); go.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var player = go.GetComponent<SurfaceMotor>(); player.Session = session;
            var thermal = go.GetComponent<PlayerThermal>(); thermal.Session = session;
            var controls = go.GetComponent<PrototypeControls>(); controls.Session = session;
            session.Player = player; session.Thermal = thermal;
            var spawn = new GameObject("Initial spawn · concourse").transform;
            spawn.SetPositionAndRotation(go.transform.position, go.transform.rotation); session.InitialSpawn = spawn;
            var camera = new GameObject("Player Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.nearClipPlane = .05f; camera.farClipPlane = 220; camera.backgroundColor = new Color(.035f, .055f, .08f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.gameObject.AddComponent<AudioListener>();
            var rig = camera.gameObject.AddComponent<SurfaceCamera>(); rig.Player = player; rig.ObstructionMask = 1 << world;
            controls.CameraRig = rig;
            var hud = session.gameObject.AddComponent<PrototypeHUD>(); hud.Session = session; hud.Controls = controls;
            session.gameObject.AddComponent<NoteUI>().Session = session;
            session.gameObject.AddComponent<LockKeypadUI>().Session = session;
        }

        static GameObject Prefab(string asset, string name, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + asset + ".prefab"));
            go.name = name; go.transform.SetParent(actors); go.transform.SetPositionAndRotation(position, Quaternion.identity);
            return go;
        }
        static void Npc(string name, bool hostile, bool loop, Vector3[] points)
        {
            var route = new GameObject(name + " route").AddComponent<PatrolRoute>(); route.transform.SetParent(actors);
            route.Points = new Transform[points.Length]; route.Loop = loop; route.WaitSeconds = hostile ? .8f : 1.4f;
            for (int i = 0; i < points.Length; i++)
            { route.Points[i] = Group("Waypoint " + (i + 1), route.transform); route.Points[i].position = points[i]; }
            var go = Prefab(hostile ? "Warden" : "Civilian", name, points[0]);
            go.transform.rotation = Quaternion.LookRotation(points[1] - points[0]);
            var motor = go.GetComponent<NpcMotor>(); motor.Session = session; motor.Route = route;
            if (go.TryGetComponent<VisionSensor>(out var sensor)) sensor.Session = session;
        }
        static GameObject Shelter(string name, Vector3 position, float target, Quaternion? rotation = null, bool label = true)
        {
            var go = Prefab(target > 25 ? "HeatingShelter" : "CoolingShelter", name, position);
            go.transform.rotation = rotation ?? Quaternion.identity;
            go.GetComponent<ThermalVolume>().TargetTemperature = target;
            if (label) Sign(target + " / SAFE", position + new Vector3(0, .045f, 0), Vector3.up, .18f);
            return go;
        }
        static void CheckpointAt(string name, Vector3 position, Vector3 spawn, Quaternion rotation, string sectionName, float temperature, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(actors); go.transform.position = position; go.layer = volume;
            var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = size;
            var checkpoint = go.AddComponent<Checkpoint>(); checkpoint.SectionName = sectionName; checkpoint.Temperature = temperature;
            checkpoint.Spawn = Group("Respawn", go.transform); checkpoint.Spawn.SetPositionAndRotation(spawn, rotation);
        }
        static void Note(int number, Vector3 position)
        {
            string path = DataRoot + "/VentNote" + number + ".asset";
            var note = AssetDatabase.LoadAssetAtPath<NoteData>(path);
            if (note == null)
            {
                note = ScriptableObject.CreateInstance<NoteData>(); note.Id = "vent-note-" + number;
                note.Title = "Service note " + number; note.Text = "blank text"; AssetDatabase.CreateAsset(note, path);
            }
            var actor = Prefab("NoteActor", "Note " + number + " · edit VentNote" + number, position).GetComponent<NoteActor>();
            actor.Session = session; actor.Note = note;
            Decoration("Note marker", position + Vector3.down * .42f, new Vector3(1, .04f, 1), gold);
        }
        static void CameraAt(string name, Vector3 position, Vector3 facing, float phase, bool vault = false)
        {
            var go = Prefab("ContinuousLevel/" + (vault ? "VaultCamera" : "DuctCamera"), name, position);
            go.transform.rotation = Quaternion.LookRotation(facing);
            go.GetComponent<VisionSensor>().Session = session;
            go.GetComponent<SecurityCamera>().PhaseOffset = phase;
        }
        static void EnsureCameraVariant(string name, float range, float fieldOfView)
        {
            string path = PrefabRoot + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/SecurityCamera.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = name; instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var sensor = instance.GetComponent<VisionSensor>(); sensor.Range = range; sensor.FieldOfView = fieldOfView;
                var camera = instance.GetComponent<SecurityCamera>(); camera.Period = 10;
                camera.ActiveFraction = .68f; camera.SweepDegrees = 48;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sensor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        static GameObject Box(string name, Vector3 position, Vector3 size, Material material = null, bool climbable = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = world;
            go.transform.SetParent(geometry); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material ?? wall;
            if (climbable) go.AddComponent<ClimbableSurface>();
            return go;
        }
        static GameObject Decoration(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(details); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static void StripeOnBaffle(Vector3 position, Quaternion rotation)
        {
            var go = Decoration("Climb stripe", position, new Vector3(2.5f, .22f, .025f), trim); go.transform.rotation = rotation;
        }
        static void Sign(string text, Vector3 position, Vector3 front, float size)
        {
            var label = new GameObject(text.Split('\n')[0]).AddComponent<TextMesh>();
            label.transform.SetParent(details); label.transform.position = position;
            label.transform.rotation = Quaternion.LookRotation(-front, Mathf.Abs(front.y) > .9f ? Vector3.forward : Vector3.up);
            label.text = text; label.characterSize = size; label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = Color.white;
            var binding = label.gameObject.AddComponent<WorldTextMaterial>();
            binding.DepthTestedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/WorldText.mat"); binding.Refresh();
        }
        static void LightAt(Vector3 position, float range, Color color, float intensity)
        {
            var light = new GameObject("Service light").AddComponent<Light>(); light.transform.SetParent(details);
            light.transform.position = position; light.type = LightType.Point; light.range = range;
            light.color = color; light.intensity = intensity; light.shadows = LightShadows.None;
        }
        static Transform Group(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = DataRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, path); return material;
        }
        static PrototypeSettings Settings()
        {
            string path = DataRoot + "/LevelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PrototypeSettings>(path);
            if (settings != null) return settings;
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PrototypeSettings>(Root + "/Data/PrototypeSettings.asset"));
            // Larger spaces need longer cooling intervals, while retaining movement heat.
            settings.HeatPerMetre = .16f; settings.PassiveCooling = .08f;
            AssetDatabase.CreateAsset(settings, path); return settings;
        }
        static void BakeLobbyNavigation()
        {
            var nav = geometry.gameObject.AddComponent<NavMeshSurface>(); nav.collectObjects = CollectObjects.Children;
            nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders; nav.layerMask = 1 << world; nav.BuildNavMesh();
            if (nav.navMeshData == null) throw new InvalidOperationException("Lobby navigation bake failed.");
            string path = DataRoot + "/LobbyNavigation.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) AssetDatabase.CreateAsset(nav.navMeshData, path);
            else { EditorUtility.CopySerialized(nav.navMeshData, existing); nav.RemoveData(); nav.navMeshData = existing; nav.AddData(); }
        }
    }
}

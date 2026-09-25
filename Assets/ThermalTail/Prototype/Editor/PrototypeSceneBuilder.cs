using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ThermalTail.Prototype.Editor
{
    /// <summary>Creates a disposable integration scene and reusable prefabs. Does not touch legacy levels.</summary>
    public static class PrototypeSceneBuilder
    {
        public const string Root = "Assets/ThermalTail/Prototype";
        public const string ScenePath = Root + "/Scenes/PrototypeExample.unity";
        static int worldLayer, actorLayer, volumeLayer;
        static PrototypeSession session;
        static Transform architecture;
        static Material stone, teal, warm, cold, red, cream, gold;

        [MenuItem("Tools/Thermal Tail/3D Prototype/Open Example")]
        public static void OpenExample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Build();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Thermal Tail/3D Prototype/Rebuild Example")]
        public static void RebuildExample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild example?",
                "This replaces the generated PrototypeExample scene using your existing prefabs. Missing prefabs are created with defaults. Your other scenes are unaffected.", "Rebuild", "Cancel")) return;
            Build();
        }

        // Batch entry point: Unity -batchmode -executeMethod ThermalTail.Prototype.Editor.PrototypeSceneBuilder.Build
        public static void Build()
        {
            foreach (string folder in new[] { "Scenes", "Prefabs", "Data", "Materials" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            worldLayer = EnsureLayer("TT World"); actorLayer = EnsureLayer("TT Actors"); volumeLayer = EnsureLayer("TT Volumes");
            stone = Material("Concrete", new Color(0.18f, 0.23f, 0.3f));
            teal = Material("Lizard", new Color(0.15f, 0.8f, 0.66f));
            warm = Material("Heating", new Color(0.95f, 0.45f, 0.16f));
            cold = Material("Cooling", new Color(0.18f, 0.65f, 0.95f));
            red = Material("Warden", new Color(0.85f, 0.18f, 0.15f));
            cream = Material("Civilian", new Color(0.75f, 0.7f, 0.55f));
            gold = Material("Clue", new Color(1f, 0.83f, 0.24f));
            var settings = Asset<PrototypeSettings>(Root + "/Data/PrototypeSettings.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.8f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            session = new GameObject("Prototype Session").AddComponent<PrototypeSession>();
            session.Settings = settings;
            architecture = new GameObject("World Geometry").transform;

            // The first floor also proves the surface motor can walk normally without a jump mode.
            Solid("Lobby floor", new Vector3(0, -0.3f, 0), new Vector3(20, 0.6f, 20));
            Solid("Lobby left wall", new Vector3(-10, 1.5f, 0), new Vector3(0.4f, 3, 20));
            Solid("Lobby right wall", new Vector3(10, 1.5f, 0), new Vector3(0.4f, 3, 20));
            Solid("Lobby rear wall", new Vector3(0, 1.5f, -10), new Vector3(20, 3, 0.4f));
            Solid("Lobby cover", new Vector3(0, 0.8f, 0), new Vector3(1, 1.6f, 4));
            var player = CreatePlayer(new Vector3(-6, 0.34f, -7));
            session.Player = player; session.Thermal = player.GetComponent<PlayerThermal>();
            session.InitialSpawn = Marker("Initial spawn", player.transform.position, Quaternion.identity);
            CreateSafeVent("Heating shelter", new Vector3(-6, 0, -5), 36, warm);
            CreateSafeVent("Cooling shelter", new Vector3(-5, 0, 5), 22, cold);

            var route = CreateRoute("Civilian loop", new[] { new Vector3(-5, 0, -4), new Vector3(-5, 0, 4), new Vector3(-2, 0, 4), new Vector3(-2, 0, -4) });
            CreateNpc("Civilian", route.Points[0].position, route, false);
            var guardRoute = CreateRoute("Warden patrol", new[] { new Vector3(5, 0, -6), new Vector3(5, 0, 6) });
            guardRoute.Loop = false;
            CreateNpc("Warden", guardRoute.Points[0].position, guardRoute, true);

            // A small elevated vent path, with floor-to-wall and authored wall-to-floor connections.
            Solid("Climb wall", new Vector3(0, 2, 9.8f), new Vector3(20, 4, 0.4f));
            Solid("Vent walkway", new Vector3(0, 3.8f, 13), new Vector3(4, 0.4f, 6));
            Solid("Vent left wall", new Vector3(-2, 4.7f, 13), new Vector3(0.3f, 1.4f, 6));
            Solid("Vent right wall", new Vector3(2, 4.7f, 13), new Vector3(0.3f, 1.4f, 6));
            AddCornerLink("Wall to vent floor", new Vector3(0, 3.55f, 9.25f),
                Quaternion.LookRotation(Vector3.up, Vector3.back), new Vector3(0, 4.34f, 10.5f),
                Quaternion.identity, new Vector3(0, 5.3f, 9.1f), new Vector3(3.5f, 0.3f, 0.7f));
            AddCornerLink("Vent floor to wall", new Vector3(0, 4.34f, 10.5f),
                Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector3(0, 3.55f, 9.25f),
                Quaternion.LookRotation(Vector3.down, Vector3.back), new Vector3(0, 5.3f, 9.1f), new Vector3(3.5f, 0.7f, 0.3f));
            CreateSafeVent("Vent cooling shelter", new Vector3(0, 4, 11.7f), 22, cold);
            CreateCheckpoint("Vents checkpoint", new Vector3(0, 4.4f, 11.7f), new Vector3(0, 4.34f, 11.7f), Quaternion.identity, "Vents");
            CreateNote("clue-first", "First position", "The first digit is 7.", new Vector3(-0.9f, 4.4f, 12.8f));
            CreateNote("clue-order", "Order of the remaining digits", "After 7, the remaining marked digits are in ascending order.", new Vector3(0.9f, 4.4f, 14.4f));
            CreateMoth(new Vector3(1, 0.45f, 6));

            // Octagonal shaft: eight flat climbable panels. The south panel doubles as the entry descent.
            Vector3 vaultCenter = new Vector3(0, 0, 22);
            Solid("Vault floor", vaultCenter + new Vector3(0, -0.3f, 0), new Vector3(13, 0.6f, 13));
            float apothem = 6f;
            float width = 2 * apothem * Mathf.Tan(Mathf.PI / 8f) + 0.1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 outward = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                var wall = Solid("Vault wall " + (i + 1), vaultCenter + outward * apothem + Vector3.up * 2,
                    new Vector3(width, 4, 0.35f));
                wall.transform.rotation = Quaternion.LookRotation(outward);
            }
            // Safe authored crossing over the south rim to the interior wall.
            AddCornerLink("Vent into vault", new Vector3(0, 4.34f, 15.2f), Quaternion.identity,
                new Vector3(0, 3.3f, 16.52f), Quaternion.LookRotation(Vector3.down, Vector3.forward),
                new Vector3(0, 5.15f, 17.1f), new Vector3(3.5f, 0.7f, 0.3f));
            CreateCheckpoint("Vault checkpoint", new Vector3(0, 3.3f, 16.52f), new Vector3(0, 3.3f, 16.52f),
                Quaternion.LookRotation(Vector3.down, Vector3.forward), "Black Vault");
            var vaultSafe = new GameObject("Vault entry cooling blind spot");
            vaultSafe.transform.position = new Vector3(0, 3.3f, 16.6f); vaultSafe.layer = volumeLayer;
            var safeBox = vaultSafe.AddComponent<BoxCollider>(); safeBox.isTrigger = true; safeBox.size = new Vector3(2.8f, 1.1f, 1.3f);
            vaultSafe.AddComponent<SafeZone>(); vaultSafe.AddComponent<ThermalVolume>().TargetTemperature = 22;
            Solid("Camera column", vaultCenter + Vector3.up * 2, new Vector3(1.2f, 4, 1.2f), false);
            for (int i = 0; i < 4; i++)
            {
                Vector3 facing = Quaternion.Euler(0, i * 90f + 35f, 0) * Vector3.forward;
                CreateCamera(vaultCenter + facing * 0.85f + Vector3.up * (1.1f + i * 0.5f), facing, i * 0.2f);
            }
            CreateSafeVent("Vault cooling shelter", new Vector3(-3, 0, 22), 22, cold);
            CreateLock(vaultCenter + new Vector3(0, 0.5f, 4));
            CreateLabels();

            var camera = new GameObject("Prototype Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = 0.05f; camera.farClipPlane = 150;
            camera.backgroundColor = new Color(0.04f, 0.07f, 0.12f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var rig = camera.gameObject.AddComponent<SurfaceCamera>(); rig.Player = player; rig.ObstructionMask = 1 << worldLayer;
            var controls = player.GetComponent<PrototypeControls>(); controls.CameraRig = rig;
            var hud = session.gameObject.AddComponent<PrototypeHUD>(); hud.Session = session; hud.Controls = controls;
            session.gameObject.AddComponent<NoteUI>().Session = session;
            session.gameObject.AddComponent<LockKeypadUI>().Session = session;

            var nav = architecture.gameObject.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.Children; nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            nav.layerMask = 1 << worldLayer;
            nav.BuildNavMesh();
            if (nav.navMeshData != null)
            {
                string navPath = Root + "/Data/PrototypeNavigation.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if (existing != null) { EditorUtility.CopySerialized(nav.navMeshData, existing); nav.RemoveData(); nav.navMeshData = existing; nav.AddData(); }
                else AssetDatabase.CreateAsset(nav.navMeshData, navPath);
            }
            RecordSceneOverrides(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Thermal Tail 3D] Generated " + ScenePath + " with reusable prefabs and baked navigation.");
        }

        static SurfaceMotor CreatePlayer(Vector3 position)
        {
            var go = PrefabInstance("Player3D", template =>
            {
                template.layer = actorLayer;
                template.AddComponent<SphereCollider>().radius = 0.32f;
                template.AddComponent<SurfaceMotor>().SolidMask = (1 << worldLayer) | (1 << actorLayer);
                template.AddComponent<PlayerThermal>(); template.AddComponent<PrototypeControls>();
                template.AddComponent<PlayerTemperatureTint>();
                Visual("Lizard body", template.transform, Vector3.zero, new Vector3(0.45f, 0.22f, 0.65f), teal, PrimitiveType.Capsule);
                Visual("Head", template.transform, new Vector3(0, 0.02f, 0.3f), Vector3.one * 0.25f, teal, PrimitiveType.Sphere);
            });
            go.name = "Player 3D"; go.transform.position = position;
            var motor = go.GetComponent<SurfaceMotor>(); motor.Session = session;
            go.GetComponent<PlayerThermal>().Session = session;
            go.GetComponent<PrototypeControls>().Session = session;
            return motor;
        }
        static NpcMotor CreateNpc(string name, Vector3 position, PatrolRoute route, bool hostile)
        {
            var go = PrefabInstance(hostile ? "Warden" : "Civilian", template => ConfigureNpc(template, hostile));
            go.name = name; go.transform.position = position;
            var npc = go.GetComponent<NpcMotor>(); npc.Session = session; npc.Route = route;
            if (go.TryGetComponent<VisionSensor>(out var sensor)) sensor.Session = session;
            return npc;
        }
        static void ConfigureNpc(GameObject go, bool hostile)
        {
            go.layer = actorLayer;
            var body = go.AddComponent<CapsuleCollider>(); body.radius = 0.3f; body.height = 1.6f; body.center = Vector3.up * 0.8f;
            var agent = go.AddComponent<NavMeshAgent>(); agent.radius = 0.35f; agent.height = 1.6f; agent.stoppingDistance = 0.15f;
            go.AddComponent<NpcMotor>();
            Visual("Body", go.transform, Vector3.up * 0.8f, new Vector3(0.6f, 0.8f, 0.6f), hostile ? red : cream, PrimitiveType.Capsule);
            Visual("Facing marker", go.transform, new Vector3(0, 1.35f, 0.36f), new Vector3(0.13f, 0.1f, 0.2f), gold);
            var visuals = go.AddComponent<StealthVisuals>();
            if (hostile)
            {
                var sensor = go.AddComponent<VisionSensor>(); sensor.ObstructionMask = 1 << worldLayer;
                sensor.CloseRange = 2f;
                sensor.Eye = Child("Eye", go.transform, new Vector3(0, 0.9f, 0.36f));
                go.AddComponent<WardenBrain>();
                go.AddComponent<WardenTakedown>();
                go.AddComponent<WardenTakedownView>().BodyVisual = go.transform.Find("Body");
                var captureGo = Child("Capture contact", go.transform, Vector3.up * 0.32f).gameObject;
                captureGo.layer = volumeLayer; captureGo.AddComponent<SphereCollider>().isTrigger = true;
                var capture = captureGo.AddComponent<CaptureContact>(); capture.ObstructionMask = 1 << worldLayer;
                visuals.Sensor = sensor; visuals.Capture = capture;
            }
            else
            {
                var crowdGo = Child("Crowd temperature zone", go.transform, Vector3.up * 0.32f).gameObject;
                crowdGo.layer = volumeLayer; visuals.Crowd = crowdGo.AddComponent<CrowdZone>();
                var suspicion = go.AddComponent<CivilianSuspicion>(); suspicion.Zone = visuals.Crowd;
                suspicion.ObstructionMask = 1 << worldLayer;
                var contactGo = Child("Personal space", go.transform, Vector3.up * 0.32f).gameObject;
                contactGo.layer = volumeLayer; visuals.PersonalSpace = contactGo.AddComponent<CivilianContact>();
            }
        }
        static PatrolRoute CreateRoute(string name, Vector3[] points)
        {
            var route = new GameObject(name).AddComponent<PatrolRoute>(); route.Points = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++) route.Points[i] = Child("Waypoint " + (i + 1), route.transform, points[i]);
            return route;
        }
        static GameObject Solid(string name, Vector3 position, Vector3 scale, bool climbable = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = worldLayer;
            go.transform.SetParent(architecture); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = stone;
            if (climbable) go.AddComponent<ClimbableSurface>();
            return go;
        }
        static void CreateSafeVent(string name, Vector3 position, float target, Material color)
        {
            var go = PrefabInstance(target > 25 ? "HeatingShelter" : "CoolingShelter", template =>
            {
                template.layer = volumeLayer;
                var volume = template.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.center = Vector3.up * 0.6f; volume.size = new Vector3(2.8f, 1.2f, 2.8f);
                template.AddComponent<SafeZone>(); template.AddComponent<ThermalVolume>().TargetTemperature = target;
                Visual("Safe area floor", template.transform, Vector3.up * 0.015f, new Vector3(2.8f, 0.03f, 2.8f), color);
            });
            go.name = name; go.transform.position = position;
            Label(name + "\nSAFE · " + go.GetComponent<ThermalVolume>().TargetTemperature + "°", position + Vector3.up * 1.5f, 0.12f);
        }
        static void CreateCheckpoint(string name, Vector3 position, Vector3 spawn, Quaternion rotation, string section)
        {
            var go = new GameObject(name); go.layer = volumeLayer; go.transform.position = position;
            var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = new Vector3(2.5f, 1f, 1f);
            var checkpoint = go.AddComponent<Checkpoint>(); checkpoint.SectionName = section;
            checkpoint.Spawn = Marker(name + " spawn", spawn, rotation);
        }
        static void CreateNote(string id, string title, string text, Vector3 position)
        {
            var data = Asset<NoteData>(Root + "/Data/" + id + ".asset"); data.Id = id; data.Title = title; data.Text = text; EditorUtility.SetDirty(data);
            var go = PrefabInstance("NoteActor", template =>
            {
                template.layer = volumeLayer;
                var c = template.AddComponent<SphereCollider>(); c.isTrigger = true; c.radius = 0.3f;
                template.AddComponent<NoteActor>();
                Visual("Note", template.transform, Vector3.zero, new Vector3(0.35f, 0.45f, 0.08f), gold);
            });
            go.name = title; go.transform.position = position;
            var note = go.GetComponent<NoteActor>(); note.Session = session; note.Note = data;
        }
        static void CreateMoth(Vector3 position)
        {
            var go = PrefabInstance("MothCollectible", template =>
            {
                template.layer = volumeLayer;
                var c = template.AddComponent<SphereCollider>(); c.isTrigger = true; c.radius = 0.25f;
                template.AddComponent<MothCollectible>();
                Visual("Moth", template.transform, Vector3.zero, Vector3.one * 0.25f, gold, PrimitiveType.Sphere);
            });
            go.name = "Glowmoth collectible"; go.transform.position = position;
            var moth = go.GetComponent<MothCollectible>(); moth.Session = session; moth.CollectionId = "example-moth";
        }
        static void CreateCamera(Vector3 position, Vector3 facing, float phase)
        {
            var go = PrefabInstance("SecurityCamera", template =>
            {
                Visual("Housing", template.transform, Vector3.zero, new Vector3(0.3f, 0.25f, 0.4f), cold);
                var sensor = template.AddComponent<VisionSensor>(); sensor.Range = 7; sensor.FieldOfView = 40;
                sensor.ObstructionMask = 1 << worldLayer; sensor.Eye = template.transform;
                template.AddComponent<SecurityCamera>().Pivot = template.transform;
                template.AddComponent<StealthVisuals>().Sensor = sensor;
            });
            go.name = "Security camera"; go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            go.GetComponent<VisionSensor>().Session = session;
            go.GetComponent<SecurityCamera>().PhaseOffset = phase;
        }
        static void CreateLock(Vector3 position)
        {
            var go = PrefabInstance("LockActor", template =>
            {
                Visual("Keypad housing", template.transform, Vector3.zero, new Vector3(0.65f, 0.8f, 0.2f), gold);
                var actor = template.AddComponent<LockActor>(); actor.Passcode = "7139"; actor.ObstructionMask = 1 << worldLayer;
            });
            go.name = "Inner Vault lock"; go.transform.position = position;
            go.GetComponent<LockActor>().Session = session;
        }

        static GameObject PrefabInstance(string name, Action<GameObject> configureDefaults)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var template = new GameObject(name);
            configureDefaults(template);
            PrefabUtility.SaveAsPrefabAssetAndConnect(template, path, InteractionMode.AutomatedAction);
            return template;
        }

        internal static void RecordSceneOverrides(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(transform)) continue;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
                    foreach (var component in transform.GetComponents<Component>())
                        if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
        }
        static void AddCornerLink(string name, Vector3 entryPosition, Quaternion entryRotation, Vector3 exitPosition,
            Quaternion exitRotation, Vector3 control, Vector3 triggerSize)
        {
            var go = new GameObject(name); go.layer = volumeLayer; go.transform.position = entryPosition;
            var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = triggerSize;
            var link = go.AddComponent<SurfaceTransition>();
            link.Entry = Child("Entry", go.transform, Vector3.zero); link.Entry.rotation = entryRotation;
            link.Exit = Child("Exit", go.transform, exitPosition - entryPosition); link.Exit.rotation = exitRotation;
            link.Control = Child("Control", go.transform, control - entryPosition);
        }
        static void CreateLabels()
        {
            Label("LOBBY\nHeat to 36 · follow the cyan ring\nKeep clear of red personal space", new Vector3(-5, 2, -8), 0.15f);
            Label("VENTS\nClimb the north wall near its centre\nClues are optional · J to reread", new Vector3(0, 2.8f, 9.3f), 0.13f);
            Label("BLACK VAULT\nCamera alarms reset this checkpoint\nFour marked digits · clues reveal order", new Vector3(0, 1.8f, 25.7f), 0.13f);
        }
        static void Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text.Split('\n')[0]).AddComponent<TextMesh>(); label.text = text; label.characterSize = size;
            label.fontSize = 36; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            label.transform.position = position; label.transform.rotation = Quaternion.identity;
            var textMaterial = label.gameObject.AddComponent<WorldTextMaterial>();
            textMaterial.DepthTestedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/WorldText.mat");
            textMaterial.Refresh();
        }
        static Transform Marker(string name, Vector3 position, Quaternion rotation)
        { var go = new GameObject(name); go.transform.SetPositionAndRotation(position, rotation); return go.transform; }
        static Transform Child(string name, Transform parent, Vector3 position)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform; }
        static void Visual(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path);
            mat.SetColor("_BaseColor", color); EditorUtility.SetDirty(mat); return mat;
        }
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name); if (existing >= 0) return existing;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layers.GetArrayElementAtIndex(i).stringValue = name; manager.ApplyModifiedProperties(); return i; }
            throw new InvalidOperationException("No free layer for " + name);
        }
    }
}

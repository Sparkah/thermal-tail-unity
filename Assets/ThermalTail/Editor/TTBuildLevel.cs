using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Builds a playable scene out of primitives, from nothing.
    ///
    /// Everything in it is a cube with a collider, a capsule with a CharacterController, and
    /// a camera. No director, no source rects, no components that have to agree with a
    /// transform. Add cubes, move them, scale them - they are the level, because Unity's
    /// physics is what the player stands on.
    /// </summary>
    public static class TTBuildLevel
    {
        const string SceneDir = "Assets/ThermalTail/Levels";

        [MenuItem("Tools/Thermal Tail/New Simple Level %#n")]
        public static void NewLevel()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Light();
            var ground = Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 40f),
                             new Color(0.26f, 0.29f, 0.33f));
            ground.AddComponent<TTPiece>().Type = TTObjectType.Platform;

            // A few platforms to jump between, so Play does something the moment it opens.
            for (int i = 0; i < 5; i++)
            {
                var p = Box($"Platform {i + 1}",
                            new Vector3(4f + i * 5f, 0.5f + i * 0.4f, 0f),
                            new Vector3(4f, 1f, 4f),
                            new Color(0.42f, 0.52f, 0.62f));
                p.AddComponent<TTPiece>().Type = TTObjectType.Platform;
            }

            var player = MakePlayer(new Vector3(0f, 1.5f, 0f));
            var cam = MakeCamera(player.transform);
            player.GetComponent<TTPlayer>().CameraTransform = cam.transform;

            Directory.CreateDirectory(SceneDir);
            string path = AssetDatabase.GenerateUniqueAssetPath(SceneDir + "/Simple Level.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = player;
            Debug.Log($"[ThermalTail] Built {path}. Press Play: WASD to move, Space to jump. " +
                      "Duplicate a Platform cube and drag it wherever you want.");
        }

        [MenuItem("Tools/Thermal Tail/Make This Scene Playable")]
        public static void MakeCurrentPlayable()
        {
            // Give every mesh a collider so the player has something to stand on.
            int added = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.GetComponent<Collider>() != null) continue;
                if (mf.GetComponent<CharacterController>() != null) continue;
                Undo.AddComponent<BoxCollider>(mf.gameObject);
                added++;
            }

            var existing = Object.FindFirstObjectByType<TTPlayer>();
            GameObject player;
            if (existing != null) player = existing.gameObject;
            else
            {
                // Start above the highest thing in the scene so the drop lands on it rather
                // than starting inside geometry.
                float top = 1f;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    top = Mathf.Max(top, r.bounds.max.y);
                var old = GameObject.Find("Lizard");
                Vector3 at = old != null ? new Vector3(old.transform.position.x, top + 1.5f, old.transform.position.z)
                                         : new Vector3(0f, top + 1.5f, 0f);
                if (old != null) Undo.DestroyObjectImmediate(old);
                player = MakePlayer(at);
            }

            var cam = Object.FindFirstObjectByType<TTChaseCam>();
            GameObject camGo = cam != null ? cam.gameObject : MakeCamera(player.transform);
            camGo.GetComponent<TTChaseCam>().Target = player.transform;
            player.GetComponent<TTPlayer>().CameraTransform = camGo.transform;

            // Get the old director out of the way properly. Setting `enabled = false` is not
            // enough: Unity still calls Awake on a disabled component, so it was still
            // loading the level, building its own floor and driving the lizard's transform -
            // which is exactly the platform that kept appearing on Play. Deactivating the
            // GameObject is what actually stops Awake.
            foreach (var d in Object.FindObjectsByType<ThermalDirector>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(d.gameObject, "Make playable");
                d.gameObject.SetActive(false);
            }
            foreach (var w in Object.FindObjectsByType<ThermalWorld3D>(FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(w.gameObject);
            foreach (var hud in Object.FindObjectsByType<ThermalHUD>(FindObjectsSortMode.None))
                hud.gameObject.SetActive(false);
            foreach (var tint in Object.FindObjectsByType<ThermalTint>(FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(tint);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[ThermalTail] Added {added} colliders, player and chase camera ready. " +
                      "The Thermal Director is disabled so nothing overrides your transforms. Press Play.");
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
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.zero;
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            go.AddComponent<TTPlayer>();
            Undo.RegisterCreatedObjectUndo(go, "Make playable");
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
            go.transform.position = target.position - Vector3.forward * 8f + Vector3.up * 4f;
            Undo.RegisterCreatedObjectUndo(go, "Make playable");
            return go;
        }

        static GameObject Box(string name, Vector3 pos, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            Paint(go, colour);
            return go;
        }

        static void Light()
        {
            var go = new GameObject("Directional Light");
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = LightType.Directional;
            l.intensity = 1.3f;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.33f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.19f, 0.22f);
        }

        static void Paint(GameObject go, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var m = new Material(shader);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}

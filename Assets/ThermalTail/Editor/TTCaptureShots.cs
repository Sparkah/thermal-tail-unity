using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Renders reference frames of the ground-plane layout straight from the editor, so the
    /// 3D conversion can be reviewed without anyone opening Unity. Batch entry point:
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod ThermalTail.EditorTools.TTCaptureShots.Run
    ///
    /// Edit-mode only. It proves the layout, the extrusion and the camera framing; it does
    /// not prove movement, which needs play mode.
    /// </summary>
    public static class TTCaptureShots
    {
        const int Width = 1280;
        const int Height = 720;

        public static void Run()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "_shots3d");
            Directory.CreateDirectory(outDir);

            string[] levels =
            {
                "Assets/ThermalTail/Levels/01_Fernlight_Verge.unity",
                "Assets/ThermalTail/Levels/03_Coldstone_Face.unity",
                "Assets/ThermalTail/Levels/06_Nightglass_Relay.unity"
            };

            foreach (var path in levels)
            {
                if (!File.Exists(path)) { Debug.LogWarning("[shots] missing " + path); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var settings = Object.FindFirstObjectByType<LevelSettings>();
                if (settings == null) { Debug.LogWarning("[shots] no LevelSettings in " + path); continue; }

                TTCoord.Plane = ViewPlane.Ground3D;
                LayOutGround(settings);
                ThermalWorld3D.Build(settings, settings.IsClimb);
                PlaceLizardMarker(settings);

                string stem = Path.GetFileNameWithoutExtension(scene.path);
                Shoot(settings, outDir, stem + "_chase", 8.5f, 3.6f, 55f, 4.5f);
                Shoot(settings, outDir, stem + "_overhead", 26f, 20f, 50f, 0f);
            }

            Debug.Log("[shots] wrote frames to " + outDir);
        }

        /// <summary>
        /// Mirror of ThermalDirector.ApplyStandingHeights for edit mode. Places the
        /// transforms directly rather than through SyncToTransform, which is authoring-space
        /// and would put everything back on the wall.
        /// </summary>
        static void LayOutGround(LevelSettings settings)
        {
            TTObject.SuppressAuthoring = true;
            try
            {
                foreach (var tt in Object.FindObjectsByType<TTObject>(FindObjectsSortMode.None))
                {
                    if (tt.IsMetadata) { tt.gameObject.SetActive(false); continue; }
                    tt.ViewDepth = TTView.StandingHeight(tt.Type, settings.IsClimb);
                    tt.transform.localRotation = Quaternion.identity;
                    tt.transform.localScale = TTCoord.RectScale(tt.SourceW, tt.SourceH, tt.ViewDepth);
                    tt.transform.localPosition = TTCoord.RectCenter(tt.SourceX, tt.SourceY, tt.SourceW, tt.SourceH, tt.ViewDepth * 0.5f);
                    var rend = tt.GetComponentInChildren<Renderer>();
                    if (rend != null && TTView.IsAirVolume(tt.Type))
                        rend.enabled = tt.SourceW * tt.SourceH < settings.Width * settings.Height * 0.8f;
                }
            }
            finally { TTObject.SuppressAuthoring = false; }
        }

        /// <summary>
        /// A stand-in lizard at the spawn. Edit mode never runs the director, so without this
        /// the frames could not show whether the camera actually frames the player - which is
        /// the exact thing that was wrong with the first pass.
        /// </summary>
        static void PlaceLizardMarker(LevelSettings settings)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "_lizard_marker";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.5f, 0.32f, 0.5f);

            Vector2 start = SpawnOnSurface(settings);
            float deck = settings.IsClimb ? 0f : TTView.DeckHeight;
            go.transform.position = TTCoord.Point(start.x, start.y, deck + TTView.PlayerRideHeight);

            var rend = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null && rend != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(1f, 0.83f, 0.35f));
                mat.SetColor("_Color", new Color(1f, 0.83f, 0.35f));
                rend.sharedMaterial = mat;
            }
        }

        /// <summary>Mirror of ThermalDirector.SnapToSurface, for the marker and the framing.</summary>
        static Vector2 SpawnOnSurface(LevelSettings settings)
        {
            Vector2 p = settings.PlayerStart;
            if (settings.IsClimb) return p;

            float inset = 31f;
            float best = float.MaxValue;
            Vector2 chosen = p;
            bool over = false;
            foreach (var tt in Object.FindObjectsByType<TTObject>(FindObjectsSortMode.None))
            {
                if (tt.Type != TTObjectType.Platform && tt.Type != TTObjectType.MovingPlatform) continue;
                var r = new Rect(tt.SourceX, tt.SourceY, tt.SourceW, tt.SourceH);
                if (p.x >= r.x && p.x <= r.xMax && p.y >= r.y && p.y <= r.yMax) { over = true; break; }
                if (r.width < inset * 2f || r.height < inset * 2f) continue;
                var c = new Vector2(Mathf.Clamp(p.x, r.x + inset, r.xMax - inset),
                                    Mathf.Clamp(p.y, r.y + inset, r.yMax - inset));
                float d = Vector2.Distance(c, p);
                if (d < best) { best = d; chosen = c; }
            }
            return over ? p : chosen;
        }

        static void Shoot(LevelSettings settings, string outDir, string name,
                          float distance, float height, float fov, float lookAhead)
        {
            var go = new GameObject("_shotcam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.10f);

            // Stand where the lizard spawns and look the way the chase rig would: down the
            // line from spawn to den, which is the heading the director latches at load.
            Vector2 start = SpawnOnSurface(settings);
            float deck = settings.IsClimb ? 0f : TTView.DeckHeight;
            Vector3 focus = TTCoord.Point(start.x, start.y, deck + TTView.PlayerRideHeight);
            Vector3 fwd = TTCoord.Direction(settings.Goal.x - start.x, settings.Goal.y - start.y);
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd.normalized;
            float d = FollowCameraRig.FittedDistance(focus, fwd, settings, distance);
            float scale = distance > 0.01f ? d / distance : 1f;
            Vector3 eye = focus - fwd * d + Vector3.up * Mathf.Max(1.9f, height * scale);
            Vector3 aim = focus + fwd * (lookAhead * scale);
            go.transform.position = eye;
            go.transform.rotation = Quaternion.LookRotation((aim - eye).normalized, Vector3.up);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}

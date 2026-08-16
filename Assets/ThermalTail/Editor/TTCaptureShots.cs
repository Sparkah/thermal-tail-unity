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
                ThermalWorld3D.Build(settings);

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
            Vector3 focus = TTCoord.Point(settings.PlayerStart.x, settings.PlayerStart.y, TTView.PlayerRideHeight);
            Vector3 fwd = TTCoord.Direction(settings.Goal.x - settings.PlayerStart.x,
                                            settings.Goal.y - settings.PlayerStart.y);
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd.normalized;
            Vector3 eye = focus - fwd * distance + Vector3.up * height;
            Vector3 aim = focus + fwd * lookAhead;
            Vector3 slid = FollowCameraRig.KeepInside(eye, settings);
            aim += slid - eye;
            go.transform.position = slid;
            go.transform.rotation = Quaternion.LookRotation((aim - slid).normalized, Vector3.up);

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

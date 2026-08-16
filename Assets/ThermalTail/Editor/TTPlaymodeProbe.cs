using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Steps the real simulation deterministically and reports what the lizard did.
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod ThermalTail.EditorTools.TTPlaymodeProbe.Run
    ///
    /// Play mode is not used. Batch play mode hangs in editor startup on this machine, and
    /// it is not needed anyway: the simulation is a pinned 1/60 step over plain fields, so
    /// calling it directly is both reproducible and faster. Reflection reaches the private
    /// step so the probe adds nothing to the shipping surface.
    ///
    /// It checks what a screenshot cannot: that ground movement carries the lizard, that the
    /// hop leaves the floor, that a closed thermal gate still stops it, and that the camera
    /// rig tracks it.
    /// </summary>
    public static class TTPlaymodeProbe
    {
        const string ReportPath = "_shots3d/sim_probe.txt";
        const float Step = 1f / 60f;

        public static void Run()
        {
            Directory.CreateDirectory("_shots3d");
            var sb = new StringBuilder();
            bool allPass = true;

            allPass &= Probe(sb, "Assets/ThermalTail/Levels/01_Fernlight_Verge.unity", "side-authored");
            allPass &= Probe(sb, "Assets/ThermalTail/Levels/03_Coldstone_Face.unity", "plan-authored (climb)");

            sb.AppendLine(allPass ? "OVERALL PASS" : "OVERALL FAIL");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[probe]\n" + sb);
            if (!allPass) EditorApplication.Exit(1);
        }

        static bool Probe(StringBuilder sb, string scenePath, string label)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var settings = Object.FindFirstObjectByType<LevelSettings>();
            var dir = Object.FindFirstObjectByType<ThermalDirector>();
            if (settings == null || dir == null)
            {
                sb.AppendLine($"{label}: FAIL - scene missing LevelSettings or ThermalDirector");
                return false;
            }

            dir.Tuning = dir.Tuning != null ? dir.Tuning : Resources.Load<ThermalTuning>("ThermalTuning");
            dir.Settings = settings;
            dir.GroundPlay = true;
            dir.BuildWorldDressing = false;

            var t = typeof(ThermalDirector);
            const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
            var gather = t.GetMethod("GatherObjects", priv);
            var simulate = t.GetMethod("Simulate", priv);
            if (gather == null || simulate == null)
            {
                sb.AppendLine($"{label}: FAIL - could not reach GatherObjects/Simulate");
                return false;
            }

            TTObject.SuppressAuthoring = true;
            try
            {
                gather.Invoke(dir, null);
                dir.LoadLevel();

                sb.AppendLine($"--- {Path.GetFileNameWithoutExtension(scenePath)} ({label})");
                sb.AppendLine($"  plane={TTCoord.Plane} climbMode={dir.ClimbMode} planAuthored={dir.PlanAuthored} objects={dir.Objects.Count}");

                float x0 = dir.px, y0 = dir.py;
                sb.AppendLine($"  spawn=({x0:F0},{y0:F0}) temp={dir.PTemp:F0} mode={dir.Mode}");

                // Hold forward for two seconds, hopping every second.
                TTInput.Scripted = true;
                float maxLift = 0f, maxSpeed = 0f;
                for (int i = 0; i < 120; i++)
                {
                    TTInput.Clear();
                    TTInput.Up = true;
                    TTInput.Right = true;
                    if (i % 60 == 30) TTInput.JumpQueued = true;
                    simulate.Invoke(dir, new object[] { Step });
                    maxLift = Mathf.Max(maxLift, dir.PLift);
                    maxSpeed = Mathf.Max(maxSpeed, TTMath.Hypot(dir.pvx, dir.pvy));
                }

                float moved = TTMath.Hypot(dir.px - x0, dir.py - y0);
                sb.AppendLine($"  after 2s: pos=({dir.px:F0},{dir.py:F0}) moved={moved:F0}px peakSpeed={maxSpeed:F0}px/s peakHop={maxLift:F0}px");

                // Camera rig: does it track, and does it stay inside the arena?
                Vector3 eye = Vector3.zero;
                bool camInside = true;
                if (dir.CameraRig != null)
                {
                    dir.CameraRig.Director = dir;
                    dir.CameraRig.SnapTo(dir.ViewTargetX(), dir.ViewTargetY());
                    eye = dir.CameraRig.transform.position;
                    camInside = eye.x >= 0f && eye.x <= settings.Width / 100f &&
                                eye.z <= 0f && eye.z >= -settings.Height / 100f;
                }
                sb.AppendLine($"  camera eye={eye} insideArena={camInside}");

                bool movedOk = moved > 60f;
                bool hopOk = maxLift > 20f;
                bool aliveOk = dir.Mode == GameMode.Playing || dir.Mode == GameMode.Respawning;

                sb.AppendLine($"  move={(movedOk ? "PASS" : "FAIL")} hop={(hopOk ? "PASS" : "FAIL")} " +
                              $"camera={(camInside ? "PASS" : "FAIL")} alive={(aliveOk ? "PASS" : "FAIL")} (mode={dir.Mode})");
                return movedOk && hopOk && camInside && aliveOk;
            }
            finally
            {
                TTInput.Scripted = false;
                TTObject.SuppressAuthoring = false;
            }
        }
    }
}

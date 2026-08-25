using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Walks each level from spawn to den while stepping the real simulation, and writes
    /// down what actually happened.
    ///
    /// Play mode cannot be driven in batch on this machine, so the lizard is moved by
    /// setting its transform - which is exactly what the CharacterController would do to it,
    /// and exactly what the director now reads. Everything else is the shipping code: the
    /// same Simulate, the same wardens, the same detection, the same catch conditions.
    /// </summary>
    public static class TTPlaythrough
    {
        const float Step = 1f / 60f;

        public static void All()
        {
            var report = new StringBuilder();
            Directory.CreateDirectory("_probe/play");
            int good = 0, bad = 0;

            foreach (var path in Directory.GetFiles("Assets/ThermalTail/Levels", "*.unity"))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string name = Path.GetFileNameWithoutExtension(path);
                if (Object.FindFirstObjectByType<LevelSettings>() == null) continue;

                report.AppendLine("=== " + name);
                if (Run(name, report)) good++; else bad++;
            }

            report.AppendLine($"{good} levels behaving, {bad} not");
            File.WriteAllText("_probe/playthrough.txt", report.ToString());
            Debug.Log("[playthrough]\n" + report);
        }

        static bool Run(string levelName, StringBuilder r)
        {
            var director = Object.FindFirstObjectByType<ThermalDirector>();
            var player = Object.FindFirstObjectByType<TTPlayer>();
            if (director == null || player == null) { r.AppendLine("  no director or player"); return false; }

            var t = typeof(ThermalDirector);
            const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
            var gather = t.GetMethod("GatherObjects", priv);
            var simulate = t.GetMethod("Simulate", priv);
            if (gather == null || simulate == null) { r.AppendLine("  cannot reach the sim"); return false; }

            director.Tuning = director.Tuning ?? Resources.Load<ThermalTuning>("ThermalTuning");
            gather.Invoke(director, null);
            director.LoadLevel();

            var start = player.transform.position;
            var den = director.Den != null ? director.Den.position : start + Vector3.forward * 5f;
            r.AppendLine($"  spawn {start}  den {den}  objects {director.Objects.Count}  wardens {director.Enemies.Count}  moths {director.TotalMoths}");

            // Where every warden began, so we can tell whether they are alive.
            var wardenStart = new List<Vector2>();
            foreach (var e in director.Enemies) wardenStart.Add(new Vector2(e.x, e.y));

            float peakDetect = 0f;
            int steps = 900;                       // 15 seconds
            bool reachedDen = false;
            var modeSeen = new HashSet<GameMode>();

            for (int i = 0; i < steps; i++)
            {
                // Walk toward the den at the speed the controller moves.
                var p = player.transform.position;
                var to = new Vector3(den.x - p.x, 0f, den.z - p.z);
                if (to.magnitude < 0.6f) reachedDen = true;
                if (to.magnitude > 0.05f)
                    player.transform.position = p + to.normalized * 6f * Step;

                simulate.Invoke(director, new object[] { Step });
                director.SyncView();

                peakDetect = Mathf.Max(peakDetect, director.MaxDetection);
                modeSeen.Add(director.Mode);
                if (director.Mode == GameMode.LevelComplete) break;
            }

            int wardensMoved = 0;
            for (int i = 0; i < director.Enemies.Count && i < wardenStart.Count; i++)
                if (Vector2.Distance(new Vector2(director.Enemies[i].x, director.Enemies[i].y), wardenStart[i]) > 4f)
                    wardensMoved++;

            r.AppendLine($"  wardens that moved: {wardensMoved}/{director.Enemies.Count}");
            r.AppendLine($"  peak detection {peakDetect:F2}   moths collected {director.MothsCollected}/{director.RequiredMoths}");
            r.AppendLine($"  reached den: {reachedDen}   modes seen: {string.Join(",", modeSeen)}");

            // Can you lose by falling?
            // Reset lives per check, or a level that killed you three times leaves the run
            // in Failed and every later check reads as already-over.
            TTSession.Lives = 3;
            director.LoadLevel();
            int catchesBefore = director.LevelCatches;
            bool fallKills = false;
            for (int i = 0; i < 120 && !fallKills; i++)
            {
                // Hold it below the world; respawn will keep yanking it back, and that is
                // the point - the death is what we are measuring, not where it ends up.
                player.transform.position = new Vector3(start.x, -20f, start.z);
                simulate.Invoke(director, new object[] { Step });
                if (director.Mode != GameMode.Playing || director.LevelCatches > catchesBefore) fallKills = true;
            }
            r.AppendLine($"  falling out of the level is fatal: {fallKills} " +
                         $"(catches {director.LevelCatches - catchesBefore}, reason '{director.CatchReason}')");

            // Can a warden catch you?
            TTSession.Lives = 3;
            director.LoadLevel();
            bool caught = false;
            if (director.Enemies.Count > 0)
            {
                int before = director.LevelCatches;
                for (int i = 0; i < 240 && !caught; i++)
                {
                    var e = director.Enemies[0];
                    player.transform.position = new Vector3(e.x / 100f, 0.6f, -e.y / 100f);
                    director.Grace = 0f;
                    simulate.Invoke(director, new object[] { Step });
                    if (director.LevelCatches > before || director.Mode != GameMode.Playing) caught = true;
                }
            }
            r.AppendLine($"  standing on a warden gets you caught: {caught}");

            // Does a glowmoth actually collect when you stand on it?
            director.LoadLevel();
            director.Grace = 0f;
            bool mothTaken = false;
            for (int i = 0; i < director.Objects.Count && !mothTaken; i++)
            {
                if (director.Objects[i].type != TTObjectType.Moth) continue;
                var o = director.Objects[i];
                player.transform.position = new Vector3((o.x + o.w * 0.5f) / 100f, 0.6f, -(o.y + o.h * 0.5f) / 100f);
                for (int k = 0; k < 20; k++) simulate.Invoke(director, new object[] { Step });
                mothTaken = director.MothsCollected > 0;
            }
            r.AppendLine($"  glowmoths can be collected: {mothTaken}");

            // Does a lens ever see you if you stand in it badly mismatched?
            director.LoadLevel();
            director.Grace = 0f;
            float seen = 0f;
            for (int i = 0; i < director.Objects.Count && seen <= 0f; i++)
            {
                if (director.Objects[i].type != TTObjectType.Camera) continue;
                var o = director.Objects[i];
                player.transform.position = new Vector3((o.x + o.w * 0.5f) / 100f, 0.6f, -(o.y + o.h * 0.5f) / 100f);
                for (int k = 0; k < 180; k++)
                {
                    director.PTemp = 100f;                 // as mismatched as it gets
                    director.Grace = 0f;
                    simulate.Invoke(director, new object[] { Step });
                    seen = Mathf.Max(seen, director.MaxDetection);
                }
            }
            r.AppendLine($"  lenses detect a mismatched lizard: {seen > 0.01f} (peak {seen:F2})");

            Shot(levelName, player.transform);

            bool ok = (director.Enemies.Count == 0 || wardensMoved > 0) && fallKills && mothTaken
                      && (director.Enemies.Count == 0 || caught);
            r.AppendLine("  => " + (ok ? "OK" : "PROBLEM"));
            return ok;
        }

        static void Shot(string name, Transform target)
        {
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;
            cam.transform.position = target.position - Vector3.forward * 9f + Vector3.up * 6.5f;
            cam.transform.rotation = Quaternion.LookRotation(
                ((target.position + Vector3.forward * 3f) - cam.transform.position).normalized, Vector3.up);

            var rt = new RenderTexture(1100, 620, 24) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1100, 620, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1100, 620), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes("_probe/play/" + name + ".png", tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}

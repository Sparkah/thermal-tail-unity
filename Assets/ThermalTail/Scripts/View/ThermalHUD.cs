using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Port of thermalRGB(t): a two-stop ramp, cold blue to teal at 45, teal to hot red at 100.
    /// The lizard, the HUD and (at P1) the shader all read this one authority.
    /// </summary>
    public static class ThermalPalette
    {
        public static Color Sample(float t)
        {
            t = TTMath.Clamp(t, 0f, 100f);
            if (t < 45f)
            {
                float q = t / 45f;
                return new Color((47f + (32f - 47f) * q) / 255f,
                                 (151f + (222f - 151f) * q) / 255f,
                                 (255f + (181f - 255f) * q) / 255f);
            }
            float p = (t - 45f) / 55f;
            return new Color((32f + (255f - 32f) * p) / 255f,
                             (222f + (76f - 222f) * p) / 255f,
                             (181f + (62f - 181f) * p) / 255f);
        }
    }

    /// <summary>
    /// Placeholder IMGUI HUD for P0. Deliberately asset-free so the greybox is playable
    /// without any UI wiring; P1 rebuilds this in UI Toolkit per the plan.
    /// </summary>
    public class ThermalHUD : MonoBehaviour
    {
        public ThermalDirector Director;
        bool _levelSelect;
        GUIStyle _label, _small, _big;

        void Awake()
        {
            if (Director == null) Director = FindFirstObjectByType<ThermalDirector>();
        }

        void Update()
        {
            if (TTInput.PressedThisFrame("l")) _levelSelect = !_levelSelect;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (Director == null || Director.Settings == null) return;
            EnsureStyles();

            const float pad = 12f;
            float w = 260f;
            GUI.Box(new Rect(pad, pad, w, 168f), GUIContent.none);
            var r = new Rect(pad + 8f, pad + 6f, w - 16f, 20f);

            GUI.Label(r, Director.Settings.LevelName, _label); r.y += 20f;

            var tempCol = ThermalPalette.Sample(Director.PTemp);
            var old = GUI.color;
            GUI.color = tempCol;
            GUI.Label(r, string.Format("BODY {0:0.0}   AIR {1:0.0}   GAP {2:0.0}",
                Director.PTemp, Director.PAmbient, Director.Mismatch), _small);
            GUI.color = old;
            r.y += 18f;

            Bar(new Rect(r.x, r.y, r.width, 10f), Director.PFocus / 100f, new Color(0.45f, 0.95f, 0.9f));
            r.y += 12f;
            GUI.Label(r, string.Format("FOCUS {0:0}{1}", Director.PFocus,
                Director.PMaskLocked ? "  (DEPLETED)" : (Director.PMatching ? "  (MASKING)" : "")), _small);
            r.y += 18f;

            GUI.Label(r, string.Format("GLOWMOTHS {0}/{1}   LIVES {2}   SCORE {3}",
                Director.MothsCollected, Director.RequiredMoths, TTSession.Lives, TTSession.Score), _small);
            r.y += 18f;

            Bar(new Rect(r.x, r.y, r.width, 10f), Director.MaxDetection, new Color(1f, 0.45f, 0.35f));
            r.y += 12f;
            GUI.Label(r, string.Format("DETECTION {0:0.00}{1}{2}", Director.MaxDetection,
                Director.PHidden ? "   HIDDEN" : "",
                Director.Grace > 0f ? "   GRACE " + Director.Grace.ToString("0.0") : ""), _small);
            r.y += 18f;

            GUI.Label(r, Director.ClimbMode ? "CLIMB  WASD move, SPACE mask, SHIFT strike"
                                            : "SIDE  A/D move, W jump, S mask, SHIFT strike", _small);

            if (Director.ToastTimer > 0f && !string.IsNullOrEmpty(Director.Toast))
                GUI.Label(new Rect(0f, Screen.height * 0.16f, Screen.width, 34f), Director.Toast, _big);

            if (Director.Mode == GameMode.LevelComplete)
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 40f),
                    "LEVEL COMPLETE  +" + Director.LastBonus, _big);
            else if (Director.Mode == GameMode.Failed)
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 40f), "OPERATION FAILED - R to restart", _big);
            else if (Director.Mode == GameMode.Victory)
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 40f), "MISSION COMPLETE", _big);
            else if (Director.Mode == GameMode.Paused)
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 40f), "PAUSED - P to resume", _big);

            if (_levelSelect) DrawLevelSelect();
            else GUI.Label(new Rect(pad, Screen.height - 22f, 400f, 20f), "L level select   R restart   P pause", _small);
        }

        void DrawLevelSelect()
        {
            int count = LevelCatalog.Count;
            float w = 320f, h = 40f + count * 26f;
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, w - 20f, 20f), "SELECT LEVEL", _label);
            for (int i = 0; i < count; i++)
            {
                if (GUI.Button(new Rect(box.x + 10f, box.y + 30f + i * 26f, w - 20f, 22f),
                    (i + 1) + ".  " + LevelCatalog.DisplayName(i)))
                {
                    _levelSelect = false;
                    TTSession.ResetRun(Director.Tuning);
                    LevelCatalog.Load(i);
                }
            }
        }

        static void Bar(Rect r, float t, Color fill)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

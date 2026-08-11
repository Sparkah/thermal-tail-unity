using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// The press-Play loop from section 3.5: open a level scene and run it, without
    /// hunting through the Project window.
    /// </summary>
    public static class TTMenu
    {
        [MenuItem("Tools/Thermal Tail/Play This Level %#p")]
        public static void PlayThisLevel()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            var settings = Object.FindFirstObjectByType<LevelSettings>();
            if (settings == null)
            {
                Debug.LogWarning("[ThermalTail] The open scene is not a Thermal Tail level.");
                return;
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Thermal Tail/Play From Level 1")]
        public static void PlayFromStart()
        {
            if (!OpenLevel(0)) return;
            EditorApplication.isPlaying = true;
        }

        public static bool OpenLevel(int index)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalogAsset>(TTPrefabBuilder.ResourceDir + "/LevelCatalog.asset");
            if (catalog == null || index < 0 || index >= catalog.Levels.Count)
            {
                Debug.LogWarning("[ThermalTail] No level catalog entry at index " + index + ". Run Import Levels JSON first.");
                return false;
            }
            string path = TTPrefabBuilder.LevelDir + "/" + catalog.Levels[index].SceneName + ".unity";
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        /// <summary>
        /// Prints the tuning asset's values against the constants read off the shipping
        /// bytes, so a balance edit that drifts from the browser build is visible.
        /// </summary>
        [MenuItem("Tools/Thermal Tail/Verify Tuning Against Shipping Build")]
        public static void VerifyTuning()
        {
            var t = TTLevelImporter.EnsureTuning();
            var expected = new (string name, float actual, float shipped)[]
            {
                ("MaskPullRate",        t.MaskPullRate,        32f),
                ("MaskFocusDrain",      t.MaskFocusDrain,      14f),
                ("DriftRate",           t.DriftRate,           4.2f),
                ("FocusRegenHidden",    t.FocusRegenHidden,    20f),
                ("FocusRegenOpen",      t.FocusRegenOpen,      2.5f),
                ("PaceBase",            t.PaceBase,            0.55f),
                ("PacePerTemp",         t.PacePerTemp,         0.0095f),
                ("PaceMax",             t.PaceMax,             1.4f),
                ("Gravity",             t.Gravity,             1250f),
                ("TerminalFall",        t.TerminalFall,        780f),
                ("JumpImpulse",         t.JumpImpulse,        -535f),
                ("GroundAccel",         t.GroundAccel,         940f),
                ("AirAccel",            t.AirAccel,            620f),
                ("GroundFriction",      t.GroundFriction,      1050f),
                ("AirFriction",         t.AirFriction,         220f),
                ("MaxSpeed",            t.MaxSpeed,            205f),
                ("MaskedMaxSpeed",      t.MaskedMaxSpeed,      112f),
                ("CoyoteTime",          t.CoyoteTime,          0.12f),
                ("JumpBufferTime",      t.JumpBufferTime,      0.14f),
                ("ClimbMaxSpeed",       t.ClimbMaxSpeed,       196f),
                ("ClimbAccel",          t.ClimbAccel,          1320f),
                ("DetectGainBase",      t.DetectGainBase,      0.3f),
                ("DetectGainPerMismatch", t.DetectGainPerMismatch, 0.034f),
                ("DetectDecaySeen",     t.DetectDecaySeen,     0.85f),
                ("DetectDecayUnseen",   t.DetectDecayUnseen,   0.5f),
                ("LensCooldown",        t.LensCooldown,        3.4f),
                ("AlarmRadius",         t.AlarmRadius,         1700f),
                ("GuardSpeedNormal",    t.GuardSpeedNormal,    58f),
                ("GuardSpeedFast",      t.GuardSpeedFast,      76f),
                ("GuardSpeedSlow",      t.GuardSpeedSlow,      44f),
                ("StrikeReachX",        t.StrikeReachX,        118f),
                ("StrikeReachY",        t.StrikeReachY,        84f),
                ("ClimbStrikeReach",    t.ClimbStrikeReach,    98f),
                ("ExecutionHeat",       t.ExecutionHeat,       5f),
                ("GateTolerance",       t.GateTolerance,       6.5f),
                ("GoalThermalTolerance",t.GoalThermalTolerance,10f),
                ("GraceInitial",        t.GraceInitial,        3.35f),
                ("BlendLatchGap",       t.BlendLatchGap,       8.5f),
                ("LedgeSpeedFast",      t.LedgeSpeedFast,      1.55f),
                ("LedgeSpeedDefault",   t.LedgeSpeedDefault,   0.82f),
            };

            var sb = new StringBuilder("[ThermalTail] Tuning parity vs the shipping browser build\n");
            int bad = 0;
            foreach (var e in expected)
            {
                bool ok = Mathf.Abs(e.actual - e.shipped) < 1e-5f;
                if (!ok) bad++;
                sb.AppendLine(string.Format("{0}  {1,-24} unity={2}  shipped={3}",
                    ok ? "OK  " : "DRIFT", e.name, e.actual, e.shipped));
            }
            sb.AppendLine(bad == 0
                ? "ALL " + expected.Length + " CONSTANTS MATCH"
                : bad + " OF " + expected.Length + " DRIFTED");
            if (bad == 0) Debug.Log(sb.ToString()); else Debug.LogError(sb.ToString());
        }
    }
}

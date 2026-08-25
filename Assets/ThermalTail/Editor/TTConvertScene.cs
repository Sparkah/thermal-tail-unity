using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Converts the open level scene to primitives, in place.
    ///
    /// The old objects are prefab instances whose script no longer exists, so their data is
    /// unreachable through the API - but everything needed is still recoverable: the type is
    /// in the object's name ("004 fern ledge [platform]"), and the footprint is in the
    /// transform, which is still laid out the old flat way. So each one is measured, thrown
    /// away, and rebuilt as a cube on the ground plane with a TTPiece on it.
    ///
    /// Use this instead of re-importing when you have a scene open that you do not want to
    /// lose. It only reads names and transforms; it never needs the missing script.
    /// </summary>
    public static class TTConvertScene
    {
        static readonly Regex NamePattern = new Regex(@"^(\d+)\s+(.*?)\s*\[(\w+)\]\s*$");

        [MenuItem("Tools/Thermal Tail/Convert Open Scene To Primitives")]
        public static void Convert()
        {
            var settings = Object.FindFirstObjectByType<LevelSettings>();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Thermal Tail",
                    "No Level Settings in the open scene. Open a level under Assets/ThermalTail/Levels.", "OK");
                return;
            }

            var root = GameObject.Find("Objects");
            if (root == null)
            {
                EditorUtility.DisplayDialog("Thermal Tail", "No 'Objects' group in this scene.", "OK");
                return;
            }

            int converted = 0, skipped = 0;
            var olds = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root.transform) olds.Add(child);

            foreach (var old in olds)
            {
                var m = NamePattern.Match(old.name);
                if (!m.Success) { skipped++; continue; }

                int index = int.Parse(m.Groups[1].Value);
                var type = TTLevelImporter.ParseType(m.Groups[3].Value);
                if (type == TTObjectType.Unknown) { skipped++; continue; }

                // Recover the authored rect from the flat transform it still carries.
                var p = old.localPosition;
                var s = old.localScale;
                float w = Mathf.Abs(s.x) * TTCoord.PixelsPerUnit;
                float h = Mathf.Abs(s.y) * TTCoord.PixelsPerUnit;
                float x = p.x * TTCoord.PixelsPerUnit - w * 0.5f;
                float y = -p.y * TTCoord.PixelsPerUnit - h * 0.5f;

                var piece = Rebuild(root.transform, type, index, old.name, x, y, w, h);
                Undo.RegisterCreatedObjectUndo(piece.gameObject, "Convert scene");
                Undo.DestroyObjectImmediate(old.gameObject);
                converted++;
            }

            // Lizard and den onto the ground plane, on top of whatever is nearest.
            var lizard = GameObject.Find("Lizard");
            if (lizard != null)
            {
                Undo.RecordObject(lizard.transform, "Convert scene");
                lizard.transform.position = OnNearestPlatform(settings.PlayerStart, 0.24f);
            }
            var den = GameObject.Find("Exit Den");
            if (den != null)
            {
                Undo.RecordObject(den.transform, "Convert scene");
                den.transform.position = OnNearestPlatform(settings.Goal, 0.35f);
            }

            var director = Object.FindFirstObjectByType<ThermalDirector>();
            if (director != null)
            {
                Undo.RecordObject(director, "Convert scene");
                if (lizard != null) director.PlayerView = lizard.transform;
                if (den != null) director.Den = den.transform;
                EditorUtility.SetDirty(director);
            }

            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            Debug.Log($"[ThermalTail] Converted {converted} objects to primitives" +
                      (skipped > 0 ? $", skipped {skipped}" : "") +
                      ". Lizard and den placed on the nearest platform. Press Play.");
        }

        static TTPiece Rebuild(Transform parent, TTObjectType type, int index, string name,
                               float x, float y, float w, float h)
        {
            float height = DefaultHeight(type);
            var go = GameObject.CreatePrimitive(
                type == TTObjectType.Moth ? PrimitiveType.Sphere :
                type == TTObjectType.Guard ? PrimitiveType.Capsule : PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Mathf.Max(0.02f, w / TTCoord.PixelsPerUnit),
                                                  height,
                                                  Mathf.Max(0.02f, h / TTCoord.PixelsPerUnit));
            go.transform.position = new Vector3((x + w * 0.5f) / TTCoord.PixelsPerUnit,
                                                height * 0.5f,
                                                -(y + h * 0.5f) / TTCoord.PixelsPerUnit);

            var piece = go.AddComponent<TTPiece>();
            piece.Type = type;
            piece.SourceIndex = index;
            return piece;
        }

        static Vector3 OnNearestPlatform(Vector2 sourcePoint, float clearance)
        {
            Vector3 want = TTCoord.Point(sourcePoint.x, sourcePoint.y);
            float best = float.MaxValue;
            Vector3 chosen = new Vector3(want.x, clearance, want.z);

            foreach (var d in Object.FindObjectsByType<TTPiece>(FindObjectsSortMode.None))
            {
                if (d.Type != TTObjectType.Platform && d.Type != TTObjectType.MovingPlatform) continue;
                var p = d.transform.position;
                var s = d.transform.lossyScale;
                float hx = Mathf.Abs(s.x) * 0.5f, hz = Mathf.Abs(s.z) * 0.5f;
                var on = new Vector3(Mathf.Clamp(want.x, p.x - hx, p.x + hx),
                                     d.TopY() + clearance,
                                     Mathf.Clamp(want.z, p.z - hz, p.z + hz));
                float dist = Vector2.Distance(new Vector2(want.x, want.z), new Vector2(on.x, on.z));
                if (dist < best) { best = dist; chosen = on; }
            }
            return chosen;
        }

        static float DefaultHeight(TTObjectType t)
        {
            switch (t)
            {
                case TTObjectType.Platform:
                case TTObjectType.MovingPlatform: return 0.5f;
                case TTObjectType.ThermalGate: return 1.9f;
                case TTObjectType.Shelter: return 0.85f;
                case TTObjectType.CoolRock: return 0.55f;
                case TTObjectType.WarmVent: return 0.22f;
                case TTObjectType.SunPatch: return 0.05f;
                case TTObjectType.IceMist:
                case TTObjectType.DryAir:
                case TTObjectType.Wind: return 1.25f;
                case TTObjectType.AmbientZone: return 0.04f;
                case TTObjectType.Thorn: return 0.45f;
                case TTObjectType.Moth: return 0.25f;
                case TTObjectType.Camera: return 2.1f;
                case TTObjectType.Guard: return 0.95f;
                case TTObjectType.Checkpoint: return 0.3f;
                default: return 0.5f;
            }
        }
    }
}

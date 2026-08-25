using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.EditorTools
{
    /// <summary>
    /// Switches a level scene between the two authoring layouts.
    ///
    /// The port was authored as a side elevation: objects stacked up the Y axis, which is
    /// what the scene view still showed after the game moved to a ground plane. Drags
    /// landed in SourceX/SourceY correctly, but at a right angle to where Play Mode put
    /// them, so moving anything felt like it did nothing. This makes the scene view agree
    /// with the game.
    ///
    /// The authored rects never change here. Only the transforms move, and the flag on
    /// LevelSettings records which axes those transforms now mean, so a later drag is read
    /// back the same way it was made.
    /// </summary>
    public static class TTAuthorLayout
    {
        [MenuItem("ThermalTail/Author/Lay Out In 3D %#3")]
        public static void LayOutInGround() => Relayout(true);

        [MenuItem("ThermalTail/Author/Restore Flat 2D Layout")]
        public static void LayOutFlat() => Relayout(false);

        static void Relayout(bool ground)
        {
            var settings = Object.FindFirstObjectByType<LevelSettings>();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("ThermalTail",
                    "No LevelSettings in the open scene. Open a level under Assets/ThermalTail/Levels first.", "OK");
                return;
            }

            Undo.RecordObject(settings, "Relayout level");
            settings.LaidOutInGround = ground;
            settings.PublishAuthorPlane();

            var objects = Object.FindObjectsByType<TTObject>(FindObjectsSortMode.None);
            foreach (var tt in objects)
            {
                Undo.RecordObject(tt.transform, "Relayout level");
                Undo.RecordObject(tt, "Relayout level");

                // Seed a height on the way into 3D so the box has a sensible thickness to
                // grab; on the way back out it is irrelevant, so it is left alone.
                if (ground && tt.HeightOverride <= 0.001f)
                    tt.HeightOverride = TTView.StandingHeight(tt.Type, settings.IsClimb);

                tt.SyncToTransform();
                EditorUtility.SetDirty(tt);
            }

            MoveLoose("Lizard", settings.PlayerStart, ground ? TTView.PlayerRideHeight : 0f);
            MoveLoose("Exit Den", settings.Goal, ground ? TTView.DeckHeight + 0.25f : 0.5f);

            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            Debug.Log($"[ThermalTail] {settings.LevelName}: {objects.Length} objects laid out in " +
                      (ground ? "3D ground plane" : "flat 2D") + ". Authored rects unchanged.");
        }

        static void MoveLoose(string name, Vector2 source, float lift)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            Undo.RecordObject(go.transform, "Relayout level");
            go.transform.position = TTCoord.AuthorPoint(source.x, source.y, lift);
        }
    }
}

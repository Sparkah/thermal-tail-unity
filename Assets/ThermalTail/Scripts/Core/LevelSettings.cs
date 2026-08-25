using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Level properties that were never really objects: the ambient / startTemp / quota /
    /// climbWall markers the browser build read through metaValue(), plus the world size,
    /// spawn, goal and Pit's designer notes.
    /// </summary>
    [ExecuteAlways]
    public class LevelSettings : MonoBehaviour
    {
        [Header("Identity")]
        public string LevelName = "Untitled";
        [Tooltip("0-based position in the level order. Seeds the deterministic scatter RNG.")]
        public int LevelIndex;

        [Header("World (source pixels)")]
        public float Width = 3000f;
        public float Height = 1200f;

        [Header("Climate")]
        [Tooltip("Air temperature, 0-100. Overridden locally by any Climate Zone.")]
        public float Ambient = 28f;
        [Tooltip("Body temperature at spawn.")]
        public float StartTemp = 40f;

        [Header("Objective")]
        [Tooltip("Glowmoths required before the exit den will open.")]
        public int Quota = 3;

        [Header("Mode")]
        [Tooltip("True when the level carries a Climb Wall marker: top-down 8-way movement, no gravity.")]
        public bool IsClimb;

        [Header("Import data only - the game never reads these")]
        [Tooltip("Where the browser build started the lizard. The importer uses it to place " +
                 "the Lizard object once; after that the object's own position is the spawn.")]
        public Vector2 PlayerStart = new Vector2(130f, 800f);
        [Tooltip("Same, for the den.")]
        public Vector2 Goal = new Vector2(2870f, 825f);

        [Header("Designer notes")]
        [TextArea(2, 5)]
        public string DesignerNotes = "";

        void OnDrawGizmos()
        {
            // Level bounds.
            Gizmos.color = new Color(0.4f, 0.6f, 0.8f, 0.5f);
            Gizmos.DrawWireCube(
                TTCoord.RectCenter(0f, 0f, Width, Height),
                TTCoord.RectScale(Width, Height, 0.05f));
        }
    }
}

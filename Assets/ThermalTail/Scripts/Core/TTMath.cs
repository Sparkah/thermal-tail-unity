using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Direct transcriptions of the shipping build's helpers. Kept as a separate
    /// static class so the simulation reads the same way as the original source.
    /// </summary>
    public static class TTMath
    {
        public static float Clamp(float v, float a, float b) => Mathf.Max(a, Mathf.Min(b, v));

        public static float MoveToward(float v, float target, float amount)
        {
            if (v < target) return Mathf.Min(target, v + amount);
            if (v > target) return Mathf.Max(target, v - amount);
            return target;
        }

        public static float Mod(float v, float m) => ((v % m) + m) % m;

        public static float AngleToward(float a, float b, float t)
        {
            float d = Mod(b - a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            return a + d * Clamp(t, 0f, 1f);
        }

        public static float Hypot(float x, float y) => Mathf.Sqrt(x * x + y * y);
    }

    /// <summary>
    /// xorshift32 seeded per level index, matching
    /// rngState = (0x6d2b79f5 ^ (index+1)*2654435761) >>> 0.
    /// Decorative scatter stays stable across runs, as in the browser build.
    /// </summary>
    public class TTRandom
    {
        uint _state;

        public TTRandom(int levelIndex)
        {
            unchecked { _state = 0x6d2b79f5u ^ ((uint)(levelIndex + 1) * 2654435761u); }
        }

        public float Next()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state / 4294967296f;
            }
        }
    }

    /// <summary>Which Unity plane the source-pixel level is laid out on.</summary>
    public enum ViewPlane
    {
        /// <summary>The original port: the level stands up in XY, depth runs along Z.</summary>
        Flat2D,
        /// <summary>Floor plan: source x is world X, source y is world Z, depth becomes height in Y.</summary>
        Ground3D
    }

    /// <summary>
    /// Source pixels to Unity units. The simulation runs in source pixels so every
    /// tuning constant stays byte-identical to the shipping build; conversion
    /// happens only at the presentation boundary.
    ///
    /// <see cref="Plane"/> reinterprets that boundary. In Ground3D the authored level is
    /// read as a floor plan rather than an elevation: the same rects land flat on the XZ
    /// ground and the old view depth extrudes them upward into standing geometry, which is
    /// what lets a chase camera sit behind the lizard. Nothing in the simulation changes -
    /// it still runs on (x, y) source pixels - so every tuning constant still holds.
    /// </summary>
    public static class TTCoord
    {
        public const float PixelsPerUnit = 100f;

        /// <summary>Set once at load from ThermalDirector. Read by every placement helper and gizmo.</summary>
        public static ViewPlane Plane = ViewPlane.Ground3D;

        public static bool IsGround => Plane == ViewPlane.Ground3D;

        /// <summary>
        /// Source point to world. Canvas y grows down, so it negates either way; `lift` is
        /// depth along Z in Flat2D and height above the floor in Ground3D.
        /// </summary>
        public static Vector3 Point(float px, float py, float lift = 0f)
            => IsGround
                ? new Vector3(px / PixelsPerUnit, lift, -py / PixelsPerUnit)
                : new Vector3(px / PixelsPerUnit, -py / PixelsPerUnit, lift);

        /// <summary>Top-left anchored source rect to a centred Unity position.</summary>
        public static Vector3 RectCenter(float x, float y, float w, float h, float lift = 0f)
            => Point(x + w * 0.5f, y + h * 0.5f, lift);

        /// <summary>`thickness` is the Z depth in Flat2D and the standing height in Ground3D.</summary>
        public static Vector3 RectScale(float w, float h, float thickness)
            => IsGround
                ? new Vector3(Mathf.Abs(w) / PixelsPerUnit, Mathf.Max(0.01f, thickness), Mathf.Abs(h) / PixelsPerUnit)
                : new Vector3(Mathf.Abs(w) / PixelsPerUnit, Mathf.Abs(h) / PixelsPerUnit, thickness);

        // ---- authoring space ----
        //
        // Authoring is ALWAYS flat, whatever Plane is set to. The scene view is the
        // authoring surface: a designer drags a rect around an elevation and TTObject reads
        // the transform back into SourceX/SourceY. If that read followed Plane, then merely
        // opening a scene while the ground mapping happened to be active would reinterpret
        // every authored transform through the wrong axes and overwrite the level with
        // nonsense. Ground3D is a presentation mapping and nothing more.

        public static Vector3 FlatPoint(float px, float py, float z = 0f)
            => new Vector3(px / PixelsPerUnit, -py / PixelsPerUnit, z);

        public static Vector3 FlatRectCenter(float x, float y, float w, float h, float z = 0f)
            => FlatPoint(x + w * 0.5f, y + h * 0.5f, z);

        public static Vector3 FlatRectScale(float w, float h, float depth)
            => new Vector3(Mathf.Abs(w) / PixelsPerUnit, Mathf.Abs(h) / PixelsPerUnit, depth);

        public static float FlatPixelsX(Vector3 localPos) => localPos.x * PixelsPerUnit;
        public static float FlatPixelsY(Vector3 localPos) => -localPos.y * PixelsPerUnit;
        public static float FlatPixelsW(Vector3 localScale) => Mathf.Abs(localScale.x) * PixelsPerUnit;
        public static float FlatPixelsH(Vector3 localScale) => Mathf.Abs(localScale.y) * PixelsPerUnit;

        /// <summary>Unit vector the source +x axis points along in world space.</summary>
        public static Vector3 AxisX => Vector3.right;
        /// <summary>Unit vector the source +y axis (canvas down) points along in world space.</summary>
        public static Vector3 AxisY => IsGround ? Vector3.back : Vector3.down;
        /// <summary>Unit vector that `lift` pushes along: up off the floor, or toward the viewer.</summary>
        public static Vector3 AxisLift => IsGround ? Vector3.up : Vector3.forward;

        /// <summary>A source-space direction (dx, dy) as a world direction on the play plane.</summary>
        public static Vector3 Direction(float dx, float dy) => AxisX * dx + AxisY * dy;
    }
}

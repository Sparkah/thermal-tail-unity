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

    /// <summary>
    /// Source pixels to Unity units. The simulation runs in source pixels so every
    /// tuning constant stays byte-identical to the shipping build; conversion
    /// happens only at the presentation boundary.
    /// </summary>
    public static class TTCoord
    {
        public const float PixelsPerUnit = 100f;

        /// <summary>Canvas y grows down, Unity y grows up.</summary>
        public static Vector3 Point(float px, float py, float z = 0f)
            => new Vector3(px / PixelsPerUnit, -py / PixelsPerUnit, z);

        /// <summary>Top-left anchored source rect to a centred Unity position.</summary>
        public static Vector3 RectCenter(float x, float y, float w, float h, float z = 0f)
            => new Vector3((x + w * 0.5f) / PixelsPerUnit, -(y + h * 0.5f) / PixelsPerUnit, z);

        public static Vector3 RectScale(float w, float h, float depth)
            => new Vector3(Mathf.Abs(w) / PixelsPerUnit, Mathf.Abs(h) / PixelsPerUnit, depth);

        public static float ToPixelsX(float unityX) => unityX * PixelsPerUnit;
        public static float ToPixelsY(float unityY) => -unityY * PixelsPerUnit;
    }
}

using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Presentation-only metrics for the ground-plane build.
    ///
    /// The authored levels only ever carried flat rects, because the browser build drew
    /// them as flat rects. Read as a floor plan those rects still say where a thing is and
    /// how much floor it covers, but nothing about how tall it stands. This table supplies
    /// the missing third dimension per object type so the level reads as a place rather
    /// than a diagram, without touching a single authored value.
    /// </summary>
    public static class TTView
    {
        /// <summary>
        /// Standing height in Unity units for the greybox proxy of a type.
        ///
        /// `planAuthored` says whether the level's rects were drawn as a floor plan or as a
        /// side elevation. It matters for ledges and only for ledges. A climb level's
        /// platforms really are obstacles seen from above, so they stand up as walls. A side
        /// level's platforms are the floor you used to run along - stand those up and the
        /// lizard spawns sealed inside the shelf it was meant to be standing on - so in plan
        /// view they lie flat as decking instead.
        /// </summary>
        public static float StandingHeight(TTObjectType type, bool planAuthored = true)
        {
            if (!planAuthored && (type == TTObjectType.Platform || type == TTObjectType.MovingPlatform))
                return 0.07f;

            switch (type)
            {
                case TTObjectType.Platform: return 1.15f;
                case TTObjectType.MovingPlatform: return 0.9f;
                case TTObjectType.ThermalGate: return 1.9f;
                case TTObjectType.Shelter: return 0.85f;
                case TTObjectType.CoolRock: return 0.55f;
                case TTObjectType.WarmVent: return 0.22f;
                case TTObjectType.SunPatch: return 0.05f;
                case TTObjectType.IceMist: return 1.25f;
                case TTObjectType.DryAir: return 1.25f;
                case TTObjectType.Wind: return 1.25f;
                case TTObjectType.AmbientZone: return 0.04f;
                case TTObjectType.Thorn: return 0.45f;
                case TTObjectType.Moth: return 0.18f;
                case TTObjectType.Camera: return 2.1f;
                case TTObjectType.Guard: return 0.95f;
                case TTObjectType.Checkpoint: return 0.3f;
                default: return 0.5f;
            }
        }

        /// <summary>Volumes read better as translucent air than as solid boxes.</summary>
        public static bool IsAirVolume(TTObjectType type) =>
            type == TTObjectType.IceMist || type == TTObjectType.DryAir ||
            type == TTObjectType.Wind || type == TTObjectType.AmbientZone ||
            type == TTObjectType.SunPatch;

        /// <summary>How far a glowmoth floats off the floor, in Unity units.</summary>
        public const float MothFloatHeight = 0.55f;

        /// <summary>Height of the lizard's centre above the floor when grounded.</summary>
        public const float PlayerRideHeight = 0.24f;

        /// <summary>Height a warden's centre sits at.</summary>
        public const float GuardRideHeight = 0.5f;
    }
}

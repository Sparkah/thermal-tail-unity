using UnityEngine;

namespace ThermalTail
{
    public enum TTObjectType
    {
        Unknown = 0,
        Platform,
        MovingPlatform,
        Moth,
        Camera,
        Guard,
        Shelter,
        CoolRock,
        IceMist,
        WarmVent,
        SunPatch,
        Thorn,
        Checkpoint,
        ThermalGate,
        Wind,
        DryAir,
        AmbientZone,
        // Level metadata markers - read once at load, never simulated as objects.
        Ambient,
        StartTemp,
        Quota,
        ClimbWall
    }

    public enum LensFacing { Left = -1, Right = 1 }
    public enum SweepRate { Slow, Normal, Fast }
    public enum SweepArc { Narrow, Normal, Wide }
    public enum ClimbAim { Down, Up, Left, Right }
    public enum GuardSpeed { Slow, Normal, Fast }
    public enum PatrolAxis { Horizontal, Vertical }

    /// <summary>
    /// Tags a plain Unity primitive as something the simulation cares about.
    ///
    /// Where it is and how big it is comes from the transform - drag the cube, that is
    /// where the wall is; scale it, that is its size and its height. This component
    /// carries only what a transform cannot say: what the thing IS, and the handful of
    /// numbers its behaviour needs.
    ///
    /// The predecessor kept a parallel copy of position and size in source pixels and
    /// synced it against the transform in both directions. That meant a drag in the scene
    /// view and a value in the inspector could disagree, and the simulation trusted the
    /// copy - so moving an object appeared to do nothing at all. The transform is the only
    /// authority now, read once at load.
    /// </summary>
    [DisallowMultipleComponent]
    public class TTPiece : MonoBehaviour
    {
        [Tooltip("What this primitive is. Everything else about it comes from the transform.")]
        public TTObjectType Type = TTObjectType.Platform;

        [Tooltip("Meaning depends on Type: lens threshold, guard patrol span, ledge amplitude, " +
                 "gate target temperature, source rate, ambient degrees, quota count.")]
        public float Value;

        [Header("Surveillance lens")]
        public LensFacing Facing = LensFacing.Left;
        public SweepRate Sweep = SweepRate.Normal;
        public SweepArc Arc = SweepArc.Normal;
        public ClimbAim ClimbBaseAim = ClimbAim.Down;
        public bool AlwaysOn;
        public bool Pulse;

        [Header("Warden")]
        public GuardSpeed Speed = GuardSpeed.Normal;
        public PatrolAxis Patrol = PatrolAxis.Horizontal;

        [Header("Moving ledge")]
        public PatrolAxis LedgeAxis = PatrolAxis.Horizontal;
        public bool LedgeFast;

        [Tooltip("Ordering that the detection meters, gate states and lens phase offsets key " +
                 "off. Left at -1, the director assigns it by scene order at load.")]
        public int SourceIndex = -1;

        public bool IsMetadata =>
            Type == TTObjectType.Ambient || Type == TTObjectType.StartTemp ||
            Type == TTObjectType.Quota || Type == TTObjectType.ClimbWall;

        /// <summary>
        /// The rect the simulation runs against, in source pixels, read straight off the
        /// transform. The level is a side elevation exactly as the original was: x across,
        /// y down the screen, and the box's Z scale is depth toward the camera, which the
        /// simulation never looks at.
        /// </summary>
        public Rect Footprint()
        {
            var p = transform.position;
            var s = transform.lossyScale;
            float w = Mathf.Abs(s.x) * TTCoord.PixelsPerUnit;
            float h = Mathf.Abs(s.y) * TTCoord.PixelsPerUnit;
            return new Rect(p.x * TTCoord.PixelsPerUnit - w * 0.5f,
                            -p.y * TTCoord.PixelsPerUnit - h * 0.5f,
                            w, h);
        }

        /// <summary>How far the box sticks out toward the camera. Presentation only.</summary>
        public float ViewDepth => Mathf.Abs(transform.lossyScale.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Type == TTObjectType.Platform || Type == TTObjectType.MovingPlatform
                ? new Color(0.55f, 0.85f, 1f, 0.9f)
                : new Color(1f, 0.8f, 0.35f, 0.9f);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}

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
    /// One authored level object. Carries the source-pixel rect the simulation runs
    /// against plus typed behaviour fields decoded from the original label at import.
    ///
    /// In the browser build the label doubled as a behaviour switch (substring match),
    /// so renaming a lens silently changed its cycle. Here DisplayName is inert and the
    /// enums below are the authority, which is the whole point of section 3.2.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TTObject : MonoBehaviour
    {
        [Header("Identity")]
        public TTObjectType Type = TTObjectType.Platform;
        [Tooltip("Display name only. Changing this changes nothing about behaviour.")]
        public string DisplayName = "";
        [Tooltip("Index in the authored object list. Preserves the original ordering, which the detection and gate state arrays key off.")]
        public int SourceIndex = -1;

        [Header("Source rect (pixels, top-left anchored)")]
        public float SourceX;
        public float SourceY;
        public float SourceW = 40f;
        public float SourceH = 40f;

        [Header("Value")]
        [Tooltip("Meaning depends on type: lens mismatch threshold, guard patrol span, ledge amplitude, gate target temperature, source rate, ambient degrees, quota count.")]
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

        /// <summary>Depth of the greybox proxy in Unity units. Presentation only.</summary>
        [HideInInspector] public float ViewDepth = 1f;

        /// <summary>
        /// Set while something places these transforms for presentation rather than
        /// authoring - the runtime ground-plane layout, or an editor screenshot pass. The
        /// transform is not the designer's intent in those moments, so it must not be read
        /// back into the authored rect.
        /// </summary>
        public static bool SuppressAuthoring;

        void OnValidate()
        {
            if (Application.isPlaying || SuppressAuthoring) return;
            SyncFromTransform();
        }

        /// <summary>
        /// Edit mode: the transform is the authoring surface, so dragging an object in the
        /// scene view writes back into the source rect the simulation reads.
        ///
        /// Only adopts a transform that actually differs from the authored rect. Without that
        /// guard a domain reload would round-trip every rect through the transform, and a
        /// moving ledge - whose transform is displaced by its sine at runtime - would have its
        /// authored position quietly overwritten.
        /// </summary>
        public void SyncFromTransform()
        {
            var p = transform.localPosition;
            var s = transform.localScale;

            var expectedPos = TTCoord.FlatRectCenter(SourceX, SourceY, SourceW, SourceH, ViewDepth * 0.5f);
            var expectedScale = TTCoord.FlatRectScale(SourceW, SourceH, Mathf.Max(0.05f, ViewDepth));
            const float eps = 1e-4f;
            if (Mathf.Abs(p.x - expectedPos.x) < eps && Mathf.Abs(p.y - expectedPos.y) < eps &&
                Mathf.Abs(s.x - expectedScale.x) < eps && Mathf.Abs(s.y - expectedScale.y) < eps)
                return;

            float w = TTCoord.FlatPixelsW(s);
            float h = TTCoord.FlatPixelsH(s);
            SourceW = w;
            SourceH = h;
            SourceX = TTCoord.FlatPixelsX(p) - w * 0.5f;
            SourceY = TTCoord.FlatPixelsY(p) - h * 0.5f;
        }

        /// <summary>Place the greybox proxy from the authored source rect.</summary>
        public void SyncToTransform()
        {
            transform.localPosition = TTCoord.FlatRectCenter(SourceX, SourceY, SourceW, SourceH, ViewDepth * 0.5f);
            transform.localScale = TTCoord.FlatRectScale(SourceW, SourceH, Mathf.Max(0.05f, ViewDepth));
        }

        public bool IsMetadata =>
            Type == TTObjectType.Ambient || Type == TTObjectType.StartTemp ||
            Type == TTObjectType.Quota || Type == TTObjectType.ClimbWall;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(
                TTCoord.FlatRectCenter(SourceX, SourceY, SourceW, SourceH),
                TTCoord.FlatRectScale(SourceW, SourceH, 0.1f));
        }

        void OnDrawGizmos()
        {
            switch (Type)
            {
                case TTObjectType.Camera:
                    DrawLensGizmo();
                    break;
                case TTObjectType.Guard:
                    DrawGuardGizmo();
                    break;
                case TTObjectType.MovingPlatform:
                    DrawLedgeGizmo();
                    break;
            }
        }

        void DrawLensGizmo()
        {
            // Beam wedge at its authored extent, matching cameraBeamHit.
            float range = Mathf.Max(80f, Mathf.Abs(SourceW));
            float height = Mathf.Max(100f, Mathf.Abs(SourceH));
            float dir = (float)(int)Facing;
            float apexY = SourceY + 25f;
            Vector3 apex = TTCoord.FlatPoint(SourceX, apexY);
            Vector3 far0 = TTCoord.FlatPoint(SourceX + dir * range, SourceY + height * 0.34f);
            Vector3 far1 = TTCoord.FlatPoint(SourceX + dir * range, SourceY + height);
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.8f);
            Gizmos.DrawLine(apex, far0);
            Gizmos.DrawLine(apex, far1);
            Gizmos.DrawLine(far0, far1);
        }

        void DrawGuardGizmo()
        {
            float span = TTMath.Clamp(Mathf.Abs(Value) == 0f ? 220f : Mathf.Abs(Value), 60f, 1400f);
            Vector3 a, b;
            if (Patrol == PatrolAxis.Vertical)
            {
                a = TTCoord.FlatPoint(SourceX, SourceY - span * 0.5f);
                b = TTCoord.FlatPoint(SourceX, SourceY + span * 0.5f);
            }
            else
            {
                a = TTCoord.FlatPoint(SourceX - span * 0.5f, SourceY);
                b = TTCoord.FlatPoint(SourceX + span * 0.5f, SourceY);
            }
            Gizmos.color = new Color(1f, 0.55f, 0.35f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.12f);
            Gizmos.DrawWireSphere(b, 0.12f);
        }

        void DrawLedgeGizmo()
        {
            float amp = TTMath.Clamp(Mathf.Abs(Value), 0f, 600f);
            Vector3 c = TTCoord.FlatRectCenter(SourceX, SourceY, SourceW, SourceH);
            Vector3 off = LedgeAxis == PatrolAxis.Vertical
                ? new Vector3(0f, amp / TTCoord.PixelsPerUnit, 0f)
                : new Vector3(amp / TTCoord.PixelsPerUnit, 0f, 0f);
            Gizmos.color = new Color(0.6f, 1f, 0.8f, 0.8f);
            Gizmos.DrawLine(c - off, c + off);
        }
    }
}

using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Camera rig for the ground-plane build.
    ///
    /// Cinemachine is not in this project's manifest and P0 forbids adding packages, so the
    /// smoothing is done here directly, reusing the shipping build's exponential constants
    /// (kx = 1-exp(-6.5 dt), ky = 1-exp(-4.2 dt)) so the follow feels like the original.
    ///
    /// The mode is the thing worth playing with. Chase sits behind and above on a fixed
    /// heading, which keeps the controls unambiguous. ChaseTurning swings round behind the
    /// lizard as it turns, which is the over-the-shoulder runner framing, and it publishes
    /// <see cref="InputYaw"/> so the simulation can steer relative to the camera instead of
    /// the map - without that the stick fights the swing every time the lizard rounds a corner.
    /// </summary>
    public class FollowCameraRig : MonoBehaviour
    {
        public enum RigMode
        {
            /// <summary>Behind and above on a fixed heading. Readable, no disorientation.</summary>
            Chase,
            /// <summary>Swings behind the lizard's heading. Runner framing, camera-relative steering.</summary>
            ChaseTurning,
            /// <summary>High and steep, the whole floor plan in view.</summary>
            Overhead,
            /// <summary>The original flat side-on port. Only meaningful with GroundPlay off.</summary>
            Flat2D
        }

        public ThermalDirector Director;

        [Header("Mode")]
        public RigMode Mode = RigMode.Chase;

        [Header("Chase framing (Unity units)")]
        [Tooltip("How far behind the lizard the camera sits.")]
        public float ChaseDistance = 8.5f;
        [Tooltip("How high above the floor.")]
        public float ChaseHeight = 3.6f;
        [Tooltip("How far above the lizard the camera aims, so it looks ahead rather than at its feet.")]
        public float ChaseLookAhead = 4.5f;
        public float ChaseFov = 55f;
        [Tooltip("Floor on the camera height when it has to pull in close to a level edge.")]
        public float MinChaseHeight = 1.9f;
        [Tooltip("How fast the rig swings round to a new heading, in ChaseTurning. Lower is lazier.")]
        public float TurnDamping = 3.2f;

        [Header("Overhead framing")]
        public float OverheadHeight = 15f;
        public float OverheadPitch = 68f;
        public float OverheadFov = 50f;

        [Header("Flat2D framing (original port)")]
        public float SideDistance = 16f;
        public float ClimbDistance = 13f;
        public float SideFov = 42f;
        public float ClimbFov = 46f;

        /// <summary>
        /// Yaw the simulation should rotate stick input by, in source-space radians.
        /// Every ground mode reports its own heading, fixed or swinging, so that "forward"
        /// always means forward on screen. Only the flat 2D fallback reports zero.
        /// </summary>
        public float InputYaw { get; private set; }

        Camera _cam;
        float _fx, _fy;          // smoothed follow target, source pixels
        float _lift;             // smoothed height off the floor, source pixels
        Vector3 _forward = Vector3.forward;
        /// <summary>
        /// The heading Chase holds. Latched from the lizard's spawn facing, which points at
        /// the den, so the fixed camera looks down the level instead of at whichever wall
        /// world +Z happens to hit.
        /// </summary>
        Vector3 _fixedForward = Vector3.forward;
        bool _snapped;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (Director == null) Director = FindFirstObjectByType<ThermalDirector>();
        }

        public void SnapTo(float targetXPixels, float targetYPixels)
        {
            _fx = targetXPixels;
            _fy = targetYPixels;
            _lift = Director != null ? Director.PLift : 0f;
            _forward = HeadingNow();
            _fixedForward = _forward;
            _snapped = true;
            Apply();
        }

        void LateUpdate()
        {
            if (Director == null || Director.Settings == null || Director.Tuning == null) return;

            float tx = Director.ViewTargetX();
            float ty = Director.ViewTargetY();

            if (!_snapped)
            {
                _fx = tx; _fy = ty;
                _lift = Director.PLift;
                _forward = HeadingNow();
                _fixedForward = _forward;
                _snapped = true;
            }
            else
            {
                float dt = Time.deltaTime;
                float kx = 1f - Mathf.Exp(-Director.Tuning.CameraFollowKx * dt);
                float ky = 1f - Mathf.Exp(-Director.Tuning.CameraFollowKy * dt);
                _fx += (tx - _fx) * kx;
                _fy += (ty - _fy) * ky;
                _lift += (Director.PLift - _lift) * kx;

                if (Mode == RigMode.ChaseTurning)
                {
                    var want = HeadingNow();
                    float kt = 1f - Mathf.Exp(-TurnDamping * dt);
                    _forward = Vector3.Slerp(_forward, want, kt);
                    if (_forward.sqrMagnitude < 1e-6f) _forward = want;
                    _forward.y = 0f;
                    _forward.Normalize();
                }
                else _forward = _fixedForward;
            }
            Apply();
        }

        /// <summary>
        /// How far back the rig can sit along -fwd from the lizard and still be inside the
        /// arena. The lizard spawns hard against an edge on most levels, so the authored
        /// distance would put the eye outside the boundary wall.
        ///
        /// This returns a shorter distance rather than a moved eye on purpose. Sliding the
        /// eye sideways to get it inside was the earlier fix and it was wrong: the aim slid
        /// with it, so at spawn the camera looked past the lizard and Tim could not see it.
        /// Dolling straight in keeps the lizard dead centre, whatever the level geometry.
        /// </summary>
        public static float FittedDistance(Vector3 focus, Vector3 fwd, LevelSettings settings,
                                           float wanted, float minimum = 2.2f, float margin = 1.2f)
        {
            if (settings == null || !TTCoord.IsGround) return wanted;
            // The apron is built ground too, so the eye may use it.
            float a = TTView.ArenaApron;
            float w = settings.Width / TTCoord.PixelsPerUnit;
            float h = settings.Height / TTCoord.PixelsPerUnit;

            float d = wanted;
            d = Mathf.Min(d, AxisLimit(focus.x, -fwd.x, -a + margin, w + a - margin, wanted));
            d = Mathf.Min(d, AxisLimit(focus.z, -fwd.z, -h - a + margin, a - margin, wanted));
            return Mathf.Clamp(d, Mathf.Min(minimum, wanted), wanted);
        }

        /// <summary>Largest travel along one axis before `from + step * d` leaves [lo, hi].</summary>
        static float AxisLimit(float from, float step, float lo, float hi, float wanted)
        {
            if (Mathf.Abs(step) < 1e-4f) return wanted;
            float bound = step > 0f ? hi : lo;
            float d = (bound - from) / step;
            return d < 0f ? 0f : d;
        }

        /// <summary>The lizard's current heading as a flat world direction.</summary>
        Vector3 HeadingNow()
        {
            if (Director == null) return Vector3.forward;
            // PAngle is a source-space angle; source +y runs into -Z on the ground plane.
            var v = TTCoord.Direction(Mathf.Cos(Director.PAngle), Mathf.Sin(Director.PAngle));
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        void Apply()
        {
            if (Director == null || Director.Settings == null) return;

            if (Mode == RigMode.Flat2D || !TTCoord.IsGround) { ApplyFlat(); return; }

            Vector3 focus = TTCoord.Point(_fx, _fy, _lift / TTCoord.PixelsPerUnit);

            if (Mode == RigMode.Overhead)
            {
                InputYaw = Mathf.Atan2(_fixedForward.x, _fixedForward.z);
                float pitch = Mathf.Clamp(OverheadPitch, 20f, 89f);
                float horiz = OverheadHeight / Mathf.Tan(Mathf.Deg2Rad * pitch);
                transform.position = focus + Vector3.up * OverheadHeight - _fixedForward * horiz;
                transform.rotation = Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up);
                if (_cam != null) _cam.fieldOfView = OverheadFov;
                return;
            }

            // Chase / ChaseTurning.
            Vector3 fwd = Mode == RigMode.ChaseTurning ? _forward : _fixedForward;
            InputYaw = Mathf.Atan2(fwd.x, fwd.z);

            // Pull in along the same ray when the arena is tight, and scale the height and
            // the lead by the same factor, so the framing tightens but the angle - and the
            // lizard's place in the frame - stay put.
            float d = FittedDistance(focus, fwd, Director.Settings, ChaseDistance);
            float scale = ChaseDistance > 0.01f ? d / ChaseDistance : 1f;
            float height = Mathf.Max(MinChaseHeight, ChaseHeight * scale);

            Vector3 eye = focus - fwd * d + Vector3.up * height;
            Vector3 aim = focus + fwd * (ChaseLookAhead * scale);

            transform.position = eye;
            transform.rotation = Quaternion.LookRotation((aim - eye).normalized, Vector3.up);
            if (_cam != null) _cam.fieldOfView = ChaseFov;
        }

        /// <summary>The original port's framing, kept so GroundPlay can be switched off.</summary>
        void ApplyFlat()
        {
            InputYaw = 0f;
            bool climb = Director.ClimbMode;
            float dist = climb ? ClimbDistance : SideDistance;
            float fov = climb ? ClimbFov : SideFov;

            float halfH = dist * Mathf.Tan(Mathf.Deg2Rad * fov * 0.5f) * TTCoord.PixelsPerUnit;
            float halfW = halfH * (_cam != null ? _cam.aspect : 1.777f);

            float w = Director.Settings.Width, h = Director.Settings.Height;
            float cx = w <= halfW * 2f ? w * 0.5f : Mathf.Clamp(_fx, halfW, w - halfW);
            float cy = h <= halfH * 2f ? h * 0.5f : Mathf.Clamp(_fy, halfH, h - halfH);

            transform.position = new Vector3(cx / TTCoord.PixelsPerUnit, -cy / TTCoord.PixelsPerUnit, -dist);
            transform.rotation = Quaternion.identity;
            if (_cam != null) _cam.fieldOfView = fov;
        }
    }
}

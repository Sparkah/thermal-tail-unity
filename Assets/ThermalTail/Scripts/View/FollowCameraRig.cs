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
        /// Hold the eye inside the arena. The lizard spawns hard against an edge on most
        /// levels, so a rig that sits a flat 7.5 units behind it starts outside the boundary
        /// wall looking at the back of it. Sliding the eye in keeps the floor on screen and
        /// costs nothing once the lizard has moved off the edge.
        /// </summary>
        public static Vector3 KeepInside(Vector3 eye, LevelSettings settings, float margin = 1.2f)
        {
            if (settings == null || !TTCoord.IsGround) return eye;
            float w = settings.Width / TTCoord.PixelsPerUnit;
            float h = settings.Height / TTCoord.PixelsPerUnit;
            eye.x = Mathf.Clamp(eye.x, margin, Mathf.Max(margin, w - margin));
            eye.z = Mathf.Clamp(eye.z, -h + margin, Mathf.Min(-margin, 0f));
            return eye;
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

            Vector3 eye = focus - fwd * ChaseDistance + Vector3.up * ChaseHeight;
            Vector3 aim = focus + fwd * ChaseLookAhead;

            // Slide the whole rig in rather than just the eye. Clamping the eye alone would
            // keep the aim where it was and tip the camera steeply downward every time the
            // lizard hugged an edge, which is most of the first second of every level.
            Vector3 slid = KeepInside(eye, Director.Settings);
            aim += slid - eye;
            eye = slid;

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

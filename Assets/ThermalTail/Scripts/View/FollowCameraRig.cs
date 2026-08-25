using UnityEngine;

namespace ThermalTail
{
    /// <summary>
    /// Stands in for the Cinemachine rigs the plan calls for. Cinemachine is not in this
    /// project's manifest and P0 forbids adding packages, so this reproduces the shipping
    /// build's exponential smoothing (kx = 1-exp(-6.5 dt), ky = 1-exp(-4.2 dt)) directly.
    /// </summary>
    public class FollowCameraRig : MonoBehaviour
    {
        public ThermalDirector Director;
        public float SideDistance = 16f;
        public float ClimbDistance = 13f;
        public float SideFov = 42f;
        public float ClimbFov = 46f;

        [Header("3D framing")]
        [Tooltip("Swing the camera round the action so the boxes read as solids instead of " +
                 "flat rectangles. Zero is the original head-on framing, pixel for pixel. " +
                 "Raise it to taste - 8 to 14 shows the depth without changing what is in shot.")]
        public float Yaw;
        [Tooltip("Tilt down over the action. Zero is the original framing.")]
        public float Pitch;

        Camera _cam;
        float _fx, _fy;
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
            _snapped = true;
            Apply();
        }

        void LateUpdate()
        {
            if (Director == null || Director.Settings == null || Director.Tuning == null) return;

            float tx = Director.ViewTargetX();
            float ty = Director.ViewTargetY();

            if (!_snapped) { _fx = tx; _fy = ty; _snapped = true; }
            else
            {
                float dt = Time.deltaTime;
                float kx = 1f - Mathf.Exp(-Director.Tuning.CameraFollowKx * dt);
                float ky = 1f - Mathf.Exp(-Director.Tuning.CameraFollowKy * dt);
                _fx += (tx - _fx) * kx;
                _fy += (ty - _fy) * ky;
            }
            Apply();
        }

        void Apply()
        {
            if (Director == null || Director.Settings == null) return;
            bool climb = Director.ClimbMode;

            // Keep the framing inside the level so the greybox never shows empty space.
            float halfW = (climb ? ClimbDistance : SideDistance) * Mathf.Tan(Mathf.Deg2Rad * (climb ? ClimbFov : SideFov) * 0.5f)
                          * (_cam != null ? _cam.aspect : 1.777f) * TTCoord.PixelsPerUnit;
            float halfH = (climb ? ClimbDistance : SideDistance) * Mathf.Tan(Mathf.Deg2Rad * (climb ? ClimbFov : SideFov) * 0.5f) * TTCoord.PixelsPerUnit;

            float w = Director.Settings.Width, h = Director.Settings.Height;
            float cx = w <= halfW * 2f ? w * 0.5f : Mathf.Clamp(_fx, halfW, w - halfW);
            float cy = h <= halfH * 2f ? h * 0.5f : Mathf.Clamp(_fy, halfH, h - halfH);

            // Orbit the same focus point rather than moving it, so exactly the same slice of
            // level stays in shot - it is just no longer seen dead on.
            float dist = climb ? ClimbDistance : SideDistance;
            Vector3 focus = TTCoord.Point(cx, cy, 0f);
            Quaternion swing = Quaternion.Euler(Pitch, -Yaw, 0f);
            transform.position = focus + swing * new Vector3(0f, 0f, -dist);
            transform.rotation = swing;
            if (_cam != null) _cam.fieldOfView = climb ? ClimbFov : SideFov;
        }
    }
}

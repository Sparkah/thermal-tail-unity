using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThermalTail
{
    /// <summary>
    /// The lizard. Moves on the ground plane, jumps, and stands on whatever has a collider.
    ///
    /// Collision, gravity and standing on things are Unity's CharacterController, not our
    /// code. Every version of this that reimplemented those in source-pixel space fell
    /// through the floor in a new way; a cube with a collider on it is a thing you stand on,
    /// and there is nothing left to get wrong.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TTPlayer : MonoBehaviour
    {
        [Header("Movement")]
        public float Speed = 6f;
        public float Acceleration = 40f;
        public float JumpHeight = 1.4f;
        public float Gravity = -22f;
        [Tooltip("How fast the lizard turns to face where it is going, degrees per second.")]
        public float TurnSpeed = 720f;

        [Header("Camera")]
        [Tooltip("Movement is relative to this. Leave empty to use the main camera.")]
        public Transform CameraTransform;

        CharacterController _cc;
        Vector3 _velocity;      // horizontal only
        float _fallSpeed;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (CameraTransform == null && Camera.main != null) CameraTransform = Camera.main.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // ---- read the stick ----
            // This project is set to the Input System package only, so the old
            // UnityEngine.Input class throws on the first frame if you touch it.
            float ix = 0f, iz = 0f;
            bool jumpPressed = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) ix -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) ix += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) iz -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) iz += 1f;
                jumpPressed = k.spaceKey.wasPressedThisFrame;
            }
#endif

            Vector3 wish = new Vector3(ix, 0f, iz);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // Relative to the camera, so forward is forward on screen.
            if (CameraTransform != null)
            {
                Vector3 f = CameraTransform.forward; f.y = 0f; f.Normalize();
                Vector3 r = CameraTransform.right;   r.y = 0f; r.Normalize();
                wish = f * wish.z + r * wish.x;
            }

            _velocity = Vector3.MoveTowards(_velocity, wish * Speed, Acceleration * dt);

            // ---- gravity and jump ----
            if (_cc.isGrounded)
            {
                if (_fallSpeed < 0f) _fallSpeed = -2f;      // keep it pinned to the ground
                if (jumpPressed)
                    _fallSpeed = Mathf.Sqrt(-2f * Gravity * Mathf.Max(0.01f, JumpHeight));
            }
            _fallSpeed += Gravity * dt;

            _cc.Move((_velocity + Vector3.up * _fallSpeed) * dt);

            // ---- face where we are going ----
            if (_velocity.sqrMagnitude > 0.05f)
            {
                var look = Quaternion.LookRotation(new Vector3(_velocity.x, 0f, _velocity.z));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, TurnSpeed * dt);
            }
        }
    }
}

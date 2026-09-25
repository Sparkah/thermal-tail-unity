using UnityEngine;
using UnityEngine.InputSystem;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(SurfaceMotor))]
    public sealed class PrototypeControls : MonoBehaviour
    {
        public PrototypeSession Session;
        public SurfaceCamera CameraRig;
        [Header("Mouse look")]
        [Min(0f), Tooltip("Horizontal rotation in degrees per pixel of mouse movement.")]
        public float HorizontalSensitivity = 0.18f;
        [Min(0f), Tooltip("Vertical rotation in degrees per pixel of mouse movement.")]
        public float VerticalSensitivity = 0.12f;
        public LockActor NearbyLock { get; private set; }
        public WardenTakedown NearbyTakedown { get; private set; }
        SurfaceMotor motor;
        LockActor[] locks;
        WardenTakedown[] takedowns;
        bool cursorReleased;
        void Awake()
        {
            motor = GetComponent<SurfaceMotor>();
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
            locks = FindObjectsByType<LockActor>(FindObjectsSortMode.None);
            takedowns = FindObjectsByType<WardenTakedown>(FindObjectsSortMode.None);
        }
        void Update()
        {
            if (Session == null) return;
            var keyboard = Keyboard.current;
            NearbyLock = null;
            NearbyTakedown = null;
            float closest = float.PositiveInfinity;
            foreach (var actor in locks)
            {
                if (actor == null || !actor.isActiveAndEnabled || !actor.CanInteract(motor)) continue;
                float distance = (actor.transform.position - transform.position).sqrMagnitude;
                if (distance < closest) { closest = distance; NearbyLock = actor; }
            }
            foreach (var actor in takedowns)
            {
                if (actor == null || !actor.CanTakedown(motor)) continue;
                float distance = (actor.transform.position - transform.position).sqrMagnitude;
                if (distance < closest) { closest = distance; NearbyTakedown = actor; NearbyLock = null; }
            }
            bool wasBlocked = Session.PlayerInputBlocked;
            if (keyboard != null && !Session.IsComplete && !Session.Resetting)
            {
                if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Session.Screen != ModalScreen.None) Session.CloseScreen();
                    else if (keyboard.eKey.wasPressedThisFrame && NearbyTakedown != null && !cursorReleased && Application.isFocused)
                    {
                        NearbyTakedown.TryTakedown(motor);
                        NearbyTakedown = null;
                    }
                    else if (keyboard.eKey.wasPressedThisFrame && NearbyLock != null) Session.OpenLock(NearbyLock);
                    else if (keyboard.escapeKey.wasPressedThisFrame) cursorReleased = true;
                }
                else if (keyboard.jKey.wasPressedThisFrame && Session.Screen != ModalScreen.Keypad)
                {
                    if (Session.Screen == ModalScreen.Notes) Session.CloseScreen(); else Session.OpenNotes();
                }
            }
            if (!wasBlocked && !Session.PlayerInputBlocked && Application.isFocused &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                cursorReleased = false;

            bool blocked = wasBlocked || Session.PlayerInputBlocked || cursorReleased || !Application.isFocused;
            motor.MoveInput = keyboard == null || blocked ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = !blocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = blocked;
            // Ignore the capture frame so locking/recentering cannot produce a camera jump.
            if (!blocked && wasLocked && Mouse.current != null)
            {
                Vector2 look = Mouse.current.delta.ReadValue();
                motor.Heading += look.x * HorizontalSensitivity;
                if (CameraRig != null) CameraRig.AdjustPitch(-look.y * VerticalSensitivity);
            }
        }
        void OnApplicationFocus(bool focused) { if (!focused) cursorReleased = true; }
        void OnDisable()
        {
            if (motor != null) motor.MoveInput = Vector2.zero;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

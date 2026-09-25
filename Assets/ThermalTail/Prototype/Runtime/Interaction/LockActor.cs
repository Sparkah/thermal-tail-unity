using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class LockActor : MonoBehaviour
    {
        public PrototypeSession Session;
        [Tooltip("Four distinct digits. Fingerprints identify the digits, not their order.")]
        public string Passcode = "7319";
        [Min(0.1f)] public float InteractionRadius = 2f;
        public LayerMask ObstructionMask = ~0;
        public bool Locked { get; private set; } = true;
        void Awake() { if (Session == null) Session = FindFirstObjectByType<PrototypeSession>(); }
        public bool CanInteract(SurfaceMotor player)
        {
            return Locked && player != null && Vector3.Distance(player.transform.position, transform.position) <= InteractionRadius &&
                VisionSensor.Unobstructed(player.transform.position, transform.position, ObstructionMask, player.transform, transform);
        }
        public bool Submit(string entered)
        {
            if (Session == null || Session.IsComplete || Session.Resetting || Session.ActiveLock != this ||
                !CanInteract(Session.Player) || !StealthRules.ValidCode(Passcode) || entered != Passcode) return false;
            Locked = false;
            Session.Complete();
            return true;
        }
        public bool HasFingerprint(int digit) => Passcode != null && Passcode.Contains(digit.ToString());
        void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, InteractionRadius); }
    }
}

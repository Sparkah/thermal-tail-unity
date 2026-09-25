using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class KeyDoor : MonoBehaviour
    {
        public PrototypeSession Session;
        [Min(1), Tooltip("Number of collected keys needed to open this door. Keys survive checkpoint resets.")]
        public int RequiredKeys = 8;
        [Tooltip("Closed door mesh, solid collider and navigation obstacle. Disabled when unlocked.")]
        public GameObject Barrier;
        public TextMesh StatusText;
        public bool IsOpen { get; private set; }

        void Awake() { if (Session == null) Session = FindFirstObjectByType<PrototypeSession>(); }
        void OnEnable()
        {
            if (Session != null) Session.KeysChanged += Refresh;
            Refresh();
        }
        void OnDisable() { if (Session != null) Session.KeysChanged -= Refresh; }
        void Refresh()
        {
            int count = Session != null ? Session.KeysCollected : 0;
            IsOpen = count >= Mathf.Max(1, RequiredKeys);
            if (Barrier != null) Barrier.SetActive(!IsOpen);
            if (StatusText != null) StatusText.text = IsOpen ? "ACCESS GRANTED" : "COLLECT KEYS\n" + count + " / " + RequiredKeys;
        }
    }
}

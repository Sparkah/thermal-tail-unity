using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Shared collection identity/lifecycle. Each subclass supplies its own reward.</summary>
    [RequireComponent(typeof(Collider))]
    public abstract class CollectibleActor : MonoBehaviour
    {
        [Tooltip("Unique authored ID. Persisted across checkpoints, not application restarts.")]
        public string CollectionId;
        public PrototypeSession Session;
        protected virtual string Id => CollectionId;
        void Reset() { GetComponent<Collider>().isTrigger = true; }
        protected virtual void Awake()
        {
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
            if (string.IsNullOrWhiteSpace(Id)) Debug.LogError("Collectible needs a stable unique ID.", this);
        }
        void Start() { if (Session != null && Session.HasCollected(Id)) gameObject.SetActive(false); }
        void OnTriggerEnter(Collider other) { if (other.GetComponentInParent<SurfaceMotor>() != null) Collect(); }
        public bool Collect()
        {
            if (Session == null || !CanCollect || !Session.TryCollect(Id)) return false;
            ApplyReward();
            gameObject.SetActive(false);
            return true;
        }
        protected virtual bool CanCollect => true;
        protected abstract void ApplyReward();
    }
}

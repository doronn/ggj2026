using UnityEngine;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Pickup that adds a mask to the player's inventory.
    /// Destroys itself upon collection.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MaskPickup : MonoBehaviour
    {
        [SerializeField] private ColorType colorToGive = ColorType.None;
        [SerializeField] private bool destroyOnPickup = true;
        
        [Header("Visual Effects")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 2f;

        private MaskInventory _inventory;
        private Vector3 _startPosition;
        private bool _collected;

        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
            _startPosition = transform.position;
        }

        private void Update()
        {
            if (_collected) return;
            
            // Rotate
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Bob up and down
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _startPosition + Vector3.up * yOffset;
        }

        /// <summary>
        /// Initialize the pickup with a specific color to give.
        /// Called by LevelGenerator after spawning.
        /// </summary>
        public void Initialize(ColorType color)
        {
            colorToGive = color;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            
            // Check if it's the player
            if (!other.CompareTag("Player")) return;
            
            TryCollect();
        }

        private void TryCollect()
        {
            // Try to add mask to inventory (may fail if inventory is full)
            if (_inventory.TryAddMask(colorToGive))
            {
                _collected = true;
                OnCollected();
                
                if (destroyOnPickup)
                {
                    Destroy(gameObject);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                OnInventoryFull();
            }
        }

        /// <summary>
        /// Called when the pickup is collected.
        /// Override or extend for custom effects.
        /// </summary>
        protected virtual void OnCollected()
        {
            Debug.Log($"[MaskPickup] Collected {colorToGive.GetDisplayName()} mask");
            
            // Optional: Spawn particle effect, play sound, etc.
        }

        /// <summary>
        /// Called when pickup cannot be collected because inventory is full.
        /// </summary>
        protected virtual void OnInventoryFull()
        {
            Debug.Log($"[MaskPickup] Cannot collect {colorToGive.GetDisplayName()} - inventory full!");
            
            // Optional: Show UI feedback, play "error" sound, etc.
        }

        /// <summary>
        /// Gets the color type this pickup grants.
        /// </summary>
        public ColorType ColorToGive => colorToGive;
    }
}

using System;
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
        [SerializeField] private string pickupId;
        
        [Header("Visual Effects")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 2f;

        private MaskInventory _inventory;
        private Renderer _renderer;
        private Vector3 _startPosition;
        private bool _collected;

        /// <summary>
        /// Event fired when any mask pickup is collected.
        /// Parameters: pickupId
        /// </summary>
        public static event Action<string> OnMaskPickupCollected;
        
        /// <summary>
        /// Event fired when a mask is picked up (for tutorial system).
        /// Parameters: pickup instance, color type
        /// </summary>
        public static event Action<MaskPickup, ColorType> OnMaskPickedUp;
        
        /// <summary>
        /// Event fired when pickup fails because inventory is full (for tutorial system).
        /// </summary>
        public static event Action<MaskPickup> OnInventoryFullAttempt;

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
            _renderer = GetComponentInChildren<Renderer>();
            
            // Generate ID if not set
            if (string.IsNullOrEmpty(pickupId))
            {
                pickupId = Guid.NewGuid().ToString();
            }
            
            UpdateVisualColor();
        }

        private void Start()
        {
            // Fallback: If Zenject injection didn't happen (e.g., instantiated via Instantiate()),
            // try to resolve the inventory manually
            if (_inventory == null)
            {
                var sceneContext = FindObjectOfType<Zenject.SceneContext>();
                if (sceneContext != null && sceneContext.Container != null)
                {
                    _inventory = sceneContext.Container.TryResolve<MaskInventory>();
                }
            }

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
            UpdateVisualColor();
        }

        /// <summary>
        /// Initialize the pickup with color and ID for save/load tracking.
        /// </summary>
        public void Initialize(ColorType color, string id)
        {
            colorToGive = color;
            pickupId = id;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (_renderer != null)
            {
                Color color = colorToGive.ToColor();
                color.a = 1f;
                _renderer.material.color = color;
                
                if (_renderer.material.HasProperty("_EmissionColor"))
                {
                    _renderer.material.EnableKeyword("_EMISSION");
                    _renderer.material.SetColor("_EmissionColor", color * 1.5f);
                }
            }
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
        /// Force collect this pickup (for bots).
        /// </summary>
        public void ForceCollect()
        {
            if (_collected) return;
            
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

        /// <summary>
        /// Called when the pickup is collected.
        /// Override or extend for custom effects.
        /// </summary>
        protected virtual void OnCollected()
        {
            Debug.Log($"[MaskPickup] Collected {colorToGive.GetDisplayName()} mask");
            OnMaskPickupCollected?.Invoke(pickupId);
            OnMaskPickedUp?.Invoke(this, colorToGive);
        }

        /// <summary>
        /// Called when pickup cannot be collected because inventory is full.
        /// </summary>
        protected virtual void OnInventoryFull()
        {
            Debug.Log($"[MaskPickup] Cannot collect {colorToGive.GetDisplayName()} - inventory full!");
            OnInventoryFullAttempt?.Invoke(this);
        }

        /// <summary>
        /// Gets the color type this pickup grants.
        /// </summary>
        public ColorType ColorToGive => colorToGive;

        /// <summary>
        /// Gets the unique ID of this pickup.
        /// </summary>
        public string PickupId => pickupId;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
        }
#endif
    }
}

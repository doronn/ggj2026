using UnityEngine;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Color barrier/door that players and bots can phase through when wearing the correct mask(s).
    /// Uses dual colliders: a trigger for detection and a solid collider for blocking.
    /// 
    /// Phasing Flow:
    /// 1. Entity approaches with combined active masks containing the required color
    /// 2. OnTriggerEnter: Solid collider disables, entity can walk through
    /// 3. OnTriggerExit: Solid collider re-enables, required colors subtracted from masks (residue remains)
    /// 
    /// Residue System:
    /// - Only the barrier's required colors are subtracted from active masks
    /// - Remaining colors stay in the masks
    /// - Example: Purple(R+B) + Green(Y+B) passes through Black(R+Y+B), leaving Blue residue
    /// </summary>
    public class ColorBarrier : MonoBehaviour
    {
        [Header("Barrier Settings")]
        [SerializeField] private ColorType requiredColor = ColorType.None;
        
        [Header("Colliders")]
        [SerializeField] private Collider solidCollider;
        [SerializeField] private Collider triggerCollider;
        
        [Header("Visual Feedback")]
        [SerializeField] private float phasingAlpha = 0.3f;
        [SerializeField] private float normalAlpha = 1f;

        private MaskInventory _inventory;
        private Renderer _renderer;
        private bool _isPhasing;
        private GameObject _phasingEntity;
        private Color _originalColor;
        
        // Interface for bot inventory access
        private IColorInventory _entityInventory;
        
        // Track which slots were active when phasing started (for proper subtraction)
        private System.Collections.Generic.List<int> _phasingActiveSlots = new System.Collections.Generic.List<int>();

        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            
            // Cache original color
            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
            }
            
            // Auto-setup colliders if not assigned
            SetupColliders();
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

        private void SetupColliders()
        {
            // If colliders aren't assigned, try to find/create them
            var colliders = GetComponents<Collider>();
            
            if (solidCollider == null || triggerCollider == null)
            {
                foreach (var col in colliders)
                {
                    if (col.isTrigger && triggerCollider == null)
                    {
                        triggerCollider = col;
                    }
                    else if (!col.isTrigger && solidCollider == null)
                    {
                        solidCollider = col;
                    }
                }
            }

            // If we still don't have both colliders, we need to create one
            if (solidCollider == null && triggerCollider != null)
            {
                // Duplicate the trigger as a solid collider
                solidCollider = DuplicateCollider(triggerCollider, false);
            }
            else if (triggerCollider == null && solidCollider != null)
            {
                // Duplicate the solid as a trigger collider
                triggerCollider = DuplicateCollider(solidCollider, true);
            }
            else if (solidCollider == null && triggerCollider == null)
            {
                // Create both from scratch
                var boxCollider = gameObject.AddComponent<BoxCollider>();
                solidCollider = boxCollider;
                
                triggerCollider = DuplicateCollider(boxCollider, true);
            }

            // Ensure trigger is slightly larger for reliable detection
            if (triggerCollider is BoxCollider triggerBox && solidCollider is BoxCollider solidBox)
            {
                triggerBox.size = solidBox.size + Vector3.one * 0.2f;
            }
        }

        private Collider DuplicateCollider(Collider source, bool asTrigger)
        {
            Collider newCollider = null;
            
            if (source is BoxCollider box)
            {
                var newBox = gameObject.AddComponent<BoxCollider>();
                newBox.center = box.center;
                newBox.size = box.size;
                newCollider = newBox;
            }
            else if (source is SphereCollider sphere)
            {
                var newSphere = gameObject.AddComponent<SphereCollider>();
                newSphere.center = sphere.center;
                newSphere.radius = sphere.radius;
                newCollider = newSphere;
            }
            else if (source is CapsuleCollider capsule)
            {
                var newCapsule = gameObject.AddComponent<CapsuleCollider>();
                newCapsule.center = capsule.center;
                newCapsule.radius = capsule.radius;
                newCapsule.height = capsule.height;
                newCapsule.direction = capsule.direction;
                newCollider = newCapsule;
            }

            if (newCollider != null)
            {
                newCollider.isTrigger = asTrigger;
            }

            return newCollider;
        }

        /// <summary>
        /// Initialize the barrier with a specific color requirement.
        /// Called by LevelGenerator after spawning.
        /// </summary>
        public void Initialize(ColorType color)
        {
            requiredColor = color;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (_renderer != null)
            {
                Color visualColor = requiredColor.ToColor();
                visualColor.a = normalAlpha;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // Use sharedMaterial in editor to avoid creating material instances on prefabs
                    _renderer.sharedMaterial.color = visualColor;
                }
                else
#endif
                {
                    _renderer.material.color = visualColor;
                }
                _originalColor = visualColor;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Already phasing? Ignore
            if (_isPhasing) return;

            // Check for player
            if (other.CompareTag("Player"))
            {
                if (_inventory != null && _inventory.CanPassThrough(requiredColor))
                {
                    _entityInventory = null; // Use injected player inventory
                    StartPhasing(other.gameObject, isPlayer: true);
                }
                else
                {
                    OnBlockedEntity(other, "Player");
                }
                return;
            }

            // Check for bot
            var botInventory = other.GetComponent<IColorInventory>();
            if (botInventory != null)
            {
                if (botInventory.CanPassThrough(requiredColor))
                {
                    _entityInventory = botInventory;
                    StartPhasing(other.gameObject, isPlayer: false);
                }
                else
                {
                    OnBlockedEntity(other, "Bot");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Only react to the entity that started phasing
            if (!_isPhasing) return;
            if (_phasingEntity != other.gameObject) return;

            EndPhasing();
        }

        private void StartPhasing(GameObject entity, bool isPlayer)
        {
            _isPhasing = true;
            _phasingEntity = entity;
            
            // Record which masks/colors were active when phasing started
            // This ensures subtraction happens even if player deactivates masks mid-phase
            _phasingActiveSlots.Clear();
            if (_inventory != null)
            {
                _phasingActiveSlots.AddRange(_inventory.GetActiveSlotIndices());
            }
            
            // Disable solid collider to allow passage
            if (solidCollider != null)
            {
                solidCollider.enabled = false;
            }
            
            // Visual feedback - make semi-transparent
            SetAlpha(phasingAlpha);
            
            string entityType = isPlayer ? "Player" : "Bot";
            Debug.Log($"[ColorBarrier] {entityType} phasing through {requiredColor.GetDisplayName()} barrier");
        }

        private void EndPhasing()
        {
            // Re-enable solid collider
            if (solidCollider != null)
            {
                solidCollider.enabled = true;
            }
            
            // Apply barrier subtraction using the slots that were active when phasing STARTED
            // This ensures masks are consumed even if player deactivates them mid-phase
            if (_entityInventory != null)
            {
                // Bot inventory
                _entityInventory.ApplyBarrierSubtraction(requiredColor);
            }
            else if (_inventory != null)
            {
                // Player inventory - use recorded start state
                _inventory.ApplyBarrierSubtractionFromSlots(requiredColor, _phasingActiveSlots);
            }
            
            // Restore visual
            SetAlpha(normalAlpha);
            
            _isPhasing = false;
            _phasingEntity = null;
            _entityInventory = null;
            _phasingActiveSlots.Clear();
            
            Debug.Log($"[ColorBarrier] {requiredColor.GetDisplayName()} colors consumed (residue may remain)");
        }

        private void SetAlpha(float alpha)
        {
            if (_renderer == null) return;
            
            Color color = _renderer.material.color;
            color.a = alpha;
            _renderer.material.color = color;
        }

        /// <summary>
        /// Called when an entity attempts to pass without the correct mask.
        /// </summary>
        protected virtual void OnBlockedEntity(Collider entityCollider, string entityType)
        {
            Debug.Log($"[ColorBarrier] {entityType} blocked - needs {requiredColor.GetDisplayName()} mask(s) active");
            
            // Optional: Visual feedback (flash red, shake, etc.)
            // Optional: Audio feedback
        }

        /// <summary>
        /// Gets the color type required to pass this barrier.
        /// </summary>
        public ColorType RequiredColor => requiredColor;

        /// <summary>
        /// Returns true if an entity is currently phasing through.
        /// </summary>
        public bool IsPhasing => _isPhasing;

        /// <summary>
        /// Checks if a given combined color can pass through this barrier.
        /// </summary>
        public bool CanColorPass(ColorType combinedColor)
        {
            return ColorTypeExtensions.CanPassThrough(combinedColor, requiredColor);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Visual helper in editor
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            
            UpdateVisualColor();
        }

        private void OnDrawGizmos()
        {
            // Draw color indicator in scene view
            Gizmos.color = requiredColor.ToColor();
            Gizmos.DrawWireCube(transform.position, Vector3.one * 1.1f);
        }
#endif
    }

    /// <summary>
    /// Interface for entities with color-based inventory (players and bots).
    /// Allows barriers to interact with different inventory implementations.
    /// </summary>
    public interface IColorInventory
    {
        ColorType GetCombinedActiveColor();
        bool CanPassThrough(ColorType barrierColor);
        void ApplyBarrierSubtraction(ColorType barrierColor);
    }
}

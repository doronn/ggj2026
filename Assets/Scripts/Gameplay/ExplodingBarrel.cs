using UnityEngine;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Exploding barrel that triggers when an entity with the matching mask color enters.
    /// 
    /// Behavior:
    /// - Blocks all entities that don't have the matching mask color (like a wall)
    /// - When an entity WITH the matching color enters:
    ///   - If player: Triggers checkpoint restore (respawn at last checkpoint)
    ///   - If bot: Subtracts barrel color from bot, bot drops remaining masks, bot is destroyed
    /// - Barrel is destroyed after explosion
    /// 
    /// Uses dual collider system like ColorBarrier:
    /// - Solid collider blocks entities without matching color
    /// - Trigger collider detects when matching entity enters
    /// </summary>
    public class ExplodingBarrel : MonoBehaviour
    {
        [Header("Barrel Settings")]
        [SerializeField] private ColorType barrelColor = ColorType.Red;
        
        [Header("Colliders")]
        [SerializeField] private Collider solidCollider;
        [SerializeField] private Collider triggerCollider;
        
        [Header("Visual")]
        [SerializeField] private ParticleSystem explosionVFX;
        [SerializeField] private AudioSource explosionSFX;

        private MaskInventory _playerInventory;
        private Renderer _renderer;
        private bool _hasExploded;
        
        /// <summary>
        /// Unique ID for this barrel (for save/load tracking).
        /// </summary>
        public string barrelId { get; private set; }

        /// <summary>
        /// Event fired when the barrel explodes.
        /// Parameter indicates if the player triggered it (true) or a bot (false).
        /// </summary>
        public static event System.Action<ExplodingBarrel, bool> OnBarrelExploded;

        /// <summary>
        /// Event specifically for player death - used by CheckpointManager.
        /// </summary>
        public static event System.Action OnPlayerExploded;

        [Inject]
        public void Construct(MaskInventory playerInventory)
        {
            _playerInventory = playerInventory;
        }

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            SetupColliders();
            
            // Generate unique ID based on position
            barrelId = $"barrel_{transform.position.x:F2}_{transform.position.z:F2}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        }
        
        /// <summary>
        /// Sets the barrel ID (used when spawning from level data with pre-defined IDs).
        /// </summary>
        public void SetBarrelId(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                barrelId = id;
            }
        }

        private void Start()
        {
            // Fallback: If Zenject injection didn't happen (e.g., instantiated via Instantiate()),
            // try to resolve the inventory manually
            if (_playerInventory == null)
            {
                var sceneContext = FindObjectOfType<Zenject.SceneContext>();
                if (sceneContext != null && sceneContext.Container != null)
                {
                    _playerInventory = sceneContext.Container.TryResolve<MaskInventory>();
                }
            }
            
            if (_playerInventory == null)
            {
                Debug.LogWarning($"[ExplodingBarrel] Failed to resolve MaskInventory for barrel at {transform.position}");
            }
        }

        private void SetupColliders()
        {
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

            // Create missing colliders
            if (solidCollider == null && triggerCollider != null)
            {
                solidCollider = DuplicateCollider(triggerCollider, false);
            }
            else if (triggerCollider == null && solidCollider != null)
            {
                triggerCollider = DuplicateCollider(solidCollider, true);
            }
            else if (solidCollider == null && triggerCollider == null)
            {
                var boxCollider = gameObject.AddComponent<BoxCollider>();
                solidCollider = boxCollider;
                triggerCollider = DuplicateCollider(boxCollider, true);
            }

            // Trigger slightly larger for detection
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

            if (newCollider != null)
            {
                newCollider.isTrigger = asTrigger;
            }

            return newCollider;
        }

        /// <summary>
        /// Initialize the barrel with a specific color.
        /// </summary>
        public void Initialize(ColorType color)
        {
            barrelColor = color;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (_renderer != null)
            {
                // Barrels use a slightly darker/different shade than barriers
                Color visualColor = barrelColor.ToColor();
                visualColor *= 0.8f; // Slightly darker
                visualColor.a = 1f;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    _renderer.sharedMaterial.color = visualColor;
                }
                else
#endif
                {
                    _renderer.material.color = visualColor;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasExploded) return;

            // Check for player
            if (other.CompareTag("Player"))
            {
                HandlePlayerContact();
                return;
            }

            // Check for bot
            var botInventory = other.GetComponent<IColorInventory>();
            if (botInventory != null)
            {
                HandleBotContact(other.gameObject, botInventory);
            }
        }

        private void HandlePlayerContact()
        {
            if (_playerInventory == null) return;

            ColorType playerColor = _playerInventory.GetCombinedActiveColor();
            
            // Check if player has the barrel's color active
            if ((playerColor & barrelColor) == barrelColor)
            {
                // Player has matching color - EXPLOSION!
                ExplodeForPlayer();
            }
            else
            {
                // Player doesn't have matching color - blocked (solid collider handles this)
                // But if they somehow got through without the color, allow passage without explosion
                Debug.Log($"[ExplodingBarrel] Player lacks {barrelColor.GetDisplayName()} - blocked");
            }
        }

        private void HandleBotContact(GameObject botObject, IColorInventory botInventory)
        {
            ColorType botColor = botInventory.GetCombinedActiveColor();
            
            // Check if bot has the barrel's color
            if ((botColor & barrelColor) == barrelColor)
            {
                // Bot has matching color - explode and destroy bot
                ExplodeForBot(botObject, botInventory);
            }
            else
            {
                Debug.Log($"[ExplodingBarrel] Bot lacks {barrelColor.GetDisplayName()} - blocked");
            }
        }

        private void ExplodeForPlayer()
        {
            _hasExploded = true;
            
            Debug.Log($"[ExplodingBarrel] Player exploded! Barrel color: {barrelColor.GetDisplayName()}");
            
            PlayExplosionEffects();
            
            // Notify systems
            OnBarrelExploded?.Invoke(this, true);
            OnPlayerExploded?.Invoke();
            
            // Destroy the barrel
            Destroy(gameObject, 0.1f);
        }

        private void ExplodeForBot(GameObject botObject, IColorInventory botInventory)
        {
            _hasExploded = true;
            
            Debug.Log($"[ExplodingBarrel] Bot exploded! Barrel color: {barrelColor.GetDisplayName()}");
            
            PlayExplosionEffects();
            
            // Subtract barrel color from bot
            botInventory.ApplyBarrierSubtraction(barrelColor);
            
            // Get the bot's mask dropper component to drop remaining masks
            var maskDropper = botObject.GetComponent<IBotMaskDropper>();
            if (maskDropper != null)
            {
                maskDropper.DropAllMasks();
            }
            
            // Notify systems
            OnBarrelExploded?.Invoke(this, false);
            
            // Destroy the bot
            var botController = botObject.GetComponent<Bot.BotController>();
            if (botController != null)
            {
                botController.OnExploded();
            }
            else
            {
                Destroy(botObject);
            }
            
            // Destroy the barrel
            Destroy(gameObject, 0.1f);
        }

        private void PlayExplosionEffects()
        {
            if (explosionVFX != null)
            {
                explosionVFX.transform.SetParent(null);
                explosionVFX.Play();
                Destroy(explosionVFX.gameObject, explosionVFX.main.duration + 1f);
            }

            if (explosionSFX != null)
            {
                explosionSFX.transform.SetParent(null);
                explosionSFX.Play();
                Destroy(explosionSFX.gameObject, explosionSFX.clip != null ? explosionSFX.clip.length + 0.5f : 2f);
            }
        }

        /// <summary>
        /// Checks if an entity with the given combined color would trigger this barrel.
        /// </summary>
        public bool WouldTriggerExplosion(ColorType entityColor)
        {
            return (entityColor & barrelColor) == barrelColor;
        }

        /// <summary>
        /// Gets the barrel's trigger color.
        /// </summary>
        public ColorType BarrelColor => barrelColor;

        /// <summary>
        /// Returns true if the barrel has already exploded.
        /// </summary>
        public bool HasExploded => _hasExploded;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            
            UpdateVisualColor();
        }

        private void OnDrawGizmos()
        {
            // Draw hazard indicator
            Gizmos.color = barrelColor.ToColor();
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            
            // Draw X to indicate danger
            Gizmos.color = Color.red;
            Vector3 pos = transform.position + Vector3.up * 0.5f;
            Gizmos.DrawLine(pos + new Vector3(-0.2f, 0, -0.2f), pos + new Vector3(0.2f, 0, 0.2f));
            Gizmos.DrawLine(pos + new Vector3(-0.2f, 0, 0.2f), pos + new Vector3(0.2f, 0, -0.2f));
        }
#endif
    }

    /// <summary>
    /// Interface for bots to drop masks when exploding.
    /// </summary>
    public interface IBotMaskDropper
    {
        void DropAllMasks();
    }
}

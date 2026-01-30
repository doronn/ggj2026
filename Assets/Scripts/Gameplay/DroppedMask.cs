using System;
using UnityEngine;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// A mask that has been dropped on the ground by a player or bot.
    /// Can be picked up by players or bots.
    /// 
    /// Rules:
    /// - Spawned at grid position when player drops
    /// - Only one mask can exist per tile (dropping on existing mask swaps them)
    /// - Player won't re-pick up a mask until they exit and re-enter the trigger
    /// - Bots pick up only colors they don't have, leaving residue
    /// </summary>
    public class DroppedMask : MonoBehaviour
    {
        [Header("Mask Settings")]
        [SerializeField] private ColorType maskColor = ColorType.None;
        
        [Header("Visual")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float rotateSpeed = 45f;
        
        private Renderer _renderer;
        private Vector3 _startPosition;
        private bool _canBePickedUpByPlayer = true;
        private bool _playerInTrigger;
        private bool _isCollected;

        /// <summary>
        /// Unique ID for this dropped mask (for save/load).
        /// </summary>
        public string MaskId { get; private set; }

        /// <summary>
        /// Static event for requesting mask spawns.
        /// Parameters: position, color
        /// </summary>
        public static event Action<Vector3, ColorType> SpawnDroppedMask;

        /// <summary>
        /// Raises the SpawnDroppedMask event. Call this to request spawning a dropped mask.
        /// </summary>
        public static void RequestSpawnDroppedMask(Vector3 position, ColorType color)
        {
            SpawnDroppedMask?.Invoke(position, color);
        }

        /// <summary>
        /// Event fired when this mask is collected.
        /// </summary>
        public event Action<DroppedMask> OnCollected;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _startPosition = transform.position;
            
            // Generate unique ID based on position
            MaskId = $"dropped_{transform.position.x:F2}_{transform.position.z:F2}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            
            UpdateVisualColor();
        }

        private void Update()
        {
            if (_isCollected) return;
            
            // Bob up and down
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _startPosition + Vector3.up * yOffset;
            
            // Rotate
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Initialize the dropped mask with a specific color.
        /// </summary>
        public void Initialize(ColorType color)
        {
            maskColor = color;
            _startPosition = transform.position;
            UpdateVisualColor();
        }

        /// <summary>
        /// Initialize with position and color.
        /// </summary>
        public void Initialize(Vector3 position, ColorType color)
        {
            transform.position = position;
            maskColor = color;
            _startPosition = position;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (_renderer != null)
            {
                Color color = maskColor.ToColor();
                color.a = 1f;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    _renderer.sharedMaterial.color = color;
                    if (_renderer.sharedMaterial.HasProperty("_EmissionColor"))
                    {
                        _renderer.sharedMaterial.SetColor("_EmissionColor", color * 1.2f);
                    }
                }
                else
#endif
                {
                    _renderer.material.color = color;
                    _renderer.material.SetColor("_EmissionColor", color * 1.2f);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;
            
            // Check for player
            if (other.CompareTag("Player"))
            {
                _playerInTrigger = true;
                
                if (_canBePickedUpByPlayer)
                {
                    TryPlayerPickup(other.gameObject);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInTrigger = false;
                // Re-enable pickup when player leaves
                _canBePickedUpByPlayer = true;
            }
        }

        private void TryPlayerPickup(GameObject player)
        {
            var inventory = player.GetComponent<PlayerController>()?.GetInventory();
            if (inventory == null)
            {
                // Try to get from Zenject context
                inventory = FindObjectOfType<Zenject.SceneContext>()?.Container?.Resolve<MaskInventory>();
            }
            
            if (inventory == null) return;
            
            if (inventory.TryAddMask(maskColor))
            {
                Collect();
            }
            else
            {
                // Inventory full - can't pick up
                Debug.Log($"[DroppedMask] Player inventory full, can't pick up {maskColor.GetDisplayName()}");
            }
        }

        /// <summary>
        /// Called when this mask is collected.
        /// </summary>
        public void Collect()
        {
            if (_isCollected) return;
            
            _isCollected = true;
            OnCollected?.Invoke(this);
            
            Debug.Log($"[DroppedMask] Collected {maskColor.GetDisplayName()}");
            
            Destroy(gameObject);
        }

        /// <summary>
        /// Prevents the player from immediately picking up a mask they just dropped.
        /// </summary>
        public void PreventImmediatePickup()
        {
            _canBePickedUpByPlayer = false;
        }

        /// <summary>
        /// Gets the mask color.
        /// </summary>
        public ColorType MaskColor => maskColor;

        /// <summary>
        /// Gets the grid position of this mask.
        /// </summary>
        public Vector3 Position => _startPosition;

        /// <summary>
        /// Returns true if the mask has been collected.
        /// </summary>
        public bool IsCollected => _isCollected;

        /// <summary>
        /// Creates a snapshot of this dropped mask for saving.
        /// </summary>
        public DroppedMaskSnapshot CreateSnapshot()
        {
            return new DroppedMaskSnapshot
            {
                MaskId = MaskId,
                Position = _startPosition,
                MaskColor = maskColor
            };
        }

        /// <summary>
        /// Spawns a dropped mask at the given position with the given color.
        /// Uses the DroppedMask prefab.
        /// </summary>
        public static DroppedMask Spawn(GameObject prefab, Vector3 position, ColorType color, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[DroppedMask] No prefab provided for spawning");
                return null;
            }
            
            var instance = Instantiate(prefab, position, Quaternion.identity, parent);
            var droppedMask = instance.GetComponent<DroppedMask>();
            
            if (droppedMask != null)
            {
                droppedMask.Initialize(position, color);
            }
            
            return droppedMask;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            
            UpdateVisualColor();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = maskColor.ToColor();
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
#endif
    }

    /// <summary>
    /// Snapshot of a dropped mask for saving.
    /// </summary>
    [Serializable]
    public class DroppedMaskSnapshot
    {
        public string MaskId;
        public Vector3 Position;
        public ColorType MaskColor;
    }
}

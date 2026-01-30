using System;
using UnityEngine;
using BreakingHue.Core;

namespace BreakingHue.Gameplay.Bot
{
    /// <summary>
    /// Controls bot movement, path following, and interactions.
    /// 
    /// Bot Rules:
    /// - Follows a predefined path (BotPathData)
    /// - Same movement physics as player
    /// - Collects masks it doesn't already own (drops residue for colors it has)
    /// - Passes through barriers using same rules as player (with residue)
    /// - Explodes in barrels (drops remaining masks)
    /// - Stops permanently when colliding with player (until player moves away)
    /// - Can regenerate initial color when passing through matching color barriers
    /// </summary>
    [RequireComponent(typeof(BotInventory))]
    [RequireComponent(typeof(Rigidbody))]
    public class BotController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float arrivalDistance = 0.1f;
        
        [Header("Path")]
        [SerializeField] private BotPathData pathData;
        
        [Header("Level Reference")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 levelOffset = Vector3.zero;
        
        [Header("Visual")]
        [SerializeField] private Renderer visualRenderer;

        private BotInventory _inventory;
        private Rigidbody _rigidbody;
        private BotPathState _pathState;
        
        private Vector3 _currentVelocity;
        private bool _isStoppedByPlayer;
        private bool _isDead;
        
        private GameObject _blockingPlayer;

        /// <summary>
        /// Event fired when bot is destroyed/exploded.
        /// </summary>
        public event Action<BotController> OnBotDestroyed;

        /// <summary>
        /// Unique ID for this bot instance (for save/load).
        /// </summary>
        public string BotId { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<BotInventory>();
            _rigidbody = GetComponent<Rigidbody>();
            
            // Generate unique ID
            BotId = Guid.NewGuid().ToString();
            
            // Setup rigidbody
            _rigidbody.useGravity = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            
            // Initialize path state
            _pathState = new BotPathState();
            
            // Subscribe to inventory changes for visual updates
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += UpdateVisualColor;
                _inventory.OnMaskDropped += HandleMaskDropped;
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= UpdateVisualColor;
                _inventory.OnMaskDropped -= HandleMaskDropped;
            }
        }

        private void Start()
        {
            UpdateVisualColor();
        }

        private void FixedUpdate()
        {
            if (_isDead) return;
            
            if (_isStoppedByPlayer)
            {
                // Check if player has moved away
                if (_blockingPlayer == null || !IsPlayerInContact())
                {
                    _isStoppedByPlayer = false;
                    _blockingPlayer = null;
                }
                else
                {
                    // Stay stopped
                    _rigidbody.linearVelocity = new Vector3(0, _rigidbody.linearVelocity.y, 0);
                    return;
                }
            }
            
            HandlePathMovement();
        }

        private void HandlePathMovement()
        {
            if (pathData == null || pathData.waypoints.Count == 0)
                return;

            // Handle waiting at waypoint
            if (_pathState.isWaiting)
            {
                _pathState.waitTimer -= Time.fixedDeltaTime;
                if (_pathState.waitTimer <= 0)
                {
                    _pathState.isWaiting = false;
                    MoveToNextWaypoint();
                }
                return;
            }

            // Get current target position
            Vector3 targetPos = pathData.GetWorldPosition(_pathState.currentWaypointIndex, cellSize, levelOffset);
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0; // Ignore vertical

            float distance = toTarget.magnitude;
            
            if (distance <= arrivalDistance)
            {
                // Arrived at waypoint
                OnWaypointReached();
            }
            else
            {
                // Move towards waypoint
                Vector3 direction = toTarget.normalized;
                float currentSpeed = moveSpeed * (pathData.speedMultiplier > 0 ? pathData.speedMultiplier : 1f);
                Vector3 targetVelocity = direction * currentSpeed;
                
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
                
                _rigidbody.linearVelocity = new Vector3(_currentVelocity.x, _rigidbody.linearVelocity.y, _currentVelocity.z);
            }
        }

        private void OnWaypointReached()
        {
            // Check for wait time
            if (pathData.waitTimeAtWaypoints > 0)
            {
                _pathState.isWaiting = true;
                _pathState.waitTimer = pathData.waitTimeAtWaypoints;
                _currentVelocity = Vector3.zero;
            }
            else
            {
                MoveToNextWaypoint();
            }
        }

        private void MoveToNextWaypoint()
        {
            // Check for direction reversal (ping-pong)
            if (pathData.ShouldReverse(_pathState.currentWaypointIndex, _pathState.movingForward))
            {
                _pathState.movingForward = !_pathState.movingForward;
            }
            
            int nextIndex = pathData.GetNextWaypointIndex(_pathState.currentWaypointIndex, _pathState.movingForward);
            
            if (nextIndex < 0)
            {
                // Path ended (one-way mode)
                _currentVelocity = Vector3.zero;
                return;
            }
            
            _pathState.currentWaypointIndex = nextIndex;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check for player collision
            if (collision.gameObject.CompareTag("Player"))
            {
                _isStoppedByPlayer = true;
                _blockingPlayer = collision.gameObject;
                _currentVelocity = Vector3.zero;
                Debug.Log("[BotController] Stopped by player collision");
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject == _blockingPlayer)
            {
                _isStoppedByPlayer = false;
                _blockingPlayer = null;
            }
        }

        private bool IsPlayerInContact()
        {
            if (_blockingPlayer == null) return false;
            
            // Check distance
            float distance = Vector3.Distance(transform.position, _blockingPlayer.transform.position);
            return distance < 1.5f; // Approximate contact distance
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isDead) return;
            
            // Check for mask pickup
            var maskPickup = other.GetComponent<MaskPickup>();
            if (maskPickup != null)
            {
                HandleMaskPickup(maskPickup);
                return;
            }
            
            // Check for dropped mask
            var droppedMask = other.GetComponent<DroppedMask>();
            if (droppedMask != null)
            {
                HandleDroppedMaskPickup(droppedMask);
                return;
            }
        }

        private void HandleMaskPickup(MaskPickup pickup)
        {
            ColorType maskColor = pickup.ColorToGive;
            
            // Check if bot already has all these colors
            if (_inventory.AlreadyHasAllColors(maskColor))
            {
                // Walk through without picking up
                Debug.Log($"[BotController] Walking through {maskColor.GetDisplayName()} mask (already owned)");
                return;
            }
            
            // Try to pick up, get residue
            ColorType residue = _inventory.TryPickupMask(maskColor);
            
            if (residue != ColorType.None && residue != maskColor)
            {
                // Drop residue at pickup location
                SpawnDroppedMask(pickup.transform.position, residue);
            }
            
            // Consume the pickup
            pickup.ForceCollect();
            
            Debug.Log($"[BotController] Picked up {maskColor.GetDisplayName()}, dropped residue: {residue.GetDisplayName()}");
        }

        private void HandleDroppedMaskPickup(DroppedMask droppedMask)
        {
            ColorType maskColor = droppedMask.MaskColor;
            
            if (_inventory.AlreadyHasAllColors(maskColor))
            {
                Debug.Log($"[BotController] Walking through dropped {maskColor.GetDisplayName()} mask (already owned)");
                return;
            }
            
            ColorType residue = _inventory.TryPickupMask(maskColor);
            
            if (residue != ColorType.None && residue != maskColor)
            {
                // Drop residue - but not at exact same position
                Vector3 residuePos = droppedMask.transform.position + UnityEngine.Random.insideUnitSphere * 0.3f;
                residuePos.y = droppedMask.transform.position.y;
                SpawnDroppedMask(residuePos, residue);
            }
            
            droppedMask.Collect();
        }

        private void SpawnDroppedMask(Vector3 position, ColorType color)
        {
            // Request spawning a dropped mask via the inventory's raise method
            _inventory.RaiseMaskDropped(position, color);
        }

        private void HandleMaskDropped(Vector3 position, ColorType color)
        {
            // Request spawning a dropped mask at this position
            // This is handled by the level manager or a dedicated spawner
            DroppedMask.RequestSpawnDroppedMask(position, color);
        }

        /// <summary>
        /// Called when this bot is destroyed by an exploding barrel.
        /// </summary>
        public void OnExploded()
        {
            _isDead = true;
            _inventory.DropAllMasks();
            OnBotDestroyed?.Invoke(this);
            
            // Play death effect if any
            
            Destroy(gameObject, 0.1f);
        }

        /// <summary>
        /// Called when bot passes through a barrier matching its initial color.
        /// Regenerates the initial color if lost.
        /// </summary>
        public void OnPassedThroughMatchingBarrier(ColorType barrierColor)
        {
            if (_inventory.InitialColor != ColorType.None && 
                (barrierColor & _inventory.InitialColor) != ColorType.None)
            {
                _inventory.TryRegenerateInitialColor();
            }
        }

        private void UpdateVisualColor()
        {
            if (visualRenderer == null)
                visualRenderer = GetComponentInChildren<Renderer>();
            
            if (visualRenderer != null)
            {
                ColorType combined = _inventory.GetCombinedActiveColor();
                Color color = combined.ToColor();
                color.a = 1f;
                visualRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Initialize the bot with path data and level settings.
        /// </summary>
        public void Initialize(BotPathData path, float gridCellSize, Vector3 offset, ColorType startColor = ColorType.None)
        {
            pathData = path;
            cellSize = gridCellSize;
            levelOffset = offset;
            
            if (startColor != ColorType.None)
            {
                _inventory.SetInitialColor(startColor);
            }
            
            // Set initial position to first waypoint
            if (pathData != null && pathData.waypoints.Count > 0)
            {
                Vector3 startPos = pathData.GetWorldPosition(0, cellSize, levelOffset);
                transform.position = startPos;
            }
            
            UpdateVisualColor();
        }

        /// <summary>
        /// Creates a snapshot of the bot's current state.
        /// </summary>
        public BotSnapshot CreateSnapshot()
        {
            return new BotSnapshot
            {
                BotId = BotId,
                Position = transform.position,
                PathState = _pathState.Clone(),
                InventorySnapshot = _inventory.CreateSnapshot(),
                IsDead = _isDead
            };
        }

        /// <summary>
        /// Restores the bot from a snapshot.
        /// </summary>
        public void RestoreFromSnapshot(BotSnapshot snapshot)
        {
            if (snapshot == null) return;
            
            transform.position = snapshot.Position;
            _pathState = snapshot.PathState.Clone();
            _inventory.RestoreFromSnapshot(snapshot.InventorySnapshot);
            _isDead = snapshot.IsDead;
            
            if (_isDead)
            {
                gameObject.SetActive(false);
            }
            
            UpdateVisualColor();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (pathData == null || pathData.waypoints.Count == 0)
                return;

            Gizmos.color = Color.cyan;
            
            for (int i = 0; i < pathData.waypoints.Count; i++)
            {
                Vector3 pos = pathData.GetWorldPosition(i, cellSize, levelOffset);
                Gizmos.DrawWireSphere(pos, 0.3f);
                
                if (i < pathData.waypoints.Count - 1)
                {
                    Vector3 nextPos = pathData.GetWorldPosition(i + 1, cellSize, levelOffset);
                    Gizmos.DrawLine(pos, nextPos);
                }
                else if (pathData.pathMode == PathMode.Loop && pathData.waypoints.Count > 1)
                {
                    Vector3 firstPos = pathData.GetWorldPosition(0, cellSize, levelOffset);
                    Gizmos.DrawLine(pos, firstPos);
                }
            }
        }
#endif
    }

    /// <summary>
    /// Complete snapshot of a bot's state for checkpointing.
    /// </summary>
    [Serializable]
    public class BotSnapshot
    {
        public string BotId;
        public Vector3 Position;
        public BotPathState PathState;
        public BotInventorySnapshot InventorySnapshot;
        public bool IsDead;
    }
}

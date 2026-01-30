using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// 3D top-down player controller using Rigidbody physics.
    /// Uses Unity's New Input System.
    /// Handles movement, mask toggling, and mask dropping.
    /// 
    /// New Controls:
    /// - Keys 1/2/3: Toggle mask active state (multi-select supported)
    /// - Key Q: Drop first active mask at current position
    /// - Key 0 or `: Deactivate all masks
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float deceleration = 40f;
        
        [Header("Visual")]
        [SerializeField] private Transform visualTransform;
        
        [Header("Mask Drop Settings")]
        [SerializeField] private GameObject droppedMaskPrefab;
        [SerializeField] private float gridCellSize = 1f;

        private Rigidbody _rigidbody;
        private MaskInventory _inventory;
        private Vector2 _inputDirection;
        private Vector3 _currentVelocity;
        
        // Track if we're standing on a dropped mask to prevent immediate re-pickup
        private DroppedMask _currentTileMask;
        
        
        // Input System
        private InputAction _moveAction;
        private InputAction _toggleSlot1Action;
        private InputAction _toggleSlot2Action;
        private InputAction _toggleSlot3Action;
        private InputAction _deactivateAllAction;
        private InputAction _dropMaskAction;
        private PlayerInput _playerInput;

        /// <summary>
        /// Event fired when player requests to drop a mask.
        /// Parameters: world position, color to drop
        /// </summary>
        public static event Action<Vector3, ColorType> OnMaskDropRequested;

        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            
            // Try to get PlayerInput component or create input actions manually
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                SetupManualInput();
            }
            else
            {
                _moveAction = _playerInput.actions["Move"];
                // Try to get actions if defined in input asset
                _toggleSlot1Action = _playerInput.actions.FindAction("ToggleSlot1");
                _toggleSlot2Action = _playerInput.actions.FindAction("ToggleSlot2");
                _toggleSlot3Action = _playerInput.actions.FindAction("ToggleSlot3");
                _deactivateAllAction = _playerInput.actions.FindAction("DeactivateAll");
                _dropMaskAction = _playerInput.actions.FindAction("DropMask");
                
                // If not defined, create them manually
                if (_toggleSlot1Action == null)
                    SetupMaskInputManually();
            }

            if (visualTransform == null)
            {
                visualTransform = transform;
            }
            
            // Ensure player has the correct tag
            gameObject.tag = "Player";
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

            // Subscribe to inventory events for visual updates
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += OnInventoryChanged;
                _inventory.OnMaskToggled += OnMaskToggled;
                
                // Initial visual update
                UpdatePlayerColor();
            }
            else
            {
                Debug.LogError("[PlayerController] Failed to resolve MaskInventory!");
            }
            
            // Fallback: If droppedMaskPrefab not assigned, try to find one in Resources
            if (droppedMaskPrefab == null)
            {
                droppedMaskPrefab = Resources.Load<GameObject>("DroppedMask");
                if (droppedMaskPrefab == null)
                {
                    // Try to find any existing DroppedMask in the scene to use as template
                    var existingMask = FindObjectOfType<DroppedMask>(true);
                    if (existingMask != null)
                    {
                        Debug.Log("[PlayerController] Found existing DroppedMask to use as prefab template");
                        droppedMaskPrefab = existingMask.gameObject;
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerController] droppedMaskPrefab not assigned and no fallback found. Mask dropping will create a primitive.");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= OnInventoryChanged;
                _inventory.OnMaskToggled -= OnMaskToggled;
            }
            
            if (_playerInput == null)
            {
                _moveAction?.Dispose();
                _toggleSlot1Action?.Dispose();
                _toggleSlot2Action?.Dispose();
                _toggleSlot3Action?.Dispose();
                _deactivateAllAction?.Dispose();
                _dropMaskAction?.Dispose();
            }
        }

        private void ConfigureRigidbody()
        {
            _rigidbody.useGravity = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void SetupManualInput()
        {
            // Create a simple move action for WASD/Arrow keys
            _moveAction = new InputAction("Move", InputActionType.Value);
            
            // Add WASD composite
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            
            // Add Arrow keys composite
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            
            // Add gamepad support
            _moveAction.AddBinding("<Gamepad>/leftStick");
            
            _moveAction.Enable();
            
            SetupMaskInputManually();
        }

        private void SetupMaskInputManually()
        {
            // Slot 1 - Key 1 (Toggle)
            _toggleSlot1Action = new InputAction("ToggleSlot1", InputActionType.Button);
            _toggleSlot1Action.AddBinding("<Keyboard>/1");
            _toggleSlot1Action.AddBinding("<Keyboard>/numpad1");
            _toggleSlot1Action.performed += _ => ToggleSlot(0);
            _toggleSlot1Action.Enable();
            
            // Slot 2 - Key 2 (Toggle)
            _toggleSlot2Action = new InputAction("ToggleSlot2", InputActionType.Button);
            _toggleSlot2Action.AddBinding("<Keyboard>/2");
            _toggleSlot2Action.AddBinding("<Keyboard>/numpad2");
            _toggleSlot2Action.performed += _ => ToggleSlot(1);
            _toggleSlot2Action.Enable();
            
            // Slot 3 - Key 3 (Toggle)
            _toggleSlot3Action = new InputAction("ToggleSlot3", InputActionType.Button);
            _toggleSlot3Action.AddBinding("<Keyboard>/3");
            _toggleSlot3Action.AddBinding("<Keyboard>/numpad3");
            _toggleSlot3Action.performed += _ => ToggleSlot(2);
            _toggleSlot3Action.Enable();
            
            // Deactivate All - Key 0 or `
            _deactivateAllAction = new InputAction("DeactivateAll", InputActionType.Button);
            _deactivateAllAction.AddBinding("<Keyboard>/0");
            _deactivateAllAction.AddBinding("<Keyboard>/numpad0");
            _deactivateAllAction.AddBinding("<Keyboard>/backquote"); // ` key
            _deactivateAllAction.performed += _ => DeactivateAllMasks();
            _deactivateAllAction.Enable();
            
            // Drop Mask - Key Q
            _dropMaskAction = new InputAction("DropMask", InputActionType.Button);
            _dropMaskAction.AddBinding("<Keyboard>/q");
            _dropMaskAction.AddBinding("<Gamepad>/buttonWest"); // X on Xbox, Square on PlayStation
            _dropMaskAction.performed += _ => DropMask();
            _dropMaskAction.Enable();
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _toggleSlot1Action?.Enable();
            _toggleSlot2Action?.Enable();
            _toggleSlot3Action?.Enable();
            _deactivateAllAction?.Enable();
            _dropMaskAction?.Enable();
        }

        private void OnDisable()
        {
            if (_playerInput == null)
            {
                _moveAction?.Disable();
                _toggleSlot1Action?.Disable();
                _toggleSlot2Action?.Disable();
                _toggleSlot3Action?.Disable();
                _deactivateAllAction?.Disable();
                _dropMaskAction?.Disable();
            }
        }

        private void Update()
        {
            // Read input
            _inputDirection = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        private void FixedUpdate()
        {
            HandleMovement();
            UpdateVisuals();
        }

        private void HandleMovement()
        {
            // Convert 2D input to 3D direction (XZ plane)
            Vector3 targetDirection = new Vector3(_inputDirection.x, 0, _inputDirection.y).normalized;
            Vector3 targetVelocity = targetDirection * moveSpeed;

            // Smooth acceleration/deceleration
            float accel = targetDirection.magnitude > 0.1f ? acceleration : deceleration;
            
            _currentVelocity = Vector3.MoveTowards(
                _currentVelocity, 
                targetVelocity, 
                accel * Time.fixedDeltaTime
            );

            // Apply velocity (preserve Y for gravity)
            Vector3 newVelocity = new Vector3(
                _currentVelocity.x, 
                _rigidbody.linearVelocity.y,  // Unity 6 uses linearVelocity
                _currentVelocity.z
            );
            
            _rigidbody.linearVelocity = newVelocity;
        }

        private void UpdateVisuals()
        {
            // Update player color based on combined active masks
            UpdatePlayerColor();
            
            // Optional: Rotate visual to face movement direction
            if (_currentVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_currentVelocity.normalized, Vector3.up);
                visualTransform.rotation = Quaternion.Slerp(
                    visualTransform.rotation, 
                    targetRotation, 
                    Time.fixedDeltaTime * 10f
                );
            }
        }

        private void UpdatePlayerColor()
        {
            if (_inventory == null) return;
            
            // Show combined active mask colors, or grey if nothing active
            ColorType combined = _inventory.GetCombinedActiveColor();
            
            // Get ALL renderers in the player hierarchy and set color on ALL of them
            var allRenderers = visualTransform?.GetComponentsInChildren<Renderer>();
            
            if (allRenderers != null && allRenderers.Length > 0)
            {
                Color playerColor = combined != ColorType.None ? combined.ToColor() : Color.grey;
                
                // Set color on ALL renderers
                foreach (var rend in allRenderers)
                {
                    // Set color using multiple property names for compatibility with different render pipelines
                    rend.material.color = playerColor;  // Sets _Color (Standard RP)
                    
                    if (rend.material.HasProperty("_BaseColor"))
                    {
                        rend.material.SetColor("_BaseColor", playerColor);  // URP/HDRP
                    }
                    
                    // Update emissive
                    if (rend.material.HasProperty("_EmissionColor"))
                    {
                        rend.material.EnableKeyword("_EMISSION");
                        rend.material.SetColor("_EmissionColor", playerColor * 1.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Called when inventory changes.
        /// </summary>
        private void OnInventoryChanged()
        {
            UpdatePlayerColor();
        }

        /// <summary>
        /// Called when a mask's active state is toggled.
        /// </summary>
        private void OnMaskToggled(int slotIndex, bool isActive)
        {
            ColorType mask = _inventory.GetSlot(slotIndex);
            if (isActive)
            {
                Debug.Log($"[PlayerController] Activated {mask.GetDisplayName()} mask (slot {slotIndex + 1})");
            }
            else
            {
                Debug.Log($"[PlayerController] Deactivated {mask.GetDisplayName()} mask (slot {slotIndex + 1})");
            }
            
            UpdatePlayerColor();
        }

        /// <summary>
        /// Toggles the active state of a mask slot (0-2).
        /// </summary>
        public void ToggleSlot(int slotIndex)
        {
            if (_inventory == null) return;
            _inventory.ToggleMask(slotIndex);
        }

        /// <summary>
        /// Deactivates all masks.
        /// </summary>
        public void DeactivateAllMasks()
        {
            if (_inventory == null) return;
            _inventory.DeactivateAll();
            Debug.Log("[PlayerController] All masks deactivated");
        }

        // Track if player is currently inside a barrier (for drop prevention)
        private ColorBarrier _currentBarrier;
        
        /// <summary>
        /// Drops the first active mask at the player's current grid position.
        /// </summary>
        public void DropMask()
        {
            if (_inventory == null) return;
            
            // Cannot drop mask while inside a barrier
            if (_currentBarrier != null)
            {
                Debug.Log("[PlayerController] Cannot drop mask while inside a barrier");
                return;
            }
            
            // Find first active slot with a mask
            var activeSlots = _inventory.GetActiveSlotIndices();
            if (activeSlots.Count == 0)
            {
                Debug.Log("[PlayerController] No active mask to drop");
                return;
            }
            
            int slotToDrop = activeSlots[0];
            ColorType colorToDrop = _inventory.DropMask(slotToDrop);
            
            if (colorToDrop == ColorType.None)
            {
                Debug.Log("[PlayerController] Failed to drop mask");
                return;
            }
            
            // Calculate grid position
            Vector3 dropPosition = GetCurrentGridPosition();
            
            // Check if there's already a mask at this position
            if (_currentTileMask != null && !_currentTileMask.IsCollected)
            {
                // Swap: pick up existing mask, drop new one
                ColorType existingColor = _currentTileMask.MaskColor;
                _currentTileMask.Collect();
                _inventory.TryAddMask(existingColor);
                Debug.Log($"[PlayerController] Swapped {colorToDrop.GetDisplayName()} with {existingColor.GetDisplayName()}");
            }
            
            // Request mask spawn at grid position
            OnMaskDropRequested?.Invoke(dropPosition, colorToDrop);
            
            // Spawn the dropped mask
            DroppedMask dropped = null;
            if (droppedMaskPrefab != null)
            {
                dropped = DroppedMask.Spawn(droppedMaskPrefab, dropPosition, colorToDrop);
            }
            else
            {
                // Fallback: Create a basic dropped mask at runtime
                dropped = CreateDroppedMaskAtRuntime(dropPosition, colorToDrop);
            }
            
            if (dropped != null)
            {
                dropped.PreventImmediatePickup();
                _currentTileMask = dropped;
            }
            
            Debug.Log($"[PlayerController] Dropped {colorToDrop.GetDisplayName()} mask at {dropPosition}");
        }

        /// <summary>
        /// Gets the current grid-aligned position.
        /// </summary>
        private Vector3 GetCurrentGridPosition()
        {
            Vector3 pos = transform.position;
            float x = Mathf.Round(pos.x / gridCellSize) * gridCellSize;
            float z = Mathf.Round(pos.z / gridCellSize) * gridCellSize;
            return new Vector3(x, 0.5f, z);
        }
        
        /// <summary>
        /// Creates a dropped mask at runtime when no prefab is assigned.
        /// This is a fallback to ensure mask dropping always works.
        /// </summary>
        private DroppedMask CreateDroppedMaskAtRuntime(Vector3 position, ColorType color)
        {
            // Create a simple sphere as the visual representation
            var maskObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            maskObj.name = $"DroppedMask_{color}";
            maskObj.transform.position = position;
            maskObj.transform.localScale = Vector3.one * 0.5f;
            
            // Set the color
            var renderer = maskObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color.ToColor();
            }
            
            // Configure the collider as a trigger
            var collider = maskObj.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // Add the DroppedMask component
            var droppedMask = maskObj.AddComponent<DroppedMask>();
            droppedMask.Initialize(position, color);
            
            return droppedMask;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Track dropped masks we're standing on
            var droppedMask = other.GetComponent<DroppedMask>();
            if (droppedMask != null)
            {
                _currentTileMask = droppedMask;
            }
            
            // Track barriers we're inside
            var barrier = other.GetComponent<ColorBarrier>();
            if (barrier != null)
            {
                _currentBarrier = barrier;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var droppedMask = other.GetComponent<DroppedMask>();
            if (droppedMask != null && droppedMask == _currentTileMask)
            {
                _currentTileMask = null;
            }
            
            var barrier = other.GetComponent<ColorBarrier>();
            if (barrier != null && barrier == _currentBarrier)
            {
                _currentBarrier = null;
            }
        }

        /// <summary>
        /// Teleports the player to a specific position.
        /// </summary>
        public void TeleportTo(Vector3 position)
        {
            _rigidbody.position = position;
            _currentVelocity = Vector3.zero;
            _rigidbody.linearVelocity = Vector3.zero;
        }

        /// <summary>
        /// Gets the player's inventory.
        /// </summary>
        public MaskInventory GetInventory() => _inventory;

        /// <summary>
        /// Gets the current movement velocity.
        /// </summary>
        public Vector3 CurrentVelocity => _currentVelocity;

        // Legacy compatibility
        [Obsolete("Use ToggleSlot instead")]
        public void EquipSlot(int slotIndex) => ToggleSlot(slotIndex);

        [Obsolete("Use DeactivateAllMasks instead")]
        public void UnequipMask() => DeactivateAllMasks();
    }
}

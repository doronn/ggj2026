using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Input;
using BreakingHue.Camera;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Third-person player controller using Rigidbody physics.
    /// Uses Unity's New Input System with camera-relative movement.
    /// Handles movement, mask toggling, and mask dropping.
    /// 
    /// Controls:
    /// - WASD/Left Stick: Move (camera-relative)
    /// - Keys 1/2/3 or D-Pad: Toggle mask active state
    /// - Key Q or X Button: Drop first active mask
    /// - Key 0/` or D-Pad Up: Deactivate all masks
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float deceleration = 40f;
        [SerializeField] private bool useCameraRelativeMovement = true;
        
        [Header("Visual")]
        [SerializeField] private Transform visualTransform;
        
        [Header("Mask Drop Settings")]
        [SerializeField] private GameObject droppedMaskPrefab;
        [SerializeField] private float gridCellSize = 1f;

        private Rigidbody _rigidbody;
        private MaskInventory _inventory;
        private GameCamera _gameCamera;
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
        private bool _usingManualInput;

        /// <summary>
        /// Event fired when player requests to drop a mask.
        /// Parameters: world position, color to drop
        /// </summary>
        public static event Action<Vector3, ColorType> OnMaskDropRequested;

        [Inject]
        public void Construct(MaskInventory inventory, GameCamera gameCamera)
        {
            _inventory = inventory;
            _gameCamera = gameCamera;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            
            // Try to get PlayerInput component first
            _playerInput = GetComponent<PlayerInput>();
            
            // Setup input - prefer InputManager, fallback to PlayerInput component, then manual
            SetupInput();

            if (visualTransform == null)
            {
                visualTransform = transform;
            }
            
            // Ensure player has the correct tag
            gameObject.tag = "Player";
        }

        private void Start()
        {
            // Fallback: If Zenject injection didn't happen, try to resolve manually
            if (_inventory == null)
            {
                var sceneContext = FindObjectOfType<Zenject.SceneContext>();
                if (sceneContext != null && sceneContext.Container != null)
                {
                    _inventory = sceneContext.Container.TryResolve<MaskInventory>();
                }
            }
            
            // Fallback for GameCamera
            if (_gameCamera == null)
            {
                _gameCamera = FindObjectOfType<GameCamera>();
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
            
            // Fallback: If droppedMaskPrefab not assigned, try to find one
            if (droppedMaskPrefab == null)
            {
                droppedMaskPrefab = Resources.Load<GameObject>("DroppedMask");
                if (droppedMaskPrefab == null)
                {
                    var existingMask = FindObjectOfType<DroppedMask>(true);
                    if (existingMask != null)
                    {
                        droppedMaskPrefab = existingMask.gameObject;
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
            
            // Only dispose actions we created manually
            if (_usingManualInput)
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

        private void SetupInput()
        {
            // Priority 1: Try InputManager
            if (InputManager.Instance != null)
            {
                SetupInputFromManager();
                return;
            }
            
            // Priority 2: Try PlayerInput component
            if (_playerInput != null && _playerInput.actions != null)
            {
                SetupInputFromPlayerInput();
                return;
            }
            
            // Priority 3: Manual fallback
            SetupManualInput();
        }

        private void SetupInputFromManager()
        {
            _moveAction = InputManager.Instance.GetPlayerAction("Move");
            _toggleSlot1Action = InputManager.Instance.GetPlayerAction("ToggleMask1");
            _toggleSlot2Action = InputManager.Instance.GetPlayerAction("ToggleMask2");
            _toggleSlot3Action = InputManager.Instance.GetPlayerAction("ToggleMask3");
            _deactivateAllAction = InputManager.Instance.GetPlayerAction("DeactivateAll");
            _dropMaskAction = InputManager.Instance.GetPlayerAction("DropMask");
            
            // Subscribe to performed events
            if (_toggleSlot1Action != null) _toggleSlot1Action.performed += _ => ToggleSlot(0);
            if (_toggleSlot2Action != null) _toggleSlot2Action.performed += _ => ToggleSlot(1);
            if (_toggleSlot3Action != null) _toggleSlot3Action.performed += _ => ToggleSlot(2);
            if (_deactivateAllAction != null) _deactivateAllAction.performed += _ => DeactivateAllMasks();
            if (_dropMaskAction != null) _dropMaskAction.performed += _ => DropMask();
            
            _usingManualInput = false;
            Debug.Log("[PlayerController] Using InputManager for input");
        }

        private void SetupInputFromPlayerInput()
        {
            _moveAction = _playerInput.actions["Move"];
            _toggleSlot1Action = _playerInput.actions.FindAction("ToggleMask1");
            _toggleSlot2Action = _playerInput.actions.FindAction("ToggleMask2");
            _toggleSlot3Action = _playerInput.actions.FindAction("ToggleMask3");
            _deactivateAllAction = _playerInput.actions.FindAction("DeactivateAll");
            _dropMaskAction = _playerInput.actions.FindAction("DropMask");
            
            // If actions are found, subscribe
            if (_toggleSlot1Action != null) _toggleSlot1Action.performed += _ => ToggleSlot(0);
            if (_toggleSlot2Action != null) _toggleSlot2Action.performed += _ => ToggleSlot(1);
            if (_toggleSlot3Action != null) _toggleSlot3Action.performed += _ => ToggleSlot(2);
            if (_deactivateAllAction != null) _deactivateAllAction.performed += _ => DeactivateAllMasks();
            if (_dropMaskAction != null) _dropMaskAction.performed += _ => DropMask();
            
            // If mask actions not found, create them manually
            if (_toggleSlot1Action == null)
            {
                SetupMaskInputManually();
            }
            
            _usingManualInput = false;
            Debug.Log("[PlayerController] Using PlayerInput component for input");
        }

        private void SetupManualInput()
        {
            _usingManualInput = true;
            
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
            _moveAction.AddBinding("<Gamepad>/leftStick").WithProcessors("StickDeadzone");
            
            _moveAction.Enable();
            
            SetupMaskInputManually();
            
            Debug.Log("[PlayerController] Using manual input setup (fallback)");
        }

        private void SetupMaskInputManually()
        {
            // Slot 1 - Key 1 / D-Pad Left
            _toggleSlot1Action = new InputAction("ToggleMask1", InputActionType.Button);
            _toggleSlot1Action.AddBinding("<Keyboard>/1");
            _toggleSlot1Action.AddBinding("<Keyboard>/numpad1");
            _toggleSlot1Action.AddBinding("<Gamepad>/dpad/left");
            _toggleSlot1Action.performed += _ => ToggleSlot(0);
            _toggleSlot1Action.Enable();
            
            // Slot 2 - Key 2 / D-Pad Down
            _toggleSlot2Action = new InputAction("ToggleMask2", InputActionType.Button);
            _toggleSlot2Action.AddBinding("<Keyboard>/2");
            _toggleSlot2Action.AddBinding("<Keyboard>/numpad2");
            _toggleSlot2Action.AddBinding("<Gamepad>/dpad/down");
            _toggleSlot2Action.performed += _ => ToggleSlot(1);
            _toggleSlot2Action.Enable();
            
            // Slot 3 - Key 3 / D-Pad Right
            _toggleSlot3Action = new InputAction("ToggleMask3", InputActionType.Button);
            _toggleSlot3Action.AddBinding("<Keyboard>/3");
            _toggleSlot3Action.AddBinding("<Keyboard>/numpad3");
            _toggleSlot3Action.AddBinding("<Gamepad>/dpad/right");
            _toggleSlot3Action.performed += _ => ToggleSlot(2);
            _toggleSlot3Action.Enable();
            
            // Deactivate All - Key 0/` / D-Pad Up
            _deactivateAllAction = new InputAction("DeactivateAll", InputActionType.Button);
            _deactivateAllAction.AddBinding("<Keyboard>/0");
            _deactivateAllAction.AddBinding("<Keyboard>/numpad0");
            _deactivateAllAction.AddBinding("<Keyboard>/backquote");
            _deactivateAllAction.AddBinding("<Gamepad>/dpad/up");
            _deactivateAllAction.performed += _ => DeactivateAllMasks();
            _deactivateAllAction.Enable();
            
            // Drop Mask - Key Q / X Button
            _dropMaskAction = new InputAction("DropMask", InputActionType.Button);
            _dropMaskAction.AddBinding("<Keyboard>/q");
            _dropMaskAction.AddBinding("<Gamepad>/buttonWest");
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
            if (_usingManualInput)
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
            Vector3 targetDirection;
            
            if (useCameraRelativeMovement && _gameCamera != null)
            {
                // Camera-relative movement
                Vector3 camForward = _gameCamera.Forward;
                Vector3 camRight = _gameCamera.Right;
                
                // Convert input to camera-relative direction
                targetDirection = (camForward * _inputDirection.y + camRight * _inputDirection.x).normalized;
            }
            else
            {
                // World-relative movement (legacy)
                targetDirection = new Vector3(_inputDirection.x, 0, _inputDirection.y).normalized;
            }
            
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
                _rigidbody.linearVelocity.y,
                _currentVelocity.z
            );
            
            _rigidbody.linearVelocity = newVelocity;
        }

        private void UpdateVisuals()
        {
            // Update player color based on combined active masks
            UpdatePlayerColor();
            
            // Rotate visual to face movement direction
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
            
            ColorType combined = _inventory.GetCombinedActiveColor();
            
            var allRenderers = visualTransform?.GetComponentsInChildren<Renderer>();
            
            if (allRenderers != null && allRenderers.Length > 0)
            {
                Color playerColor = combined != ColorType.None ? combined.ToColor() : Color.grey;
                
                foreach (var rend in allRenderers)
                {
                    rend.material.color = playerColor;
                    
                    if (rend.material.HasProperty("_BaseColor"))
                    {
                        rend.material.SetColor("_BaseColor", playerColor);
                    }
                    
                    if (rend.material.HasProperty("_EmissionColor"))
                    {
                        rend.material.EnableKeyword("_EMISSION");
                        rend.material.SetColor("_EmissionColor", playerColor * 1.5f);
                    }
                }
            }
        }

        private void OnInventoryChanged()
        {
            UpdatePlayerColor();
        }

        private void OnMaskToggled(int slotIndex, bool isActive)
        {
            ColorType mask = _inventory.GetSlot(slotIndex);
            Debug.Log($"[PlayerController] {(isActive ? "Activated" : "Deactivated")} {mask.GetDisplayName()} mask (slot {slotIndex + 1})");
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

        // Track if player is currently inside a barrier
        private ColorBarrier _currentBarrier;
        
        /// <summary>
        /// Drops the first active mask at the player's current grid position.
        /// </summary>
        public void DropMask()
        {
            if (_inventory == null) return;
            
            if (_currentBarrier != null)
            {
                Debug.Log("[PlayerController] Cannot drop mask while inside a barrier");
                return;
            }
            
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
            
            Vector3 dropPosition = GetCurrentGridPosition();
            
            if (_currentTileMask != null && !_currentTileMask.IsCollected)
            {
                ColorType existingColor = _currentTileMask.MaskColor;
                _currentTileMask.Collect();
                _inventory.TryAddMask(existingColor);
                Debug.Log($"[PlayerController] Swapped {colorToDrop.GetDisplayName()} with {existingColor.GetDisplayName()}");
            }
            
            OnMaskDropRequested?.Invoke(dropPosition, colorToDrop);
            
            DroppedMask dropped = null;
            if (droppedMaskPrefab != null)
            {
                dropped = DroppedMask.Spawn(droppedMaskPrefab, dropPosition, colorToDrop);
            }
            else
            {
                dropped = CreateDroppedMaskAtRuntime(dropPosition, colorToDrop);
            }
            
            if (dropped != null)
            {
                dropped.PreventImmediatePickup();
                _currentTileMask = dropped;
            }
            
            Debug.Log($"[PlayerController] Dropped {colorToDrop.GetDisplayName()} mask at {dropPosition}");
        }

        private Vector3 GetCurrentGridPosition()
        {
            Vector3 pos = transform.position;
            float x = Mathf.Round(pos.x / gridCellSize) * gridCellSize;
            float z = Mathf.Round(pos.z / gridCellSize) * gridCellSize;
            return new Vector3(x, 0.5f, z);
        }
        
        private DroppedMask CreateDroppedMaskAtRuntime(Vector3 position, ColorType color)
        {
            var maskObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            maskObj.name = $"DroppedMask_{color}";
            maskObj.transform.position = position;
            maskObj.transform.localScale = Vector3.one * 0.5f;
            
            var renderer = maskObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color.ToColor();
            }
            
            var collider = maskObj.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            var droppedMask = maskObj.AddComponent<DroppedMask>();
            droppedMask.Initialize(position, color);
            
            return droppedMask;
        }

        private void OnTriggerEnter(Collider other)
        {
            var droppedMask = other.GetComponent<DroppedMask>();
            if (droppedMask != null)
            {
                _currentTileMask = droppedMask;
            }
            
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

        /// <summary>
        /// Sets whether to use camera-relative movement.
        /// </summary>
        public void SetCameraRelativeMovement(bool enabled)
        {
            useCameraRelativeMovement = enabled;
        }

        // Legacy compatibility
        [Obsolete("Use ToggleSlot instead")]
        public void EquipSlot(int slotIndex) => ToggleSlot(slotIndex);

        [Obsolete("Use DeactivateAllMasks instead")]
        public void UnequipMask() => DeactivateAllMasks();
    }
}

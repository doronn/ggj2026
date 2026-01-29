using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// 3D top-down player controller using Rigidbody physics.
    /// Uses Unity's New Input System.
    /// Handles movement and mask equipping.
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
        
        private Rigidbody _rigidbody;
        private MaskInventory _inventory;
        private Vector2 _inputDirection;
        private Vector3 _currentVelocity;
        
        // Input System
        private InputAction _moveAction;
        private InputAction _equipSlot1Action;
        private InputAction _equipSlot2Action;
        private InputAction _equipSlot3Action;
        private InputAction _unequipAction;
        private PlayerInput _playerInput;

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
                // Try to get equip actions if defined in input asset
                _equipSlot1Action = _playerInput.actions.FindAction("EquipSlot1");
                _equipSlot2Action = _playerInput.actions.FindAction("EquipSlot2");
                _equipSlot3Action = _playerInput.actions.FindAction("EquipSlot3");
                _unequipAction = _playerInput.actions.FindAction("Unequip");
                
                // If not defined, create them manually
                if (_equipSlot1Action == null)
                    SetupEquipInputManually();
            }

            if (visualTransform == null)
            {
                visualTransform = transform;
            }
            
            // Ensure player has the correct tag
            gameObject.tag = "Player";
            
            // Subscribe to inventory events for visual updates
            if (_inventory != null)
            {
                _inventory.OnMaskEquipped += OnMaskEquipped;
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnMaskEquipped -= OnMaskEquipped;
            }
            
            if (_playerInput == null)
            {
                _moveAction?.Dispose();
                _equipSlot1Action?.Dispose();
                _equipSlot2Action?.Dispose();
                _equipSlot3Action?.Dispose();
                _unequipAction?.Dispose();
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
            
            SetupEquipInputManually();
        }

        private void SetupEquipInputManually()
        {
            // Slot 1 - Key 1
            _equipSlot1Action = new InputAction("EquipSlot1", InputActionType.Button);
            _equipSlot1Action.AddBinding("<Keyboard>/1");
            _equipSlot1Action.AddBinding("<Keyboard>/numpad1");
            _equipSlot1Action.performed += _ => EquipSlot(0);
            _equipSlot1Action.Enable();
            
            // Slot 2 - Key 2
            _equipSlot2Action = new InputAction("EquipSlot2", InputActionType.Button);
            _equipSlot2Action.AddBinding("<Keyboard>/2");
            _equipSlot2Action.AddBinding("<Keyboard>/numpad2");
            _equipSlot2Action.performed += _ => EquipSlot(1);
            _equipSlot2Action.Enable();
            
            // Slot 3 - Key 3
            _equipSlot3Action = new InputAction("EquipSlot3", InputActionType.Button);
            _equipSlot3Action.AddBinding("<Keyboard>/3");
            _equipSlot3Action.AddBinding("<Keyboard>/numpad3");
            _equipSlot3Action.performed += _ => EquipSlot(2);
            _equipSlot3Action.Enable();
            
            // Unequip - Key 0 or Escape
            _unequipAction = new InputAction("Unequip", InputActionType.Button);
            _unequipAction.AddBinding("<Keyboard>/0");
            _unequipAction.AddBinding("<Keyboard>/numpad0");
            _unequipAction.AddBinding("<Keyboard>/backquote"); // ` key
            _unequipAction.performed += _ => UnequipMask();
            _unequipAction.Enable();
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _equipSlot1Action?.Enable();
            _equipSlot2Action?.Enable();
            _equipSlot3Action?.Enable();
            _unequipAction?.Enable();
        }

        private void OnDisable()
        {
            if (_playerInput == null)
            {
                _moveAction?.Disable();
                _equipSlot1Action?.Disable();
                _equipSlot2Action?.Disable();
                _equipSlot3Action?.Disable();
                _unequipAction?.Disable();
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
            // Update player color based on equipped mask
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
            
            var renderer = visualTransform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color playerColor;
                
                // Show equipped mask color, or grey if nothing equipped
                ColorType equipped = _inventory.EquippedMask;
                if (equipped != ColorType.None)
                {
                    playerColor = equipped.ToColor();
                }
                else
                {
                    playerColor = Color.grey;
                }
                
                renderer.material.color = playerColor;
                
                // Update emissive
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", playerColor * 1.5f);
                }
            }
        }

        /// <summary>
        /// Called when a mask is equipped or unequipped.
        /// </summary>
        private void OnMaskEquipped(ColorType mask)
        {
            if (mask != ColorType.None)
            {
                Debug.Log($"[PlayerController] Equipped {mask.GetDisplayName()} mask");
            }
            else
            {
                Debug.Log("[PlayerController] Mask unequipped");
            }
            
            // Force visual update
            UpdatePlayerColor();
        }

        /// <summary>
        /// Equips the mask in the specified slot (0-2).
        /// </summary>
        public void EquipSlot(int slotIndex)
        {
            if (_inventory == null) return;
            _inventory.EquipSlot(slotIndex);
        }

        /// <summary>
        /// Unequips the currently worn mask.
        /// </summary>
        public void UnequipMask()
        {
            if (_inventory == null) return;
            _inventory.UnequipMask();
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
        /// Gets the current movement velocity.
        /// </summary>
        public Vector3 CurrentVelocity => _currentVelocity;
    }
}

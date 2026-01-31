using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using BreakingHue.Save;
using BreakingHue.Input;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Handles the self-destruct / reload checkpoint mechanic.
    /// Player must hold the self-destruct button for a duration to trigger.
    /// Provides progress events for UI feedback.
    /// </summary>
    public class SelfDestructController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float holdDuration = 2f;
        [SerializeField] private bool resetOnRelease = true;
        
        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip chargingSound;
        [SerializeField] private AudioClip triggerSound;
        [SerializeField] private AudioClip cancelSound;
        
        private CheckpointManager _checkpointManager;
        private InputManager _inputManager;
        
        private InputAction _selfDestructAction;
        private float _holdTime;
        private bool _isHolding;
        private bool _hasTriggered;
        
        private AudioSource _audioSource;

        /// <summary>
        /// Event fired when self-destruct is triggered (checkpoint restore).
        /// </summary>
        public static event Action OnSelfDestructTriggered;
        
        /// <summary>
        /// Event fired every frame while holding, with progress (0-1).
        /// </summary>
        public static event Action<float> OnSelfDestructProgress;
        
        /// <summary>
        /// Event fired when self-destruct is cancelled (button released early).
        /// </summary>
        public static event Action OnSelfDestructCancelled;
        
        /// <summary>
        /// Event fired when self-destruct button is first pressed.
        /// </summary>
        public static event Action OnSelfDestructStarted;

        /// <summary>
        /// Current progress of the self-destruct hold (0-1).
        /// </summary>
        public float Progress => Mathf.Clamp01(_holdTime / holdDuration);
        
        /// <summary>
        /// Whether the player is currently holding the self-destruct button.
        /// </summary>
        public bool IsHolding => _isHolding;

        [Inject]
        public void Construct(CheckpointManager checkpointManager)
        {
            _checkpointManager = checkpointManager;
        }

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null && (chargingSound != null || triggerSound != null || cancelSound != null))
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            // #region agent log
            System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                $"{{\"hypothesisId\":\"H1\",\"location\":\"SelfDestructController.cs:Start\",\"message\":\"SelfDestructController started\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
            // #endregion
            
            SetupInput();
            
            // Try to get InputManager if not injected
            if (_inputManager == null)
            {
                _inputManager = InputManager.Instance;
            }
            
            // #region agent log
            System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                $"{{\"hypothesisId\":\"H2,H3\",\"location\":\"SelfDestructController.cs:Start\",\"message\":\"Setup complete\",\"data\":{{\"actionNull\":{(_selfDestructAction == null).ToString().ToLower()},\"actionEnabled\":{(_selfDestructAction?.enabled ?? false).ToString().ToLower()},\"checkpointMgrNull\":{(_checkpointManager == null).ToString().ToLower()}}},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
            // #endregion
        }

        private void SetupInput()
        {
            // Try to get from InputManager
            if (InputManager.Instance != null)
            {
                _selfDestructAction = InputManager.Instance.GetPlayerAction("SelfDestruct");
            }
            
            // Fallback: create manual input action
            if (_selfDestructAction == null)
            {
                _selfDestructAction = new InputAction("SelfDestruct", InputActionType.Button);
                _selfDestructAction.AddBinding("<Keyboard>/r");
                _selfDestructAction.AddBinding("<Gamepad>/buttonEast");
            }
            
            _selfDestructAction.Enable();
        }

        private void OnEnable()
        {
            _selfDestructAction?.Enable();
        }

        private void OnDisable()
        {
            _selfDestructAction?.Disable();
            
            // Clean up if disabled while holding
            if (_isHolding)
            {
                CancelSelfDestruct();
            }
        }

        private void Update()
        {
            if (_selfDestructAction == null)
            {
                // #region agent log
                if (Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
                    System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                        $"{{\"hypothesisId\":\"H2\",\"location\":\"SelfDestructController.cs:Update\",\"message\":\"selfDestructAction is NULL\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
                // #endregion
                return;
            }
            
            bool buttonHeld = _selfDestructAction.IsPressed();
            
            // #region agent log
            if (buttonHeld)
                System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                    $"{{\"hypothesisId\":\"H4,H5\",\"location\":\"SelfDestructController.cs:Update\",\"message\":\"Button held\",\"data\":{{\"holdTime\":{_holdTime:F2},\"holdDuration\":{holdDuration:F2},\"isHolding\":{_isHolding.ToString().ToLower()},\"hasTriggered\":{_hasTriggered.ToString().ToLower()}}},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
            // #endregion
            
            if (buttonHeld && !_hasTriggered)
            {
                if (!_isHolding)
                {
                    StartSelfDestruct();
                }
                
                UpdateHoldProgress();
            }
            else if (_isHolding && !buttonHeld)
            {
                CancelSelfDestruct();
            }
            
            // Reset trigger flag when button is released
            if (!buttonHeld)
            {
                _hasTriggered = false;
            }
        }

        private void StartSelfDestruct()
        {
            _isHolding = true;
            _holdTime = 0f;
            
            OnSelfDestructStarted?.Invoke();
            
            // Play charging sound
            if (_audioSource != null && chargingSound != null)
            {
                _audioSource.clip = chargingSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            
            Debug.Log("[SelfDestruct] Started holding self-destruct button");
        }

        private void UpdateHoldProgress()
        {
            _holdTime += Time.deltaTime;
            
            float progress = Progress;
            OnSelfDestructProgress?.Invoke(progress);
            
            // Check if complete
            if (_holdTime >= holdDuration)
            {
                TriggerSelfDestruct();
            }
        }

        private void CancelSelfDestruct()
        {
            if (!_isHolding) return;
            
            _isHolding = false;
            
            if (resetOnRelease)
            {
                _holdTime = 0f;
            }
            
            // Stop charging sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            // Play cancel sound
            if (_audioSource != null && cancelSound != null)
            {
                _audioSource.PlayOneShot(cancelSound);
            }
            
            OnSelfDestructCancelled?.Invoke();
            OnSelfDestructProgress?.Invoke(0f);
            
            Debug.Log("[SelfDestruct] Cancelled self-destruct");
        }

        private void TriggerSelfDestruct()
        {
            // #region agent log
            System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                $"{{\"hypothesisId\":\"H3\",\"location\":\"SelfDestructController.cs:TriggerSelfDestruct\",\"message\":\"Self-destruct triggered\",\"data\":{{\"checkpointMgrNull\":{(_checkpointManager == null).ToString().ToLower()}}},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
            // #endregion
            
            _isHolding = false;
            _hasTriggered = true;
            _holdTime = 0f;
            
            // Stop charging sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            // Play trigger sound
            if (_audioSource != null && triggerSound != null)
            {
                _audioSource.PlayOneShot(triggerSound);
            }
            
            Debug.Log("[SelfDestruct] Self-destruct triggered! Restoring checkpoint...");
            
            OnSelfDestructTriggered?.Invoke();
            OnSelfDestructProgress?.Invoke(0f);
            
            // Restore checkpoint
            if (_checkpointManager != null)
            {
                _checkpointManager.RestoreCheckpoint();
            }
            else
            {
                // Fallback: try to find CheckpointManager
                var checkpointManager = FindObjectOfType<CheckpointManager>();
                // #region agent log
                System.IO.File.AppendAllText("/Users/doronnacash/RiderProjects/adventure-game/ggj2026/.cursor/debug.log", 
                    $"{{\"hypothesisId\":\"H3\",\"location\":\"SelfDestructController.cs:TriggerSelfDestruct\",\"message\":\"Fallback CheckpointManager search\",\"data\":{{\"found\":{(checkpointManager != null).ToString().ToLower()}}},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()}}}\n");
                // #endregion
                if (checkpointManager != null)
                {
                    checkpointManager.RestoreCheckpoint();
                }
                else
                {
                    Debug.LogWarning("[SelfDestruct] No CheckpointManager found!");
                }
            }
        }

        /// <summary>
        /// Sets the hold duration required to trigger self-destruct.
        /// </summary>
        public void SetHoldDuration(float duration)
        {
            holdDuration = Mathf.Max(0.1f, duration);
        }

        /// <summary>
        /// Force trigger self-destruct immediately (for testing/debug).
        /// </summary>
        public void ForceTrigger()
        {
            TriggerSelfDestruct();
        }

        /// <summary>
        /// Force cancel self-destruct if currently holding.
        /// </summary>
        public void ForceCancel()
        {
            if (_isHolding)
            {
                CancelSelfDestruct();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            holdDuration = Mathf.Max(0.1f, holdDuration);
        }
#endif
    }
}

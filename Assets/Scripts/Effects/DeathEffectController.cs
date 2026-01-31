using System;
using UnityEngine;
using Zenject;
using BreakingHue.Gameplay;
using BreakingHue.Save;
using BreakingHue.UI;

namespace BreakingHue.Effects
{
    /// <summary>
    /// Manages the death effect sequence when player is killed by a barrel.
    /// Coordinates explosion VFX, white-out effect, and delayed checkpoint restore.
    /// 
    /// Sequence:
    /// 1. Player explodes (barrel triggers OnPlayerExploded)
    /// 2. Spawn explosion particles at death location
    /// 3. Start white-out fade
    /// 4. Once white, trigger checkpoint restore
    /// 5. After level resets, fade back from white
    /// </summary>
    public class DeathEffectController : MonoBehaviour
    {
        [Header("Explosion Effect")]
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private float explosionDuration = 2f;
        
        [Header("White-Out Effect")]
        [SerializeField] private WhiteOutEffect whiteOutEffect;
        [SerializeField] private float delayBeforeWhiteOut = 0.3f;
        
        [Header("Timing")]
        [SerializeField] private float delayAfterRestoreBeforeFadeBack = 0.2f;
        
        private CheckpointManager _checkpointManager;
        private Vector3 _deathPosition;
        private bool _isProcessingDeath;

        /// <summary>
        /// Event fired when death sequence starts.
        /// </summary>
        public static event Action OnDeathSequenceStarted;
        
        /// <summary>
        /// Event fired when death sequence completes (after fade back).
        /// </summary>
        public static event Action OnDeathSequenceCompleted;

        [Inject]
        public void Construct(CheckpointManager checkpointManager)
        {
            _checkpointManager = checkpointManager;
        }

        private void Awake()
        {
            // Subscribe to player exploded event
            ExplodingBarrel.OnPlayerExploded += OnPlayerExploded;
        }

        private void OnDestroy()
        {
            ExplodingBarrel.OnPlayerExploded -= OnPlayerExploded;
        }

        private void Start()
        {
            // Try to find WhiteOutEffect if not assigned
            if (whiteOutEffect == null)
            {
                whiteOutEffect = FindObjectOfType<WhiteOutEffect>();
            }
            
            // Fallback: Try to find CheckpointManager if not injected
            if (_checkpointManager == null)
            {
                _checkpointManager = FindObjectOfType<CheckpointManager>();
            }
        }

        /// <summary>
        /// Called when the player is killed by a barrel explosion.
        /// </summary>
        private void OnPlayerExploded()
        {
            if (_isProcessingDeath) return;
            
            // Find player position for explosion spawn
            var player = GameObject.FindGameObjectWithTag("Player");
            _deathPosition = player != null ? player.transform.position : Vector3.zero;
            
            StartDeathSequence();
        }

        /// <summary>
        /// Starts the death effect sequence.
        /// </summary>
        private void StartDeathSequence()
        {
            _isProcessingDeath = true;
            
            Debug.Log("[DeathEffectController] Starting death sequence");
            OnDeathSequenceStarted?.Invoke();
            
            // Spawn explosion effect
            SpawnExplosion();
            
            // Start white-out after delay
            Invoke(nameof(StartWhiteOut), delayBeforeWhiteOut);
        }

        /// <summary>
        /// Spawns the explosion particle effect at the death position.
        /// </summary>
        private void SpawnExplosion()
        {
            if (explosionPrefab == null)
            {
                Debug.Log("[DeathEffectController] No explosion prefab assigned - skipping explosion VFX");
                return;
            }
            
            var explosionInstance = Instantiate(explosionPrefab, _deathPosition, Quaternion.identity);
            
            // Auto-destroy after duration
            Destroy(explosionInstance, explosionDuration);
            
            Debug.Log($"[DeathEffectController] Spawned explosion at {_deathPosition}");
        }

        /// <summary>
        /// Starts the white-out fade effect.
        /// </summary>
        private void StartWhiteOut()
        {
            if (whiteOutEffect != null)
            {
                whiteOutEffect.FadeToWhite(OnWhiteOutComplete);
            }
            else
            {
                // No white-out effect available, proceed directly to restore
                Debug.LogWarning("[DeathEffectController] No WhiteOutEffect found - restoring immediately");
                RestoreCheckpoint();
            }
        }

        /// <summary>
        /// Called when the screen is fully white.
        /// </summary>
        private void OnWhiteOutComplete()
        {
            Debug.Log("[DeathEffectController] White-out complete, restoring checkpoint");
            
            // Trigger checkpoint restore
            RestoreCheckpoint();
            
            // Delay before fading back
            Invoke(nameof(StartFadeBack), delayAfterRestoreBeforeFadeBack);
        }

        /// <summary>
        /// Restores the checkpoint (level reset).
        /// </summary>
        private void RestoreCheckpoint()
        {
            if (_checkpointManager != null)
            {
                _checkpointManager.RestoreCheckpoint();
            }
            else
            {
                Debug.LogError("[DeathEffectController] No CheckpointManager found - cannot restore!");
            }
        }

        /// <summary>
        /// Starts fading back from white.
        /// </summary>
        private void StartFadeBack()
        {
            if (whiteOutEffect != null)
            {
                whiteOutEffect.FadeFromWhite(OnFadeBackComplete);
            }
            else
            {
                OnFadeBackComplete();
            }
        }

        /// <summary>
        /// Called when the fade back from white is complete.
        /// </summary>
        private void OnFadeBackComplete()
        {
            _isProcessingDeath = false;
            
            Debug.Log("[DeathEffectController] Death sequence complete");
            OnDeathSequenceCompleted?.Invoke();
        }

        /// <summary>
        /// Cancels the death sequence if it's in progress.
        /// Useful for debug/testing.
        /// </summary>
        public void CancelDeathSequence()
        {
            CancelInvoke();
            
            if (whiteOutEffect != null)
            {
                whiteOutEffect.ClearOverlay();
            }
            
            _isProcessingDeath = false;
        }

        /// <summary>
        /// Returns true if a death sequence is currently in progress.
        /// </summary>
        public bool IsProcessingDeath => _isProcessingDeath;
        
        /// <summary>
        /// Gets or sets the explosion prefab.
        /// Assign your particle system prefab here.
        /// </summary>
        public GameObject ExplosionPrefab
        {
            get => explosionPrefab;
            set => explosionPrefab = value;
        }
    }
}

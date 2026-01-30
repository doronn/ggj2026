using System;
using UnityEngine;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Hidden area block that reveals what's underneath when touched.
    /// 
    /// Behavior:
    /// - Displays as dark gray (not true black) to distinguish from Black barriers
    /// - Does NOT block movement (no solid collider)
    /// - When any entity enters the trigger: disables renderer and collider
    /// - Reveals whatever is beneath/behind the block
    /// - State persists for checkpoint saving
    /// </summary>
    public class HiddenBlock : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Color hiddenColor = new Color(0.25f, 0.25f, 0.25f, 1f); // Dark gray
        
        [Header("Optional Effects")]
        [SerializeField] private ParticleSystem revealVFX;
        [SerializeField] private AudioSource revealSFX;
        [SerializeField] private float fadeOutDuration = 0.3f;

        private Renderer _renderer;
        private Collider _triggerCollider;
        private bool _isRevealed;
        private float _fadeTimer;
        private Color _originalColor;

        /// <summary>
        /// Unique ID for this hidden block (for save/load).
        /// </summary>
        public string BlockId { get; private set; }

        /// <summary>
        /// Event fired when any hidden block is revealed.
        /// </summary>
        public static event Action<HiddenBlock> OnBlockRevealed;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _triggerCollider = GetComponent<Collider>();
            
            // Generate unique ID based on position (more stable than GUID for level objects)
            BlockId = $"hidden_{transform.position.x:F2}_{transform.position.y:F2}_{transform.position.z:F2}";
            
            // Ensure collider is trigger
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }
            
            // Set initial color
            if (_renderer != null)
            {
                _renderer.material.color = hiddenColor;
                _originalColor = hiddenColor;
            }
        }

        private void Update()
        {
            // Handle fade out animation
            if (_isRevealed && fadeOutDuration > 0 && _fadeTimer < fadeOutDuration)
            {
                _fadeTimer += Time.deltaTime;
                float t = _fadeTimer / fadeOutDuration;
                
                if (_renderer != null)
                {
                    Color color = _originalColor;
                    color.a = Mathf.Lerp(1f, 0f, t);
                    _renderer.material.color = color;
                }
                
                if (t >= 1f)
                {
                    CompleteReveal();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isRevealed) return;
            
            // Reveal when anything enters (player or bot)
            if (other.CompareTag("Player") || other.GetComponent<Bot.BotController>() != null)
            {
                Reveal();
            }
        }

        /// <summary>
        /// Reveals the hidden block, disabling its visual and collider.
        /// </summary>
        public void Reveal()
        {
            if (_isRevealed) return;
            
            _isRevealed = true;
            _fadeTimer = 0f;
            
            Debug.Log($"[HiddenBlock] Revealed at {transform.position}");
            
            // Play effects
            if (revealVFX != null)
            {
                revealVFX.Play();
            }
            
            if (revealSFX != null)
            {
                revealSFX.Play();
            }
            
            // If no fade duration, complete immediately
            if (fadeOutDuration <= 0)
            {
                CompleteReveal();
            }
            
            OnBlockRevealed?.Invoke(this);
        }

        private void CompleteReveal()
        {
            // Disable renderer and collider
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
            
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
            }
        }

        /// <summary>
        /// Restores the hidden block to its unrevealed state.
        /// Used when restoring from checkpoint.
        /// </summary>
        public void Restore()
        {
            _isRevealed = false;
            _fadeTimer = 0f;
            
            if (_renderer != null)
            {
                _renderer.enabled = true;
                _renderer.material.color = hiddenColor;
            }
            
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }
        }

        /// <summary>
        /// Sets the revealed state directly (for loading from save).
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            _isRevealed = revealed;
            
            if (revealed)
            {
                CompleteReveal();
            }
            else
            {
                Restore();
            }
        }

        /// <summary>
        /// Returns true if this block has been revealed.
        /// </summary>
        public bool IsRevealed => _isRevealed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            
            if (_renderer != null && !Application.isPlaying)
            {
                // Preview color in editor
                _renderer.sharedMaterial.color = hiddenColor;
            }
        }

        private void OnDrawGizmos()
        {
            // Draw hidden indicator
            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawCube(transform.position, Vector3.one * 0.95f);
            
            // Draw question mark-like indicator
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.3f, 0.15f);
        }
#endif
    }
}

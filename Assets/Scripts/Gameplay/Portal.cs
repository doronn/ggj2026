using System;
using UnityEngine;
using Zenject;
using BreakingHue.Level;
using BreakingHue.Level.Data;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Bidirectional portal that connects levels or areas within the same level.
    /// Can be configured as a checkpoint to save player progress.
    /// 
    /// Behavior:
    /// - On player enter: Trigger level transition or intra-level teleport
    /// - If checkpoint: Save game state before transitioning
    /// - Visual distinction between regular portals and checkpoint portals
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [Header("Portal Settings")]
        [SerializeField] private string portalId;
        [SerializeField] private EntranceExitLink link;
        [SerializeField] private bool isCheckpoint;
        
        [Header("Visual")]
        [SerializeField] private Renderer portalRenderer;
        [SerializeField] private Color normalPortalColor = new Color(0f, 0.8f, 1f, 1f); // Cyan
        [SerializeField] private Color checkpointPortalColor = new Color(1f, 0.8f, 0f, 1f); // Gold
        [SerializeField] private ParticleSystem portalVFX;
        
        [Header("Transition Settings")]
        [SerializeField] private float transitionDelay = 0.5f;
        
        private LevelManager _levelManager;
        private bool _isTransitioning;

        /// <summary>
        /// Event fired when a player enters a checkpoint portal.
        /// Used by CheckpointManager.
        /// </summary>
        public static event Action<Portal> OnCheckpointReached;

        /// <summary>
        /// Event fired when a player enters any portal.
        /// </summary>
        public static event Action<Portal> OnPortalEntered;

        /// <summary>
        /// Gets the portal's unique ID.
        /// </summary>
        public string PortalId => portalId;

        /// <summary>
        /// Gets whether this portal is a checkpoint.
        /// </summary>
        public bool IsCheckpoint => isCheckpoint;

        /// <summary>
        /// Gets the portal's link asset.
        /// </summary>
        public EntranceExitLink Link => link;

        [Inject]
        public void Construct(LevelManager levelManager)
        {
            _levelManager = levelManager;
        }

        private void Awake()
        {
            if (portalRenderer == null)
            {
                portalRenderer = GetComponentInChildren<Renderer>();
            }
            
            UpdateVisualColor();
        }

        /// <summary>
        /// Initialize the portal with its configuration.
        /// </summary>
        public void Initialize(string id, EntranceExitLink linkAsset, bool checkpoint)
        {
            portalId = id;
            link = linkAsset;
            isCheckpoint = checkpoint;
            UpdateVisualColor();
        }

        private void UpdateVisualColor()
        {
            if (portalRenderer != null)
            {
                Color color = isCheckpoint ? checkpointPortalColor : normalPortalColor;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    portalRenderer.sharedMaterial.color = color;
                    if (portalRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                    {
                        portalRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                        portalRenderer.sharedMaterial.SetColor("_EmissionColor", color * 2f);
                    }
                }
                else
#endif
                {
                    portalRenderer.material.color = color;
                    if (portalRenderer.material.HasProperty("_EmissionColor"))
                    {
                        portalRenderer.material.EnableKeyword("_EMISSION");
                        portalRenderer.material.SetColor("_EmissionColor", color * 2f);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTransitioning) return;
            
            // Only respond to player
            if (!other.CompareTag("Player")) return;

            OnPortalEntered?.Invoke(this);
            
            // Trigger checkpoint save if this is a checkpoint portal
            if (isCheckpoint)
            {
                OnCheckpointReached?.Invoke(this);
            }
            
            // Start transition
            StartTransition();
        }

        private void StartTransition()
        {
            if (link == null)
            {
                Debug.LogWarning($"[Portal] Portal {portalId} has no link configured");
                return;
            }

            _isTransitioning = true;
            
            // Play transition effect
            if (portalVFX != null)
            {
                portalVFX.Play();
            }

            // Delay the actual transition for visual effect
            if (transitionDelay > 0)
            {
                Invoke(nameof(ExecuteTransition), transitionDelay);
            }
            else
            {
                ExecuteTransition();
            }
        }

        private void ExecuteTransition()
        {
            if (_levelManager == null)
            {
                _levelManager = FindObjectOfType<LevelManager>();
            }

            if (_levelManager != null && link != null)
            {
                _levelManager.TransitionToLevel(link, portalId);
            }
            else
            {
                Debug.LogError($"[Portal] Cannot transition - LevelManager or Link is null");
            }

            _isTransitioning = false;
        }

        /// <summary>
        /// Gets the destination information for this portal.
        /// </summary>
        public bool GetDestination(out LevelData destLevel, out string destPortalId)
        {
            if (link == null)
            {
                destLevel = null;
                destPortalId = null;
                return false;
            }

            return link.GetDestination(portalId, out destLevel, out destPortalId);
        }

        /// <summary>
        /// Gets the spawn position for entities exiting this portal.
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            // Spawn slightly in front of the portal
            return transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (portalRenderer == null)
                portalRenderer = GetComponentInChildren<Renderer>();
            
            UpdateVisualColor();
        }

        private void OnDrawGizmos()
        {
            // Draw portal indicator
            Gizmos.color = isCheckpoint ? checkpointPortalColor : normalPortalColor;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.9f);
            
            // Draw direction arrow
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
            
            // Draw checkpoint star if applicable
            if (isCheckpoint)
            {
                Gizmos.color = Color.yellow;
                Vector3 starPos = transform.position + Vector3.up * 1.5f;
                Gizmos.DrawWireSphere(starPos, 0.2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw link to destination if available
            if (link != null)
            {
                Gizmos.color = Color.green;
                
                // Try to find the linked portal in the scene
                var allPortals = FindObjectsOfType<Portal>();
                foreach (var portal in allPortals)
                {
                    if (portal != this && link.ConnectsPortal(portal.portalId))
                    {
                        Gizmos.DrawLine(transform.position, portal.transform.position);
                    }
                }
            }
        }
#endif
    }
}

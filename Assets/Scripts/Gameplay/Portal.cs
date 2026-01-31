using System;
using UnityEngine;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Level;
using BreakingHue.Level.Data;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Bidirectional portal that connects levels or areas within the same level.
    /// Can be configured as a checkpoint to save player progress.
    /// Can also be configured as an end game portal.
    /// 
    /// Behavior:
    /// - On player enter: Trigger level transition or intra-level teleport
    /// - If checkpoint: Save game state before transitioning
    /// - If end game: Trigger end game sequence
    /// - Visual distinction between regular, checkpoint, and end game portals
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [Header("Portal Settings")]
        [SerializeField] private string portalId;
        [SerializeField] private EntranceExitLink link;
        [SerializeField] private bool isCheckpoint;
        
        [Header("End Game Settings")]
        [SerializeField] private bool isEndGame;
        [SerializeField] private EndGameConfig endGameConfig;
        
        [Header("Visual")]
        [SerializeField] private Renderer portalRenderer;
        [SerializeField] private Color normalPortalColor = new Color(0f, 0.8f, 1f, 1f); // Cyan
        [SerializeField] private Color checkpointPortalColor = new Color(1f, 0.8f, 0f, 1f); // Gold
        [SerializeField] private Color endGamePortalColor = new Color(1f, 0.4f, 1f, 1f); // Magenta/Pink
        [SerializeField] private ParticleSystem portalVFX;
        
        [Header("Transition Settings")]
        [SerializeField] private float transitionDelay = 0.5f;
        
        private LevelManager _levelManager;
        private bool _isTransitioning;
        private bool _checkpointTriggeredThisEntry; // Prevents multiple checkpoint triggers while standing on portal
        private bool _requiresExitBeforeTransition; // Prevents immediate transition when player spawns on portal

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
        /// Gets whether this portal triggers the end game.
        /// </summary>
        public bool IsEndGame => isEndGame;
        
        /// <summary>
        /// Gets the end game configuration for this portal.
        /// </summary>
        public EndGameConfig EndGameConfig => endGameConfig;

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
            Initialize(id, linkAsset, checkpoint, false, null);
        }
        
        /// <summary>
        /// Initialize the portal with full configuration including end game.
        /// </summary>
        public void Initialize(string id, EntranceExitLink linkAsset, bool checkpoint, bool endGame, EndGameConfig endConfig)
        {
            portalId = id;
            link = linkAsset;
            isCheckpoint = checkpoint;
            isEndGame = endGame;
            endGameConfig = endConfig;
            UpdateVisualColor();
            
            // Assume player might be inside until we verify otherwise
            // This prevents immediate transition if spawned on portal
            _requiresExitBeforeTransition = true;
            
            // Check if player is actually inside after a short delay for physics to settle
            Invoke(nameof(CheckForPlayerInsideAndArm), 0.15f);
        }
        
        private void CheckForPlayerInsideAndArm()
        {
            // Check if player is overlapping with this portal
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Collider[] overlaps = Physics.OverlapBox(
                    collider.bounds.center,
                    collider.bounds.extents,
                    transform.rotation
                );
                
                foreach (var overlap in overlaps)
                {
                    if (overlap.CompareTag("Player"))
                    {
                        // Player IS inside - keep the flag set, they must exit first
                        Debug.Log($"[Portal] Player inside portal {portalId} - must exit before transition");
                        return;
                    }
                }
            }
            
            // Player is NOT inside this portal - arm it immediately
            _requiresExitBeforeTransition = false;
            Debug.Log($"[Portal] Portal {portalId} armed (player not inside)");
        }

        private void UpdateVisualColor()
        {
            if (portalRenderer != null)
            {
                // Determine color based on portal type (priority: endGame > checkpoint > normal)
                Color color;
                if (isEndGame)
                {
                    color = endGamePortalColor;
                }
                else if (isCheckpoint)
                {
                    color = checkpointPortalColor;
                }
                else
                {
                    color = normalPortalColor;
                }
                
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
            
            // If player spawned on this portal, they must exit first before it activates
            if (_requiresExitBeforeTransition)
            {
                Debug.Log($"[Portal] Player must exit portal {portalId} before it can be used (spawned on it)");
                return;
            }

            // Reset the checkpoint trigger flag when player enters
            _checkpointTriggeredThisEntry = false;

            OnPortalEntered?.Invoke(this);
            
            // NOTE: Checkpoint is now saved on ARRIVAL at destination, not on entering source portal
            // See StartTransition/ExecuteTransition for checkpoint handling
            
            // Start transition
            StartTransition();
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            // Reset flags when player leaves portal
            _checkpointTriggeredThisEntry = false;
            _isTransitioning = false;
            
            // Portal is now armed - player exited so they can re-enter to trigger transition
            if (_requiresExitBeforeTransition)
            {
                _requiresExitBeforeTransition = false;
                Debug.Log($"[Portal] Portal {portalId} is now armed - player exited");
            }
        }

        private void StartTransition()
        {
            _isTransitioning = true;
            
            // Play transition effect
            if (portalVFX != null)
            {
                portalVFX.Play();
            }

            // If no link, handle checkpoint immediately and return
            if (link == null)
            {
                // Still save checkpoint if this is a checkpoint portal (player stays here)
                // But only once per entry
                if (isCheckpoint && !_checkpointTriggeredThisEntry)
                {
                    _checkpointTriggeredThisEntry = true;
                    OnCheckpointReached?.Invoke(this);
                    Debug.Log($"[Portal] Checkpoint saved at portal {portalId}");
                }
                _isTransitioning = false;
                return;
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
            // Check if this is an end game portal
            if (isEndGame && endGameConfig != null)
            {
                Debug.Log($"[Portal] End game portal triggered: {portalId}");
                EndGameManager.TriggerEndGame(endGameConfig);
                _isTransitioning = false;
                return;
            }
            
            if (_levelManager == null)
            {
                _levelManager = FindObjectOfType<LevelManager>();
            }

            if (_levelManager != null && link != null)
            {
                _levelManager.TransitionToLevel(link, portalId);
                
                // Save checkpoint AFTER arriving at destination (if this portal is a checkpoint)
                if (isCheckpoint)
                {
                    OnCheckpointReached?.Invoke(this);
                }
            }
            else
            {
                // No link configured - if this is a checkpoint, save state here
                // (player stays at this location)
                if (isCheckpoint)
                {
                    OnCheckpointReached?.Invoke(this);
                }
                Debug.LogWarning($"[Portal] Cannot transition - Link is null. Checkpoint saved at current location.");
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

using System;
using UnityEngine;

namespace BreakingHue.Level.Data
{
    /// <summary>
    /// ScriptableObject that links two portals together.
    /// Each link represents a bidirectional connection between an entrance and exit.
    /// Can link portals within the same level or across different levels.
    /// </summary>
    [CreateAssetMenu(fileName = "PortalLink", menuName = "Breaking Hue/Portal Link")]
    public class EntranceExitLink : ScriptableObject
    {
        [Header("Link Info")]
        [Tooltip("Unique identifier for this link")]
        public string linkId;
        
        [Tooltip("Display name for editor")]
        public string displayName = "Portal Link";
        
        [Header("Portal A")]
        [Tooltip("The level containing Portal A")]
        public LevelData levelA;
        
        [Tooltip("The portal ID within Level A")]
        public string portalIdA;
        
        [Header("Portal B")]
        [Tooltip("The level containing Portal B (can be same as Level A)")]
        public LevelData levelB;
        
        [Tooltip("The portal ID within Level B")]
        public string portalIdB;
        
        [Header("Settings")]
        [Tooltip("If true, entering Portal A creates a checkpoint")]
        public bool isCheckpointA;
        
        [Tooltip("If true, entering Portal B creates a checkpoint")]
        public bool isCheckpointB;

        private void OnEnable()
        {
            // Generate ID if not set
            if (string.IsNullOrEmpty(linkId))
            {
                linkId = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Gets the destination portal when entering from a specific portal.
        /// </summary>
        /// <param name="fromPortalId">The portal ID being entered</param>
        /// <param name="destLevel">Output: The destination level</param>
        /// <param name="destPortalId">Output: The destination portal ID</param>
        /// <returns>True if a valid destination was found</returns>
        public bool GetDestination(string fromPortalId, out LevelData destLevel, out string destPortalId)
        {
            if (fromPortalId == portalIdA)
            {
                destLevel = levelB;
                destPortalId = portalIdB;
                return levelB != null && !string.IsNullOrEmpty(portalIdB);
            }
            else if (fromPortalId == portalIdB)
            {
                destLevel = levelA;
                destPortalId = portalIdA;
                return levelA != null && !string.IsNullOrEmpty(portalIdA);
            }
            
            destLevel = null;
            destPortalId = null;
            return false;
        }

        /// <summary>
        /// Checks if this link connects a specific portal.
        /// </summary>
        public bool ConnectsPortal(string portalId)
        {
            return portalIdA == portalId || portalIdB == portalId;
        }

        /// <summary>
        /// Checks if entering from the given portal triggers a checkpoint.
        /// </summary>
        public bool IsCheckpointFromPortal(string fromPortalId)
        {
            if (fromPortalId == portalIdA)
                return isCheckpointA;
            if (fromPortalId == portalIdB)
                return isCheckpointB;
            return false;
        }

        /// <summary>
        /// Checks if this link connects two different levels.
        /// </summary>
        public bool IsInterLevelLink => levelA != levelB && levelA != null && levelB != null;

        /// <summary>
        /// Checks if this link connects portals within the same level.
        /// </summary>
        public bool IsIntraLevelLink => levelA == levelB && levelA != null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-generate display name
            if (string.IsNullOrEmpty(displayName) || displayName == "Portal Link")
            {
                string aName = levelA != null ? levelA.levelName : "?";
                string bName = levelB != null ? levelB.levelName : "?";
                displayName = $"{aName} <-> {bName}";
            }
        }
#endif
    }
}

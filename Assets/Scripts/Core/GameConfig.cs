using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Level.Data;

namespace BreakingHue.Core
{
    /// <summary>
    /// Global game configuration that holds all prefab references and level data.
    /// This is the central place for all game assets, eliminating the need to 
    /// manually connect prefabs to individual levels or managers.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Breaking Hue/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Prefabs")]
        [Tooltip("All prefabs used for level generation")]
        public LevelPrefabs prefabs = new LevelPrefabs();

        [Header("Levels")]
        [Tooltip("The level to load when the game starts")]
        public LevelData startingLevel;
        
        [Tooltip("All levels in the game")]
        public List<LevelData> allLevels = new List<LevelData>();

        [Header("Portal Links")]
        [Tooltip("All portal connections between levels")]
        public List<EntranceExitLink> portalLinks = new List<EntranceExitLink>();

        /// <summary>
        /// Finds the portal link that connects a specific portal.
        /// </summary>
        public EntranceExitLink FindLinkForPortal(string portalId)
        {
            foreach (var link in portalLinks)
            {
                if (link != null && link.ConnectsPortal(portalId))
                    return link;
            }
            return null;
        }

        /// <summary>
        /// Finds a level by its ID.
        /// </summary>
        public LevelData FindLevelById(string levelId)
        {
            foreach (var level in allLevels)
            {
                if (level != null && level.levelId == levelId)
                    return level;
            }
            return null;
        }

        /// <summary>
        /// Validates that all required prefabs are assigned.
        /// </summary>
        public bool ValidatePrefabs(out List<string> missingPrefabs)
        {
            missingPrefabs = new List<string>();
            
            if (prefabs == null)
            {
                missingPrefabs.Add("prefabs (entire struct is null)");
                return false;
            }

            if (prefabs.floorPrefab == null) missingPrefabs.Add("floorPrefab");
            if (prefabs.wallPrefab == null) missingPrefabs.Add("wallPrefab");
            if (prefabs.barrierPrefab == null) missingPrefabs.Add("barrierPrefab");
            if (prefabs.pickupPrefab == null) missingPrefabs.Add("pickupPrefab");
            if (prefabs.barrelPrefab == null) missingPrefabs.Add("barrelPrefab");
            if (prefabs.botPrefab == null) missingPrefabs.Add("botPrefab");
            if (prefabs.portalPrefab == null) missingPrefabs.Add("portalPrefab");
            if (prefabs.hiddenBlockPrefab == null) missingPrefabs.Add("hiddenBlockPrefab");
            if (prefabs.droppedMaskPrefab == null) missingPrefabs.Add("droppedMaskPrefab");
            if (prefabs.playerPrefab == null) missingPrefabs.Add("playerPrefab");

            return missingPrefabs.Count == 0;
        }

        /// <summary>
        /// Validates the overall configuration.
        /// </summary>
        public bool Validate(out List<string> issues)
        {
            issues = new List<string>();

            if (!ValidatePrefabs(out var missingPrefabs))
            {
                foreach (var missing in missingPrefabs)
                    issues.Add($"Missing prefab: {missing}");
            }

            if (startingLevel == null)
                issues.Add("No starting level assigned");

            if (allLevels == null || allLevels.Count == 0)
                issues.Add("No levels in allLevels list");

            return issues.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure starting level is in allLevels
            if (startingLevel != null && !allLevels.Contains(startingLevel))
            {
                allLevels.Insert(0, startingLevel);
            }
        }
#endif
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Core;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Level.Data
{
    /// <summary>
    /// ScriptableObject containing all data for a single level.
    /// Created and edited via the Level Editor tool.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Breaking Hue/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Level Info")]
        public string levelName = "New Level";
        public string levelId;
        public int levelIndex;
        
        [Header("Grid Settings")]
        public const int GridSize = 32;
        public float cellSize = 1f;
        
        [Header("Layers")]
        public GroundLayer groundLayer;
        public WallLayer wallLayer;
        public BarrierLayer barrierLayer;
        public PickupLayer pickupLayer;
        public BarrelLayer barrelLayer;
        public BotLayer botLayer;
        public PortalLayer portalLayer;
        public HiddenAreaLayer hiddenAreaLayer;
        
        [Header("Prefab References")]
        public LevelPrefabs prefabs;

        private void OnEnable()
        {
            // Generate ID if not set
            if (string.IsNullOrEmpty(levelId))
            {
                levelId = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Gets world position from grid coordinates.
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(
                x * cellSize + cellSize * 0.5f,
                0,
                y * cellSize + cellSize * 0.5f
            );
        }

        /// <summary>
        /// Gets grid coordinates from world position.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.z / cellSize)
            );
        }

        /// <summary>
        /// Gets the level offset to center the level.
        /// </summary>
        public Vector3 GetLevelOffset()
        {
            return new Vector3(-GridSize * cellSize * 0.5f, 0, -GridSize * cellSize * 0.5f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (groundLayer == null) groundLayer = new GroundLayer();
            if (wallLayer == null) wallLayer = new WallLayer();
            if (barrierLayer == null) barrierLayer = new BarrierLayer();
            if (pickupLayer == null) pickupLayer = new PickupLayer();
            if (barrelLayer == null) barrelLayer = new BarrelLayer();
            if (botLayer == null) botLayer = new BotLayer();
            if (portalLayer == null) portalLayer = new PortalLayer();
            if (hiddenAreaLayer == null) hiddenAreaLayer = new HiddenAreaLayer();
        }
#endif
    }

    /// <summary>
    /// References to prefabs used for level generation.
    /// </summary>
    [Serializable]
    public class LevelPrefabs
    {
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public GameObject barrierPrefab;
        public GameObject pickupPrefab;
        public GameObject barrelPrefab;
        public GameObject botPrefab;
        public GameObject portalPrefab;
        public GameObject hiddenBlockPrefab;
        public GameObject droppedMaskPrefab;
        public GameObject playerPrefab;
    }

    /// <summary>
    /// Base class for level layers.
    /// </summary>
    [Serializable]
    public abstract class LevelLayer
    {
        public bool isVisible = true;
    }

    /// <summary>
    /// Ground/floor layer - marks walkable areas.
    /// </summary>
    [Serializable]
    public class GroundLayer : LevelLayer
    {
        public List<Vector2Int> floorTiles = new List<Vector2Int>();
    }

    /// <summary>
    /// Wall layer - solid, impassable blocks.
    /// </summary>
    [Serializable]
    public class WallLayer : LevelLayer
    {
        public List<Vector2Int> wallTiles = new List<Vector2Int>();
    }

    /// <summary>
    /// Barrier layer - color-coded barriers that can be passed with correct masks.
    /// </summary>
    [Serializable]
    public class BarrierLayer : LevelLayer
    {
        public List<BarrierData> barriers = new List<BarrierData>();
    }

    [Serializable]
    public class BarrierData
    {
        public Vector2Int position;
        public ColorType color;
    }

    /// <summary>
    /// Pickup layer - mask pickups.
    /// </summary>
    [Serializable]
    public class PickupLayer : LevelLayer
    {
        public List<PickupData> pickups = new List<PickupData>();
    }

    [Serializable]
    public class PickupData
    {
        public Vector2Int position;
        public ColorType color;
        public string pickupId; // For save/load tracking
    }

    /// <summary>
    /// Barrel layer - exploding barrels.
    /// </summary>
    [Serializable]
    public class BarrelLayer : LevelLayer
    {
        public List<BarrelData> barrels = new List<BarrelData>();
    }

    [Serializable]
    public class BarrelData
    {
        public Vector2Int position;
        public ColorType color;
        public string barrelId; // For save/load tracking
    }

    /// <summary>
    /// Bot layer - bot entities and their configurations.
    /// </summary>
    [Serializable]
    public class BotLayer : LevelLayer
    {
        public List<BotData> bots = new List<BotData>();
    }

    [Serializable]
    public class BotData
    {
        public string botId;
        public Vector2Int startPosition;
        public ColorType initialColor;
        public BotPathData pathData; // Reference to path ScriptableObject
        public List<Vector2Int> inlineWaypoints; // Or inline waypoints if no separate asset
        public PathMode pathMode = PathMode.Loop;
    }

    /// <summary>
    /// Portal layer - entrance/exit portals.
    /// </summary>
    [Serializable]
    public class PortalLayer : LevelLayer
    {
        public List<PortalData> portals = new List<PortalData>();
        public Vector2Int playerSpawnPosition;
    }

    [Serializable]
    public class PortalData
    {
        public string portalId;
        public Vector2Int position;
        public EntranceExitLink link; // Reference to the link asset
        public bool isCheckpoint;
        public bool isEntrance; // True = entrance, False = exit (though they're bidirectional)
    }

    /// <summary>
    /// Hidden area layer - concealed blocks that reveal secrets.
    /// </summary>
    [Serializable]
    public class HiddenAreaLayer : LevelLayer
    {
        public List<HiddenBlockData> hiddenBlocks = new List<HiddenBlockData>();
    }

    [Serializable]
    public class HiddenBlockData
    {
        public Vector2Int position;
        public string blockId;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Gameplay;
using BreakingHue.Gameplay.Bot;
using BreakingHue.Level.Data;
using BreakingHue.Save;

namespace BreakingHue.Level
{
    /// <summary>
    /// Manages level loading, unloading, and transitions.
    /// Handles multi-level navigation with persistent player inventory.
    /// Works with CheckpointManager for save/restore functionality.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Global Configuration")]
        [Tooltip("The global game configuration. If assigned, prefabs and levels are loaded from here.")]
        [SerializeField] private GameConfig gameConfig;
        
        [Header("Level Configuration (Override)")]
        [Tooltip("Override levels list if not using GameConfig")]
        [SerializeField] private List<LevelData> allLevels = new List<LevelData>();
        [Tooltip("Override starting level if not using GameConfig")]
        [SerializeField] private LevelData startingLevel;
        [SerializeField] private Transform levelContainer;
        
        [Header("Prefabs (Override)")]
        [Tooltip("Override prefabs if not using GameConfig")]
        [SerializeField] private LevelPrefabs defaultPrefabs;
        
        [Header("Settings")]
        [SerializeField] private bool autoLoadOnStart = true;

        private MaskInventory _playerInventory;
        private PlayerController _playerController;
        private LevelData _currentLevel;
        private GameObject _currentLevelInstance;
        private Dictionary<string, LevelSaveData> _levelStates = new Dictionary<string, LevelSaveData>();
        
        // Track spawned objects for cleanup
        private List<GameObject> _spawnedObjects = new List<GameObject>();
        private Dictionary<string, Portal> _activePortals = new Dictionary<string, Portal>();
        private Dictionary<string, BotController> _activeBots = new Dictionary<string, BotController>();

        /// <summary>
        /// Event fired when a level is loaded.
        /// </summary>
        public event Action<LevelData> OnLevelLoaded;

        /// <summary>
        /// Event fired when a level is unloaded.
        /// </summary>
        public event Action<LevelData> OnLevelUnloaded;

        /// <summary>
        /// Event fired when player transitions between levels.
        /// </summary>
        public event Action<LevelData, LevelData> OnLevelTransition;

        public LevelData CurrentLevel => _currentLevel;
        
        /// <summary>
        /// Gets the effective prefabs (from GameConfig or override).
        /// </summary>
        public LevelPrefabs EffectivePrefabs => gameConfig != null ? gameConfig.prefabs : defaultPrefabs;
        
        /// <summary>
        /// Gets the effective starting level (from GameConfig or override).
        /// </summary>
        public LevelData EffectiveStartingLevel => gameConfig != null && gameConfig.startingLevel != null 
            ? gameConfig.startingLevel : startingLevel;
        
        /// <summary>
        /// Gets the effective levels list (from GameConfig or override).
        /// </summary>
        public List<LevelData> EffectiveLevels => gameConfig != null && gameConfig.allLevels.Count > 0 
            ? gameConfig.allLevels : allLevels;
        
        /// <summary>
        /// Gets or sets the game config reference.
        /// </summary>
        public GameConfig GameConfig
        {
            get => gameConfig;
            set => gameConfig = value;
        }

        [Inject]
        public void Construct(MaskInventory playerInventory)
        {
            _playerInventory = playerInventory;
        }

        private void Start()
        {
            // Subscribe to pickup collection events to track collected pickups
            MaskPickup.OnMaskPickupCollected += OnPickupCollected;
            
            // Subscribe to barrel explosion events to track destroyed barrels
            ExplodingBarrel.OnBarrelExploded += OnBarrelExploded;
            
            // Subscribe to dropped mask events to track dropped masks
            DroppedMask.OnMaskSpawned += OnMaskSpawned;
            DroppedMask.OnAnyMaskCollected += OnMaskCollected;
            
            // Before loading level, check if CheckpointManager has states to apply
            var checkpointManager = FindObjectOfType<CheckpointManager>();
            if (checkpointManager != null && checkpointManager.HasCheckpoint)
            {
                var checkpointStates = checkpointManager.GetCheckpointLevelStates();
                if (checkpointStates != null && checkpointStates.Count > 0)
                {
                    foreach (var kvp in checkpointStates)
                    {
                        _levelStates[kvp.Key] = kvp.Value.Clone();
                    }
                    Debug.Log($"[LevelManager] Applied {checkpointStates.Count} checkpoint level states before loading");
                }
            }
            
            var levelToLoad = EffectiveStartingLevel;
            if (autoLoadOnStart && levelToLoad != null)
            {
                LoadLevel(levelToLoad);
            }
            else if (autoLoadOnStart && levelToLoad == null)
            {
                Debug.LogWarning("[LevelManager] Auto-load is enabled but no starting level is set. " +
                    "Assign a GameConfig or set startingLevel directly.");
            }
        }
        
        private void OnDestroy()
        {
            MaskPickup.OnMaskPickupCollected -= OnPickupCollected;
            ExplodingBarrel.OnBarrelExploded -= OnBarrelExploded;
            DroppedMask.OnMaskSpawned -= OnMaskSpawned;
            DroppedMask.OnAnyMaskCollected -= OnMaskCollected;
        }
        
        /// <summary>
        /// Called when a mask pickup is collected - tracks it in save data.
        /// </summary>
        private void OnPickupCollected(string pickupId)
        {
            if (_currentLevel == null) return;
            
            var saveData = GetOrCreateLevelSaveData(_currentLevel.levelId);
            saveData.MarkPickupCollected(pickupId);
            
            Debug.Log($"[LevelManager] Tracked pickup collection: {pickupId}");
        }
        
        /// <summary>
        /// Called when a barrel explodes - tracks it in save data.
        /// </summary>
        private void OnBarrelExploded(ExplodingBarrel barrel, bool wasPlayer)
        {
            if (_currentLevel == null || barrel == null) return;
            
            var saveData = GetOrCreateLevelSaveData(_currentLevel.levelId);
            saveData.MarkBarrelDestroyed(barrel.barrelId);
            
            Debug.Log($"[LevelManager] Tracked barrel destruction: {barrel.barrelId}");
        }
        
        /// <summary>
        /// Called when a dropped mask is spawned - tracks it in save data.
        /// </summary>
        private void OnMaskSpawned(DroppedMask mask)
        {
            if (_currentLevel == null || mask == null) return;
            
            // Don't track masks spawned from save data (they already exist in save)
            // Only track new drops (masks created during gameplay)
            var saveData = GetOrCreateLevelSaveData(_currentLevel.levelId);
            
            // Check if this mask already exists in save data (was loaded from save)
            if (saveData.droppedMasks.Exists(m => m.maskId == mask.MaskId))
            {
                return; // Already tracked
            }
            
            saveData.AddDroppedMask(mask.MaskId, mask.transform.position, mask.MaskColor);
            
            Debug.Log($"[LevelManager] Tracked dropped mask: {mask.MaskId} ({mask.MaskColor})");
        }
        
        /// <summary>
        /// Called when a dropped mask is collected - removes it from save data.
        /// </summary>
        private void OnMaskCollected(DroppedMask mask)
        {
            if (_currentLevel == null || mask == null) return;
            
            var saveData = GetOrCreateLevelSaveData(_currentLevel.levelId);
            saveData.RemoveDroppedMask(mask.MaskId);
            
            Debug.Log($"[LevelManager] Removed collected mask from tracking: {mask.MaskId}");
        }

        /// <summary>
        /// Loads a level by its data asset.
        /// </summary>
        public void LoadLevel(LevelData levelData, string spawnPortalId = null)
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelManager] Cannot load null level");
                return;
            }

            // Unload current level if any
            if (_currentLevel != null)
            {
                UnloadCurrentLevel();
            }

            _currentLevel = levelData;
            
            // Get or create save state for this level
            if (!_levelStates.TryGetValue(levelData.levelId, out var saveData))
            {
                saveData = new LevelSaveData(levelData.levelId);
                _levelStates[levelData.levelId] = saveData;
            }

            // Generate level from data
            GenerateLevel(levelData, saveData);
            
            // Find spawn position
            Vector3 spawnPos = FindSpawnPosition(levelData, spawnPortalId);
            SpawnPlayer(spawnPos);
            
            saveData.hasBeenVisited = true;
            
            Debug.Log($"[LevelManager] Loaded level: {levelData.levelName}");
            OnLevelLoaded?.Invoke(levelData);
        }

        /// <summary>
        /// Transitions to another level via a portal.
        /// </summary>
        public void TransitionToLevel(EntranceExitLink link, string fromPortalId)
        {
            if (!link.GetDestination(fromPortalId, out var destLevel, out var destPortalId))
            {
                Debug.LogError($"[LevelManager] Invalid portal transition from {fromPortalId}");
                return;
            }

            LevelData previousLevel = _currentLevel;
            
            // Save current level state before leaving
            SaveCurrentLevelState();
            
            // Load destination level
            LoadLevel(destLevel, destPortalId);
            
            OnLevelTransition?.Invoke(previousLevel, destLevel);
        }

        /// <summary>
        /// Transitions to a level by index.
        /// </summary>
        public void TransitionToLevel(int levelIndex, string spawnPortalId = null)
        {
            if (levelIndex < 0 || levelIndex >= allLevels.Count)
            {
                Debug.LogError($"[LevelManager] Invalid level index: {levelIndex}");
                return;
            }

            LoadLevel(allLevels[levelIndex], spawnPortalId);
        }

        private bool _skipSaveOnUnload = false;
        
        private void UnloadCurrentLevel()
        {
            if (_currentLevel == null) return;

            // Save state before unloading (unless restoring from checkpoint)
            if (!_skipSaveOnUnload)
            {
                SaveCurrentLevelState();
            }
            _skipSaveOnUnload = false;

            // Destroy all spawned objects
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            _spawnedObjects.Clear();
            _activePortals.Clear();
            _activeBots.Clear();

            if (_currentLevelInstance != null)
            {
                Destroy(_currentLevelInstance);
                _currentLevelInstance = null;
            }

            OnLevelUnloaded?.Invoke(_currentLevel);
            _currentLevel = null;
        }

        private void GenerateLevel(LevelData data, LevelSaveData saveData)
        {
            // Create level container
            _currentLevelInstance = new GameObject($"Level_{data.levelName}");
            _currentLevelInstance.transform.SetParent(levelContainer);

            Vector3 offset = data.GetLevelOffset();
            // Use level-specific prefabs if they have actual prefabs assigned, otherwise use GameConfig/defaults
            // Check if level has its own prefabs with at least one actual prefab assigned
            var prefabs = (data.prefabs != null && data.prefabs.floorPrefab != null) 
                ? data.prefabs 
                : EffectivePrefabs;

            // Generate floor
            GenerateFloor(data, prefabs, offset);
            
            // Generate walls
            GenerateWalls(data, prefabs, offset);
            
            // Generate barriers
            GenerateBarriers(data, prefabs, offset);
            
            // Generate pickups (respecting collected state)
            GeneratePickups(data, prefabs, offset, saveData);
            
            // Generate barrels (respecting destroyed state)
            GenerateBarrels(data, prefabs, offset, saveData);
            
            // Generate bots (respecting saved states)
            GenerateBots(data, prefabs, offset, saveData);
            
            // Generate portals
            GeneratePortals(data, prefabs, offset);
            
            // Generate hidden blocks (respecting revealed state)
            GenerateHiddenBlocks(data, prefabs, offset, saveData);
            
            // Spawn dropped masks from save data
            SpawnDroppedMasks(saveData, prefabs, offset);
        }

        private void GenerateFloor(LevelData data, LevelPrefabs prefabs, Vector3 offset)
        {
            if (prefabs.floorPrefab == null || data.groundLayer?.floorTiles == null) return;

            foreach (var tile in data.groundLayer.floorTiles)
            {
                Vector3 pos = data.GridToWorld(tile.x, tile.y) + offset;
                var floor = Instantiate(prefabs.floorPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                _spawnedObjects.Add(floor);
            }
        }

        private void GenerateWalls(LevelData data, LevelPrefabs prefabs, Vector3 offset)
        {
            if (prefabs.wallPrefab == null || data.wallLayer?.wallTiles == null) return;

            foreach (var tile in data.wallLayer.wallTiles)
            {
                Vector3 pos = data.GridToWorld(tile.x, tile.y) + offset;
                pos.y = 0.5f; // Wall height
                var wall = Instantiate(prefabs.wallPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                _spawnedObjects.Add(wall);
            }
        }

        private void GenerateBarriers(LevelData data, LevelPrefabs prefabs, Vector3 offset)
        {
            if (prefabs.barrierPrefab == null || data.barrierLayer?.barriers == null) return;

            foreach (var barrierData in data.barrierLayer.barriers)
            {
                Vector3 pos = data.GridToWorld(barrierData.position.x, barrierData.position.y) + offset;
                pos.y = 0.5f;
                var barrier = Instantiate(prefabs.barrierPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                
                var barrierComponent = barrier.GetComponent<ColorBarrier>();
                if (barrierComponent != null)
                {
                    barrierComponent.Initialize(barrierData.color);
                }
                
                _spawnedObjects.Add(barrier);
            }
        }

        private void GeneratePickups(LevelData data, LevelPrefabs prefabs, Vector3 offset, LevelSaveData saveData)
        {
            if (prefabs.pickupPrefab == null || data.pickupLayer?.pickups == null) return;

            foreach (var pickupData in data.pickupLayer.pickups)
            {
                // Skip if already collected
                if (saveData.collectedPickupIds.Contains(pickupData.pickupId))
                {
                    continue;
                }

                Vector3 pos = data.GridToWorld(pickupData.position.x, pickupData.position.y) + offset;
                pos.y = 0.5f;
                var pickup = Instantiate(prefabs.pickupPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                
                var pickupComponent = pickup.GetComponent<MaskPickup>();
                if (pickupComponent != null)
                {
                    pickupComponent.Initialize(pickupData.color, pickupData.pickupId);
                }
                
                _spawnedObjects.Add(pickup);
            }
        }

        private void GenerateBarrels(LevelData data, LevelPrefabs prefabs, Vector3 offset, LevelSaveData saveData)
        {
            if (prefabs.barrelPrefab == null || data.barrelLayer?.barrels == null) return;

            foreach (var barrelData in data.barrelLayer.barrels)
            {
                // Skip if already destroyed
                if (saveData.destroyedBarrelIds.Contains(barrelData.barrelId))
                    continue;

                Vector3 pos = data.GridToWorld(barrelData.position.x, barrelData.position.y) + offset;
                pos.y = 0.5f;
                var barrel = Instantiate(prefabs.barrelPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                
                var barrelComponent = barrel.GetComponent<ExplodingBarrel>();
                if (barrelComponent != null)
                {
                    barrelComponent.SetBarrelId(barrelData.barrelId);
                    barrelComponent.Initialize(barrelData.color);
                }
                
                _spawnedObjects.Add(barrel);
            }
        }

        private void GenerateBots(LevelData data, LevelPrefabs prefabs, Vector3 offset, LevelSaveData saveData)
        {
            if (prefabs.botPrefab == null || data.botLayer?.bots == null) return;

            foreach (var botData in data.botLayer.bots)
            {
                // Check if bot has saved state
                var botSaveData = saveData.GetBotState(botData.botId);
                
                // Skip if bot is dead
                if (botSaveData != null && botSaveData.isDead)
                    continue;

                Vector3 pos;
                if (botSaveData != null)
                {
                    pos = botSaveData.position;
                }
                else
                {
                    pos = data.GridToWorld(botData.startPosition.x, botData.startPosition.y) + offset;
                }
                pos.y = 0.5f;

                var bot = Instantiate(prefabs.botPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                var botController = bot.GetComponent<BotController>();
                
                if (botController != null)
                {
                    // Create path data from inline waypoints if no asset reference
                    BotPathData pathData = botData.pathData;
                    if (pathData == null && botData.inlineWaypoints != null && botData.inlineWaypoints.Count > 0)
                    {
                        pathData = ScriptableObject.CreateInstance<BotPathData>();
                        pathData.waypoints = botData.inlineWaypoints;
                        pathData.pathMode = botData.pathMode;
                    }
                    
                    botController.Initialize(pathData, data.cellSize, offset, botData.initialColor);
                    
                    // Restore saved state if available
                    if (botSaveData != null)
                    {
                        var snapshot = new BotSnapshot
                        {
                            BotId = botData.botId,
                            Position = botSaveData.position,
                            IsDead = botSaveData.isDead,
                            PathState = new BotPathState
                            {
                                currentWaypointIndex = botSaveData.currentWaypointIndex,
                                movingForward = botSaveData.movingForward,
                                waitTimer = botSaveData.waitTimer,
                                isWaiting = botSaveData.isWaiting
                            },
                            InventorySnapshot = new BotInventorySnapshot
                            {
                                Slots = botSaveData.inventorySlots,
                                ActiveSlots = botSaveData.activeSlots
                            }
                        };
                        botController.RestoreFromSnapshot(snapshot);
                    }
                    
                    _activeBots[botData.botId] = botController;
                }
                
                _spawnedObjects.Add(bot);
            }
        }

        private void GeneratePortals(LevelData data, LevelPrefabs prefabs, Vector3 offset)
        {
            if (prefabs.portalPrefab == null || data.portalLayer?.portals == null) return;

            foreach (var portalData in data.portalLayer.portals)
            {
                Vector3 pos = data.GridToWorld(portalData.position.x, portalData.position.y) + offset;
                var portal = Instantiate(prefabs.portalPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                
                var portalComponent = portal.GetComponent<Portal>();
                if (portalComponent != null)
                {
                    portalComponent.Initialize(portalData.portalId, portalData.link, portalData.isCheckpoint);
                    _activePortals[portalData.portalId] = portalComponent;
                }
                
                _spawnedObjects.Add(portal);
            }
        }

        private void GenerateHiddenBlocks(LevelData data, LevelPrefabs prefabs, Vector3 offset, LevelSaveData saveData)
        {
            if (prefabs.hiddenBlockPrefab == null || data.hiddenAreaLayer?.hiddenBlocks == null) return;

            foreach (var blockData in data.hiddenAreaLayer.hiddenBlocks)
            {
                Vector3 pos = data.GridToWorld(blockData.position.x, blockData.position.y) + offset;
                pos.y = 0.5f;
                var block = Instantiate(prefabs.hiddenBlockPrefab, pos, Quaternion.identity, _currentLevelInstance.transform);
                
                var hiddenBlock = block.GetComponent<HiddenBlock>();
                if (hiddenBlock != null)
                {
                    // Restore revealed state
                    if (saveData.revealedHiddenBlockIds.Contains(blockData.blockId))
                    {
                        hiddenBlock.SetRevealed(true);
                    }
                }
                
                _spawnedObjects.Add(block);
            }
        }

        private void SpawnDroppedMasks(LevelSaveData saveData, LevelPrefabs prefabs, Vector3 offset)
        {
            if (prefabs.droppedMaskPrefab == null) return;

            foreach (var maskData in saveData.droppedMasks)
            {
                // Pass the saved maskId to preserve it across save/load
                var mask = DroppedMask.Spawn(prefabs.droppedMaskPrefab, maskData.position, maskData.color, maskData.maskId, _currentLevelInstance.transform);
                if (mask != null)
                {
                    _spawnedObjects.Add(mask.gameObject);
                }
            }
        }

        private Vector3 FindSpawnPosition(LevelData data, string portalId)
        {
            Vector3 offset = data.GetLevelOffset();

            // Try to spawn at specific portal
            if (!string.IsNullOrEmpty(portalId) && data.portalLayer?.portals != null)
            {
                var portal = data.portalLayer.portals.Find(p => p.portalId == portalId);
                if (portal != null)
                {
                    return data.GridToWorld(portal.position.x, portal.position.y) + offset + Vector3.up * 0.5f;
                }
            }

            // Fall back to player spawn position
            if (data.portalLayer != null)
            {
                return data.GridToWorld(data.portalLayer.playerSpawnPosition.x, data.portalLayer.playerSpawnPosition.y) + offset + Vector3.up * 0.5f;
            }

            // Default center
            return Vector3.up * 0.5f;
        }

        private void SpawnPlayer(Vector3 position)
        {
            if (_playerController == null)
            {
                _playerController = FindObjectOfType<PlayerController>();
            }

            if (_playerController != null)
            {
                _playerController.TeleportTo(position);
            }
            else if (EffectivePrefabs?.playerPrefab != null)
            {
                var player = Instantiate(EffectivePrefabs.playerPrefab, position, Quaternion.identity);
                _playerController = player.GetComponent<PlayerController>();
            }
        }

        private void SaveCurrentLevelState()
        {
            if (_currentLevel == null) return;

            var saveData = GetOrCreateLevelSaveData(_currentLevel.levelId);
            
            // Save bot states
            foreach (var kvp in _activeBots)
            {
                // Skip destroyed bots (null check for Unity objects)
                if (kvp.Value == null)
                {
                    // Bot was destroyed - mark as dead in save data
                    saveData.UpdateBotState(new BotSaveData
                    {
                        botId = kvp.Key,
                        isDead = true
                    });
                    continue;
                }
                
                var snapshot = kvp.Value.CreateSnapshot();
                saveData.UpdateBotState(new BotSaveData
                {
                    botId = kvp.Key,
                    position = snapshot.Position,
                    isDead = snapshot.IsDead,
                    currentWaypointIndex = snapshot.PathState.currentWaypointIndex,
                    movingForward = snapshot.PathState.movingForward,
                    waitTimer = snapshot.PathState.waitTimer,
                    isWaiting = snapshot.PathState.isWaiting,
                    inventorySlots = snapshot.InventorySnapshot?.Slots,
                    activeSlots = snapshot.InventorySnapshot?.ActiveSlots
                });
            }
        }

        /// <summary>
        /// Gets or creates save data for a level.
        /// </summary>
        public LevelSaveData GetOrCreateLevelSaveData(string levelId)
        {
            if (!_levelStates.TryGetValue(levelId, out var saveData))
            {
                saveData = new LevelSaveData(levelId);
                _levelStates[levelId] = saveData;
            }
            return saveData;
        }

        /// <summary>
        /// Gets all level save states.
        /// </summary>
        public Dictionary<string, LevelSaveData> GetAllLevelStates()
        {
            return new Dictionary<string, LevelSaveData>(_levelStates);
        }

        /// <summary>
        /// Restores all level states from save data.
        /// </summary>
        public void RestoreAllLevelStates(Dictionary<string, LevelSaveData> states)
        {
            _levelStates.Clear();
            foreach (var kvp in states)
            {
                _levelStates[kvp.Key] = kvp.Value.Clone();
            }
            
            // Skip saving on next unload to prevent overwriting restored checkpoint data
            _skipSaveOnUnload = true;
        }

        /// <summary>
        /// Reloads the current level from checkpoint data.
        /// </summary>
        public void ReloadCurrentLevel(string spawnPortalId = null)
        {
            if (_currentLevel != null)
            {
                LoadLevel(_currentLevel, spawnPortalId);
            }
        }
        
        /// <summary>
        /// Forces saving the current level state without unloading.
        /// Used by CheckpointManager before capturing checkpoint data.
        /// </summary>
        public void ForceSaveCurrentLevelState()
        {
            SaveCurrentLevelState();
        }
        
        /// <summary>
        /// Resets the skipSaveOnUnload flag.
        /// Used after applying checkpoint states on startup.
        /// </summary>
        public void ResetSkipSaveFlag()
        {
            _skipSaveOnUnload = false;
        }

        /// <summary>
        /// Gets a portal by ID.
        /// </summary>
        public Portal GetPortal(string portalId)
        {
            _activePortals.TryGetValue(portalId, out var portal);
            return portal;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Gameplay;
using BreakingHue.Level;
using BreakingHue.Level.Data;

namespace BreakingHue.Save
{
    /// <summary>
    /// Manages checkpoint-based saving and restoring.
    /// 
    /// Checkpoint Flow:
    /// 1. Player enters a checkpoint portal
    /// 2. Full game state is captured (player position, inventory, all level states)
    /// 3. On player death (barrel explosion), restore to this checkpoint exactly
    /// 
    /// Persistence:
    /// - Checkpoints are saved to JSON for quit/resume functionality
    /// - Only the last checkpoint is kept in memory for fast restore
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Save Settings")]
        [SerializeField] private string saveFileName = "checkpoint.json";
        [SerializeField] private bool autoSaveToFile = true;

        private MaskInventory _playerInventory;
        private LevelManager _levelManager;
        private PlayerController _playerController;
        
        private CheckpointData _lastCheckpoint;
        private string _savePath;
        
        // Flag to prevent checkpoint capture during restore
        private bool _isRestoring;
        // Cooldown to prevent immediate re-capture after restore
        private float _captureBlockedUntil;

        /// <summary>
        /// Event fired when a checkpoint is captured.
        /// </summary>
        public event Action<CheckpointData> OnCheckpointCaptured;

        /// <summary>
        /// Event fired when restoring to a checkpoint.
        /// </summary>
        public event Action<CheckpointData> OnCheckpointRestoring;

        /// <summary>
        /// Event fired after checkpoint restore is complete.
        /// </summary>
        public event Action OnCheckpointRestored;

        [Inject]
        public void Construct(MaskInventory playerInventory, LevelManager levelManager)
        {
            _playerInventory = playerInventory;
            _levelManager = levelManager;
        }

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, saveFileName);
            
            // Subscribe to checkpoint portal events
            Portal.OnCheckpointReached += OnPortalCheckpoint;
            
            // Subscribe to player death events
            ExplodingBarrel.OnPlayerExploded += OnPlayerDeath;
            
            // Load checkpoint from file early (before LevelManager.Start loads the level)
            if (autoSaveToFile)
            {
                LoadCheckpointFromFile();
            }
        }

        private void OnDestroy()
        {
            Portal.OnCheckpointReached -= OnPortalCheckpoint;
            ExplodingBarrel.OnPlayerExploded -= OnPlayerDeath;
        }

        private void Start()
        {
            _playerController = FindObjectOfType<PlayerController>();
            // Note: Checkpoint states are applied by LevelManager.Start() before level load
        }
        
        /// <summary>
        /// Applies the checkpoint's level states to the LevelManager without triggering a full restore.
        /// Used on game startup to remember collected items, bot states, etc.
        /// </summary>
        private void ApplyCheckpointLevelStates()
        {
            if (_lastCheckpoint == null) return;
            
            // Try to find LevelManager if not injected
            if (_levelManager == null)
            {
                _levelManager = FindObjectOfType<LevelManager>();
            }
            
            if (_levelManager == null)
            {
                Debug.LogWarning("[CheckpointManager] Cannot apply checkpoint states - LevelManager not found");
                return;
            }
            
            // Use RestoreAllLevelStates to properly apply checkpoint data
            var statesToApply = new Dictionary<string, LevelSaveData>();
            foreach (var levelState in _lastCheckpoint.levelStates)
            {
                statesToApply[levelState.levelId] = levelState.Clone();
            }
            
            _levelManager.RestoreAllLevelStates(statesToApply);
            
            // Reset the skipSaveOnUnload flag since this is startup, not a restore
            // (we want normal save behavior after applying checkpoint states)
            _levelManager.ResetSkipSaveFlag();
            
            Debug.Log($"[CheckpointManager] Applied checkpoint level states on startup ({statesToApply.Count} levels)");
        }

        /// <summary>
        /// Called when player enters a checkpoint portal.
        /// </summary>
        private void OnPortalCheckpoint(Portal portal)
        {
            CaptureCheckpoint(portal.PortalId);
        }

        /// <summary>
        /// Called when player dies (barrel explosion).
        /// </summary>
        private void OnPlayerDeath()
        {
            RestoreCheckpoint();
        }

        /// <summary>
        /// Captures a checkpoint at the current game state.
        /// </summary>
        public void CaptureCheckpoint(string portalId = null)
        {
            // Prevent checkpoint capture during restore or cooldown period
            if (_isRestoring)
            {
                Debug.Log("[CheckpointManager] Checkpoint capture blocked - currently restoring");
                return;
            }
            
            if (Time.time < _captureBlockedUntil)
            {
                Debug.Log("[CheckpointManager] Checkpoint capture blocked - cooldown active");
                return;
            }
            
            if (_playerController == null)
            {
                _playerController = FindObjectOfType<PlayerController>();
            }

            // IMPORTANT: Force save the current level state BEFORE capturing checkpoint
            // This ensures bot positions and states are up-to-date
            _levelManager.ForceSaveCurrentLevelState();

            _lastCheckpoint = new CheckpointData
            {
                checkpointLevelId = _levelManager.CurrentLevel?.levelId,
                checkpointPortalId = portalId,
                playerPosition = _playerController != null ? _playerController.transform.position : Vector3.zero,
                playerInventory = _playerInventory?.CreateSnapshot()
            };

            // Capture all level states
            var levelStates = _levelManager.GetAllLevelStates();
            
            foreach (var kvp in levelStates)
            {
                _lastCheckpoint.levelStates.Add(kvp.Value.Clone());
            }

            Debug.Log($"[CheckpointManager] Checkpoint captured at {_lastCheckpoint.checkpointLevelId}, portal: {portalId}");
            
            OnCheckpointCaptured?.Invoke(_lastCheckpoint);

            // Persist to file
            if (autoSaveToFile)
            {
                SaveCheckpointToFile();
            }
        }

        /// <summary>
        /// Restores to the last checkpoint.
        /// </summary>
        public void RestoreCheckpoint()
        {
            if (_lastCheckpoint == null)
            {
                Debug.LogWarning("[CheckpointManager] No checkpoint to restore!");
                return;
            }

            // Block checkpoint capture during restore
            _isRestoring = true;

            Debug.Log($"[CheckpointManager] Restoring to checkpoint at {_lastCheckpoint.checkpointLevelId}");
            
            OnCheckpointRestoring?.Invoke(_lastCheckpoint);

            // Restore player inventory
            if (_lastCheckpoint.playerInventory != null && _playerInventory != null)
            {
                _playerInventory.RestoreFromSnapshot(_lastCheckpoint.playerInventory);
            }

            // Restore all level states
            var restoredStates = new Dictionary<string, LevelSaveData>();
            foreach (var levelState in _lastCheckpoint.levelStates)
            {
                restoredStates[levelState.levelId] = levelState.Clone();
            }
            _levelManager.RestoreAllLevelStates(restoredStates);

            // Reload the checkpoint level
            var checkpointLevel = FindLevelById(_lastCheckpoint.checkpointLevelId);
            
            if (checkpointLevel != null)
            {
                _levelManager.LoadLevel(checkpointLevel, _lastCheckpoint.checkpointPortalId);
            }
            else
            {
                // Fall back to reload current level
                _levelManager.ReloadCurrentLevel(_lastCheckpoint.checkpointPortalId);
            }

            // Teleport player to checkpoint position
            if (_playerController != null && _lastCheckpoint.playerPosition != Vector3.zero)
            {
                _playerController.TeleportTo(_lastCheckpoint.playerPosition);
            }

            // End restore mode and start cooldown to prevent immediate re-capture
            _isRestoring = false;
            _captureBlockedUntil = Time.time + 1.0f; // 1 second cooldown

            OnCheckpointRestored?.Invoke();
        }

        /// <summary>
        /// Clears the current checkpoint.
        /// </summary>
        public void ClearCheckpoint()
        {
            _lastCheckpoint = null;
            
            if (autoSaveToFile && File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }
        }

        /// <summary>
        /// Checks if there is a checkpoint saved.
        /// </summary>
        public bool HasCheckpoint => _lastCheckpoint != null;

        /// <summary>
        /// Gets the current checkpoint data.
        /// </summary>
        public CheckpointData CurrentCheckpoint => _lastCheckpoint;
        
        /// <summary>
        /// Gets the level states from the current checkpoint for LevelManager to apply on startup.
        /// </summary>
        public Dictionary<string, LevelSaveData> GetCheckpointLevelStates()
        {
            if (_lastCheckpoint == null) return null;
            
            var states = new Dictionary<string, LevelSaveData>();
            foreach (var levelState in _lastCheckpoint.levelStates)
            {
                states[levelState.levelId] = levelState.Clone();
            }
            return states;
        }

        /// <summary>
        /// Saves the current checkpoint to a file.
        /// </summary>
        public void SaveCheckpointToFile()
        {
            if (_lastCheckpoint == null) return;

            try
            {
                var wrapper = new CheckpointFileWrapper
                {
                    checkpoint = _lastCheckpoint,
                    savedAt = DateTime.Now.ToString("o")
                };
                
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(_savePath, json);
                
                Debug.Log($"[CheckpointManager] Checkpoint saved to {_savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CheckpointManager] Failed to save checkpoint: {e.Message}");
            }
        }

        /// <summary>
        /// Loads checkpoint from file.
        /// </summary>
        public bool LoadCheckpointFromFile()
        {
            if (!File.Exists(_savePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(_savePath);
                var wrapper = JsonUtility.FromJson<CheckpointFileWrapper>(json);
                
                if (wrapper?.checkpoint != null)
                {
                    _lastCheckpoint = wrapper.checkpoint;
                    Debug.Log($"[CheckpointManager] Checkpoint loaded from file (saved at {wrapper.savedAt})");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CheckpointManager] Failed to load checkpoint: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// Manually trigger a checkpoint capture.
        /// </summary>
        public void ForceCheckpoint()
        {
            CaptureCheckpoint();
        }

        /// <summary>
        /// Finds a level by its ID from the LevelManager.
        /// </summary>
        private LevelData FindLevelById(string levelId)
        {
            // Try to get from LevelManager's level list via reflection or find in project
            // For now, just return null and let the system reload current
            return null;
        }

        /// <summary>
        /// Gets the save file path.
        /// </summary>
        public string SaveFilePath => _savePath;
    }

    /// <summary>
    /// Wrapper for JSON serialization.
    /// </summary>
    [Serializable]
    internal class CheckpointFileWrapper
    {
        public CheckpointData checkpoint;
        public string savedAt;
    }
}

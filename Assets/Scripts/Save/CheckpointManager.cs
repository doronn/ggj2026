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
        }

        private void OnDestroy()
        {
            Portal.OnCheckpointReached -= OnPortalCheckpoint;
            ExplodingBarrel.OnPlayerExploded -= OnPlayerDeath;
        }

        private void Start()
        {
            _playerController = FindObjectOfType<PlayerController>();
            
            // Try to load existing checkpoint
            if (autoSaveToFile)
            {
                LoadCheckpointFromFile();
            }
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
            if (_playerController == null)
            {
                _playerController = FindObjectOfType<PlayerController>();
            }

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

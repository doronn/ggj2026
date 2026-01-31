using System;
using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Core;
using BreakingHue.Gameplay;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Level.Data
{
    /// <summary>
    /// Runtime save data for a single level.
    /// Tracks the state of all dynamic objects (bots, pickups, barrels, hidden blocks).
    /// Serialized to JSON for persistence.
    /// </summary>
    [Serializable]
    public class LevelSaveData
    {
        public string levelId;
        public bool hasBeenVisited;
        
        // Collected/destroyed items (by ID)
        public List<string> collectedPickupIds = new List<string>();
        public List<string> destroyedBarrelIds = new List<string>();
        public List<string> revealedHiddenBlockIds = new List<string>();
        
        // Bot states
        public List<BotSaveData> botStates = new List<BotSaveData>();
        
        // Dropped masks (player/bot dropped, not original pickups)
        public List<DroppedMaskSaveData> droppedMasks = new List<DroppedMaskSaveData>();

        public LevelSaveData() { }

        public LevelSaveData(string levelId)
        {
            this.levelId = levelId;
        }

        /// <summary>
        /// Marks a pickup as collected.
        /// </summary>
        public void MarkPickupCollected(string pickupId)
        {
            if (!collectedPickupIds.Contains(pickupId))
            {
                collectedPickupIds.Add(pickupId);
            }
        }

        /// <summary>
        /// Marks a barrel as destroyed.
        /// </summary>
        public void MarkBarrelDestroyed(string barrelId)
        {
            if (!destroyedBarrelIds.Contains(barrelId))
            {
                destroyedBarrelIds.Add(barrelId);
            }
        }

        /// <summary>
        /// Marks a hidden block as revealed.
        /// </summary>
        public void MarkHiddenBlockRevealed(string blockId)
        {
            if (!revealedHiddenBlockIds.Contains(blockId))
            {
                revealedHiddenBlockIds.Add(blockId);
            }
        }

        /// <summary>
        /// Adds a dropped mask to the level.
        /// </summary>
        public void AddDroppedMask(string maskId, Vector3 position, ColorType color)
        {
            droppedMasks.Add(new DroppedMaskSaveData
            {
                maskId = maskId,
                position = position,
                color = color
            });
        }

        /// <summary>
        /// Removes a dropped mask (when picked up).
        /// </summary>
        public void RemoveDroppedMask(string maskId)
        {
            droppedMasks.RemoveAll(m => m.maskId == maskId);
        }

        /// <summary>
        /// Updates or adds a bot's state.
        /// </summary>
        public void UpdateBotState(BotSaveData botData)
        {
            int index = botStates.FindIndex(b => b.botId == botData.botId);
            if (index >= 0)
            {
                botStates[index] = botData;
            }
            else
            {
                botStates.Add(botData);
            }
        }

        /// <summary>
        /// Gets a bot's saved state.
        /// </summary>
        public BotSaveData GetBotState(string botId)
        {
            return botStates.Find(b => b.botId == botId);
        }

        /// <summary>
        /// Creates a deep copy of this save data.
        /// </summary>
        public LevelSaveData Clone()
        {
            var clone = new LevelSaveData(levelId)
            {
                hasBeenVisited = hasBeenVisited,
                collectedPickupIds = new List<string>(collectedPickupIds),
                destroyedBarrelIds = new List<string>(destroyedBarrelIds),
                revealedHiddenBlockIds = new List<string>(revealedHiddenBlockIds)
            };
            
            foreach (var bot in botStates)
            {
                clone.botStates.Add(bot.Clone());
            }
            
            foreach (var mask in droppedMasks)
            {
                clone.droppedMasks.Add(mask.Clone());
            }
            
            return clone;
        }

        /// <summary>
        /// Resets to initial state (nothing collected/destroyed).
        /// </summary>
        public void Reset()
        {
            hasBeenVisited = false;
            collectedPickupIds.Clear();
            destroyedBarrelIds.Clear();
            revealedHiddenBlockIds.Clear();
            botStates.Clear();
            droppedMasks.Clear();
        }
    }

    /// <summary>
    /// Save data for a single bot.
    /// </summary>
    [Serializable]
    public class BotSaveData
    {
        public string botId;
        public Vector3 position;
        public bool isDead;
        
        // Path state
        public int currentWaypointIndex;
        public bool movingForward;
        public float waitTimer;
        public bool isWaiting;
        
        // Inventory state
        public ColorType[] inventorySlots;
        public bool[] activeSlots;

        public BotSaveData Clone()
        {
            return new BotSaveData
            {
                botId = botId,
                position = position,
                isDead = isDead,
                currentWaypointIndex = currentWaypointIndex,
                movingForward = movingForward,
                waitTimer = waitTimer,
                isWaiting = isWaiting,
                inventorySlots = inventorySlots != null ? (ColorType[])inventorySlots.Clone() : null,
                activeSlots = activeSlots != null ? (bool[])activeSlots.Clone() : null
            };
        }
    }

    /// <summary>
    /// Save data for a dropped mask.
    /// </summary>
    [Serializable]
    public class DroppedMaskSaveData
    {
        public string maskId;
        public Vector3 position;
        public ColorType color;

        public DroppedMaskSaveData Clone()
        {
            return new DroppedMaskSaveData
            {
                maskId = maskId,
                position = position,
                color = color
            };
        }
    }

    /// <summary>
    /// Global save data containing player state and all level states.
    /// This is what gets serialized to disk.
    /// </summary>
    [Serializable]
    public class GlobalSaveData
    {
        public string currentLevelId;
        public Vector3 playerPosition;
        
        // Player inventory
        public InventorySnapshot playerInventory;
        
        // All level states
        public List<LevelSaveData> levelStates = new List<LevelSaveData>();
        
        // Checkpoint data
        public CheckpointData lastCheckpoint;

        /// <summary>
        /// Gets or creates save data for a specific level.
        /// </summary>
        public LevelSaveData GetOrCreateLevelData(string levelId)
        {
            var existing = levelStates.Find(l => l.levelId == levelId);
            if (existing != null)
                return existing;
            
            var newData = new LevelSaveData(levelId);
            levelStates.Add(newData);
            return newData;
        }

        /// <summary>
        /// Creates a deep copy for checkpoint saving.
        /// </summary>
        public GlobalSaveData Clone()
        {
            var clone = new GlobalSaveData
            {
                currentLevelId = currentLevelId,
                playerPosition = playerPosition,
                playerInventory = playerInventory != null ? new InventorySnapshot
                {
                    Slots = (ColorType[])playerInventory.Slots?.Clone(),
                    ActiveSlots = (bool[])playerInventory.ActiveSlots?.Clone()
                } : null
            };
            
            foreach (var level in levelStates)
            {
                clone.levelStates.Add(level.Clone());
            }
            
            if (lastCheckpoint != null)
            {
                clone.lastCheckpoint = lastCheckpoint.Clone();
            }
            
            return clone;
        }
    }

    /// <summary>
    /// Snapshot of game state at a checkpoint.
    /// </summary>
    [Serializable]
    public class CheckpointData
    {
        public string checkpointLevelId;
        public string checkpointPortalId;
        public Vector3 playerPosition;
        public InventorySnapshot playerInventory;
        public List<LevelSaveData> levelStates = new List<LevelSaveData>();
        
        /// <summary>
        /// IDs of tutorials that have been completed.
        /// </summary>
        public List<string> completedTutorials = new List<string>();

        public CheckpointData Clone()
        {
            var clone = new CheckpointData
            {
                checkpointLevelId = checkpointLevelId,
                checkpointPortalId = checkpointPortalId,
                playerPosition = playerPosition,
                playerInventory = playerInventory != null ? new InventorySnapshot
                {
                    Slots = (ColorType[])playerInventory.Slots?.Clone(),
                    ActiveSlots = (bool[])playerInventory.ActiveSlots?.Clone()
                } : null,
                completedTutorials = new List<string>(completedTutorials)
            };
            
            foreach (var level in levelStates)
            {
                clone.levelStates.Add(level.Clone());
            }
            
            return clone;
        }
    }
}

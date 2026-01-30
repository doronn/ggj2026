using System;
using System.Collections.Generic;
using UnityEngine;
using BreakingHue.Core;

namespace BreakingHue.Gameplay.Bot
{
    /// <summary>
    /// Inventory system for bots. Similar to player inventory but with special rules:
    /// - 3 slots maximum
    /// - Cannot carry duplicate colors (walks through masks of colors it already owns)
    /// - When picking up a mask, only takes colors it doesn't have, drops residue
    /// - Has an "initial color" that can regenerate when passing through matching color blocks
    /// 
    /// Implements IColorInventory for barrier/barrel interaction.
    /// </summary>
    public class BotInventory : MonoBehaviour, IColorInventory, IBotMaskDropper
    {
        public const int MaxSlots = 3;
        
        [Header("Initial State")]
        [SerializeField] private ColorType initialColor = ColorType.None;
        
        private readonly ColorType[] _slots = new ColorType[MaxSlots];
        private readonly bool[] _activeSlots = new bool[MaxSlots];
        
        /// <summary>
        /// Event fired when a mask is dropped by this bot.
        /// Parameters: world position, color to drop
        /// </summary>
        public event Action<Vector3, ColorType> OnMaskDropped;

        /// <summary>
        /// Event fired when inventory changes.
        /// </summary>
        public event Action OnInventoryChanged;

        /// <summary>
        /// Raises the OnMaskDropped event. Called externally to trigger mask spawn.
        /// </summary>
        public void RaiseMaskDropped(Vector3 position, ColorType color)
        {
            OnMaskDropped?.Invoke(position, color);
        }

        private void Awake()
        {
            ResetToInitial();
        }

        /// <summary>
        /// Resets the inventory to initial state (initial color only).
        /// </summary>
        public void ResetToInitial()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = ColorType.None;
                _activeSlots[i] = false;
            }
            
            if (initialColor != ColorType.None)
            {
                _slots[0] = initialColor;
                _activeSlots[0] = true; // Initial mask is always active
            }
            
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Sets the initial color for this bot.
        /// </summary>
        public void SetInitialColor(ColorType color)
        {
            initialColor = color;
            ResetToInitial();
        }

        /// <summary>
        /// Gets the initial color of this bot.
        /// </summary>
        public ColorType InitialColor => initialColor;

        /// <summary>
        /// Gets the combined color of all active masks.
        /// </summary>
        public ColorType GetCombinedActiveColor()
        {
            ColorType combined = ColorType.None;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_activeSlots[i] && _slots[i] != ColorType.None)
                {
                    combined |= _slots[i];
                }
            }
            return combined;
        }

        /// <summary>
        /// Gets the combined color of ALL masks (active or not).
        /// </summary>
        public ColorType GetTotalColor()
        {
            ColorType combined = ColorType.None;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] != ColorType.None)
                {
                    combined |= _slots[i];
                }
            }
            return combined;
        }

        /// <summary>
        /// Checks if bot can pass through a barrier of the given color.
        /// </summary>
        public bool CanPassThrough(ColorType barrierColor)
        {
            ColorType combined = GetCombinedActiveColor();
            return ColorTypeExtensions.CanPassThrough(combined, barrierColor);
        }

        /// <summary>
        /// Applies barrier subtraction with residue system.
        /// </summary>
        public void ApplyBarrierSubtraction(ColorType barrierColor)
        {
            var subtractions = CalculateOptimalSubtraction(barrierColor);
            
            foreach (var kvp in subtractions)
            {
                int slotIndex = kvp.Key;
                ColorType toRemove = kvp.Value;
                
                ColorType currentMask = _slots[slotIndex];
                ColorType newMask = currentMask.Subtract(toRemove);
                
                _slots[slotIndex] = newMask;
                if (newMask == ColorType.None)
                {
                    _activeSlots[slotIndex] = false;
                }
            }
            
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Calculates optimal subtraction (same algorithm as player inventory).
        /// </summary>
        private Dictionary<int, ColorType> CalculateOptimalSubtraction(ColorType required)
        {
            var result = new Dictionary<int, ColorType>();
            ColorType remaining = required.GetPrimaryComponents();
            
            foreach (ColorType primary in new[] { ColorType.Red, ColorType.Yellow, ColorType.Blue })
            {
                if ((remaining & primary) == 0)
                    continue;
                
                int bestSlot = -1;
                int bestWaste = int.MaxValue;
                
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (!_activeSlots[i] || _slots[i] == ColorType.None)
                        continue;
                    
                    ColorType mask = _slots[i];
                    if ((mask & primary) == 0)
                        continue;
                    
                    int waste = mask.CountPrimaries() - 1;
                    
                    if (result.ContainsKey(i))
                    {
                        bestSlot = i;
                        break;
                    }
                    
                    if (waste < bestWaste)
                    {
                        bestWaste = waste;
                        bestSlot = i;
                    }
                }
                
                if (bestSlot >= 0)
                {
                    if (result.ContainsKey(bestSlot))
                        result[bestSlot] |= primary;
                    else
                        result[bestSlot] = primary;
                    
                    remaining = remaining.Subtract(primary);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Attempts to pick up a mask. Bot only takes colors it doesn't have.
        /// Returns the residue color to drop (colors the bot already had).
        /// </summary>
        public ColorType TryPickupMask(ColorType maskColor)
        {
            if (maskColor == ColorType.None)
                return ColorType.None;

            ColorType ownedColors = GetTotalColor();
            ColorType neededColors = maskColor.Subtract(ownedColors);
            ColorType residue = maskColor.Subtract(neededColors);
            
            if (neededColors == ColorType.None)
            {
                // Bot already has all these colors - walk through, no pickup
                return maskColor; // Return entire mask as residue
            }
            
            // Find a slot for the needed colors
            int emptySlot = FindEmptySlot();
            if (emptySlot >= 0)
            {
                _slots[emptySlot] = neededColors;
                _activeSlots[emptySlot] = true; // Auto-activate new masks
                OnInventoryChanged?.Invoke();
            }
            else
            {
                // No empty slot - can't pick up
                return maskColor;
            }
            
            return residue; // Return what the bot didn't need
        }

        /// <summary>
        /// Checks if bot already has all colors in the given mask.
        /// </summary>
        public bool AlreadyHasAllColors(ColorType maskColor)
        {
            ColorType owned = GetTotalColor();
            return (maskColor & ~owned) == ColorType.None;
        }

        /// <summary>
        /// Finds an empty slot, or -1 if full.
        /// </summary>
        private int FindEmptySlot()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == ColorType.None)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Regenerates the initial color if bot passed through a matching color block.
        /// Called when bot passes through a barrier matching its initial color.
        /// </summary>
        public void TryRegenerateInitialColor()
        {
            if (initialColor == ColorType.None)
                return;

            // Check if bot has lost its initial color
            ColorType owned = GetTotalColor();
            ColorType missingInitial = initialColor.Subtract(owned);
            
            if (missingInitial != ColorType.None)
            {
                // Try to add the missing initial colors
                int emptySlot = FindEmptySlot();
                if (emptySlot >= 0)
                {
                    _slots[emptySlot] = missingInitial;
                    _activeSlots[emptySlot] = true;
                    OnInventoryChanged?.Invoke();
                    
                    Debug.Log($"[BotInventory] Regenerated initial color: {missingInitial.GetDisplayName()}");
                }
            }
        }

        /// <summary>
        /// Drops all masks at the current position.
        /// Used when bot explodes.
        /// </summary>
        public void DropAllMasks()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] != ColorType.None)
                {
                    OnMaskDropped?.Invoke(transform.position, _slots[i]);
                    _slots[i] = ColorType.None;
                    _activeSlots[i] = false;
                }
            }
            
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Gets a specific slot's color.
        /// </summary>
        public ColorType GetSlot(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return ColorType.None;
            return _slots[index];
        }

        /// <summary>
        /// Checks if a slot is active.
        /// </summary>
        public bool IsSlotActive(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return false;
            return _activeSlots[index];
        }

        /// <summary>
        /// Creates a snapshot of the current inventory state.
        /// </summary>
        public BotInventorySnapshot CreateSnapshot()
        {
            return new BotInventorySnapshot
            {
                Slots = (ColorType[])_slots.Clone(),
                ActiveSlots = (bool[])_activeSlots.Clone()
            };
        }

        /// <summary>
        /// Restores inventory from a snapshot.
        /// </summary>
        public void RestoreFromSnapshot(BotInventorySnapshot snapshot)
        {
            if (snapshot?.Slots == null || snapshot.ActiveSlots == null)
                return;

            for (int i = 0; i < MaxSlots && i < snapshot.Slots.Length; i++)
            {
                _slots[i] = snapshot.Slots[i];
                _activeSlots[i] = snapshot.ActiveSlots[i];
            }
            
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Gets the count of non-empty slots.
        /// </summary>
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_slots[i] != ColorType.None)
                        count++;
                }
                return count;
            }
        }
    }

    /// <summary>
    /// Snapshot of bot inventory for checkpointing.
    /// </summary>
    [Serializable]
    public class BotInventorySnapshot
    {
        public ColorType[] Slots;
        public bool[] ActiveSlots;
    }
}

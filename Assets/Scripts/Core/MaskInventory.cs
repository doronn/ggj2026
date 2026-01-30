using System;
using System.Collections.Generic;

namespace BreakingHue.Core
{
    /// <summary>
    /// Slot-based mask inventory system with multi-mask toggle support.
    /// Supports 3 discrete mask slots with independent activation.
    /// Active masks combine their colors for barrier passage.
    /// When passing through barriers, only the required colors are consumed,
    /// leaving residue colors in the masks.
    /// Bound as singleton via Zenject.
    /// </summary>
    public class MaskInventory
    {
        public const int MaxSlots = 3;
        
        private readonly ColorType[] _slots = new ColorType[MaxSlots];
        private readonly bool[] _activeSlots = new bool[MaxSlots];

        /// <summary>
        /// Fired when any slot changes (add, remove, transform).
        /// </summary>
        public event Action OnInventoryChanged;
        
        /// <summary>
        /// Fired when a mask's active state changes.
        /// Parameters: slot index, new active state
        /// </summary>
        public event Action<int, bool> OnMaskToggled;
        
        /// <summary>
        /// Fired when masks are consumed/transformed after barrier passage.
        /// Parameter is the ColorType that was consumed from the combined masks.
        /// </summary>
        public event Action<ColorType> OnMaskConsumed;

        /// <summary>
        /// Fired when a mask is dropped.
        /// Parameters: slot index, ColorType that was dropped
        /// </summary>
        public event Action<int, ColorType> OnMaskDropped;

        public MaskInventory()
        {
            // Initialize all slots to empty and inactive
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = ColorType.None;
                _activeSlots[i] = false;
            }
        }

        /// <summary>
        /// Gets the mask in a specific slot.
        /// </summary>
        public ColorType GetSlot(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return ColorType.None;
            return _slots[index];
        }

        /// <summary>
        /// Gets all slots as a read-only span.
        /// </summary>
        public ReadOnlySpan<ColorType> Slots => _slots;

        /// <summary>
        /// Checks if a specific slot is active (toggled on).
        /// </summary>
        public bool IsSlotActive(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return false;
            return _activeSlots[index];
        }

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
        /// Gets indices of all active slots.
        /// </summary>
        public List<int> GetActiveSlotIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_activeSlots[i] && _slots[i] != ColorType.None)
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        /// <summary>
        /// Checks if a slot is empty.
        /// </summary>
        public bool IsSlotEmpty(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return true;
            return _slots[index] == ColorType.None;
        }

        /// <summary>
        /// Finds the first empty slot index, or -1 if inventory is full.
        /// </summary>
        public int FindEmptySlot()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == ColorType.None)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Attempts to add a mask to the first available slot.
        /// Returns true if successful, false if inventory is full.
        /// </summary>
        public bool TryAddMask(ColorType mask)
        {
            if (mask == ColorType.None)
                return false;

            int emptySlot = FindEmptySlot();
            if (emptySlot < 0)
                return false;

            _slots[emptySlot] = mask;
            _activeSlots[emptySlot] = false; // New masks start inactive

            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Adds a mask to a specific slot, replacing any existing mask.
        /// </summary>
        public void SetSlot(int index, ColorType mask)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            _slots[index] = mask;
            if (mask == ColorType.None)
            {
                _activeSlots[index] = false;
            }
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Toggles the active state of a mask in the specified slot.
        /// Empty slots cannot be activated.
        /// </summary>
        public void ToggleMask(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            // Can't activate empty slot
            if (_slots[index] == ColorType.None)
            {
                _activeSlots[index] = false;
                return;
            }

            _activeSlots[index] = !_activeSlots[index];

            OnMaskToggled?.Invoke(index, _activeSlots[index]);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Sets the active state of a specific slot.
        /// </summary>
        public void SetSlotActive(int index, bool active)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            // Can't activate empty slot
            if (_slots[index] == ColorType.None)
            {
                _activeSlots[index] = false;
                return;
            }

            if (_activeSlots[index] != active)
            {
                _activeSlots[index] = active;
                OnMaskToggled?.Invoke(index, active);
                OnInventoryChanged?.Invoke();
            }
        }

        /// <summary>
        /// Deactivates all masks.
        /// </summary>
        public void DeactivateAll()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_activeSlots[i])
                {
                    _activeSlots[i] = false;
                    OnMaskToggled?.Invoke(i, false);
                }
            }
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Checks if the combined active masks can pass through a barrier of the given color.
        /// </summary>
        public bool CanPassThrough(ColorType barrierColor)
        {
            ColorType combined = GetCombinedActiveColor();
            return ColorTypeExtensions.CanPassThrough(combined, barrierColor);
        }

        /// <summary>
        /// Transforms a mask in a specific slot to a new color.
        /// Used for the residue system when colors are partially consumed.
        /// </summary>
        public void TransformMask(int index, ColorType newColor)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            _slots[index] = newColor;
            
            // If mask becomes empty, deactivate it
            if (newColor == ColorType.None)
            {
                _activeSlots[index] = false;
            }
            
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Calculates the optimal way to subtract required colors from active masks,
        /// minimizing waste and maximizing residue.
        /// Returns a dictionary mapping slot indices to the colors that should be removed from them.
        /// </summary>
        public Dictionary<int, ColorType> CalculateOptimalSubtraction(ColorType required)
        {
            var result = new Dictionary<int, ColorType>();
            var activeIndices = GetActiveSlotIndices();
            
            if (activeIndices.Count == 0)
                return result;

            // Get the primary components we need to satisfy
            ColorType remaining = required.GetPrimaryComponents();
            
            // Strategy: For each required primary, find the mask that has it
            // with the least "waste" (fewest other primaries)
            foreach (ColorType primary in new[] { ColorType.Red, ColorType.Yellow, ColorType.Blue })
            {
                if ((remaining & primary) == 0)
                    continue; // This primary not needed
                
                // Find best slot to take this primary from
                int bestSlot = -1;
                int bestWaste = int.MaxValue;
                
                foreach (int slotIndex in activeIndices)
                {
                    ColorType mask = _slots[slotIndex];
                    if ((mask & primary) == 0)
                        continue; // Slot doesn't have this primary
                    
                    // Count how many other primaries this mask has (waste potential)
                    int waste = mask.CountPrimaries() - 1;
                    
                    // Prefer masks that will contribute this primary with least waste
                    // Also prefer masks we're already taking from
                    if (result.ContainsKey(slotIndex))
                    {
                        // Already taking from this slot, prefer it to minimize fragmentation
                        bestSlot = slotIndex;
                        break;
                    }
                    
                    if (waste < bestWaste)
                    {
                        bestWaste = waste;
                        bestSlot = slotIndex;
                    }
                }
                
                if (bestSlot >= 0)
                {
                    // Add this primary to what we're taking from this slot
                    if (result.ContainsKey(bestSlot))
                    {
                        result[bestSlot] |= primary;
                    }
                    else
                    {
                        result[bestSlot] = primary;
                    }
                    
                    remaining = remaining.Subtract(primary);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Applies the color subtraction from a barrier passage.
        /// Transforms masks in-place, leaving residue colors.
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
                
                TransformMask(slotIndex, newMask);
            }
            
            OnMaskConsumed?.Invoke(barrierColor);
        }

        /// <summary>
        /// Applies barrier subtraction using specific slots (used when player deactivated masks mid-phase).
        /// This ensures masks are consumed based on what was active when phasing STARTED, not current state.
        /// </summary>
        public void ApplyBarrierSubtractionFromSlots(ColorType barrierColor, List<int> slotIndices)
        {
            if (slotIndices == null || slotIndices.Count == 0)
                return;
            
            var subtractions = CalculateOptimalSubtractionFromSlots(barrierColor, slotIndices);
            
            foreach (var kvp in subtractions)
            {
                int slotIndex = kvp.Key;
                ColorType toRemove = kvp.Value;
                
                ColorType currentMask = _slots[slotIndex];
                ColorType newMask = currentMask.Subtract(toRemove);
                
                TransformMask(slotIndex, newMask);
            }
            
            OnMaskConsumed?.Invoke(barrierColor);
        }

        /// <summary>
        /// Calculates optimal subtraction using specific slot indices (not current active slots).
        /// </summary>
        private Dictionary<int, ColorType> CalculateOptimalSubtractionFromSlots(ColorType required, List<int> slotIndices)
        {
            var result = new Dictionary<int, ColorType>();
            
            if (slotIndices.Count == 0)
                return result;

            // Get the primary components we need to satisfy
            ColorType remaining = required.GetPrimaryComponents();
            
            // Strategy: For each required primary, find the mask that has it
            foreach (ColorType primary in new[] { ColorType.Red, ColorType.Yellow, ColorType.Blue })
            {
                if ((remaining & primary) == 0)
                    continue; // This primary not needed
                
                // Find best slot to take this primary from (only from provided slots)
                int bestSlot = -1;
                int bestWaste = int.MaxValue;
                
                foreach (int slotIndex in slotIndices)
                {
                    if (slotIndex < 0 || slotIndex >= MaxSlots)
                        continue;
                        
                    ColorType mask = _slots[slotIndex];
                    if ((mask & primary) == 0)
                        continue; // Slot doesn't have this primary
                    
                    // Count how many other primaries this mask has (waste potential)
                    int waste = mask.CountPrimaries() - 1;
                    
                    // Prefer masks we're already taking from
                    if (result.ContainsKey(slotIndex))
                    {
                        bestSlot = slotIndex;
                        break;
                    }
                    
                    if (waste < bestWaste)
                    {
                        bestWaste = waste;
                        bestSlot = slotIndex;
                    }
                }
                
                if (bestSlot >= 0)
                {
                    if (result.ContainsKey(bestSlot))
                    {
                        result[bestSlot] |= primary;
                    }
                    else
                    {
                        result[bestSlot] = primary;
                    }
                    
                    remaining = remaining.Subtract(primary);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Drops a mask from a specific slot.
        /// Returns the color that was dropped (for spawning a pickup).
        /// </summary>
        public ColorType DropMask(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return ColorType.None;

            ColorType dropped = _slots[index];
            if (dropped == ColorType.None)
                return ColorType.None;

            _slots[index] = ColorType.None;
            _activeSlots[index] = false;
            
            OnMaskDropped?.Invoke(index, dropped);
            OnInventoryChanged?.Invoke();
            
            return dropped;
        }

        /// <summary>
        /// Removes the mask from a specific slot.
        /// </summary>
        public void RemoveFromSlot(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            _slots[index] = ColorType.None;
            _activeSlots[index] = false;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Resets the entire inventory to empty state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = ColorType.None;
                _activeSlots[i] = false;
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

        /// <summary>
        /// Gets the count of active (toggled on) slots.
        /// </summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_activeSlots[i] && _slots[i] != ColorType.None)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Checks if the inventory is full.
        /// </summary>
        public bool IsFull => FindEmptySlot() < 0;

        /// <summary>
        /// Creates a snapshot of the current inventory state for checkpointing.
        /// </summary>
        public InventorySnapshot CreateSnapshot()
        {
            return new InventorySnapshot
            {
                Slots = (ColorType[])_slots.Clone(),
                ActiveSlots = (bool[])_activeSlots.Clone()
            };
        }

        /// <summary>
        /// Restores inventory state from a snapshot.
        /// </summary>
        public void RestoreFromSnapshot(InventorySnapshot snapshot)
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
        /// Checks if the inventory already contains a specific color.
        /// Used by bots to avoid picking up duplicate colors.
        /// </summary>
        public bool ContainsColor(ColorType color)
        {
            if (color == ColorType.None)
                return false;

            for (int i = 0; i < MaxSlots; i++)
            {
                if ((_slots[i] & color) != ColorType.None)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets colors that are NOT already in the inventory.
        /// Used for bots picking up partial colors from masks.
        /// </summary>
        public ColorType GetMissingColors(ColorType maskColor)
        {
            ColorType owned = ColorType.None;
            for (int i = 0; i < MaxSlots; i++)
            {
                owned |= _slots[i];
            }
            
            // Return only the colors from maskColor that we don't have
            return maskColor.Subtract(owned);
        }

        // Legacy compatibility - maps to toggle behavior
        [Obsolete("Use ToggleMask instead for the new multi-select system")]
        public int EquippedSlotIndex => GetActiveSlotIndices().Count > 0 ? GetActiveSlotIndices()[0] : -1;

        [Obsolete("Use GetCombinedActiveColor instead")]
        public ColorType EquippedMask => GetCombinedActiveColor();

        [Obsolete("Use ToggleMask instead")]
        public void EquipSlot(int index) => ToggleMask(index);

        [Obsolete("Use DeactivateAll instead")]
        public void UnequipMask() => DeactivateAll();

        [Obsolete("Use ApplyBarrierSubtraction instead")]
        public void ConsumeEquipped()
        {
            var active = GetActiveSlotIndices();
            if (active.Count > 0)
            {
                RemoveFromSlot(active[0]);
            }
        }
    }

    /// <summary>
    /// Snapshot of inventory state for checkpointing.
    /// </summary>
    [Serializable]
    public class InventorySnapshot
    {
        public ColorType[] Slots;
        public bool[] ActiveSlots;
    }
}

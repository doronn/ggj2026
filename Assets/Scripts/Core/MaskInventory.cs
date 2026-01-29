using System;

namespace BreakingHue.Core
{
    /// <summary>
    /// Slot-based mask inventory system.
    /// Supports 3 discrete mask slots with manual equipping.
    /// Bound as singleton via Zenject.
    /// </summary>
    public class MaskInventory
    {
        public const int MaxSlots = 3;
        
        private readonly ColorType[] _slots = new ColorType[MaxSlots];
        private int _equippedSlotIndex = -1;

        /// <summary>
        /// Fired when any slot changes (add, remove, consume).
        /// </summary>
        public event Action OnInventoryChanged;
        
        /// <summary>
        /// Fired when a mask is equipped or unequipped.
        /// Parameter is the equipped ColorType (None if unequipped).
        /// </summary>
        public event Action<ColorType> OnMaskEquipped;
        
        /// <summary>
        /// Fired when the equipped mask is consumed (destroyed).
        /// Parameter is the ColorType that was consumed.
        /// </summary>
        public event Action<ColorType> OnMaskConsumed;

        public MaskInventory()
        {
            // Initialize all slots to empty
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = ColorType.None;
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
        /// Gets the currently equipped slot index (-1 if none equipped).
        /// </summary>
        public int EquippedSlotIndex => _equippedSlotIndex;

        /// <summary>
        /// Gets the currently equipped mask color (None if nothing equipped).
        /// </summary>
        public ColorType EquippedMask => _equippedSlotIndex >= 0 ? _slots[_equippedSlotIndex] : ColorType.None;

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

            // If we're replacing the equipped slot with None, unequip first
            if (index == _equippedSlotIndex && mask == ColorType.None)
            {
                UnequipMask();
            }

            _slots[index] = mask;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Equips the mask in the specified slot.
        /// If the slot is empty, does nothing.
        /// If the same slot is already equipped, unequips it (toggle).
        /// </summary>
        public void EquipSlot(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            // Toggle off if same slot
            if (_equippedSlotIndex == index)
            {
                UnequipMask();
                return;
            }

            // Can't equip empty slot
            if (_slots[index] == ColorType.None)
                return;

            _equippedSlotIndex = index;
            OnMaskEquipped?.Invoke(_slots[index]);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Unequips the currently worn mask (returns it to inventory, still in slot).
        /// </summary>
        public void UnequipMask()
        {
            if (_equippedSlotIndex < 0)
                return;

            _equippedSlotIndex = -1;
            OnMaskEquipped?.Invoke(ColorType.None);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Checks if the currently equipped mask can pass through a door of the given color.
        /// </summary>
        public bool CanPassThrough(ColorType doorColor)
        {
            if (_equippedSlotIndex < 0)
                return false;

            return ColorTypeExtensions.CanPassThrough(_slots[_equippedSlotIndex], doorColor);
        }

        /// <summary>
        /// Consumes (destroys) the currently equipped mask.
        /// Called when passing through a door.
        /// </summary>
        public void ConsumeEquipped()
        {
            if (_equippedSlotIndex < 0)
                return;

            ColorType consumed = _slots[_equippedSlotIndex];
            _slots[_equippedSlotIndex] = ColorType.None;
            
            int previousSlot = _equippedSlotIndex;
            _equippedSlotIndex = -1;
            
            OnMaskConsumed?.Invoke(consumed);
            OnMaskEquipped?.Invoke(ColorType.None);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Removes the mask from a specific slot.
        /// If that slot was equipped, unequips it first.
        /// </summary>
        public void RemoveFromSlot(int index)
        {
            if (index < 0 || index >= MaxSlots)
                return;

            if (index == _equippedSlotIndex)
            {
                _equippedSlotIndex = -1;
                OnMaskEquipped?.Invoke(ColorType.None);
            }

            _slots[index] = ColorType.None;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Resets the entire inventory to empty state.
        /// </summary>
        public void Reset()
        {
            _equippedSlotIndex = -1;
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = ColorType.None;
            }
            OnMaskEquipped?.Invoke(ColorType.None);
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
        /// Checks if the inventory is full.
        /// </summary>
        public bool IsFull => FindEmptySlot() < 0;
    }
}

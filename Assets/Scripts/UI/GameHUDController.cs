using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.UI
{
    /// <summary>
    /// HUD controller using Unity UI Toolkit.
    /// Displays 3 fixed inventory slots with equip hotkey indicators.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int slotSize = 48;
        [SerializeField] private int spacing = 8;
        
        private UIDocument _uiDocument;
        private VisualElement _slotsContainer;
        private MaskInventory _inventory;
        
        // Cached slot elements
        private readonly VisualElement[] _slotElements = new VisualElement[MaskInventory.MaxSlots];
        private readonly VisualElement[] _slotColorBlocks = new VisualElement[MaskInventory.MaxSlots];
        private readonly Label[] _slotLabels = new Label[MaskInventory.MaxSlots];

        [Inject]
        public void Construct(MaskInventory inventory)
        {
            _inventory = inventory;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Wait for UI to be ready
            _uiDocument.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnUIReady);
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= OnInventoryChanged;
                _inventory.OnMaskEquipped -= OnMaskEquipped;
            }
        }

        private void OnUIReady(GeometryChangedEvent evt)
        {
            _uiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnUIReady);
            
            _slotsContainer = _uiDocument.rootVisualElement.Q<VisualElement>("SlotsContainer");
            
            if (_slotsContainer == null)
            {
                // Fallback to old Container name
                _slotsContainer = _uiDocument.rootVisualElement.Q<VisualElement>("Container");
            }
            
            if (_slotsContainer == null)
            {
                Debug.LogWarning("[GameHUDController] SlotsContainer not found in UXML!");
                return;
            }
            
            // Create the slot elements
            CreateSlotElements();
            
            // Subscribe to inventory changes
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += OnInventoryChanged;
                _inventory.OnMaskEquipped += OnMaskEquipped;
                
                // Initial update
                RefreshAllSlots();
            }
        }

        private void CreateSlotElements()
        {
            _slotsContainer.Clear();
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                var slot = CreateSlotElement(i);
                _slotsContainer.Add(slot);
                _slotElements[i] = slot;
            }
        }

        private VisualElement CreateSlotElement(int index)
        {
            // Container for the slot
            var slotContainer = new VisualElement();
            slotContainer.name = $"Slot_{index}";
            slotContainer.AddToClassList("slot-container");
            
            // Slot background/frame
            var slotFrame = new VisualElement();
            slotFrame.name = $"SlotFrame_{index}";
            slotFrame.AddToClassList("slot-frame");
            slotFrame.style.width = slotSize;
            slotFrame.style.height = slotSize;
            
            // Color block inside the slot
            var colorBlock = new VisualElement();
            colorBlock.name = $"ColorBlock_{index}";
            colorBlock.AddToClassList("slot-color-block");
            colorBlock.AddToClassList("empty");
            slotFrame.Add(colorBlock);
            _slotColorBlocks[index] = colorBlock;
            
            slotContainer.Add(slotFrame);
            
            // Hotkey label below the slot
            var hotkeyLabel = new Label($"{index + 1}");
            hotkeyLabel.name = $"HotkeyLabel_{index}";
            hotkeyLabel.AddToClassList("hotkey-label");
            slotContainer.Add(hotkeyLabel);
            _slotLabels[index] = hotkeyLabel;
            
            return slotContainer;
        }

        private void OnInventoryChanged()
        {
            RefreshAllSlots();
        }

        private void OnMaskEquipped(ColorType mask)
        {
            RefreshAllSlots();
        }

        private void RefreshAllSlots()
        {
            if (_inventory == null) return;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                RefreshSlot(i);
            }
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= MaskInventory.MaxSlots) return;
            
            var colorBlock = _slotColorBlocks[index];
            var slotFrame = colorBlock?.parent;
            if (colorBlock == null || slotFrame == null) return;
            
            ColorType maskInSlot = _inventory.GetSlot(index);
            bool isEquipped = _inventory.EquippedSlotIndex == index;
            bool isEmpty = maskInSlot == ColorType.None;
            
            // Update color block appearance
            colorBlock.RemoveFromClassList("empty");
            colorBlock.RemoveFromClassList("color-red");
            colorBlock.RemoveFromClassList("color-green");
            colorBlock.RemoveFromClassList("color-blue");
            colorBlock.RemoveFromClassList("color-yellow");
            colorBlock.RemoveFromClassList("color-cyan");
            colorBlock.RemoveFromClassList("color-magenta");
            colorBlock.RemoveFromClassList("color-white");
            
            // Update frame for equipped state
            slotFrame.RemoveFromClassList("equipped");
            
            if (isEmpty)
            {
                colorBlock.AddToClassList("empty");
                colorBlock.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
            }
            else
            {
                // Set color based on mask type
                Color maskColor = maskInSlot.ToColor();
                colorBlock.style.backgroundColor = new StyleColor(maskColor);
                
                // Add specific class for styling
                string colorClass = GetColorClass(maskInSlot);
                if (!string.IsNullOrEmpty(colorClass))
                {
                    colorBlock.AddToClassList(colorClass);
                }
                
                // Highlight if equipped
                if (isEquipped)
                {
                    slotFrame.AddToClassList("equipped");
                }
            }
        }

        private string GetColorClass(ColorType color)
        {
            return color switch
            {
                ColorType.Red => "color-red",
                ColorType.Green => "color-green",
                ColorType.Blue => "color-blue",
                ColorType.Yellow => "color-yellow",
                ColorType.Cyan => "color-cyan",
                ColorType.Magenta => "color-magenta",
                ColorType.White => "color-white",
                _ => null
            };
        }

        /// <summary>
        /// Force refresh the display.
        /// </summary>
        public void ForceRefresh()
        {
            RefreshAllSlots();
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Core;

namespace BreakingHue.UI
{
    /// <summary>
    /// HUD controller using Unity UI Toolkit.
    /// Displays 3 fixed inventory slots with toggle indicators.
    /// Updated for RYB color system with multi-mask toggle support.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int slotSize = 64;
        [SerializeField] private int spacing = 12;
        
        private UIDocument _uiDocument;
        private VisualElement _slotsContainer;
        private VisualElement _combinedColorDisplay;
        private MaskInventory _inventory;
        
        // Cached slot elements
        private readonly VisualElement[] _slotElements = new VisualElement[MaskInventory.MaxSlots];
        private readonly VisualElement[] _slotColorBlocks = new VisualElement[MaskInventory.MaxSlots];
        private readonly VisualElement[] _slotActiveIndicators = new VisualElement[MaskInventory.MaxSlots];
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
                _inventory.OnMaskToggled -= OnMaskToggled;
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
                Debug.LogWarning("[GameHUDController] SlotsContainer not found in UXML! Creating dynamically.");
                CreateDynamicUI();
                return;
            }
            
            // Create the slot elements
            CreateSlotElements();
            
            // Subscribe to inventory changes
            SubscribeToInventory();
        }

        private void CreateDynamicUI()
        {
            var root = _uiDocument.rootVisualElement;
            
            // Create a container for the HUD
            var hudContainer = new VisualElement();
            hudContainer.name = "HUDContainer";
            hudContainer.style.position = Position.Absolute;
            hudContainer.style.bottom = 20;
            hudContainer.style.left = 0;
            hudContainer.style.right = 0;
            hudContainer.style.flexDirection = FlexDirection.Column;
            hudContainer.style.alignItems = Align.Center;
            
            // Combined color display
            _combinedColorDisplay = new VisualElement();
            _combinedColorDisplay.name = "CombinedColor";
            _combinedColorDisplay.style.width = 80;
            _combinedColorDisplay.style.height = 80;
            _combinedColorDisplay.style.marginBottom = 10;
            _combinedColorDisplay.style.borderTopLeftRadius = 40;
            _combinedColorDisplay.style.borderTopRightRadius = 40;
            _combinedColorDisplay.style.borderBottomLeftRadius = 40;
            _combinedColorDisplay.style.borderBottomRightRadius = 40;
            _combinedColorDisplay.style.backgroundColor = new StyleColor(Color.grey);
            _combinedColorDisplay.style.borderTopWidth = 3;
            _combinedColorDisplay.style.borderBottomWidth = 3;
            _combinedColorDisplay.style.borderLeftWidth = 3;
            _combinedColorDisplay.style.borderRightWidth = 3;
            _combinedColorDisplay.style.borderTopColor = new StyleColor(Color.white);
            _combinedColorDisplay.style.borderBottomColor = new StyleColor(Color.white);
            _combinedColorDisplay.style.borderLeftColor = new StyleColor(Color.white);
            _combinedColorDisplay.style.borderRightColor = new StyleColor(Color.white);
            hudContainer.Add(_combinedColorDisplay);
            
            // Slots container
            _slotsContainer = new VisualElement();
            _slotsContainer.name = "SlotsContainer";
            _slotsContainer.style.flexDirection = FlexDirection.Row;
            _slotsContainer.style.justifyContent = Justify.Center;
            hudContainer.Add(_slotsContainer);
            
            root.Add(hudContainer);
            
            CreateSlotElements();
            SubscribeToInventory();
        }

        private void SubscribeToInventory()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += OnInventoryChanged;
                _inventory.OnMaskToggled += OnMaskToggled;
                
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
            slotContainer.style.flexDirection = FlexDirection.Column;
            slotContainer.style.alignItems = Align.Center;
            slotContainer.style.marginLeft = spacing / 2;
            slotContainer.style.marginRight = spacing / 2;
            
            // Slot background/frame
            var slotFrame = new VisualElement();
            slotFrame.name = $"SlotFrame_{index}";
            slotFrame.style.width = slotSize;
            slotFrame.style.height = slotSize;
            slotFrame.style.borderTopLeftRadius = 8;
            slotFrame.style.borderTopRightRadius = 8;
            slotFrame.style.borderBottomLeftRadius = 8;
            slotFrame.style.borderBottomRightRadius = 8;
            slotFrame.style.borderTopWidth = 2;
            slotFrame.style.borderBottomWidth = 2;
            slotFrame.style.borderLeftWidth = 2;
            slotFrame.style.borderRightWidth = 2;
            slotFrame.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            slotFrame.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            slotFrame.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            slotFrame.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            slotFrame.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.9f));
            
            // Color block inside the slot
            var colorBlock = new VisualElement();
            colorBlock.name = $"ColorBlock_{index}";
            colorBlock.style.position = Position.Absolute;
            colorBlock.style.top = 4;
            colorBlock.style.left = 4;
            colorBlock.style.right = 4;
            colorBlock.style.bottom = 4;
            colorBlock.style.borderTopLeftRadius = 4;
            colorBlock.style.borderTopRightRadius = 4;
            colorBlock.style.borderBottomLeftRadius = 4;
            colorBlock.style.borderBottomRightRadius = 4;
            colorBlock.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
            slotFrame.Add(colorBlock);
            _slotColorBlocks[index] = colorBlock;
            
            // Active indicator (check mark or glow)
            var activeIndicator = new VisualElement();
            activeIndicator.name = $"ActiveIndicator_{index}";
            activeIndicator.style.position = Position.Absolute;
            activeIndicator.style.top = -5;
            activeIndicator.style.right = -5;
            activeIndicator.style.width = 16;
            activeIndicator.style.height = 16;
            activeIndicator.style.borderTopLeftRadius = 8;
            activeIndicator.style.borderTopRightRadius = 8;
            activeIndicator.style.borderBottomLeftRadius = 8;
            activeIndicator.style.borderBottomRightRadius = 8;
            activeIndicator.style.backgroundColor = new StyleColor(new Color(0f, 1f, 0.3f));
            activeIndicator.style.display = DisplayStyle.None;
            slotFrame.Add(activeIndicator);
            _slotActiveIndicators[index] = activeIndicator;
            
            slotContainer.Add(slotFrame);
            
            // Hotkey label below the slot
            var hotkeyLabel = new Label($"{index + 1}");
            hotkeyLabel.name = $"HotkeyLabel_{index}";
            hotkeyLabel.style.marginTop = 4;
            hotkeyLabel.style.fontSize = 14;
            hotkeyLabel.style.color = new StyleColor(Color.white);
            hotkeyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slotContainer.Add(hotkeyLabel);
            _slotLabels[index] = hotkeyLabel;
            
            return slotContainer;
        }

        private void OnInventoryChanged()
        {
            RefreshAllSlots();
        }

        private void OnMaskToggled(int slotIndex, bool isActive)
        {
            RefreshSlot(slotIndex);
            UpdateCombinedColorDisplay();
        }

        private void RefreshAllSlots()
        {
            if (_inventory == null) return;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                RefreshSlot(i);
            }
            
            UpdateCombinedColorDisplay();
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= MaskInventory.MaxSlots) return;
            
            var colorBlock = _slotColorBlocks[index];
            var slotFrame = colorBlock?.parent;
            var activeIndicator = _slotActiveIndicators[index];
            if (colorBlock == null || slotFrame == null) return;
            
            ColorType maskInSlot = _inventory.GetSlot(index);
            bool isActive = _inventory.IsSlotActive(index);
            bool isEmpty = maskInSlot == ColorType.None;
            
            // Update color block appearance
            if (isEmpty)
            {
                colorBlock.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
            }
            else
            {
                // Set color based on mask type (RYB system)
                Color maskColor = maskInSlot.ToColor();
                colorBlock.style.backgroundColor = new StyleColor(maskColor);
            }
            
            // Update active indicator
            if (activeIndicator != null)
            {
                activeIndicator.style.display = isActive && !isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Update frame border for active state
            Color borderColor = isActive && !isEmpty 
                ? new Color(0f, 1f, 0.3f) 
                : new Color(0.4f, 0.4f, 0.4f);
            slotFrame.style.borderTopColor = new StyleColor(borderColor);
            slotFrame.style.borderBottomColor = new StyleColor(borderColor);
            slotFrame.style.borderLeftColor = new StyleColor(borderColor);
            slotFrame.style.borderRightColor = new StyleColor(borderColor);
            
            // Update hotkey label styling
            if (_slotLabels[index] != null)
            {
                _slotLabels[index].style.color = new StyleColor(isActive && !isEmpty ? Color.green : Color.white);
            }
        }

        private void UpdateCombinedColorDisplay()
        {
            if (_combinedColorDisplay == null || _inventory == null) return;
            
            ColorType combined = _inventory.GetCombinedActiveColor();
            Color displayColor = combined != ColorType.None ? combined.ToColor() : Color.grey;
            _combinedColorDisplay.style.backgroundColor = new StyleColor(displayColor);
        }

        private string GetColorClass(ColorType color)
        {
            // Updated for RYB color system
            return color switch
            {
                ColorType.Red => "color-red",
                ColorType.Yellow => "color-yellow",
                ColorType.Blue => "color-blue",
                ColorType.Orange => "color-orange",
                ColorType.Green => "color-green",
                ColorType.Purple => "color-purple",
                ColorType.Black => "color-black",
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

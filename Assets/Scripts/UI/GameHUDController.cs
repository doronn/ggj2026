using UnityEngine;
using UnityEngine.UIElements;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Input;

namespace BreakingHue.UI
{
    /// <summary>
    /// HUD controller using Unity UI Toolkit.
    /// Displays 3 fixed inventory slots with toggle indicators.
    /// Updated for RYB color system with multi-mask toggle support.
    /// Includes contextual prompts that adapt to input device.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUDController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int slotSize = 56;
        [SerializeField] private int spacing = 6;
        
        [Header("Mask UI")]
        [SerializeField] private Texture2D maskTexture;
        
        // Themed colors matching Breaking Hue branding
        private static readonly Color AccentColor = new Color(0f, 0.86f, 0.71f); // Cyan/Teal
        private static readonly Color ActiveColor = new Color(0f, 0.9f, 0.7f);   // Bright teal
        private static readonly Color InactiveColor = new Color(0.24f, 0.25f, 0.31f);
        private static readonly Color FilledBorderColor = new Color(0.31f, 0.35f, 0.43f);
        
        private UIDocument _uiDocument;
        private VisualElement _slotsContainer;
        private VisualElement _dropPrompt;
        private MaskInventory _inventory;
        
        // Cached slot elements
        private readonly VisualElement[] _slotElements = new VisualElement[MaskInventory.MaxSlots];
        private readonly VisualElement[] _slotFrames = new VisualElement[MaskInventory.MaxSlots];
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
            InputManager.OnInputDeviceChanged -= OnInputDeviceChanged;
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
            
            // Create a container for the HUD at the TOP LEFT of the screen
            var hudContainer = new VisualElement();
            hudContainer.name = "HUDContainer";
            hudContainer.AddToClassList("hud-container");
            hudContainer.style.position = Position.Absolute;
            hudContainer.style.top = 10;
            hudContainer.style.left = 10;
            hudContainer.style.flexDirection = FlexDirection.Row;
            hudContainer.style.alignItems = Align.FlexStart;
            
            // Slots container - main HUD panel (created FIRST, so it's on the left)
            _slotsContainer = new VisualElement();
            _slotsContainer.name = "SlotsContainer";
            _slotsContainer.AddToClassList("slots-container");
            _slotsContainer.style.flexDirection = FlexDirection.Row;
            _slotsContainer.style.justifyContent = Justify.Center;
            _slotsContainer.style.alignItems = Align.FlexStart;
            _slotsContainer.style.backgroundColor = new StyleColor(new Color(0.06f, 0.07f, 0.1f, 0.85f));
            _slotsContainer.style.paddingTop = 10;
            _slotsContainer.style.paddingBottom = 8;
            _slotsContainer.style.paddingLeft = 16;
            _slotsContainer.style.paddingRight = 16;
            _slotsContainer.style.borderTopLeftRadius = 12;
            _slotsContainer.style.borderTopRightRadius = 12;
            _slotsContainer.style.borderBottomLeftRadius = 12;
            _slotsContainer.style.borderBottomRightRadius = 12;
            _slotsContainer.style.borderTopWidth = 2;
            _slotsContainer.style.borderBottomWidth = 2;
            _slotsContainer.style.borderLeftWidth = 2;
            _slotsContainer.style.borderRightWidth = 2;
            var panelBorder = new Color(0f, 0.7f, 0.86f, 0.4f);
            _slotsContainer.style.borderTopColor = new StyleColor(panelBorder);
            _slotsContainer.style.borderBottomColor = new StyleColor(panelBorder);
            _slotsContainer.style.borderLeftColor = new StyleColor(panelBorder);
            _slotsContainer.style.borderRightColor = new StyleColor(panelBorder);
            hudContainer.Add(_slotsContainer);
            
            // Create contextual drop prompt (hidden by default)
            CreateDropPrompt(hudContainer);
            
            root.Add(hudContainer);
            
            CreateSlotElements();
            SubscribeToInventory();
            
            // Subscribe to input device changes
            InputManager.OnInputDeviceChanged += OnInputDeviceChanged;
        }
        
        private void CreateDropPrompt(VisualElement parent)
        {
            _dropPrompt = new VisualElement();
            _dropPrompt.name = "DropPrompt";
            _dropPrompt.AddToClassList("contextual-prompt");
            _dropPrompt.style.position = Position.Absolute;
            _dropPrompt.style.bottom = 220;
            _dropPrompt.style.flexDirection = FlexDirection.Row;
            _dropPrompt.style.alignItems = Align.Center;
            _dropPrompt.style.paddingTop = 8;
            _dropPrompt.style.paddingBottom = 8;
            _dropPrompt.style.paddingLeft = 14;
            _dropPrompt.style.paddingRight = 14;
            _dropPrompt.style.backgroundColor = new StyleColor(new Color(0.06f, 0.07f, 0.1f, 0.92f));
            _dropPrompt.style.borderTopLeftRadius = 10;
            _dropPrompt.style.borderTopRightRadius = 10;
            _dropPrompt.style.borderBottomLeftRadius = 10;
            _dropPrompt.style.borderBottomRightRadius = 10;
            _dropPrompt.style.borderTopWidth = 2;
            _dropPrompt.style.borderBottomWidth = 2;
            _dropPrompt.style.borderLeftWidth = 2;
            _dropPrompt.style.borderRightWidth = 2;
            var promptBorder = new Color(0f, 0.78f, 1f, 0.5f);
            _dropPrompt.style.borderTopColor = new StyleColor(promptBorder);
            _dropPrompt.style.borderBottomColor = new StyleColor(promptBorder);
            _dropPrompt.style.borderLeftColor = new StyleColor(promptBorder);
            _dropPrompt.style.borderRightColor = new StyleColor(promptBorder);
            _dropPrompt.style.display = DisplayStyle.None;
            
            // Create styled button using InputIconProvider (device-specific styling)
            var keyButton = InputIconProvider.CreateStyledButton("DropMask", 16f);
            keyButton.name = "DropKey";
            keyButton.style.marginRight = 10;
            _dropPrompt.Add(keyButton);
            
            // Action label
            var actionLabel = new Label("Drop Mask");
            actionLabel.name = "DropAction";
            actionLabel.style.fontSize = 14;
            actionLabel.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.9f, 0.95f));
            _dropPrompt.Add(actionLabel);
            
            parent.Add(_dropPrompt);
        }
        
        private void UpdateDropPromptKey()
        {
            if (_dropPrompt == null) return;
            var keyLabel = _dropPrompt.Q<Label>("DropKey");
            if (keyLabel != null)
            {
                // Update text and styling based on current device
                InputIconProvider.UpdateStyledButton(keyLabel, "DropMask");
            }
        }
        
        private void OnInputDeviceChanged(InputManager.InputDeviceType deviceType)
        {
            UpdateDropPromptKey();
            // Update slot labels if they show device-specific keys
            UpdateSlotLabels();
        }
        
        private void UpdateSlotLabels()
        {
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                if (_slotLabels[i] != null)
                {
                    string actionName = $"ToggleMask{i + 1}";
                    // Update text and device-specific styling
                    InputIconProvider.UpdateStyledButton(_slotLabels[i], actionName);
                }
            }
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
            slotContainer.AddToClassList("slot-container");
            slotContainer.style.flexDirection = FlexDirection.Column;
            slotContainer.style.alignItems = Align.Center;
            slotContainer.style.marginLeft = spacing;
            slotContainer.style.marginRight = spacing;
            
            // Slot background/frame
            var slotFrame = new VisualElement();
            slotFrame.name = $"SlotFrame_{index}";
            slotFrame.AddToClassList("slot-frame");
            slotFrame.style.width = slotSize;
            slotFrame.style.height = slotSize;
            slotFrame.style.borderTopLeftRadius = 10;
            slotFrame.style.borderTopRightRadius = 10;
            slotFrame.style.borderBottomLeftRadius = 10;
            slotFrame.style.borderBottomRightRadius = 10;
            slotFrame.style.borderTopWidth = 2;
            slotFrame.style.borderBottomWidth = 2;
            slotFrame.style.borderLeftWidth = 2;
            slotFrame.style.borderRightWidth = 2;
            var emptyBorder = new Color(0.24f, 0.25f, 0.31f, 0.9f);
            slotFrame.style.borderTopColor = new StyleColor(emptyBorder);
            slotFrame.style.borderBottomColor = new StyleColor(emptyBorder);
            slotFrame.style.borderLeftColor = new StyleColor(emptyBorder);
            slotFrame.style.borderRightColor = new StyleColor(emptyBorder);
            slotFrame.style.backgroundColor = new StyleColor(new Color(0.1f, 0.11f, 0.14f, 0.95f));
            _slotFrames[index] = slotFrame;
            
            // Mask icon inside the slot (uses texture with color tint)
            var colorBlock = new VisualElement();
            colorBlock.name = $"ColorBlock_{index}";
            colorBlock.AddToClassList("color-block");
            colorBlock.style.position = Position.Absolute;
            colorBlock.style.top = 4;
            colorBlock.style.left = 4;
            colorBlock.style.right = 4;
            colorBlock.style.bottom = 4;
            
            // Use mask texture if available
            if (maskTexture != null)
            {
                colorBlock.style.backgroundImage = new StyleBackground(maskTexture);
                colorBlock.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                colorBlock.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f, 0.4f)); // Empty slot tint
            }
            else
            {
                // Fallback to plain color block
                colorBlock.style.borderTopLeftRadius = 8;
                colorBlock.style.borderTopRightRadius = 8;
                colorBlock.style.borderBottomLeftRadius = 8;
                colorBlock.style.borderBottomRightRadius = 8;
                colorBlock.style.backgroundColor = new StyleColor(new Color(0.14f, 0.15f, 0.18f, 0.6f));
            }
            
            slotFrame.Add(colorBlock);
            _slotColorBlocks[index] = colorBlock;
            
            // Active indicator (glowing dot when mask is equipped)
            var activeIndicator = new VisualElement();
            activeIndicator.name = $"ActiveIndicator_{index}";
            activeIndicator.AddToClassList("active-indicator");
            activeIndicator.style.position = Position.Absolute;
            activeIndicator.style.top = -6;
            activeIndicator.style.right = -6;
            activeIndicator.style.width = 16;
            activeIndicator.style.height = 16;
            activeIndicator.style.borderTopLeftRadius = 8;
            activeIndicator.style.borderTopRightRadius = 8;
            activeIndicator.style.borderBottomLeftRadius = 8;
            activeIndicator.style.borderBottomRightRadius = 8;
            activeIndicator.style.backgroundColor = new StyleColor(ActiveColor);
            activeIndicator.style.borderTopWidth = 2;
            activeIndicator.style.borderBottomWidth = 2;
            activeIndicator.style.borderLeftWidth = 2;
            activeIndicator.style.borderRightWidth = 2;
            activeIndicator.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
            activeIndicator.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
            activeIndicator.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
            activeIndicator.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
            activeIndicator.style.display = DisplayStyle.None;
            slotFrame.Add(activeIndicator);
            _slotActiveIndicators[index] = activeIndicator;
            
            slotContainer.Add(slotFrame);
            
            // Hotkey label below the slot (styled like a mini keycap)
            string actionName = $"ToggleMask{index + 1}";
            var hotkeyLabel = new Label(InputIconProvider.GetBindingText(actionName));
            hotkeyLabel.name = $"HotkeyLabel_{index}";
            hotkeyLabel.AddToClassList("hotkey-label");
            hotkeyLabel.style.marginTop = 4;
            hotkeyLabel.style.paddingTop = 2;
            hotkeyLabel.style.paddingBottom = 2;
            hotkeyLabel.style.paddingLeft = 6;
            hotkeyLabel.style.paddingRight = 6;
            hotkeyLabel.style.fontSize = 11;
            hotkeyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            hotkeyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            hotkeyLabel.style.color = new StyleColor(new Color(0.7f, 0.75f, 0.82f, 0.9f));
            hotkeyLabel.style.backgroundColor = new StyleColor(new Color(0.16f, 0.18f, 0.22f, 0.7f));
            hotkeyLabel.style.borderTopLeftRadius = 6;
            hotkeyLabel.style.borderTopRightRadius = 6;
            hotkeyLabel.style.borderBottomLeftRadius = 6;
            hotkeyLabel.style.borderBottomRightRadius = 6;
            hotkeyLabel.style.borderTopWidth = 1;
            hotkeyLabel.style.borderBottomWidth = 1;
            hotkeyLabel.style.borderLeftWidth = 1;
            hotkeyLabel.style.borderRightWidth = 1;
            var labelBorder = new Color(0.27f, 0.31f, 0.39f, 0.6f);
            hotkeyLabel.style.borderTopColor = new StyleColor(labelBorder);
            hotkeyLabel.style.borderBottomColor = new StyleColor(labelBorder);
            hotkeyLabel.style.borderLeftColor = new StyleColor(labelBorder);
            hotkeyLabel.style.borderRightColor = new StyleColor(labelBorder);
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
        }

        private void RefreshAllSlots()
        {
            if (_inventory == null) return;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                RefreshSlot(i);
            }
            
            UpdateDropPromptVisibility();
        }
        
        private void UpdateDropPromptVisibility()
        {
            if (_dropPrompt == null || _inventory == null) return;
            
            // Find first filled slot and first active slot
            int firstFilledSlot = -1;
            int firstActiveSlot = -1;
            
            for (int i = 0; i < MaskInventory.MaxSlots; i++)
            {
                if (_inventory.GetSlot(i) != ColorType.None)
                {
                    if (firstFilledSlot < 0) firstFilledSlot = i;
                    
                    if (_inventory.IsSlotActive(i) && firstActiveSlot < 0)
                    {
                        firstActiveSlot = i;
                    }
                }
            }
            
            bool hasAnyMask = firstFilledSlot >= 0;
            
            if (hasAnyMask)
            {
                // Position drop prompt above the first active slot, or first filled slot if none active
                int targetSlot = firstActiveSlot >= 0 ? firstActiveSlot : firstFilledSlot;
                PositionDropPromptAboveSlot(targetSlot);
                _dropPrompt.style.display = DisplayStyle.Flex;
                
                // Update the drop prompt key styling
                UpdateDropPromptKey();
            }
            else
            {
                _dropPrompt.style.display = DisplayStyle.None;
            }
        }
        
        private void PositionDropPromptAboveSlot(int slotIndex)
        {
            if (_dropPrompt == null || slotIndex < 0 || slotIndex >= MaskInventory.MaxSlots) return;
            if (_slotElements[slotIndex] == null) return;
            
            // Calculate the horizontal offset for the target slot from center
            // Each slot occupies (slotSize + spacing*2) pixels
            float slotWidth = slotSize + spacing * 2;
            float centerSlotIndex = (MaskInventory.MaxSlots - 1) / 2f;
            float offsetFromCenter = (slotIndex - centerSlotIndex) * slotWidth;
            
            // Center the drop prompt horizontally, then offset to align with target slot
            _dropPrompt.style.left = Length.Percent(50);
            _dropPrompt.style.translate = new Translate(offsetFromCenter, 0);
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= MaskInventory.MaxSlots) return;
            
            var colorBlock = _slotColorBlocks[index];
            var slotFrame = _slotFrames[index];
            var activeIndicator = _slotActiveIndicators[index];
            if (colorBlock == null || slotFrame == null) return;
            
            ColorType maskInSlot = _inventory.GetSlot(index);
            bool isActive = _inventory.IsSlotActive(index);
            bool isEmpty = maskInSlot == ColorType.None;
            
            // Update color block appearance (use texture tint if texture is set, otherwise backgroundColor)
            if (maskTexture != null)
            {
                // Use texture with color tint
                if (isEmpty)
                {
                    colorBlock.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.3f, 0.3f, 0.35f, 0.3f)); // Dim empty slot
                }
                else
                {
                    Color maskColor = maskInSlot.ToColor();
                    colorBlock.style.unityBackgroundImageTintColor = new StyleColor(maskColor);
                }
            }
            else
            {
                // Fallback to backgroundColor
                if (isEmpty)
                {
                    colorBlock.style.backgroundColor = new StyleColor(new Color(0.14f, 0.15f, 0.18f, 0.6f));
                }
                else
                {
                    Color maskColor = maskInSlot.ToColor();
                    colorBlock.style.backgroundColor = new StyleColor(maskColor);
                }
            }
            
            // Update active indicator
            if (activeIndicator != null)
            {
                activeIndicator.style.display = isActive && !isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Update frame border for active state
            Color borderColor;
            if (isActive && !isEmpty)
            {
                // Active - bright teal glow
                borderColor = ActiveColor;
                slotFrame.style.backgroundColor = new StyleColor(new Color(0f, 0.31f, 0.24f, 0.3f));
            }
            else if (!isEmpty)
            {
                // Filled but inactive - subtle border
                borderColor = FilledBorderColor;
                slotFrame.style.backgroundColor = new StyleColor(new Color(0.1f, 0.11f, 0.14f, 0.95f));
            }
            else
            {
                // Empty - dark border
                borderColor = InactiveColor;
                slotFrame.style.backgroundColor = new StyleColor(new Color(0.1f, 0.11f, 0.14f, 0.95f));
            }
            
            slotFrame.style.borderTopColor = new StyleColor(borderColor);
            slotFrame.style.borderBottomColor = new StyleColor(borderColor);
            slotFrame.style.borderLeftColor = new StyleColor(borderColor);
            slotFrame.style.borderRightColor = new StyleColor(borderColor);
            
            // Update hotkey label styling
            if (_slotLabels[index] != null)
            {
                if (isActive && !isEmpty)
                {
                    _slotLabels[index].style.color = new StyleColor(ActiveColor);
                    _slotLabels[index].style.backgroundColor = new StyleColor(new Color(0f, 0.24f, 0.2f, 0.6f));
                    var activeLabelBorder = new Color(0f, 0.7f, 0.55f, 0.6f);
                    _slotLabels[index].style.borderTopColor = new StyleColor(activeLabelBorder);
                    _slotLabels[index].style.borderBottomColor = new StyleColor(activeLabelBorder);
                    _slotLabels[index].style.borderLeftColor = new StyleColor(activeLabelBorder);
                    _slotLabels[index].style.borderRightColor = new StyleColor(activeLabelBorder);
                }
                else if (!isEmpty)
                {
                    _slotLabels[index].style.color = new StyleColor(new Color(0.86f, 0.88f, 0.92f, 0.95f));
                    _slotLabels[index].style.backgroundColor = new StyleColor(new Color(0.16f, 0.18f, 0.22f, 0.7f));
                    var filledLabelBorder = new Color(0.27f, 0.31f, 0.39f, 0.6f);
                    _slotLabels[index].style.borderTopColor = new StyleColor(filledLabelBorder);
                    _slotLabels[index].style.borderBottomColor = new StyleColor(filledLabelBorder);
                    _slotLabels[index].style.borderLeftColor = new StyleColor(filledLabelBorder);
                    _slotLabels[index].style.borderRightColor = new StyleColor(filledLabelBorder);
                }
                else
                {
                    _slotLabels[index].style.color = new StyleColor(new Color(0.7f, 0.75f, 0.82f, 0.9f));
                    _slotLabels[index].style.backgroundColor = new StyleColor(new Color(0.16f, 0.18f, 0.22f, 0.7f));
                    var emptyLabelBorder = new Color(0.27f, 0.31f, 0.39f, 0.6f);
                    _slotLabels[index].style.borderTopColor = new StyleColor(emptyLabelBorder);
                    _slotLabels[index].style.borderBottomColor = new StyleColor(emptyLabelBorder);
                    _slotLabels[index].style.borderLeftColor = new StyleColor(emptyLabelBorder);
                    _slotLabels[index].style.borderRightColor = new StyleColor(emptyLabelBorder);
                }
            }
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

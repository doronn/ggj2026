using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using BreakingHue.Input;

namespace BreakingHue.UI
{
    /// <summary>
    /// Provides input icons for different control schemes (keyboard, Xbox, PlayStation).
    /// Returns appropriate text/icons based on the current input device.
    /// Supports visual styling per device type with optional sprite support.
    /// </summary>
    public class InputIconProvider : MonoBehaviour
    {
        [System.Serializable]
        public class ActionBinding
        {
            public string actionName;
            public string keyboardKey;
            public string xboxButton;
            public string playstationButton;
            public string genericGamepadButton;
            
            // Optional sprite overrides
            public Sprite keyboardSprite;
            public Sprite xboxSprite;
            public Sprite playstationSprite;
        }

        [Header("Icon Configuration (Optional)")]
        [SerializeField] private InputIconConfig iconConfig;

        [Header("Legacy Icon Sprites (Optional)")]
        [SerializeField] private Sprite[] keyboardSprites;
        [SerializeField] private Sprite[] xboxSprites;
        [SerializeField] private Sprite[] playstationSprites;

        // Default action bindings
        private static readonly Dictionary<string, ActionBinding> DefaultBindings = new Dictionary<string, ActionBinding>
        {
            { "Move", new ActionBinding { actionName = "Move", keyboardKey = "WASD", xboxButton = "L Stick", playstationButton = "L Stick", genericGamepadButton = "L Stick" } },
            { "Look", new ActionBinding { actionName = "Look", keyboardKey = "Mouse", xboxButton = "R Stick", playstationButton = "R Stick", genericGamepadButton = "R Stick" } },
            { "ToggleMask1", new ActionBinding { actionName = "Mask 1", keyboardKey = "1", xboxButton = "D-Pad ←", playstationButton = "D-Pad ←", genericGamepadButton = "D-Pad ←" } },
            { "ToggleMask2", new ActionBinding { actionName = "Mask 2", keyboardKey = "2", xboxButton = "D-Pad ↓", playstationButton = "D-Pad ↓", genericGamepadButton = "D-Pad ↓" } },
            { "ToggleMask3", new ActionBinding { actionName = "Mask 3", keyboardKey = "3", xboxButton = "D-Pad →", playstationButton = "D-Pad →", genericGamepadButton = "D-Pad →" } },
            { "DeactivateAll", new ActionBinding { actionName = "Deactivate All", keyboardKey = "0", xboxButton = "D-Pad ↑", playstationButton = "D-Pad ↑", genericGamepadButton = "D-Pad ↑" } },
            { "DropMask", new ActionBinding { actionName = "Drop Mask", keyboardKey = "Q", xboxButton = "X", playstationButton = "□", genericGamepadButton = "West" } },
            { "SelfDestruct", new ActionBinding { actionName = "Restart", keyboardKey = "Hold R", xboxButton = "Hold B", playstationButton = "Hold ○", genericGamepadButton = "Hold East" } },
            { "Pause", new ActionBinding { actionName = "Pause", keyboardKey = "Esc", xboxButton = "Menu", playstationButton = "Options", genericGamepadButton = "Start" } }
        };

        private static InputIconProvider _instance;
        public static InputIconProvider Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Gets the display text for an action based on current input device.
        /// </summary>
        public static string GetBindingText(string actionName)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            return GetBindingText(actionName, deviceType);
        }

        /// <summary>
        /// Gets the display text for an action for a specific device type.
        /// </summary>
        public static string GetBindingText(string actionName, InputManager.InputDeviceType deviceType)
        {
            if (!DefaultBindings.TryGetValue(actionName, out var binding))
            {
                return actionName;
            }

            return deviceType switch
            {
                InputManager.InputDeviceType.KeyboardMouse => binding.keyboardKey,
                InputManager.InputDeviceType.XboxController => binding.xboxButton,
                InputManager.InputDeviceType.PlayStationController => binding.playstationButton,
                InputManager.InputDeviceType.GenericGamepad => binding.genericGamepadButton,
                _ => binding.keyboardKey
            };
        }

        /// <summary>
        /// Gets the action display name.
        /// </summary>
        public static string GetActionDisplayName(string actionName)
        {
            if (DefaultBindings.TryGetValue(actionName, out var binding))
            {
                return binding.actionName;
            }
            return actionName;
        }

        /// <summary>
        /// Gets all available action names.
        /// </summary>
        public static IEnumerable<string> GetAllActionNames()
        {
            return DefaultBindings.Keys;
        }

        /// <summary>
        /// Creates a formatted control hint string (e.g., "[Q] Drop Mask").
        /// </summary>
        public static string GetControlHint(string actionName)
        {
            string binding = GetBindingText(actionName);
            string displayName = GetActionDisplayName(actionName);
            return $"[{binding}] {displayName}";
        }

        /// <summary>
        /// Creates a VisualElement representing a control hint.
        /// </summary>
        public static VisualElement CreateControlHintElement(string actionName)
        {
            var container = new VisualElement();
            container.AddToClassList("control-hint");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var keyLabel = new Label(GetBindingText(actionName));
            keyLabel.AddToClassList("control-key");
            
            var actionLabel = new Label(GetActionDisplayName(actionName));
            actionLabel.AddToClassList("control-action");

            container.Add(keyLabel);
            container.Add(actionLabel);

            return container;
        }

        /// <summary>
        /// Updates an existing control hint element with current device bindings.
        /// </summary>
        public static void UpdateControlHintElement(VisualElement container, string actionName)
        {
            var keyLabel = container.Q<Label>(className: "control-key");
            if (keyLabel != null)
            {
                keyLabel.text = GetBindingText(actionName);
            }
        }

        /// <summary>
        /// Gets the color associated with controller type (for styling).
        /// </summary>
        public static Color GetDeviceColor(InputManager.InputDeviceType deviceType)
        {
            return deviceType switch
            {
                InputManager.InputDeviceType.XboxController => new Color(0.1f, 0.7f, 0.1f), // Xbox green
                InputManager.InputDeviceType.PlayStationController => new Color(0.0f, 0.4f, 0.9f), // PlayStation blue
                _ => Color.white
            };
        }

        /// <summary>
        /// Checks if the current device is a gamepad.
        /// </summary>
        public static bool IsGamepad()
        {
            return InputManager.Instance?.IsGamepad ?? false;
        }

        /// <summary>
        /// Gets a summary of all controls for display.
        /// </summary>
        public static string GetControlsSummary()
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            
            return deviceType switch
            {
                InputManager.InputDeviceType.KeyboardMouse => 
                    "WASD - Move | Mouse - Look | 1/2/3 - Masks | Q - Drop | Hold R - Restart",
                InputManager.InputDeviceType.XboxController => 
                    "L Stick - Move | R Stick - Look | D-Pad - Masks | X - Drop | Hold B - Restart",
                InputManager.InputDeviceType.PlayStationController => 
                    "L Stick - Move | R Stick - Look | D-Pad - Masks | □ - Drop | Hold ○ - Restart",
                _ => 
                    "L Stick - Move | R Stick - Look | D-Pad - Masks | West - Drop | Hold East - Restart"
            };
        }

        /// <summary>
        /// Creates a styled button prompt element with device-specific appearance.
        /// </summary>
        public static VisualElement CreateButtonPrompt(string actionName, string actionText = null, float fontSize = 16f)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            return CreateButtonPrompt(actionName, deviceType, actionText, fontSize);
        }

        /// <summary>
        /// Creates a styled button prompt element for a specific device type.
        /// </summary>
        public static VisualElement CreateButtonPrompt(string actionName, InputManager.InputDeviceType deviceType, string actionText = null, float fontSize = 16f)
        {
            var container = new VisualElement();
            container.AddToClassList("button-prompt");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Button/Key element
            var buttonElement = CreateStyledButton(actionName, deviceType, fontSize);
            container.Add(buttonElement);

            // Action text (optional)
            if (!string.IsNullOrEmpty(actionText))
            {
                var actionLabel = new Label(actionText);
                actionLabel.AddToClassList("prompt-action-text");
                actionLabel.style.marginLeft = 10;
                actionLabel.style.fontSize = fontSize;
                actionLabel.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.9f, 0.95f));
                container.Add(actionLabel);
            }

            return container;
        }

        /// <summary>
        /// Creates a styled button element (the key/button itself, not the full prompt).
        /// </summary>
        public static VisualElement CreateStyledButton(string actionName, float fontSize = 16f)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            return CreateStyledButton(actionName, deviceType, fontSize);
        }

        /// <summary>
        /// Creates a styled button element for a specific device type.
        /// </summary>
        public static VisualElement CreateStyledButton(string actionName, InputManager.InputDeviceType deviceType, float fontSize = 16f)
        {
            var buttonText = GetBindingText(actionName, deviceType);
            var (bgColor, borderColor, textColor, isRound) = GetDeviceButtonStyle(deviceType);

            var button = new Label(buttonText);
            button.name = $"Button_{actionName}";
            button.AddToClassList("styled-button");
            button.AddToClassList(GetDeviceClass(deviceType));

            // Base styling
            float padding = fontSize * 0.5f;
            float minWidth = fontSize * 2f;
            float borderRadius = isRound ? fontSize * 1.5f : fontSize * 0.4f;

            button.style.paddingTop = padding;
            button.style.paddingBottom = padding;
            button.style.paddingLeft = padding * 1.2f;
            button.style.paddingRight = padding * 1.2f;
            button.style.minWidth = minWidth;
            button.style.fontSize = fontSize;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = new StyleColor(textColor);
            button.style.backgroundColor = new StyleColor(bgColor);

            // Border styling (keycap effect)
            button.style.borderTopLeftRadius = borderRadius;
            button.style.borderTopRightRadius = borderRadius;
            button.style.borderBottomLeftRadius = borderRadius;
            button.style.borderBottomRightRadius = borderRadius;
            button.style.borderTopWidth = 2;
            button.style.borderBottomWidth = 4; // Thicker bottom for 3D keycap effect
            button.style.borderLeftWidth = 2;
            button.style.borderRightWidth = 2;
            button.style.borderTopColor = new StyleColor(borderColor * 1.2f);
            button.style.borderBottomColor = new StyleColor(borderColor * 0.8f);
            button.style.borderLeftColor = new StyleColor(borderColor);
            button.style.borderRightColor = new StyleColor(borderColor);

            return button;
        }

        /// <summary>
        /// Updates a styled button element with current device bindings.
        /// </summary>
        public static void UpdateStyledButton(VisualElement button, string actionName)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            UpdateStyledButton(button, actionName, deviceType);
        }

        /// <summary>
        /// Updates a styled button element for a specific device type.
        /// </summary>
        public static void UpdateStyledButton(VisualElement button, string actionName, InputManager.InputDeviceType deviceType)
        {
            if (button is Label label)
            {
                label.text = GetBindingText(actionName, deviceType);
            }

            var (bgColor, borderColor, textColor, isRound) = GetDeviceButtonStyle(deviceType);

            // Remove old device classes
            button.RemoveFromClassList("device-keyboard");
            button.RemoveFromClassList("device-xbox");
            button.RemoveFromClassList("device-playstation");
            button.RemoveFromClassList("device-gamepad");

            // Add new device class
            button.AddToClassList(GetDeviceClass(deviceType));

            // Update colors
            button.style.backgroundColor = new StyleColor(bgColor);
            button.style.color = new StyleColor(textColor);
            button.style.borderTopColor = new StyleColor(borderColor * 1.2f);
            button.style.borderBottomColor = new StyleColor(borderColor * 0.8f);
            button.style.borderLeftColor = new StyleColor(borderColor);
            button.style.borderRightColor = new StyleColor(borderColor);
        }

        /// <summary>
        /// Gets button styling for a device type.
        /// Returns: (backgroundColor, borderColor, textColor, isRound)
        /// </summary>
        public static (Color bgColor, Color borderColor, Color textColor, bool isRound) GetDeviceButtonStyle(InputManager.InputDeviceType deviceType)
        {
            return deviceType switch
            {
                InputManager.InputDeviceType.XboxController => (
                    new Color(0.1f, 0.5f, 0.1f, 0.95f),   // Xbox green background
                    new Color(0.05f, 0.35f, 0.05f, 1f),   // Darker green border
                    Color.white,
                    true // Xbox buttons are more round
                ),
                InputManager.InputDeviceType.PlayStationController => (
                    new Color(0.15f, 0.25f, 0.55f, 0.95f), // PlayStation blue background
                    new Color(0.1f, 0.15f, 0.4f, 1f),      // Darker blue border
                    Color.white,
                    true // PlayStation buttons are round
                ),
                InputManager.InputDeviceType.GenericGamepad => (
                    new Color(0.35f, 0.35f, 0.4f, 0.95f),  // Neutral gray
                    new Color(0.25f, 0.25f, 0.3f, 1f),
                    Color.white,
                    true
                ),
                _ => ( // Keyboard/Mouse
                    new Color(0.2f, 0.22f, 0.27f, 0.95f),  // Dark gray keycap
                    new Color(0.12f, 0.14f, 0.18f, 1f),    // Darker border
                    Color.white,
                    false // Keyboard keys are more rectangular
                )
            };
        }

        /// <summary>
        /// Gets the CSS class name for a device type.
        /// </summary>
        public static string GetDeviceClass(InputManager.InputDeviceType deviceType)
        {
            return deviceType switch
            {
                InputManager.InputDeviceType.XboxController => "device-xbox",
                InputManager.InputDeviceType.PlayStationController => "device-playstation",
                InputManager.InputDeviceType.GenericGamepad => "device-gamepad",
                _ => "device-keyboard"
            };
        }

        /// <summary>
        /// Gets the sprite for an action if available, otherwise null.
        /// </summary>
        public static Sprite GetButtonSprite(string actionName)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            return GetButtonSprite(actionName, deviceType);
        }

        /// <summary>
        /// Gets the sprite for an action and device type if available, otherwise null.
        /// </summary>
        public static Sprite GetButtonSprite(string actionName, InputManager.InputDeviceType deviceType)
        {
            // If we have an instance with icon config, try to get sprite from there
            if (_instance != null && _instance.iconConfig != null)
            {
                return _instance.iconConfig.GetSprite(actionName, deviceType);
            }
            return null;
        }

        /// <summary>
        /// Creates a button element that uses a sprite if available, otherwise text.
        /// </summary>
        public static VisualElement CreateButtonWithSprite(string actionName, float size = 32f)
        {
            var deviceType = InputManager.Instance?.CurrentDeviceType ?? InputManager.InputDeviceType.KeyboardMouse;
            var sprite = GetButtonSprite(actionName, deviceType);

            if (sprite != null)
            {
                // Create image element with sprite
                var image = new VisualElement();
                image.name = $"ButtonSprite_{actionName}";
                image.AddToClassList("button-sprite");
                image.AddToClassList(GetDeviceClass(deviceType));
                image.style.width = size;
                image.style.height = size;
                image.style.backgroundImage = new StyleBackground(sprite);
                image.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                return image;
            }
            else
            {
                // Fall back to styled text button
                return CreateStyledButton(actionName, deviceType, size * 0.5f);
            }
        }
    }

    /// <summary>
    /// ScriptableObject for configuring input icons.
    /// Create via Assets > Create > Breaking Hue > Input Icon Config
    /// </summary>
    [CreateAssetMenu(fileName = "InputIconConfig", menuName = "Breaking Hue/Input Icon Config")]
    public class InputIconConfig : ScriptableObject
    {
        [System.Serializable]
        public class ButtonIconSet
        {
            public string actionName;
            public Sprite keyboardSprite;
            public Sprite xboxSprite;
            public Sprite playstationSprite;
            public Sprite genericSprite;
        }

        [SerializeField] private List<ButtonIconSet> buttonIcons = new List<ButtonIconSet>();

        // Common button sprites (for quick setup)
        [Header("Xbox Common Buttons")]
        public Sprite xboxA;
        public Sprite xboxB;
        public Sprite xboxX;
        public Sprite xboxY;
        public Sprite xboxLB;
        public Sprite xboxRB;
        public Sprite xboxLT;
        public Sprite xboxRT;
        public Sprite xboxMenu;
        public Sprite xboxView;
        public Sprite xboxDPad;

        [Header("PlayStation Common Buttons")]
        public Sprite psCross;
        public Sprite psCircle;
        public Sprite psSquare;
        public Sprite psTriangle;
        public Sprite psL1;
        public Sprite psR1;
        public Sprite psL2;
        public Sprite psR2;
        public Sprite psOptions;
        public Sprite psShare;
        public Sprite psDPad;

        public Sprite GetSprite(string actionName, InputManager.InputDeviceType deviceType)
        {
            var iconSet = buttonIcons.Find(b => b.actionName == actionName);
            if (iconSet == null) return null;

            return deviceType switch
            {
                InputManager.InputDeviceType.XboxController => iconSet.xboxSprite,
                InputManager.InputDeviceType.PlayStationController => iconSet.playstationSprite,
                InputManager.InputDeviceType.GenericGamepad => iconSet.genericSprite ?? iconSet.xboxSprite,
                _ => iconSet.keyboardSprite
            };
        }
    }
}

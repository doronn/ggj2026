using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using BreakingHue.Input;

namespace BreakingHue.UI
{
    /// <summary>
    /// Provides input icons for different control schemes (keyboard, Xbox, PlayStation).
    /// Returns appropriate text/icons based on the current input device.
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
        }

        [Header("Icon Sprites (Optional)")]
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
    }
}

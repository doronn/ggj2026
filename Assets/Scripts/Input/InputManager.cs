using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace BreakingHue.Input
{
    /// <summary>
    /// Manages input state and auto-detects the active input device.
    /// Provides events for device switching so UI can update accordingly.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public enum InputDeviceType
        {
            KeyboardMouse,
            XboxController,
            PlayStationController,
            GenericGamepad
        }

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        
        [Header("Settings")]
        [SerializeField] private float deviceSwitchCooldown = 0.1f;

        private InputDeviceType _currentDeviceType = InputDeviceType.KeyboardMouse;
        private float _lastDeviceSwitchTime;

        // Singleton instance
        private static InputManager _instance;
        public static InputManager Instance => _instance;

        /// <summary>
        /// Event fired when the active input device changes.
        /// </summary>
        public static event Action<InputDeviceType> OnInputDeviceChanged;

        /// <summary>
        /// The currently active input device type.
        /// </summary>
        public InputDeviceType CurrentDeviceType => _currentDeviceType;

        /// <summary>
        /// Whether the current device is a keyboard/mouse.
        /// </summary>
        public bool IsKeyboardMouse => _currentDeviceType == InputDeviceType.KeyboardMouse;

        /// <summary>
        /// Whether the current device is any type of gamepad.
        /// </summary>
        public bool IsGamepad => _currentDeviceType != InputDeviceType.KeyboardMouse;

        /// <summary>
        /// The input action asset used by the game.
        /// </summary>
        public InputActionAsset InputActions => inputActions;

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Enable all action maps
            if (inputActions != null)
            {
                inputActions.Enable();
            }

            // Subscribe to input system events
            InputSystem.onActionChange += OnActionChange;
            InputUser.onChange += OnInputUserChange;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            InputSystem.onActionChange -= OnActionChange;
            InputUser.onChange -= OnInputUserChange;
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed)
                return;

            if (obj is InputAction action && action.activeControl != null)
            {
                DetectDeviceFromControl(action.activeControl);
            }
        }

        private void OnInputUserChange(InputUser user, InputUserChange change, InputDevice device)
        {
            if (change == InputUserChange.ControlSchemeChanged || 
                change == InputUserChange.DevicePaired)
            {
                if (device != null)
                {
                    DetectDeviceType(device);
                }
            }
        }

        private void DetectDeviceFromControl(InputControl control)
        {
            if (control?.device == null)
                return;

            DetectDeviceType(control.device);
        }

        private void DetectDeviceType(InputDevice device)
        {
            // Cooldown to prevent rapid switching
            if (Time.unscaledTime - _lastDeviceSwitchTime < deviceSwitchCooldown)
                return;

            InputDeviceType newDeviceType = DetermineDeviceType(device);

            if (newDeviceType != _currentDeviceType)
            {
                _currentDeviceType = newDeviceType;
                _lastDeviceSwitchTime = Time.unscaledTime;
                
                Debug.Log($"[InputManager] Device switched to: {_currentDeviceType}");
                OnInputDeviceChanged?.Invoke(_currentDeviceType);
            }
        }

        private InputDeviceType DetermineDeviceType(InputDevice device)
        {
            if (device is Keyboard || device is Mouse)
            {
                return InputDeviceType.KeyboardMouse;
            }

            if (device is Gamepad gamepad)
            {
                // Check device name/description for specific controller types
                string deviceName = device.name.ToLowerInvariant();
                string deviceDesc = device.description.product?.ToLowerInvariant() ?? "";

                // Xbox controllers
                if (deviceName.Contains("xbox") || 
                    deviceDesc.Contains("xbox") ||
                    deviceName.Contains("xinput"))
                {
                    return InputDeviceType.XboxController;
                }

                // PlayStation controllers
                if (deviceName.Contains("playstation") || 
                    deviceName.Contains("dualshock") || 
                    deviceName.Contains("dualsense") ||
                    deviceDesc.Contains("playstation") ||
                    deviceDesc.Contains("dualshock") ||
                    deviceDesc.Contains("dualsense"))
                {
                    return InputDeviceType.PlayStationController;
                }

                // Generic gamepad
                return InputDeviceType.GenericGamepad;
            }

            // Default to keyboard/mouse for unknown devices
            return InputDeviceType.KeyboardMouse;
        }

        /// <summary>
        /// Gets the Player action map.
        /// </summary>
        public InputActionMap GetPlayerActionMap()
        {
            return inputActions?.FindActionMap("Player");
        }

        /// <summary>
        /// Gets a specific action by name from the Player action map.
        /// </summary>
        public InputAction GetPlayerAction(string actionName)
        {
            return GetPlayerActionMap()?.FindAction(actionName);
        }

        /// <summary>
        /// Gets the UI action map.
        /// </summary>
        public InputActionMap GetUIActionMap()
        {
            return inputActions?.FindActionMap("UI");
        }

        /// <summary>
        /// Enables the Player action map and disables UI.
        /// </summary>
        public void EnablePlayerInput()
        {
            GetPlayerActionMap()?.Enable();
            GetUIActionMap()?.Disable();
        }

        /// <summary>
        /// Enables the UI action map and disables Player.
        /// </summary>
        public void EnableUIInput()
        {
            GetPlayerActionMap()?.Disable();
            GetUIActionMap()?.Enable();
        }

        /// <summary>
        /// Enables both Player and UI action maps.
        /// </summary>
        public void EnableAllInput()
        {
            GetPlayerActionMap()?.Enable();
            GetUIActionMap()?.Enable();
        }

        /// <summary>
        /// Disables all input action maps.
        /// </summary>
        public void DisableAllInput()
        {
            GetPlayerActionMap()?.Disable();
            GetUIActionMap()?.Disable();
        }

        /// <summary>
        /// Force a device type (useful for testing).
        /// </summary>
        public void ForceDeviceType(InputDeviceType deviceType)
        {
            if (_currentDeviceType != deviceType)
            {
                _currentDeviceType = deviceType;
                OnInputDeviceChanged?.Invoke(_currentDeviceType);
            }
        }

        /// <summary>
        /// Gets a human-readable name for the current device type.
        /// </summary>
        public string GetDeviceDisplayName()
        {
            return _currentDeviceType switch
            {
                InputDeviceType.KeyboardMouse => "Keyboard & Mouse",
                InputDeviceType.XboxController => "Xbox Controller",
                InputDeviceType.PlayStationController => "PlayStation Controller",
                InputDeviceType.GenericGamepad => "Gamepad",
                _ => "Unknown"
            };
        }
    }
}

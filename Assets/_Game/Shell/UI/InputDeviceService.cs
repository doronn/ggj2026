using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ggj2026.Game.Shell.UI
{
    public enum InputDeviceKind
    {
        KeyboardMouse,
        XboxController,
        PlayStationController,
        GenericGamepad
    }

    public sealed class InputDeviceService : MonoBehaviour
    {
        public static InputDeviceService Active { get; private set; }
        public InputDeviceKind CurrentDevice { get; private set; } = InputDeviceKind.KeyboardMouse;
        public bool IsGamepad => CurrentDevice != InputDeviceKind.KeyboardMouse;

        public event Action<InputDeviceKind> DeviceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Active != null) return;
            var go = new GameObject("InputDeviceService");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Active = go.AddComponent<InputDeviceService>();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            InputDeviceKind detected = Detect();
            if (detected == CurrentDevice) return;
            CurrentDevice = detected;
            DeviceChanged?.Invoke(CurrentDevice);
        }

        private static InputDeviceKind Detect()
        {
            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
            {
                string name = Gamepad.current.name.ToLowerInvariant();
                if (name.Contains("dualsense") || name.Contains("dualshock"))
                    return InputDeviceKind.PlayStationController;
                if (name.Contains("xbox"))
                    return InputDeviceKind.XboxController;
                return InputDeviceKind.GenericGamepad;
            }

            return InputDeviceKind.KeyboardMouse;
        }
    }
}

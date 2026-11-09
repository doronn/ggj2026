using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Shell.UI
{
    public static class KeycapStyleFactory
    {
        public static VisualElement CreateHint(string keyText, string actionText, InputDeviceKind device = InputDeviceKind.KeyboardMouse)
        {
            var container = new VisualElement();
            container.AddToClassList("control-hint");
            container.pickingMode = PickingMode.Ignore;

            var keycap = CreateKeycap(keyText, device);
            var label = new Label(actionText);
            label.AddToClassList("control-hint__action");
            label.pickingMode = PickingMode.Ignore;

            container.Add(keycap);
            container.Add(label);
            return container;
        }

        public static Label CreateKeycap(string text, InputDeviceKind device = InputDeviceKind.KeyboardMouse)
        {
            var keycap = new Label(text);
            keycap.AddToClassList("keycap");
            keycap.pickingMode = PickingMode.Ignore;

            switch (device)
            {
                case InputDeviceKind.XboxController:
                    keycap.AddToClassList("keycap--xbox");
                    break;
                case InputDeviceKind.PlayStationController:
                    keycap.AddToClassList("keycap--playstation");
                    break;
                case InputDeviceKind.GenericGamepad:
                    keycap.AddToClassList("keycap--gamepad");
                    break;
            }

            return keycap;
        }

        public static string GetKeyText(string actionId, InputDeviceKind device)
        {
            return device switch
            {
                InputDeviceKind.XboxController => GetXboxText(actionId),
                InputDeviceKind.PlayStationController => GetPlayStationText(actionId),
                InputDeviceKind.GenericGamepad => GetXboxText(actionId),
                _ => GetKeyboardText(actionId)
            };
        }

        private static string GetKeyboardText(string actionId)
        {
            return actionId switch
            {
                "ToggleSim" => "Space",
                "SpeedUp" => "+",
                "SlowDown" => "-",
                "Pause" => "Esc",
                "CycleTab" => "Tab",
                "FloorTool" => "1",
                "EdgeTool" => "2",
                "ModuleTool" => "3",
                "LandmarkTool" => "4",
                "ClearCellTool" => "5",
                "ClearEdgeTool" => "6",
                "CancelTool" => "Esc",
                _ => actionId
            };
        }

        private static string GetXboxText(string actionId)
        {
            return actionId switch
            {
                "ToggleSim" => "A",
                "SpeedUp" => "RB",
                "SlowDown" => "LB",
                "Pause" => "Menu",
                "CycleTab" => "Y",
                _ => actionId
            };
        }

        private static string GetPlayStationText(string actionId)
        {
            return actionId switch
            {
                "ToggleSim" => "X",
                "SpeedUp" => "R1",
                "SlowDown" => "L1",
                "Pause" => "Options",
                "CycleTab" => "Triangle",
                _ => actionId
            };
        }
    }
}

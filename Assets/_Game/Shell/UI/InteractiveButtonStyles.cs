using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Shell.UI
{
    public static class InteractiveButtonStyles
    {
        private static readonly Color CyanBorder = new Color(0f, 1f, 1f, 1f);

        public static void Apply(Button button, Color baseBackground)
        {
            button.focusable = true;
            button.pickingMode = PickingMode.Position;

            Color originalBg = baseBackground;

            button.RegisterCallback<FocusInEvent>(_ =>
            {
                button.style.borderTopWidth = 4;
                button.style.borderBottomWidth = 4;
                button.style.borderLeftWidth = 4;
                button.style.borderRightWidth = 4;
                button.style.borderTopColor = CyanBorder;
                button.style.borderBottomColor = CyanBorder;
                button.style.borderLeftColor = CyanBorder;
                button.style.borderRightColor = CyanBorder;
                button.style.scale = new Scale(new Vector3(1.08f, 1.08f, 1f));
                button.style.backgroundColor = Color.white;
                button.style.color = Color.black;
            });

            button.RegisterCallback<FocusOutEvent>(_ =>
            {
                button.style.borderTopWidth = 0;
                button.style.borderBottomWidth = 0;
                button.style.borderLeftWidth = 0;
                button.style.borderRightWidth = 0;
                button.style.scale = new Scale(Vector3.one);
                button.style.backgroundColor = originalBg;
                button.style.color = Color.white;
            });

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (button.focusController?.focusedElement == button) return;
                button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));
                button.style.backgroundColor = new Color(
                    Mathf.Min(1f, originalBg.r + 0.10f),
                    Mathf.Min(1f, originalBg.g + 0.10f),
                    Mathf.Min(1f, originalBg.b + 0.10f),
                    originalBg.a);
            });

            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (button.focusController?.focusedElement == button) return;
                button.style.scale = new Scale(Vector3.one);
                button.style.backgroundColor = originalBg;
            });
        }
    }
}

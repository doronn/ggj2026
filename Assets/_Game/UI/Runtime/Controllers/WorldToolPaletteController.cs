using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ggj2026.Game.UI.Runtime.Controllers
{
    public sealed class WorldToolPaletteController
    {
        public void Populate<T>(VisualElement container, IReadOnlyList<T> items, Func<T, string> labelSelector, Action<T> onClick)
        {
            if (container == null)
            {
                return;
            }

            container.Clear();
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                var button = new Button(() => onClick(item)) { text = labelSelector(item) };
                container.Add(button);
            }
        }
    }
}

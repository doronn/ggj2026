using System.Collections.Generic;
using Ggj2026.Game.Shared.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Shell.UI
{
    public sealed class RuntimeInputCaptureService : MonoBehaviour, IInputCaptureGate
    {
        private readonly List<VisualElement> _registeredElements = new List<VisualElement>();
        private bool _runtimePointerOverUi;
        private bool _runtimePointerPressedFromUi;
        private bool _testPointerOverUi;
        private bool _testPointerPressedFromUi;

        public static RuntimeInputCaptureService Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Active != null)
            {
                return;
            }

            var gameObject = new GameObject("RuntimeInputCaptureService");
            DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Active = gameObject.AddComponent<RuntimeInputCaptureService>();
        }

        public bool IsWorldInputBlocked()
        {
            return _runtimePointerOverUi || _runtimePointerPressedFromUi || _testPointerOverUi || _testPointerPressedFromUi;
        }

        public void RegisterBlockingElement(VisualElement element)
        {
            if (element == null || _registeredElements.Contains(element))
            {
                return;
            }

            _registeredElements.Add(element);
        }

        public void ClearRegisteredElements()
        {
            _registeredElements.Clear();
            _runtimePointerOverUi = false;
            _runtimePointerPressedFromUi = false;
            _testPointerOverUi = false;
            _testPointerPressedFromUi = false;
        }

        private void OnDestroy()
        {
            ClearRegisteredElements();
            if (Active == this)
            {
                Active = null;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _runtimePointerOverUi = false;
                _runtimePointerPressedFromUi = false;
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            _runtimePointerOverUi = IsPointerOverRegisteredUi(pointerPosition);

            bool pressedThisFrame =
                mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame;
            if (pressedThisFrame && _runtimePointerOverUi)
            {
                _runtimePointerPressedFromUi = true;
            }

            bool anyButtonPressed =
                mouse.leftButton.isPressed ||
                mouse.rightButton.isPressed ||
                mouse.middleButton.isPressed;
            if (!anyButtonPressed)
            {
                _runtimePointerPressedFromUi = false;
            }
        }

        public void NotifyPointerEnter()
        {
            _testPointerOverUi = true;
        }

        public void NotifyPointerLeave()
        {
            _testPointerOverUi = false;
        }

        public void NotifyPointerDown()
        {
            _testPointerPressedFromUi = true;
        }

        public void NotifyPointerUp()
        {
            _testPointerPressedFromUi = false;
        }

        private bool IsPointerOverRegisteredUi(Vector2 pointerPosition)
        {
            for (int i = _registeredElements.Count - 1; i >= 0; i--)
            {
                VisualElement element = _registeredElements[i];
                if (element == null || element.panel == null)
                {
                    continue;
                }

                if (element.resolvedStyle.display == DisplayStyle.None || !element.visible)
                {
                    continue;
                }

                if (element.worldBound.Contains(pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

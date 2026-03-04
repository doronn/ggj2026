using UnityEngine;
using UnityEngine.InputSystem;
using Ggj2026.Game.Shell.UI;

namespace Ggj2026.Game.Shell.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class StrategicCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 28f;
        [SerializeField] private float edgePanThickness = 12f;
        [SerializeField] private bool useScreenEdgePan = true;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 200f;
        [SerializeField] private float minHeight = 18f;
        [SerializeField] private float maxHeight = 120f;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private bool allowQERotation = true;

        private void Awake()
        {
            if (TryGetComponent(out UnityEngine.Camera sceneCamera))
            {
                sceneCamera.orthographic = false;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (RuntimeInputCaptureService.Active != null && RuntimeInputCaptureService.Active.IsWorldInputBlocked())
            {
                return;
            }

            HandlePan();
            HandleZoom();
            HandleRotation();
        }

        private void HandlePan()
        {
            Vector3 pan = Vector3.zero;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan += Vector3.forward;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan += Vector3.back;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan += Vector3.left;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan += Vector3.right;
            }

            if (useScreenEdgePan && mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (pos.x <= edgePanThickness) pan += Vector3.left;
                if (pos.x >= Screen.width - edgePanThickness) pan += Vector3.right;
                if (pos.y <= edgePanThickness) pan += Vector3.back;
                if (pos.y >= Screen.height - edgePanThickness) pan += Vector3.forward;
            }

            if (pan.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 planarForward = transform.forward;
            planarForward.y = 0f;
            planarForward.Normalize();

            Vector3 planarRight = transform.right;
            planarRight.y = 0f;
            planarRight.Normalize();

            Vector3 move = ((planarForward * pan.z) + (planarRight * pan.x)).normalized;
            transform.position += move * (panSpeed * Time.deltaTime);
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < Mathf.Epsilon)
            {
                return;
            }

            Vector3 position = transform.position;
            position.y = Mathf.Clamp(position.y - (scroll * zoomSpeed * 0.01f * Time.deltaTime), minHeight, maxHeight);
            transform.position = position;
        }

        private void HandleRotation()
        {
            if (!allowQERotation || Keyboard.current == null)
            {
                return;
            }

            float direction = 0f;
            if (Keyboard.current.qKey.isPressed) direction -= 1f;
            if (Keyboard.current.eKey.isPressed) direction += 1f;

            if (Mathf.Abs(direction) > 0f)
            {
                transform.Rotate(Vector3.up, direction * rotationSpeed * Time.deltaTime, Space.World);
            }
        }
    }
}

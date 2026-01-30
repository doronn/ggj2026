using UnityEngine;
using UnityEngine.InputSystem;
using BreakingHue.Input;

namespace BreakingHue.Camera
{
    /// <summary>
    /// Third-person camera controller that follows the player and rotates around Y-axis.
    /// Supports both perspective and orthographic modes.
    /// Handles smooth follow, rotation input from mouse/gamepad, and optional letterboxing.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class GameCamera : MonoBehaviour
    {
        [Header("Camera Mode")]
        [SerializeField] private bool useThirdPerson = true;
        
        [Header("Third Person Settings")]
        [SerializeField] private float distance = 12f;
        [SerializeField] private float heightAngle = 60f; // degrees from horizontal (0 = behind, 90 = top-down)
        [SerializeField] private float rotationSpeed = 120f; // degrees per second for gamepad
        [SerializeField] private float mouseSensitivity = 0.3f;
        [SerializeField] private float followSmoothTime = 0.1f;
        
        [Header("Target")]
        [SerializeField] private Transform target;
        
        [Header("Fixed Camera Settings (Legacy)")]
        [SerializeField] private float gridSize = 32f;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float padding = 1f;
        [SerializeField] private float cameraHeight = 30f;
        
        [Header("Letterboxing")]
        [SerializeField] private Color letterboxColor = Color.black;
        [SerializeField] private bool useLetterboxing = true;

        private UnityEngine.Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private GameObject[] _letterboxBars;
        
        // Third person state
        private float _currentYRotation;
        private Vector3 _currentVelocity;
        private Vector3 _targetPosition;
        
        // Input
        private InputAction _lookAction;
        private Vector2 _lookInput;

        /// <summary>
        /// The current Y rotation of the camera around the target.
        /// </summary>
        public float CurrentYRotation => _currentYRotation;
        
        /// <summary>
        /// The camera's forward direction projected onto the XZ plane.
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                return forward.normalized;
            }
        }
        
        /// <summary>
        /// The camera's right direction projected onto the XZ plane.
        /// </summary>
        public Vector3 Right
        {
            get
            {
                Vector3 right = transform.right;
                right.y = 0;
                return right.normalized;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            
            if (useThirdPerson)
            {
                // Configure as perspective
                _camera.orthographic = false;
                _camera.fieldOfView = 60f;
            }
            else
            {
                // Configure as orthographic (legacy fixed camera)
                _camera.orthographic = true;
                transform.position = new Vector3(0, cameraHeight, 0);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            
            UpdateCamera();
        }

        private void Start()
        {
            if (useLetterboxing && !useThirdPerson)
            {
                CreateLetterboxBars();
            }
            
            // Find player if no target assigned
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
            
            // Setup input
            SetupInput();
            
            // Initialize camera position
            if (useThirdPerson && target != null)
            {
                _targetPosition = target.position;
                UpdateThirdPersonPosition(true);
            }
            else
            {
                UpdateCamera();
            }
        }

        private void SetupInput()
        {
            // Try to get Look action from InputManager
            if (InputManager.Instance != null)
            {
                _lookAction = InputManager.Instance.GetPlayerAction("Look");
                
                // Ensure the action is enabled
                if (_lookAction != null)
                {
                    _lookAction.Enable();
                }
            }
            
            // Fallback: create manual input action
            if (_lookAction == null)
            {
                _lookAction = new InputAction("Look", InputActionType.Value, binding: "<Mouse>/delta");
                _lookAction.AddBinding("<Gamepad>/rightStick").WithProcessors("StickDeadzone,ScaleVector2(x=3,y=3)");
                _lookAction.Enable();
            }
        }

        private void OnEnable()
        {
            _lookAction?.Enable();
        }

        private void OnDisable()
        {
            _lookAction?.Disable();
        }

        private void Update()
        {
            if (useThirdPerson)
            {
                UpdateThirdPersonCamera();
            }
            else
            {
                // Check for screen size changes (fixed camera mode)
                if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
                {
                    UpdateCamera();
                    _lastScreenWidth = Screen.width;
                    _lastScreenHeight = Screen.height;
                }
            }
        }

        private void LateUpdate()
        {
            if (useThirdPerson)
            {
                // Keep searching for player if target is null (player spawns after camera)
                if (target == null)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        target = player.transform;
                        _targetPosition = target.position;
                    }
                }
                
                if (target != null)
                {
                    UpdateThirdPersonPosition(false);
                }
            }
        }

        private void UpdateThirdPersonCamera()
        {
            // Read look input
            if (_lookAction != null)
            {
                _lookInput = _lookAction.ReadValue<Vector2>();
            }
            
            // Apply rotation based on input device
            bool isGamepad = InputManager.Instance != null && InputManager.Instance.IsGamepad;
            
            if (isGamepad)
            {
                // Gamepad: continuous rotation
                _currentYRotation += _lookInput.x * rotationSpeed * Time.deltaTime;
            }
            else
            {
                // Mouse: direct delta application
                _currentYRotation += _lookInput.x * mouseSensitivity;
            }
            
            // Wrap rotation
            if (_currentYRotation > 360f) _currentYRotation -= 360f;
            if (_currentYRotation < 0f) _currentYRotation += 360f;
        }

        private void UpdateThirdPersonPosition(bool instant)
        {
            if (target == null) return;
            
            // Smooth follow target position
            if (instant)
            {
                _targetPosition = target.position;
            }
            else
            {
                _targetPosition = Vector3.SmoothDamp(
                    _targetPosition, 
                    target.position, 
                    ref _currentVelocity, 
                    followSmoothTime
                );
            }
            
            // Calculate camera position based on angle and rotation
            float heightAngleRad = heightAngle * Mathf.Deg2Rad;
            float horizontalDistance = distance * Mathf.Cos(heightAngleRad);
            float verticalDistance = distance * Mathf.Sin(heightAngleRad);
            
            // Calculate offset based on Y rotation
            float yRotRad = _currentYRotation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                -Mathf.Sin(yRotRad) * horizontalDistance,
                verticalDistance,
                -Mathf.Cos(yRotRad) * horizontalDistance
            );
            
            // Apply position
            transform.position = _targetPosition + offset;
            
            // Look at target
            transform.LookAt(_targetPosition);
        }

        private void UpdateCamera()
        {
            if (useThirdPerson) return;
            
            // Legacy fixed camera logic
            float worldWidth = gridSize * cellSize + padding * 2;
            float worldHeight = gridSize * cellSize + padding * 2;
            
            float screenAspect = (float)Screen.width / Screen.height;
            float targetAspect = worldWidth / worldHeight;
            
            if (useLetterboxing)
            {
                if (screenAspect > targetAspect)
                {
                    float viewportWidth = targetAspect / screenAspect;
                    float viewportX = (1f - viewportWidth) / 2f;
                    _camera.rect = new Rect(viewportX, 0, viewportWidth, 1);
                }
                else
                {
                    float viewportHeight = screenAspect / targetAspect;
                    float viewportY = (1f - viewportHeight) / 2f;
                    _camera.rect = new Rect(0, viewportY, 1, viewportHeight);
                }
                
                _camera.orthographicSize = worldHeight / 2f;
            }
            else
            {
                _camera.rect = new Rect(0, 0, 1, 1);
                
                if (screenAspect > targetAspect)
                {
                    _camera.orthographicSize = worldHeight / 2f;
                }
                else
                {
                    _camera.orthographicSize = worldWidth / (2f * screenAspect);
                }
            }
            
            UpdateLetterboxBars();
        }

        private void CreateLetterboxBars()
        {
            GameObject letterboxContainer = new GameObject("LetterboxBars");
            letterboxContainer.transform.SetParent(transform);
            
            _letterboxBars = new GameObject[4];
            
            string[] names = { "LeftBar", "RightBar", "TopBar", "BottomBar" };
            for (int i = 0; i < 4; i++)
            {
                _letterboxBars[i] = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _letterboxBars[i].name = names[i];
                _letterboxBars[i].transform.SetParent(letterboxContainer.transform);
                
                var collider = _letterboxBars[i].GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                
                var renderer = _letterboxBars[i].GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = letterboxColor;
                
                _letterboxBars[i].SetActive(false);
            }
        }

        private void UpdateLetterboxBars()
        {
            if (_letterboxBars == null || !useLetterboxing) return;
            
            foreach (var bar in _letterboxBars)
            {
                if (bar != null) bar.SetActive(false);
            }
            
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = letterboxColor;
        }

        /// <summary>
        /// Sets the follow target for third-person mode.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null && useThirdPerson)
            {
                _targetPosition = target.position;
                UpdateThirdPersonPosition(true);
            }
        }

        /// <summary>
        /// Sets the camera rotation directly (in degrees).
        /// </summary>
        public void SetYRotation(float rotation)
        {
            _currentYRotation = rotation;
            if (useThirdPerson && target != null)
            {
                UpdateThirdPersonPosition(true);
            }
        }

        /// <summary>
        /// Converts a screen position to world position on the game plane.
        /// </summary>
        public Vector3 ScreenToWorldPosition(Vector2 screenPosition)
        {
            Vector3 viewportPos = _camera.ScreenToViewportPoint(screenPosition);
            Ray ray = _camera.ViewportPointToRay(viewportPos);
            Plane gamePlane = new Plane(Vector3.up, Vector3.zero);
            
            if (gamePlane.Raycast(ray, out float rayDistance))
            {
                return ray.GetPoint(rayDistance);
            }
            
            return Vector3.zero;
        }

        /// <summary>
        /// Gets the world bounds of the visible area.
        /// </summary>
        public Bounds GetVisibleBounds()
        {
            float worldSize = gridSize * cellSize + padding * 2;
            return new Bounds(Vector3.zero, new Vector3(worldSize, 1f, worldSize));
        }

        /// <summary>
        /// Sets the grid size and updates the camera (legacy mode only).
        /// </summary>
        public void SetGridSize(float size)
        {
            gridSize = size;
            UpdateCamera();
        }

        /// <summary>
        /// Sets the letterbox color.
        /// </summary>
        public void SetLetterboxColor(Color color)
        {
            letterboxColor = color;
            _camera.backgroundColor = color;
            
            if (_letterboxBars != null)
            {
                foreach (var bar in _letterboxBars)
                {
                    if (bar != null)
                    {
                        var renderer = bar.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material.color = color;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enables or disables third-person mode.
        /// </summary>
        public void SetThirdPersonMode(bool enabled)
        {
            if (useThirdPerson == enabled) return;
            
            useThirdPerson = enabled;
            
            if (useThirdPerson)
            {
                _camera.orthographic = false;
                _camera.fieldOfView = 60f;
                _camera.rect = new Rect(0, 0, 1, 1);
                
                if (target != null)
                {
                    _targetPosition = target.position;
                    UpdateThirdPersonPosition(true);
                }
            }
            else
            {
                _camera.orthographic = true;
                transform.position = new Vector3(0, cameraHeight, 0);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                UpdateCamera();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();
            
            // Clamp values
            distance = Mathf.Max(1f, distance);
            heightAngle = Mathf.Clamp(heightAngle, 10f, 89f);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
            followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
            
            if (_camera != null && Application.isPlaying && useThirdPerson && target != null)
            {
                UpdateThirdPersonPosition(true);
            }
        }

        private void OnDrawGizmos()
        {
            if (useThirdPerson)
            {
                // Draw camera orbit
                if (target != null)
                {
                    Gizmos.color = Color.cyan;
                    float heightAngleRad = heightAngle * Mathf.Deg2Rad;
                    float horizontalDistance = distance * Mathf.Cos(heightAngleRad);
                    
                    // Draw orbit circle
                    int segments = 32;
                    Vector3 prevPoint = Vector3.zero;
                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
                        Vector3 point = target.position + new Vector3(
                            Mathf.Sin(angle) * horizontalDistance,
                            distance * Mathf.Sin(heightAngleRad),
                            Mathf.Cos(angle) * horizontalDistance
                        );
                        
                        if (i > 0)
                        {
                            Gizmos.DrawLine(prevPoint, point);
                        }
                        prevPoint = point;
                    }
                    
                    // Draw line to target
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, target.position);
                }
            }
            else
            {
                // Draw grid bounds
                float worldSize = gridSize * cellSize;
                
                Gizmos.color = Color.green;
                Vector3 center = Vector3.zero;
                Vector3 size = new Vector3(worldSize, 0.1f, worldSize);
                Gizmos.DrawWireCube(center, size);
                
                Gizmos.color = Color.yellow;
                float paddedSize = worldSize + padding * 2;
                Gizmos.DrawWireCube(center, new Vector3(paddedSize, 0.1f, paddedSize));
            }
        }
#endif
    }
}

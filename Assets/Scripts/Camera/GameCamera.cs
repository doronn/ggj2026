using UnityEngine;

namespace BreakingHue.Camera
{
    /// <summary>
    /// Fixed orthographic camera controller for the 32x32 grid.
    /// Handles letterboxing/pillarboxing for different aspect ratios.
    /// Ensures the entire level is always visible on screen.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class GameCamera : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private float gridSize = 32f;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float padding = 1f; // Extra space around the grid
        
        [Header("Camera Position")]
        [SerializeField] private float cameraHeight = 30f;
        
        [Header("Letterboxing")]
        [SerializeField] private Color letterboxColor = Color.black;
        [SerializeField] private bool useLetterboxing = true;

        private UnityEngine.Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private GameObject[] _letterboxBars;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            
            // Configure as orthographic
            _camera.orthographic = true;
            
            // Position camera to look down at the center of the grid
            float gridCenter = gridSize * cellSize * 0.5f;
            transform.position = new Vector3(0, cameraHeight, 0);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            
            UpdateCamera();
        }

        private void Start()
        {
            if (useLetterboxing)
            {
                CreateLetterboxBars();
            }
            
            UpdateCamera();
        }

        private void Update()
        {
            // Check for screen size changes
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                UpdateCamera();
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
            }
        }

        private void UpdateCamera()
        {
            // Calculate the total world size we need to display
            float worldWidth = gridSize * cellSize + padding * 2;
            float worldHeight = gridSize * cellSize + padding * 2;
            
            // Get screen aspect ratio
            float screenAspect = (float)Screen.width / Screen.height;
            float targetAspect = worldWidth / worldHeight; // Should be 1:1 for a square grid
            
            if (useLetterboxing)
            {
                // Calculate viewport rect for letterboxing
                if (screenAspect > targetAspect)
                {
                    // Screen is wider than target - add pillarbox (black bars on sides)
                    float viewportWidth = targetAspect / screenAspect;
                    float viewportX = (1f - viewportWidth) / 2f;
                    _camera.rect = new Rect(viewportX, 0, viewportWidth, 1);
                }
                else
                {
                    // Screen is taller than target - add letterbox (black bars top/bottom)
                    float viewportHeight = screenAspect / targetAspect;
                    float viewportY = (1f - viewportHeight) / 2f;
                    _camera.rect = new Rect(0, viewportY, 1, viewportHeight);
                }
                
                // Set orthographic size to fit the grid exactly
                _camera.orthographicSize = worldHeight / 2f;
            }
            else
            {
                // No letterboxing - fit to screen
                _camera.rect = new Rect(0, 0, 1, 1);
                
                if (screenAspect > targetAspect)
                {
                    // Wider screen - height limited
                    _camera.orthographicSize = worldHeight / 2f;
                }
                else
                {
                    // Taller screen - width limited
                    _camera.orthographicSize = worldWidth / (2f * screenAspect);
                }
            }
            
            UpdateLetterboxBars();
        }

        private void CreateLetterboxBars()
        {
            // Create a separate camera for letterbox rendering
            GameObject letterboxContainer = new GameObject("LetterboxBars");
            letterboxContainer.transform.SetParent(transform);
            
            // Create 4 quads for potential letterbox bars
            _letterboxBars = new GameObject[4]; // Left, Right, Top, Bottom
            
            string[] names = { "LeftBar", "RightBar", "TopBar", "BottomBar" };
            for (int i = 0; i < 4; i++)
            {
                _letterboxBars[i] = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _letterboxBars[i].name = names[i];
                _letterboxBars[i].transform.SetParent(letterboxContainer.transform);
                
                // Remove collider
                var collider = _letterboxBars[i].GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                
                // Set material
                var renderer = _letterboxBars[i].GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = letterboxColor;
                
                // Initially hide
                _letterboxBars[i].SetActive(false);
            }
        }

        private void UpdateLetterboxBars()
        {
            if (_letterboxBars == null || !useLetterboxing) return;
            
            float screenAspect = (float)Screen.width / Screen.height;
            float worldHeight = gridSize * cellSize + padding * 2;
            float worldWidth = worldHeight; // Square grid
            float targetAspect = worldWidth / worldHeight;
            
            // Hide all bars first
            foreach (var bar in _letterboxBars)
            {
                if (bar != null) bar.SetActive(false);
            }
            
            // Note: With viewport rect approach, we don't need actual 3D bars
            // The camera rect handles letterboxing automatically
            // Unity fills the uncovered area with the background clear color
            
            // Ensure camera clear flags and background color are set
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = letterboxColor;
        }

        /// <summary>
        /// Converts a screen position to world position on the game plane.
        /// </summary>
        public Vector3 ScreenToWorldPosition(Vector2 screenPosition)
        {
            // Adjust for viewport
            Vector3 viewportPos = _camera.ScreenToViewportPoint(screenPosition);
            
            // Convert viewport to world
            Ray ray = _camera.ViewportPointToRay(viewportPos);
            Plane gamePlane = new Plane(Vector3.up, Vector3.zero);
            
            if (gamePlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
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
        /// Sets the grid size and updates the camera.
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();
            
            if (_camera != null && Application.isPlaying)
            {
                UpdateCamera();
            }
        }

        private void OnDrawGizmos()
        {
            // Draw grid bounds
            float worldSize = gridSize * cellSize;
            
            Gizmos.color = Color.green;
            Vector3 center = Vector3.zero;
            Vector3 size = new Vector3(worldSize, 0.1f, worldSize);
            Gizmos.DrawWireCube(center, size);
            
            // Draw padding bounds
            Gizmos.color = Color.yellow;
            float paddedSize = worldSize + padding * 2;
            Gizmos.DrawWireCube(center, new Vector3(paddedSize, 0.1f, paddedSize));
        }
#endif
    }
}

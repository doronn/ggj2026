using UnityEngine;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Gameplay;

namespace BreakingHue.Level
{
    /// <summary>
    /// Generates a level from a 16x16 Texture2D using pixel mapping.
    /// 
    /// Pixel Protocol:
    /// - Alpha > 0.9: SOLID objects
    ///   - White (1,1,1): Wall
    ///   - Grey (~0.5,0.5,0.5): Player Spawn
    ///   - RGB colors: Barrier/Door (color determines required mask)
    /// 
    /// - Alpha ~ 0.5 (0.4-0.6): PICKUP objects
    ///   - RGB determines the mask color type
    /// 
    /// - Alpha < 0.4: Empty space
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Level Data")]
        [SerializeField] private Texture2D levelMap;
        [SerializeField] private float cellSize = 1f;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject barrierPrefab;
        [SerializeField] private GameObject pickupPrefab;

        [Header("Parent Containers")]
        [SerializeField] private Transform levelContainer;

        [Header("Barrier Setup")]
        [Tooltip("If true, barriers will auto-configure dual colliders for phasing")]
        [SerializeField] private bool autoConfigureBarrierColliders = true;

        private DiContainer _container;
        private GameObject _spawnedPlayer;
        private Vector3 _levelOffset;

        [Inject]
        public void Construct(DiContainer container)
        {
            _container = container;
        }

        private void Awake()
        {
            if (levelContainer == null)
            {
                levelContainer = new GameObject("Level").transform;
            }
        }

        /// <summary>
        /// Generates the level from the assigned texture.
        /// Call this from a scene controller or automatically in Start().
        /// </summary>
        public void GenerateLevel()
        {
            if (levelMap == null)
            {
                Debug.LogError("[LevelGenerator] No level map assigned!");
                return;
            }

            ClearLevel();
            
            int width = levelMap.width;
            int height = levelMap.height;
            
            // Center the level
            _levelOffset = new Vector3(-width * cellSize * 0.5f, 0, -height * cellSize * 0.5f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = levelMap.GetPixel(x, y);
                    ProcessPixel(x, y, pixel);
                }
            }
            
            Debug.Log($"[LevelGenerator] Level generated: {width}x{height}");
        }

        private void ProcessPixel(int x, int y, Color pixel)
        {
            Vector3 worldPos = GetWorldPosition(x, y);

            // Solid objects (alpha > 0.9)
            if (pixel.a > 0.9f)
            {
                ProcessSolidPixel(worldPos, pixel);
            }
            // Pickup objects (alpha ~ 0.5)
            else if (pixel.a > 0.4f && pixel.a < 0.6f)
            {
                ProcessPickupPixel(worldPos, pixel);
            }
            // Alpha < 0.4 = empty space, do nothing
        }

        private void ProcessSolidPixel(Vector3 position, Color pixel)
        {
            // Check for grey (player spawn): RGB values all similar and around 0.5
            if (IsGrey(pixel))
            {
                SpawnPlayer(position);
                return;
            }

            // Check for white (wall): RGB values all > 0.9
            if (IsWhite(pixel))
            {
                SpawnWall(position);
                return;
            }

            // Otherwise it's a colored barrier/door
            ColorType barrierColor = ColorTypeExtensions.FromColor(pixel);
            if (barrierColor != ColorType.None)
            {
                SpawnBarrier(position, barrierColor, pixel);
            }
        }

        private void ProcessPickupPixel(Vector3 position, Color pixel)
        {
            ColorType pickupColor = ColorTypeExtensions.FromColor(pixel);
            if (pickupColor != ColorType.None)
            {
                SpawnPickup(position, pickupColor, pixel);
            }
        }

        private void SpawnWall(Vector3 position)
        {
            if (wallPrefab == null) return;
            
            GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, levelContainer);
            wall.name = $"Wall_{position.x:F0}_{position.z:F0}";
        }

        private void SpawnPlayer(Vector3 position)
        {
            if (playerPrefab == null) return;
            
            // Use Zenject to spawn player so dependencies are injected
            _spawnedPlayer = _container.InstantiatePrefab(playerPrefab, position, Quaternion.identity, null);
            _spawnedPlayer.name = "Player";
        }

        private void SpawnBarrier(Vector3 position, ColorType colorType, Color visualColor)
        {
            if (barrierPrefab == null) return;

            GameObject barrier = _container.InstantiatePrefab(barrierPrefab, position, Quaternion.identity, levelContainer);
            barrier.name = $"Door_{colorType.GetDisplayName()}";
            
            // Configure the barrier
            var barrierComponent = barrier.GetComponent<ColorBarrier>();
            if (barrierComponent != null)
            {
                barrierComponent.Initialize(colorType);
            }

            // Set visual color
            SetObjectColor(barrier, visualColor);
        }

        private void SpawnPickup(Vector3 position, ColorType colorType, Color visualColor)
        {
            if (pickupPrefab == null) return;

            GameObject pickup = _container.InstantiatePrefab(pickupPrefab, position, Quaternion.identity, levelContainer);
            pickup.name = $"Mask_{colorType.GetDisplayName()}";
            
            // Configure the pickup
            var pickupComponent = pickup.GetComponent<MaskPickup>();
            if (pickupComponent != null)
            {
                pickupComponent.Initialize(colorType);
            }

            // Set visual color
            SetObjectColor(pickup, visualColor);
        }

        private void SetObjectColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Create material instance to avoid shared material issues
                Material mat = renderer.material;
                mat.color = color;
                
                // Set emissive color for URP glow effect
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 2f);
                }
            }
        }

        private Vector3 GetWorldPosition(int x, int y)
        {
            return _levelOffset + new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f);
        }

        private bool IsWhite(Color color)
        {
            return color.r > 0.9f && color.g > 0.9f && color.b > 0.9f;
        }

        private bool IsGrey(Color color)
        {
            float avg = (color.r + color.g + color.b) / 3f;
            float variance = Mathf.Abs(color.r - avg) + Mathf.Abs(color.g - avg) + Mathf.Abs(color.b - avg);
            return avg > 0.3f && avg < 0.7f && variance < 0.1f;
        }

        /// <summary>
        /// Clears all generated level objects.
        /// </summary>
        public void ClearLevel()
        {
            if (_spawnedPlayer != null)
            {
                Destroy(_spawnedPlayer);
                _spawnedPlayer = null;
            }

            if (levelContainer != null)
            {
                for (int i = levelContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(levelContainer.GetChild(i).gameObject);
                }
            }
        }

        /// <summary>
        /// Gets the spawned player GameObject.
        /// </summary>
        public GameObject SpawnedPlayer => _spawnedPlayer;

#if UNITY_EDITOR
        [ContextMenu("Generate Level")]
        private void EditorGenerateLevel()
        {
            GenerateLevel();
        }

        [ContextMenu("Clear Level")]
        private void EditorClearLevel()
        {
            ClearLevel();
        }
#endif
    }
}

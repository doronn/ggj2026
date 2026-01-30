#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BreakingHue.Core;
using BreakingHue.Level.Data;
using BreakingHue.Gameplay.Bot;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Generates example levels demonstrating all game features.
    /// Access via Window > Breaking Hue > Generate Example Levels
    /// </summary>
    public class ExampleLevelGenerator : EditorWindow
    {
        private const string LevelsPath = "Assets/Levels";

        [MenuItem("Window/Breaking Hue/Generate Example Levels")]
        public static void ShowWindow()
        {
            var window = GetWindow<ExampleLevelGenerator>("Example Levels");
            window.minSize = new Vector2(300, 200);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Example Level Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Generate example levels that demonstrate all game features:\n" +
                "• Tutorial: Basic mechanics\n" +
                "• Advanced: All features combined",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate Tutorial Level", GUILayout.Height(30)))
            {
                GenerateTutorialLevel();
            }

            if (GUILayout.Button("Generate Advanced Level", GUILayout.Height(30)))
            {
                GenerateAdvancedLevel();
            }

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate All Example Levels", GUILayout.Height(40)))
            {
                GenerateTutorialLevel();
                GenerateAdvancedLevel();
                EditorUtility.DisplayDialog("Complete", "Example levels generated successfully!", "OK");
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateFolderIfNeeded(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                var folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private void GenerateTutorialLevel()
        {
            CreateFolderIfNeeded(LevelsPath);

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelName = "Level 1 - Basics";
            level.levelId = Guid.NewGuid().ToString();
            level.levelIndex = 0;

            InitializeLayers(level);

            // Create a 12x12 playable area
            int size = 12;
            
            // Floor
            for (int x = 1; x <= size; x++)
            {
                for (int y = 1; y <= size; y++)
                {
                    level.groundLayer.floorTiles.Add(new Vector2Int(x, y));
                }
            }

            // Border walls
            for (int i = 0; i <= size + 1; i++)
            {
                level.wallLayer.wallTiles.Add(new Vector2Int(i, 0));
                level.wallLayer.wallTiles.Add(new Vector2Int(i, size + 1));
                level.wallLayer.wallTiles.Add(new Vector2Int(0, i));
                level.wallLayer.wallTiles.Add(new Vector2Int(size + 1, i));
            }

            // Player spawn
            level.portalLayer.playerSpawnPosition = new Vector2Int(2, 2);

            // === SECTION 1: Basic mask pickup and barrier ===
            // Red mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(4, 2),
                color = ColorType.Red,
                pickupId = Guid.NewGuid().ToString()
            });

            // Red barrier
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(6, 2),
                color = ColorType.Red
            });

            // === SECTION 2: Color combination ===
            // Yellow mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(8, 2),
                color = ColorType.Yellow,
                pickupId = Guid.NewGuid().ToString()
            });

            // Orange barrier (needs Red + Yellow combined)
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(10, 2),
                color = ColorType.Orange
            });

            // === Wall divider ===
            for (int y = 4; y <= 8; y++)
            {
                level.wallLayer.wallTiles.Add(new Vector2Int(6, y));
            }

            // === SECTION 3: Upper area with Blue ===
            // Blue mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(3, 6),
                color = ColorType.Blue,
                pickupId = Guid.NewGuid().ToString()
            });

            // Blue barrier
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(3, 9),
                color = ColorType.Blue
            });

            // Exit portal (checkpoint)
            level.portalLayer.portals.Add(new PortalData
            {
                portalId = Guid.NewGuid().ToString(),
                position = new Vector2Int(10, 10),
                isCheckpoint = true
            });

            SaveLevel(level, "Level01_Basics.asset");
            Debug.Log("[ExampleLevelGenerator] Tutorial level created");
        }

        private void GenerateAdvancedLevel()
        {
            CreateFolderIfNeeded(LevelsPath);

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelName = "Level 2 - Advanced";
            level.levelId = Guid.NewGuid().ToString();
            level.levelIndex = 1;

            InitializeLayers(level);

            // Create a 20x20 playable area
            int size = 20;
            
            // Floor
            for (int x = 1; x <= size; x++)
            {
                for (int y = 1; y <= size; y++)
                {
                    level.groundLayer.floorTiles.Add(new Vector2Int(x, y));
                }
            }

            // Border walls
            for (int i = 0; i <= size + 1; i++)
            {
                level.wallLayer.wallTiles.Add(new Vector2Int(i, 0));
                level.wallLayer.wallTiles.Add(new Vector2Int(i, size + 1));
                level.wallLayer.wallTiles.Add(new Vector2Int(0, i));
                level.wallLayer.wallTiles.Add(new Vector2Int(size + 1, i));
            }

            // Player spawn
            level.portalLayer.playerSpawnPosition = new Vector2Int(2, 2);

            // === MASKS ===
            // Red mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(4, 3),
                color = ColorType.Red,
                pickupId = Guid.NewGuid().ToString()
            });

            // Yellow mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(6, 3),
                color = ColorType.Yellow,
                pickupId = Guid.NewGuid().ToString()
            });

            // Blue mask
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(8, 3),
                color = ColorType.Blue,
                pickupId = Guid.NewGuid().ToString()
            });

            // Purple mask (combined) in hidden area
            level.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(18, 3),
                color = ColorType.Purple,
                pickupId = Guid.NewGuid().ToString()
            });

            // === BARRIERS ===
            // Red barrier
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(10, 5),
                color = ColorType.Red
            });

            // Yellow barrier
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(10, 8),
                color = ColorType.Yellow
            });

            // Blue barrier
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(10, 11),
                color = ColorType.Blue
            });

            // Black barrier (needs all 3 primaries)
            level.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(15, 15),
                color = ColorType.Black
            });

            // === EXPLODING BARRELS ===
            // Red barrel - player must avoid or use bot
            level.barrelLayer.barrels.Add(new BarrelData
            {
                position = new Vector2Int(12, 7),
                color = ColorType.Red,
                barrelId = Guid.NewGuid().ToString()
            });

            // Yellow barrel
            level.barrelLayer.barrels.Add(new BarrelData
            {
                position = new Vector2Int(14, 10),
                color = ColorType.Yellow,
                barrelId = Guid.NewGuid().ToString()
            });

            // === BOT ===
            // Bot with Red initial color that patrols
            var botPath = new List<Vector2Int>
            {
                new Vector2Int(3, 10),
                new Vector2Int(3, 15),
                new Vector2Int(8, 15),
                new Vector2Int(8, 10)
            };

            level.botLayer.bots.Add(new BotData
            {
                botId = Guid.NewGuid().ToString(),
                startPosition = new Vector2Int(3, 10),
                initialColor = ColorType.Red,
                inlineWaypoints = botPath,
                pathMode = PathMode.Loop
            });

            // === HIDDEN AREA ===
            // Hidden blocks covering secret area
            for (int x = 16; x <= 19; x++)
            {
                for (int y = 2; y <= 4; y++)
                {
                    level.hiddenAreaLayer.hiddenBlocks.Add(new HiddenBlockData
                    {
                        position = new Vector2Int(x, y),
                        blockId = Guid.NewGuid().ToString()
                    });
                }
            }

            // === INTERNAL WALLS ===
            // Wall maze elements
            for (int y = 5; y <= 12; y++)
            {
                level.wallLayer.wallTiles.Add(new Vector2Int(9, y));
            }

            for (int x = 13; x <= 18; x++)
            {
                level.wallLayer.wallTiles.Add(new Vector2Int(x, 14));
            }

            // === PORTALS ===
            // Checkpoint portal
            level.portalLayer.portals.Add(new PortalData
            {
                portalId = Guid.NewGuid().ToString(),
                position = new Vector2Int(5, 10),
                isCheckpoint = true
            });

            // Exit portal (behind Black barrier)
            level.portalLayer.portals.Add(new PortalData
            {
                portalId = Guid.NewGuid().ToString(),
                position = new Vector2Int(18, 18),
                isCheckpoint = true
            });

            SaveLevel(level, "Level02_Advanced.asset");
            Debug.Log("[ExampleLevelGenerator] Advanced level created");
        }

        private void InitializeLayers(LevelData level)
        {
            level.groundLayer = new GroundLayer { floorTiles = new List<Vector2Int>() };
            level.wallLayer = new WallLayer { wallTiles = new List<Vector2Int>() };
            level.barrierLayer = new BarrierLayer { barriers = new List<BarrierData>() };
            level.pickupLayer = new PickupLayer { pickups = new List<PickupData>() };
            level.barrelLayer = new BarrelLayer { barrels = new List<BarrelData>() };
            level.botLayer = new BotLayer { bots = new List<BotData>() };
            level.portalLayer = new PortalLayer { portals = new List<PortalData>() };
            level.hiddenAreaLayer = new HiddenAreaLayer { hiddenBlocks = new List<HiddenBlockData>() };
        }

        private void SaveLevel(LevelData level, string filename)
        {
            string path = $"{LevelsPath}/{filename}";
            
            // Delete existing if present
            if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BreakingHue.Core;
using BreakingHue.Gameplay;
using BreakingHue.Gameplay.Bot;
using BreakingHue.Installers;
using BreakingHue.Level;
using BreakingHue.Level.Data;
using BreakingHue.Save;
using Zenject;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Editor window for automatically connecting all game assets.
    /// Handles prefab assignment, level configuration, and scene setup.
    /// </summary>
    public class AutoConnectWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Config";
        private const string ConfigAssetPath = "Assets/Config/GameConfig.asset";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string LevelsPath = "Assets/Levels";
        private const string PortalLinksPath = "Assets/Levels/Links";
        private const string WorldScenePath = "Assets/Scenes/World.unity";

        private GameConfig _config;
        private Vector2 _scrollPosition;
        private List<ValidationResult> _validationResults = new List<ValidationResult>();
        private bool _showValidation = true;
        private bool _showPrefabs = true;
        private bool _showLevels = true;

        private struct ValidationResult
        {
            public string category;
            public string message;
            public bool isValid;
            public System.Action fixAction;
        }

        [MenuItem("Window/Breaking Hue/Auto Connect")]
        public static void ShowWindow()
        {
            var window = GetWindow<AutoConnectWindow>("Auto Connect");
            window.minSize = new Vector2(400, 500);
            window.RefreshConfig();
        }

        private void OnEnable()
        {
            RefreshConfig();
        }

        private void RefreshConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            RunValidation();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Breaking Hue - Auto Connect", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Config section
            DrawConfigSection();
            EditorGUILayout.Space(10);

            // Auto-connect buttons
            DrawAutoConnectSection();
            EditorGUILayout.Space(10);

            // Validation section
            DrawValidationSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Game Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (_config == null)
            {
                EditorGUILayout.HelpBox("No GameConfig found. Click 'Create Config' to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Config"))
                {
                    CreateGameConfig();
                }
            }
            else
            {
                EditorGUILayout.ObjectField("Config Asset", _config, typeof(GameConfig), false);
                
                if (GUILayout.Button("Select Config Asset"))
                {
                    Selection.activeObject = _config;
                    EditorGUIUtility.PingObject(_config);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAutoConnectSection()
        {
            EditorGUILayout.LabelField("Auto-Connect Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // Main auto-connect button
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Auto-Connect All", GUILayout.Height(40)))
            {
                AutoConnectAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // Individual buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Populate Prefabs"))
            {
                PopulatePrefabs();
            }
            if (GUILayout.Button("Populate Levels"))
            {
                PopulateLevels();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Populate Portal Links"))
            {
                PopulatePortalLinks();
            }
            if (GUILayout.Button("Fix World Scene"))
            {
                FixWorldScene();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Refresh Validation"))
            {
                RunValidation();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection()
        {
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation Results", true);
            if (!_showValidation) return;

            EditorGUILayout.BeginVertical("box");

            if (_validationResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Refresh Validation' to check configuration.", MessageType.Info);
            }
            else
            {
                int validCount = _validationResults.Count(r => r.isValid);
                int totalCount = _validationResults.Count;
                
                var statusColor = validCount == totalCount ? Color.green : Color.yellow;
                var oldColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField($"Status: {validCount}/{totalCount} checks passed");
                GUI.color = oldColor;

                EditorGUILayout.Space(5);

                foreach (var result in _validationResults)
                {
                    DrawValidationResult(result);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationResult(ValidationResult result)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Status icon
            var icon = result.isValid ? "\u2713" : "\u2717"; // Check mark or X
            var color = result.isValid ? Color.green : Color.red;
            var oldColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));
            GUI.color = oldColor;

            // Category and message
            EditorGUILayout.LabelField($"[{result.category}] {result.message}");

            // Fix button if available
            if (!result.isValid && result.fixAction != null)
            {
                if (GUILayout.Button("Fix", GUILayout.Width(50)))
                {
                    result.fixAction();
                    RunValidation();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ==================== AUTO-CONNECT LOGIC ====================

        private void AutoConnectAll()
        {
            EditorUtility.DisplayProgressBar("Auto Connect", "Creating config...", 0.1f);
            
            if (_config == null)
            {
                CreateGameConfig();
            }

            EditorUtility.DisplayProgressBar("Auto Connect", "Populating prefabs...", 0.3f);
            PopulatePrefabs();

            EditorUtility.DisplayProgressBar("Auto Connect", "Populating levels...", 0.5f);
            PopulateLevels();

            EditorUtility.DisplayProgressBar("Auto Connect", "Populating portal links...", 0.7f);
            PopulatePortalLinks();

            EditorUtility.DisplayProgressBar("Auto Connect", "Fixing World scene...", 0.9f);
            FixWorldScene();

            EditorUtility.ClearProgressBar();

            RunValidation();
            
            Debug.Log("[AutoConnect] All connections completed!");
            EditorUtility.DisplayDialog("Auto Connect", "All connections completed successfully!", "OK");
        }

        private void CreateGameConfig()
        {
            // Ensure Config folder exists
            if (!AssetDatabase.IsValidFolder(ConfigPath))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            _config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(_config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[AutoConnect] Created GameConfig at {ConfigAssetPath}");
        }

        private void PopulatePrefabs()
        {
            if (_config == null)
            {
                Debug.LogError("[AutoConnect] No GameConfig found. Create one first.");
                return;
            }

            if (_config.prefabs == null)
            {
                _config.prefabs = new LevelPrefabs();
            }

            // Find and assign prefabs
            _config.prefabs.floorPrefab = LoadPrefab("Floor");
            _config.prefabs.wallPrefab = LoadPrefab("Wall");
            _config.prefabs.barrierPrefab = LoadPrefab("ColorBarrier");
            _config.prefabs.pickupPrefab = LoadPrefab("MaskPickup");
            _config.prefabs.barrelPrefab = LoadPrefab("ExplodingBarrel");
            _config.prefabs.botPrefab = LoadPrefab("Bot");
            _config.prefabs.portalPrefab = LoadPrefab("Portal");
            _config.prefabs.hiddenBlockPrefab = LoadPrefab("HiddenBlock");
            _config.prefabs.droppedMaskPrefab = LoadPrefab("DroppedMask");
            _config.prefabs.playerPrefab = LoadPrefab("Player");

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[AutoConnect] Prefabs populated");
        }

        private GameObject LoadPrefab(string name)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[AutoConnect] Prefab not found: {path}");
            }
            return prefab;
        }

        private void PopulateLevels()
        {
            if (_config == null)
            {
                Debug.LogError("[AutoConnect] No GameConfig found. Create one first.");
                return;
            }

            _config.allLevels.Clear();

            // Find all LevelData assets
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var levelData = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (levelData != null)
                {
                    _config.allLevels.Add(levelData);
                }
            }

            // Set starting level if not set
            if (_config.startingLevel == null && _config.allLevels.Count > 0)
            {
                // Prefer "Tutorial" level if exists
                _config.startingLevel = _config.allLevels.FirstOrDefault(l => 
                    l.levelName.ToLower().Contains("tutorial")) ?? _config.allLevels[0];
            }

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[AutoConnect] Populated {_config.allLevels.Count} levels");
        }

        private void PopulatePortalLinks()
        {
            if (_config == null)
            {
                Debug.LogError("[AutoConnect] No GameConfig found. Create one first.");
                return;
            }

            _config.portalLinks.Clear();

            // Find all EntranceExitLink assets
            string[] searchPaths = new[] { LevelsPath, PortalLinksPath };
            foreach (var searchPath in searchPaths)
            {
                if (!AssetDatabase.IsValidFolder(searchPath)) continue;
                
                string[] guids = AssetDatabase.FindAssets("t:EntranceExitLink", new[] { searchPath });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var link = AssetDatabase.LoadAssetAtPath<EntranceExitLink>(path);
                    if (link != null && !_config.portalLinks.Contains(link))
                    {
                        _config.portalLinks.Add(link);
                    }
                }
            }

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[AutoConnect] Populated {_config.portalLinks.Count} portal links");
        }

        private void FixWorldScene()
        {
            if (_config == null)
            {
                Debug.LogError("[AutoConnect] No GameConfig found. Create one first.");
                return;
            }

            // Check if scene exists
            if (!File.Exists(WorldScenePath))
            {
                Debug.LogError($"[AutoConnect] World scene not found at {WorldScenePath}. Run Game Setup first.");
                return;
            }

            // Open the scene
            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

            bool modified = false;

            // Find and fix SceneContext
            var sceneContext = FindObjectOfType<SceneContext>();
            if (sceneContext != null)
            {
                modified |= FixSceneContext(sceneContext);
            }
            else
            {
                Debug.LogWarning("[AutoConnect] SceneContext not found in World scene");
            }

            // Find and fix LevelManager
            var levelManager = FindObjectOfType<LevelManager>();
            if (levelManager != null)
            {
                modified |= FixLevelManager(levelManager);
            }
            else
            {
                Debug.LogWarning("[AutoConnect] LevelManager not found in World scene");
            }

            // Find and fix CheckpointManager
            var checkpointManager = FindObjectOfType<CheckpointManager>();
            if (checkpointManager != null)
            {
                modified |= FixCheckpointManager(checkpointManager);
            }

            if (modified)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[AutoConnect] World scene updated and saved");
            }
            else
            {
                Debug.Log("[AutoConnect] World scene already configured correctly");
            }
        }

        private bool FixSceneContext(SceneContext sceneContext)
        {
            bool modified = false;
            var so = new SerializedObject(sceneContext);

            // Find or create GameInstaller
            var gameInstaller = sceneContext.GetComponent<GameInstaller>();
            if (gameInstaller == null)
            {
                gameInstaller = sceneContext.gameObject.AddComponent<GameInstaller>();
                modified = true;
                Debug.Log("[AutoConnect] Added GameInstaller component to SceneContext");
            }

            // Try to find the installers property
            var installersProperty = so.FindProperty("_monoInstallers");
            if (installersProperty == null)
            {
                installersProperty = so.FindProperty("_installers");
            }

            if (installersProperty != null && installersProperty.isArray)
            {
                // Check if installer is already added
                bool hasInstaller = false;
                for (int i = 0; i < installersProperty.arraySize; i++)
                {
                    var element = installersProperty.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == gameInstaller)
                    {
                        hasInstaller = true;
                        break;
                    }
                }

                if (!hasInstaller)
                {
                    installersProperty.arraySize++;
                    var newElement = installersProperty.GetArrayElementAtIndex(installersProperty.arraySize - 1);
                    newElement.objectReferenceValue = gameInstaller;
                    so.ApplyModifiedProperties();
                    modified = true;
                    Debug.Log("[AutoConnect] Added GameInstaller to SceneContext installers");
                }
            }
            else
            {
                Debug.LogWarning("[AutoConnect] Could not find installers property on SceneContext. You may need to assign manually.");
            }

            return modified;
        }

        private bool FixLevelManager(LevelManager levelManager)
        {
            var so = new SerializedObject(levelManager);
            var configProperty = so.FindProperty("gameConfig");

            if (configProperty != null && configProperty.objectReferenceValue != _config)
            {
                configProperty.objectReferenceValue = _config;
                so.ApplyModifiedProperties();
                Debug.Log("[AutoConnect] Assigned GameConfig to LevelManager");
                return true;
            }

            return false;
        }

        private bool FixCheckpointManager(CheckpointManager checkpointManager)
        {
            // CheckpointManager uses injection, so we just verify it exists
            // No direct config assignment needed
            return false;
        }

        // ==================== VALIDATION ====================

        private void RunValidation()
        {
            _validationResults.Clear();

            ValidateConfig();
            ValidatePrefabs();
            ValidateLevels();
            ValidateWorldScene();
        }

        private void ValidateConfig()
        {
            _validationResults.Add(new ValidationResult
            {
                category = "Config",
                message = _config != null ? "GameConfig exists" : "GameConfig not found",
                isValid = _config != null,
                fixAction = _config == null ? CreateGameConfig : null
            });
        }

        private void ValidatePrefabs()
        {
            if (_config == null) return;

            var prefabChecks = new Dictionary<string, GameObject>
            {
                { "floorPrefab", _config.prefabs?.floorPrefab },
                { "wallPrefab", _config.prefabs?.wallPrefab },
                { "barrierPrefab", _config.prefabs?.barrierPrefab },
                { "pickupPrefab", _config.prefabs?.pickupPrefab },
                { "barrelPrefab", _config.prefabs?.barrelPrefab },
                { "botPrefab", _config.prefabs?.botPrefab },
                { "portalPrefab", _config.prefabs?.portalPrefab },
                { "hiddenBlockPrefab", _config.prefabs?.hiddenBlockPrefab },
                { "droppedMaskPrefab", _config.prefabs?.droppedMaskPrefab },
                { "playerPrefab", _config.prefabs?.playerPrefab },
            };

            foreach (var kvp in prefabChecks)
            {
                _validationResults.Add(new ValidationResult
                {
                    category = "Prefab",
                    message = kvp.Value != null ? $"{kvp.Key} assigned" : $"{kvp.Key} missing",
                    isValid = kvp.Value != null,
                    fixAction = kvp.Value == null ? PopulatePrefabs : null
                });
            }

            // Validate prefab components
            if (_config.prefabs?.barrierPrefab != null)
            {
                var hasComponent = _config.prefabs.barrierPrefab.GetComponent<ColorBarrier>() != null;
                _validationResults.Add(new ValidationResult
                {
                    category = "Component",
                    message = hasComponent ? "ColorBarrier has component" : "ColorBarrier missing ColorBarrier component",
                    isValid = hasComponent
                });
            }

            if (_config.prefabs?.pickupPrefab != null)
            {
                var hasComponent = _config.prefabs.pickupPrefab.GetComponent<MaskPickup>() != null;
                _validationResults.Add(new ValidationResult
                {
                    category = "Component",
                    message = hasComponent ? "MaskPickup has component" : "MaskPickup missing MaskPickup component",
                    isValid = hasComponent
                });
            }

            if (_config.prefabs?.playerPrefab != null)
            {
                var hasComponent = _config.prefabs.playerPrefab.GetComponent<PlayerController>() != null;
                _validationResults.Add(new ValidationResult
                {
                    category = "Component",
                    message = hasComponent ? "Player has PlayerController" : "Player missing PlayerController component",
                    isValid = hasComponent
                });
            }
        }

        private void ValidateLevels()
        {
            if (_config == null) return;

            _validationResults.Add(new ValidationResult
            {
                category = "Levels",
                message = _config.allLevels.Count > 0 ? $"{_config.allLevels.Count} levels loaded" : "No levels found",
                isValid = _config.allLevels.Count > 0,
                fixAction = _config.allLevels.Count == 0 ? PopulateLevels : null
            });

            _validationResults.Add(new ValidationResult
            {
                category = "Levels",
                message = _config.startingLevel != null ? $"Starting level: {_config.startingLevel.levelName}" : "No starting level set",
                isValid = _config.startingLevel != null,
                fixAction = _config.startingLevel == null ? PopulateLevels : null
            });
        }

        private void ValidateWorldScene()
        {
            bool sceneExists = File.Exists(WorldScenePath);
            _validationResults.Add(new ValidationResult
            {
                category = "Scene",
                message = sceneExists ? "World.unity exists" : "World.unity not found",
                isValid = sceneExists
            });

            // We can't fully validate scene contents without opening it
            // Add a general note
            if (sceneExists)
            {
                _validationResults.Add(new ValidationResult
                {
                    category = "Scene",
                    message = "Click 'Fix World Scene' to ensure proper connections",
                    isValid = true
                });
            }
        }
    }
}

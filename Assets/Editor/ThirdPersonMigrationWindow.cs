#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System;
using System.IO;
using System.Collections.Generic;
using Zenject;
using BreakingHue.Camera;
using BreakingHue.Gameplay;
using BreakingHue.UI;
using BreakingHue.Input;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Editor window for migrating the game to the third-person camera system.
    /// Automatically configures the World scene with all new components and bindings.
    /// Access via Window > Breaking Hue > Third Person Migration
    /// </summary>
    public class ThirdPersonMigrationWindow : EditorWindow
    {
        // Paths
        private const string InputPath = "Assets/Input";
        private const string InputActionsPath = "Assets/Input/GameInputActions.inputactions";
        private const string UIPath = "Assets/UI";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string ScenesPath = "Assets/Scenes";
        private const string WorldScenePath = "Assets/Scenes/World.unity";

        // Status tracking
        private bool _inputActionsExist;
        private bool _cameraConfigured;
        private bool _playerPrefabConfigured;
        private bool _uiGameObjectsExist;
        private bool _inputManagerExists;
        
        private Vector2 _scrollPosition;
        private List<string> _logMessages = new List<string>();

        [MenuItem("Window/Breaking Hue/Third Person Migration")]
        public static void ShowWindow()
        {
            var window = GetWindow<ThirdPersonMigrationWindow>("Third Person Migration");
            window.minSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            CheckCurrentStatus();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawStatusSection();
            EditorGUILayout.Space(20);

            DrawMigrationButton();
            EditorGUILayout.Space(20);

            DrawIndividualSteps();
            EditorGUILayout.Space(20);

            DrawLogSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Third Person Camera Migration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool will configure your World scene for the new third-person camera system.\n\n" +
                "Features:\n" +
                "• Third-person camera with Y-axis rotation\n" +
                "• Camera-relative player movement\n" +
                "• Full controller support with auto-switching\n" +
                "• Contextual control hints UI\n" +
                "• Self-destruct / restart mechanic\n" +
                "• Pause menu",
                MessageType.Info);
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Input Actions Asset Exists", _inputActionsExist);
            EditorGUILayout.Toggle("Camera Configured for Third-Person", _cameraConfigured);
            EditorGUILayout.Toggle("Player Prefab Has PlayerInput", _playerPrefabConfigured);
            EditorGUILayout.Toggle("UI GameObjects Created", _uiGameObjectsExist);
            EditorGUILayout.Toggle("InputManager Exists", _inputManagerExists);
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Refresh Status"))
            {
                CheckCurrentStatus();
            }
        }

        private void DrawMigrationButton()
        {
            EditorGUILayout.LabelField("Full Migration", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("Run Full Migration", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Run Full Migration",
                    "This will:\n" +
                    "• Create a backup of World.unity\n" +
                    "• Configure camera for third-person\n" +
                    "• Update Player prefab\n" +
                    "• Create UI GameObjects\n" +
                    "• Setup InputManager\n\n" +
                    "Continue?",
                    "Yes, Migrate", "Cancel"))
                {
                    RunFullMigration();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawIndividualSteps()
        {
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Verify Input Actions Asset"))
                VerifyInputActions();

            if (GUILayout.Button("2. Configure Camera"))
                ConfigureCamera();

            if (GUILayout.Button("3. Update Player Prefab"))
                UpdatePlayerPrefab();

            if (GUILayout.Button("4. Create UI GameObjects"))
                CreateUIGameObjects();

            if (GUILayout.Button("5. Create InputManager"))
                CreateInputManager();

            if (GUILayout.Button("6. Save Scene"))
                SaveScene();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(150));
            foreach (var msg in _logMessages)
            {
                EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Clear Log"))
            {
                _logMessages.Clear();
            }
        }

        private void Log(string message)
        {
            _logMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log($"[ThirdPersonMigration] {message}");
            Repaint();
        }

        private void CheckCurrentStatus()
        {
            _inputActionsExist = File.Exists(InputActionsPath);
            
            // Check camera
            var camera = FindObjectOfType<GameCamera>();
            _cameraConfigured = camera != null;
            
            // Check player prefab
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Player.prefab");
            _playerPrefabConfigured = playerPrefab != null && playerPrefab.GetComponent<PlayerInput>() != null;
            
            // Check UI GameObjects
            _uiGameObjectsExist = FindObjectOfType<ControlsBarController>() != null;
            
            // Check InputManager
            _inputManagerExists = FindObjectOfType<InputManager>() != null;
            
            Repaint();
        }

        // ==================== FULL MIGRATION ====================

        private void RunFullMigration()
        {
            _logMessages.Clear();
            Log("Starting full migration...");

            try
            {
                EditorUtility.DisplayProgressBar("Third Person Migration", "Creating backup...", 0.05f);
                CreateSceneBackup();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Opening World scene...", 0.1f);
                OpenWorldScene();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Verifying input actions...", 0.2f);
                VerifyInputActions();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Configuring camera...", 0.35f);
                ConfigureCamera();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Updating player prefab...", 0.5f);
                UpdatePlayerPrefab();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Creating UI GameObjects...", 0.65f);
                CreateUIGameObjects();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Creating InputManager...", 0.8f);
                CreateInputManager();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Saving scene...", 0.9f);
                SaveScene();

                EditorUtility.DisplayProgressBar("Third Person Migration", "Finalizing...", 0.95f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Log("Full migration complete!");
                CheckCurrentStatus();

                EditorUtility.DisplayDialog("Migration Complete",
                    "Third-person camera system has been configured!\n\n" +
                    "New features:\n" +
                    "• Camera rotates with mouse/right stick\n" +
                    "• Movement is camera-relative\n" +
                    "• Press Escape/Start to pause\n" +
                    "• Hold R/B to restart from checkpoint\n" +
                    "• Controls bar shows at bottom of screen",
                    "OK");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Migration Error", 
                    $"An error occurred during migration:\n\n{ex.Message}\n\nCheck the console for details.", 
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CreateSceneBackup()
        {
            if (!File.Exists(WorldScenePath))
            {
                Log("World scene not found, skipping backup");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = $"{ScenesPath}/World_backup_{timestamp}.unity";
            
            AssetDatabase.CopyAsset(WorldScenePath, backupPath);
            Log($"Created backup: {backupPath}");
        }

        private void OpenWorldScene()
        {
            if (!File.Exists(WorldScenePath))
            {
                Log("World scene not found!");
                return;
            }

            EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            Log("Opened World scene");
        }

        // ==================== INDIVIDUAL STEPS ====================

        private void VerifyInputActions()
        {
            if (!Directory.Exists(InputPath))
            {
                Directory.CreateDirectory(InputPath);
                AssetDatabase.Refresh();
                Log("Created Input folder");
            }

            if (!File.Exists(InputActionsPath))
            {
                Log("Input actions asset not found at expected path");
                Log("Please ensure GameInputActions.inputactions exists in Assets/Input/");
            }
            else
            {
                Log("Input actions asset verified");
            }

            _inputActionsExist = File.Exists(InputActionsPath);
        }

        private void ConfigureCamera()
        {
            var cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObj == null)
            {
                cameraObj = GameObject.Find("Main Camera");
            }

            if (cameraObj == null)
            {
                Log("ERROR: Main Camera not found!");
                return;
            }

            // Ensure GameCamera component exists
            var gameCamera = cameraObj.GetComponent<GameCamera>();
            if (gameCamera == null)
            {
                gameCamera = cameraObj.AddComponent<GameCamera>();
                Log("Added GameCamera component");
            }

            // Configure camera for third-person
            var so = new SerializedObject(gameCamera);
            
            var useThirdPersonProp = so.FindProperty("useThirdPerson");
            if (useThirdPersonProp != null)
            {
                useThirdPersonProp.boolValue = true;
            }

            var distanceProp = so.FindProperty("distance");
            if (distanceProp != null)
            {
                distanceProp.floatValue = 12f;
            }

            var heightAngleProp = so.FindProperty("heightAngle");
            if (heightAngleProp != null)
            {
                heightAngleProp.floatValue = 60f;
            }

            var rotationSpeedProp = so.FindProperty("rotationSpeed");
            if (rotationSpeedProp != null)
            {
                rotationSpeedProp.floatValue = 120f;
            }

            var followSmoothTimeProp = so.FindProperty("followSmoothTime");
            if (followSmoothTimeProp != null)
            {
                followSmoothTimeProp.floatValue = 0.1f;
            }

            so.ApplyModifiedProperties();

            // Configure Unity Camera component
            var unityCamera = cameraObj.GetComponent<UnityEngine.Camera>();
            if (unityCamera != null)
            {
                unityCamera.orthographic = false;
                unityCamera.fieldOfView = 60f;
                Log("Configured Unity Camera for perspective");
            }

            Log("Camera configured for third-person");
            _cameraConfigured = true;
        }

        private void UpdatePlayerPrefab()
        {
            string playerPrefabPath = $"{PrefabsPath}/Player.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            
            if (playerPrefab == null)
            {
                Log("ERROR: Player prefab not found!");
                return;
            }

            // Open prefab for editing
            var prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            bool modified = false;

            // Add PlayerInput component if not present
            var playerInput = prefabRoot.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = prefabRoot.AddComponent<PlayerInput>();
                modified = true;
                Log("Added PlayerInput component to Player prefab");
            }

            // Try to assign input actions asset
            var inputActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActionsAsset != null && playerInput.actions != inputActionsAsset)
            {
                playerInput.actions = inputActionsAsset;
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.SendMessages;
                modified = true;
                Log("Assigned GameInputActions to PlayerInput");
            }

            // Add SelfDestructController if not present
            var selfDestruct = prefabRoot.GetComponent<SelfDestructController>();
            if (selfDestruct == null)
            {
                selfDestruct = prefabRoot.AddComponent<SelfDestructController>();
                modified = true;
                Log("Added SelfDestructController to Player prefab");
            }

            // Configure PlayerController for camera-relative movement
            var playerController = prefabRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                var so = new SerializedObject(playerController);
                var cameraRelativeProp = so.FindProperty("useCameraRelativeMovement");
                if (cameraRelativeProp != null && !cameraRelativeProp.boolValue)
                {
                    cameraRelativeProp.boolValue = true;
                    so.ApplyModifiedProperties();
                    modified = true;
                    Log("Enabled camera-relative movement on PlayerController");
                }
            }

            // Save prefab changes
            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                Log("Saved Player prefab changes");
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
            _playerPrefabConfigured = true;
        }

        private void CreateUIGameObjects()
        {
            // Find or create parent
            var hudObj = GameObject.Find("HUD");
            Transform uiParent = hudObj?.transform.parent ?? null;

            // Create ControlsBar
            var controlsBar = FindObjectOfType<ControlsBarController>();
            if (controlsBar == null)
            {
                var controlsBarObj = new GameObject("ControlsBar");
                if (uiParent != null) controlsBarObj.transform.SetParent(uiParent);
                
                var uiDoc = controlsBarObj.AddComponent<UIDocument>();
                controlsBarObj.AddComponent<ControlsBarController>();
                
                // Try to assign panel settings
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
                if (panelSettings != null) uiDoc.panelSettings = panelSettings;
                
                // Try to assign UXML
                var controlsBarUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/ControlsBar.uxml");
                if (controlsBarUxml != null) uiDoc.visualTreeAsset = controlsBarUxml;
                
                Log("Created ControlsBar GameObject");
            }

            // Create PauseMenu
            var pauseMenu = FindObjectOfType<PauseMenuController>();
            if (pauseMenu == null)
            {
                var pauseMenuObj = new GameObject("PauseMenu");
                if (uiParent != null) pauseMenuObj.transform.SetParent(uiParent);
                
                var uiDoc = pauseMenuObj.AddComponent<UIDocument>();
                pauseMenuObj.AddComponent<PauseMenuController>();
                
                // Try to assign panel settings
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
                if (panelSettings != null) uiDoc.panelSettings = panelSettings;
                
                // Try to assign UXML
                var pauseMenuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/PauseMenu.uxml");
                if (pauseMenuUxml != null) uiDoc.visualTreeAsset = pauseMenuUxml;
                
                Log("Created PauseMenu GameObject");
            }

            _uiGameObjectsExist = true;
        }

        private void CreateInputManager()
        {
            var inputManager = FindObjectOfType<InputManager>();
            if (inputManager == null)
            {
                var inputManagerObj = new GameObject("InputManager");
                inputManager = inputManagerObj.AddComponent<InputManager>();
                
                // Try to assign input actions asset
                var inputActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (inputActionsAsset != null)
                {
                    var so = new SerializedObject(inputManager);
                    var inputActionsProp = so.FindProperty("inputActions");
                    if (inputActionsProp != null)
                    {
                        inputActionsProp.objectReferenceValue = inputActionsAsset;
                        so.ApplyModifiedProperties();
                    }
                }
                
                Log("Created InputManager GameObject");
            }

            // Also create InputIconProvider if not present
            var iconProvider = FindObjectOfType<InputIconProvider>();
            if (iconProvider == null)
            {
                var iconProviderObj = new GameObject("InputIconProvider");
                iconProviderObj.AddComponent<InputIconProvider>();
                Log("Created InputIconProvider GameObject");
            }

            _inputManagerExists = true;
        }

        private void SaveScene()
        {
            EditorSceneManager.SaveOpenScenes();
            Log("Saved scene");
        }
    }
}
#endif

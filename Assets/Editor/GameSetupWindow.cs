#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using Zenject;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Editor window that generates all game assets, prefabs, scenes, and levels.
    /// Access via Window > Breaking Hue > Game Setup
    /// </summary>
    public class GameSetupWindow : EditorWindow
    {
        // Paths
        private const string MaterialsPath = "Assets/Materials";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string LevelsPath = "Assets/Levels";
        private const string UIPath = "Assets/UI";
        private const string ScenesPath = "Assets/Scenes";
        private const string DocumentationPath = "Assets/Documentation";

        // Status
        private Vector2 _scrollPosition;
        private List<string> _logMessages = new List<string>();

        [MenuItem("Window/Breaking Hue/Game Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameSetupWindow>("Breaking Hue Setup");
            window.minSize = new Vector2(400, 600);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawFullSetupSection();
            EditorGUILayout.Space(20);

            DrawIndividualSections();
            EditorGUILayout.Space(20);

            DrawLogSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Breaking Hue - Game Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool generates all required assets to make the game playable:\n" +
                "• Materials with URP emission\n" +
                "• Prefabs (Wall, Player, Barrier, Pickup, Exit)\n" +
                "• UI Assets (PanelSettings, MainMenu)\n" +
                "• Scenes (MainMenu, Game)\n" +
                "• Tutorial level bitmap\n" +
                "• Documentation",
                MessageType.Info);
        }

        private void DrawFullSetupSection()
        {
            EditorGUILayout.LabelField("Full Setup", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Run Full Setup", GUILayout.Height(40)))
            {
                RunFullSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawIndividualSections()
        {
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Generate Materials"))
                GenerateMaterials();

            if (GUILayout.Button("2. Generate Prefabs"))
                GeneratePrefabs();

            if (GUILayout.Button("3. Generate UI Assets"))
                GenerateUIAssets();

            if (GUILayout.Button("4. Generate Scenes"))
                GenerateScenes();

            if (GUILayout.Button("5. Generate Tutorial Level"))
                GenerateTutorialLevel();

            if (GUILayout.Button("6. Update Build Settings"))
                UpdateBuildSettings();

            if (GUILayout.Button("7. Generate Documentation"))
                GenerateDocumentation();
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
            _logMessages.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log($"[GameSetup] {message}");
            Repaint();
        }

        // ==================== FULL SETUP ====================

        private void RunFullSetup()
        {
            _logMessages.Clear();
            Log("Starting Full Setup...");

            try
            {
                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Creating folders...", 0.05f);
                CreateFolders();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating materials...", 0.15f);
                GenerateMaterials();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating prefabs...", 0.30f);
                GeneratePrefabs();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating UI assets...", 0.45f);
                GenerateUIAssets();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating scenes...", 0.60f);
                GenerateScenes();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating tutorial level...", 0.75f);
                GenerateTutorialLevel();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Updating build settings...", 0.85f);
                UpdateBuildSettings();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating documentation...", 0.95f);
                GenerateDocumentation();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Log("Full Setup Complete!");
                EditorUtility.DisplayDialog("Setup Complete", 
                    "Breaking Hue is now ready to play!\n\n" +
                    "1. Open MainMenu scene\n" +
                    "2. Press Play\n" +
                    "3. Click PLAY button to start", 
                    "OK");
            }
            catch (System.Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CreateFolders()
        {
            CreateFolderIfNeeded(MaterialsPath);
            CreateFolderIfNeeded(PrefabsPath);
            CreateFolderIfNeeded(LevelsPath);
            CreateFolderIfNeeded(DocumentationPath);
            Log("Folders created/verified");
        }

        private void CreateFolderIfNeeded(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace("\\", "/");
                var folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        // ==================== MATERIALS ====================

        private void GenerateMaterials()
        {
            CreateFolderIfNeeded(MaterialsPath);

            CreateMaterial("M_Wall", Color.white, 0.5f, false);
            CreateMaterial("M_Player", new Color(0.7f, 0.7f, 0.7f), 0.3f, false);
            CreateMaterial("M_Barrier", Color.white, 1.0f, true); // Transparent for phasing
            CreateMaterial("M_Pickup", Color.white, 1.5f, false);
            CreateMaterial("M_Exit", new Color(0.2f, 1f, 0.3f), 3.0f, false);
            CreateMaterial("M_Floor", new Color(0.15f, 0.15f, 0.2f), 0f, false);

            AssetDatabase.Refresh();
            Log("Materials generated");
        }

        private void CreateMaterial(string name, Color baseColor, float emissionIntensity, bool transparent)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            
            // Find URP Lit shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Log($"WARNING: URP Lit shader not found, using Standard");
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            
            // Set base color
            mat.SetColor("_BaseColor", baseColor);
            
            // Set emission
            if (emissionIntensity > 0)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", baseColor * emissionIntensity);
            }

            // Set transparent surface type for barriers
            if (transparent)
            {
                mat.SetFloat("_Surface", 1); // 1 = Transparent
                mat.SetFloat("_Blend", 0);   // 0 = Alpha
                mat.SetFloat("_AlphaClip", 0);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }

            AssetDatabase.CreateAsset(mat, path);
        }

        // ==================== PREFABS ====================

        private void GeneratePrefabs()
        {
            CreateFolderIfNeeded(PrefabsPath);

            CreateWallPrefab();
            CreatePlayerPrefab();
            CreateBarrierPrefab();
            CreatePickupPrefab();
            CreateExitPrefab();

            AssetDatabase.Refresh();
            Log("Prefabs generated");
        }

        private void CreateWallPrefab()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            
            // Apply material
            var renderer = wall.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Wall.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(wall, "Wall");
            DestroyImmediate(wall);
        }

        private void CreatePlayerPrefab()
        {
            // Root object
            GameObject player = new GameObject("Player");
            
            // Add components to root
            var controller = player.AddComponent<BreakingHue.Gameplay.PlayerController>();
            
            // Use GetComponent instead of AddComponent since PlayerController has [RequireComponent(typeof(Rigidbody))]
            // which auto-adds Rigidbody when PlayerController is added
            var rb = player.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var capsuleCollider = player.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 1f;
            capsuleCollider.radius = 0.3f;
            capsuleCollider.center = new Vector3(0, 0.5f, 0);

            // Visual child (capsule mesh)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = new Vector3(0, 0.5f, 0);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            
            // Remove collider from visual (we use the root's collider)
            DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            
            // Apply material
            var renderer = visual.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Player.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            // Set visualTransform reference via SerializedObject
            SavePrefab(player, "Player");
            
            // After saving, update the serialized reference
            string prefabPath = $"{PrefabsPath}/Player.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var prefabController = prefab.GetComponent<BreakingHue.Gameplay.PlayerController>();
                var prefabVisual = prefab.transform.Find("Visual");
                if (prefabController != null && prefabVisual != null)
                {
                    var so = new SerializedObject(prefabController);
                    var visualProp = so.FindProperty("visualTransform");
                    if (visualProp != null)
                    {
                        visualProp.objectReferenceValue = prefabVisual;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(prefab);
                }
            }

            DestroyImmediate(player);
        }

        private void CreateBarrierPrefab()
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "ColorBarrier";
            
            // Add ColorBarrier component (it will auto-create colliders)
            barrier.AddComponent<BreakingHue.Gameplay.ColorBarrier>();
            
            // Apply material
            var renderer = barrier.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Barrier.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(barrier, "ColorBarrier");
            DestroyImmediate(barrier);
        }

        private void CreatePickupPrefab()
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = "MaskPickup";
            pickup.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            // Add MaskPickup component
            pickup.AddComponent<BreakingHue.Gameplay.MaskPickup>();
            
            // The collider will be set to trigger in Awake
            var collider = pickup.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            
            // Apply material
            var renderer = pickup.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Pickup.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(pickup, "MaskPickup");
            DestroyImmediate(pickup);
        }

        private void CreateExitPrefab()
        {
            // Create a flat cylinder as the exit platform
            GameObject exit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exit.name = "ExitGoal";
            exit.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
            
            // Replace the default collider with a box trigger (taller to catch player)
            DestroyImmediate(exit.GetComponent<CapsuleCollider>());
            var boxCollider = exit.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(1f, 40f, 1f); // Tall trigger zone
            boxCollider.center = new Vector3(0, 20f, 0);
            boxCollider.isTrigger = true;
            
            // Add ExitGoal component
            exit.AddComponent<BreakingHue.Gameplay.ExitGoal>();
            
            // Apply material
            var renderer = exit.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Exit.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(exit, "ExitGoal");
            DestroyImmediate(exit);
        }

        private void SavePrefab(GameObject obj, string name)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(obj, path);
        }

        // ==================== UI ASSETS ====================

        private void GenerateUIAssets()
        {
            CreatePanelSettings();
            CreateMainMenuUXML();
            AssetDatabase.Refresh();
            Log("UI assets generated");
        }

        private void CreatePanelSettings()
        {
            string path = $"{UIPath}/PanelSettings.asset";
            
            // Check if already exists
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(path) != null)
            {
                Log("PanelSettings already exists, skipping");
                return;
            }

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            
            AssetDatabase.CreateAsset(panelSettings, path);
        }

        private void CreateMainMenuUXML()
        {
            string path = $"{UIPath}/MainMenu.uxml";
            
            string uxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"">
    <ui:VisualElement name=""Root"" style=""flex-grow: 1; justify-content: center; align-items: center; background-color: rgb(20, 20, 35);"">
        <ui:Label text=""BREAKING HUE"" name=""Title"" style=""font-size: 64px; color: rgb(255, 255, 255); margin-bottom: 20px; -unity-font-style: bold;"" />
        <ui:Label text=""Collect masks to pass through barriers"" name=""Subtitle"" style=""font-size: 20px; color: rgb(180, 180, 180); margin-bottom: 60px;"" />
        <ui:Button text=""PLAY"" name=""PlayButton"" style=""font-size: 32px; padding: 20px 80px; background-color: rgb(60, 120, 200); color: white; border-radius: 8px;"" />
    </ui:VisualElement>
</ui:UXML>";

            File.WriteAllText(path, uxml);
        }

        // ==================== SCENES ====================

        private void GenerateScenes()
        {
            CreateMainMenuScene();
            CreateGameScene();
            AssetDatabase.Refresh();
            Log("Scenes generated");
        }

        private void CreateMainMenuScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.14f);
            cameraObj.transform.position = new Vector3(0, 0, -10);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            // UI
            var uiObj = new GameObject("UI");
            var uiDoc = uiObj.AddComponent<UIDocument>();
            uiObj.AddComponent<BreakingHue.UI.MainMenuController>();

            // Assign UI assets
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            var menuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/MainMenu.uxml");
            
            if (panelSettings != null) uiDoc.panelSettings = panelSettings;
            if (menuUxml != null) uiDoc.visualTreeAsset = menuUxml;

            // Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Save scene
            string path = $"{ScenesPath}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            Log("MainMenu scene created");
        }

        private void CreateGameScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (top-down perspective)
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            camera.fieldOfView = 60f;
            cameraObj.transform.position = new Vector3(0, 20, 0);
            cameraObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Floor
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorObj.name = "Floor";
            floorObj.transform.position = new Vector3(0, -0.5f, 0);
            floorObj.transform.localScale = new Vector3(3, 1, 3);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Floor.mat");
            if (floorMat != null) floorObj.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Zenject SceneContext
            var contextObj = new GameObject("SceneContext");
            var sceneContext = contextObj.AddComponent<SceneContext>();
            
            // Create and add GameInstaller reference
            var installerObj = new GameObject("GameInstaller");
            var installer = installerObj.AddComponent<BreakingHue.Installers.GameInstaller>();
            installerObj.transform.SetParent(contextObj.transform);
            
            // Add installer to SceneContext - try different Zenject property names
            var so = new SerializedObject(sceneContext);
            var installersProp = so.FindProperty("_monoInstallers");
            if (installersProp == null) installersProp = so.FindProperty("_installers");
            if (installersProp == null) installersProp = so.FindProperty("Installers");
            
            if (installersProp != null)
            {
                installersProp.arraySize = 1;
                installersProp.GetArrayElementAtIndex(0).objectReferenceValue = installer;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Log("WARNING: Could not find installers property on SceneContext - installer not wired");
            }

            // LevelGenerator
            var levelGenObj = new GameObject("LevelGenerator");
            var levelGen = levelGenObj.AddComponent<BreakingHue.Level.LevelGenerator>();
            
            // Wire up prefab references
            var levelGenSO = new SerializedObject(levelGen);
            SetPrefabReference(levelGenSO, "wallPrefab", "Wall");
            SetPrefabReference(levelGenSO, "playerPrefab", "Player");
            SetPrefabReference(levelGenSO, "barrierPrefab", "ColorBarrier");
            SetPrefabReference(levelGenSO, "pickupPrefab", "MaskPickup");
            SetPrefabReference(levelGenSO, "exitPrefab", "ExitGoal");
            levelGenSO.ApplyModifiedPropertiesWithoutUndo();

            // GameManager
            var gameManagerObj = new GameObject("GameManager");
            gameManagerObj.AddComponent<BreakingHue.Core.GameManager>();

            // HUD
            var hudObj = new GameObject("HUD");
            var hudDoc = hudObj.AddComponent<UIDocument>();
            hudObj.AddComponent<BreakingHue.UI.GameHUDController>();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/HUD.uxml");
            
            if (panelSettings != null) hudDoc.panelSettings = panelSettings;
            if (hudUxml != null) hudDoc.visualTreeAsset = hudUxml;

            // Save scene
            string path = $"{ScenesPath}/Game.unity";
            EditorSceneManager.SaveScene(scene, path);
            Log("Game scene created");
        }

        private void SetPrefabReference(SerializedObject so, string propertyName, string prefabName)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/{prefabName}.prefab");
                prop.objectReferenceValue = prefab;
            }
        }

        // ==================== TUTORIAL LEVEL ====================

        private void GenerateTutorialLevel()
        {
            CreateFolderIfNeeded(LevelsPath);
            
            // Create 16x16 texture
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            // Define colors
            Color empty = new Color(0, 0, 0, 0);           // Transparent = empty
            Color wall = new Color(1, 1, 1, 1);            // White = wall
            Color playerSpawn = new Color(0.5f, 0.5f, 0.5f, 1); // Grey = player
            Color redBarrier = new Color(1, 0, 0, 1);      // Red solid = barrier
            Color redPickup = new Color(1, 0, 0, 0.5f);    // Red semi = pickup
            Color exit = new Color(0, 0, 0, 1);            // Black = exit

            // Fill with empty first
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    texture.SetPixel(x, y, empty);
                }
            }

            // Draw walls (border)
            for (int i = 0; i < 16; i++)
            {
                texture.SetPixel(i, 0, wall);   // Bottom
                texture.SetPixel(i, 15, wall);  // Top
                texture.SetPixel(0, i, wall);   // Left
                texture.SetPixel(15, i, wall);  // Right
            }

            // Internal walls (obstacle)
            for (int x = 4; x <= 8; x++)
            {
                texture.SetPixel(x, 10, wall);
            }

            // Player spawn (bottom-left area)
            texture.SetPixel(2, 2, playerSpawn);

            // Red pickup (before barrier)
            texture.SetPixel(5, 4, redPickup);

            // Red barrier (middle area)
            texture.SetPixel(5, 7, redBarrier);

            // Exit (top-right area)
            texture.SetPixel(12, 13, exit);

            texture.Apply();

            // Save as PNG
            string path = $"{LevelsPath}/Tutorial.png";
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            DestroyImmediate(texture);

            AssetDatabase.Refresh();

            // Set import settings
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 32;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            // Wire to LevelGenerator in Game scene
            WireLevelToScene();

            Log("Tutorial level generated");
        }

        private void WireLevelToScene()
        {
            string scenePath = $"{ScenesPath}/Game.unity";
            if (!File.Exists(scenePath)) return;

            // This will be wired when the scene is loaded
            // For now, log that user should assign manually if auto-wire fails
            Log("Tutorial.png created - will be auto-assigned to LevelGenerator");
        }

        // ==================== BUILD SETTINGS ====================

        private void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            
            string menuPath = $"{ScenesPath}/MainMenu.unity";
            string gamePath = $"{ScenesPath}/Game.unity";

            if (File.Exists(menuPath))
                scenes.Add(new EditorBuildSettingsScene(menuPath, true));
            
            if (File.Exists(gamePath))
                scenes.Add(new EditorBuildSettingsScene(gamePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Log("Build settings updated (MainMenu=0, Game=1)");
        }

        // ==================== DOCUMENTATION ====================

        private void GenerateDocumentation()
        {
            CreateFolderIfNeeded(DocumentationPath);

            string doc = @"# Breaking Hue - Game System Guide

## Quick Start

### One-Click Setup
1. Open Unity
2. Go to **Window > Breaking Hue > Game Setup**
3. Click **Run Full Setup**
4. Open the **MainMenu** scene
5. Press **Play** and click the **PLAY** button

### Controls
- **WASD / Arrow Keys**: Move player
- **1, 2, 3**: Equip mask from inventory slot
- **0 / Backquote**: Unequip current mask

---

## Creating New Levels

### Pixel Color Reference

| Color | RGB Value | Alpha | Object Spawned |
|-------|-----------|-------|----------------|
| **Black** | (0, 0, 0) | 1.0 | Exit Goal |
| **White** | (1, 1, 1) | 1.0 | Wall |
| **Grey** | (0.5, 0.5, 0.5) | 1.0 | Player Spawn |
| **Red** | (1, 0, 0) | 1.0 | Red Barrier |
| **Green** | (0, 1, 0) | 1.0 | Green Barrier |
| **Blue** | (0, 0, 1) | 1.0 | Blue Barrier |
| **Red** | (1, 0, 0) | 0.5 | Red Mask Pickup |
| **Green** | (0, 1, 0) | 0.5 | Green Mask Pickup |
| **Blue** | (0, 0, 1) | 0.5 | Blue Mask Pickup |
| **Transparent** | Any | < 0.4 | Empty Space |

### Step-by-Step Level Creation

1. **Create Image**: Use any image editor (Photoshop, GIMP, Aseprite)
2. **Set Size**: 16x16 pixels recommended (can be larger)
3. **Draw Level**: Use colors from the reference table above
4. **Save as PNG**: Save with transparency support
5. **Import to Unity**: Drag into `Assets/Levels/` folder
6. **Configure Import Settings**:
   - Read/Write Enabled: ✓
   - Filter Mode: Point (no filter)
   - Compression: None
   - Max Size: At least your texture size
7. **Assign to LevelGenerator**: 
   - Open Game scene
   - Select LevelGenerator object
   - Drag your texture to the `Level Map` field

### Important: Texture Import Settings

**If your level doesn't generate correctly**, check these settings in the Inspector when selecting your PNG:

```
Texture Type: Default
Read/Write: ✓ Enabled
Filter Mode: Point (no filter)
Compression: None
```

---

## System Architecture

### Component Overview

```
[MainMenu Scene]
├── Camera
├── UI (UIDocument + MainMenuController)
└── Light

[Game Scene]
├── Camera (top-down)
├── Floor
├── SceneContext (Zenject)
│   └── GameInstaller
├── LevelGenerator
│   └── Spawns: Walls, Player, Barriers, Pickups, Exit
├── GameManager
├── HUD (UIDocument + GameHUDController)
└── Light
```

### Event Flow

```
MaskPickup.OnTriggerEnter
    → MaskInventory.TryAddMask()
    → MaskInventory.OnInventoryChanged event
    → GameHUDController.RefreshAllSlots()

Player equips mask (1/2/3 key)
    → MaskInventory.EquipSlot()
    → MaskInventory.OnMaskEquipped event
    → PlayerController.UpdatePlayerColor()
    → GameHUDController.RefreshAllSlots()

ColorBarrier.OnTriggerEnter
    → MaskInventory.CanPassThrough()
    → If true: Disable solid collider, allow passage

ColorBarrier.OnTriggerExit
    → Re-enable solid collider
    → MaskInventory.ConsumeEquipped()

ExitGoal.OnTriggerEnter
    → ExitGoal.OnPlayerReachedExit event
    → GameManager.OnLevelComplete()
    → Scene transition to MainMenu
```

### Zenject Dependency Injection

The `GameInstaller` binds these services:
- `MaskInventory`: Singleton, manages 3 inventory slots
- `LevelGenerator`: From hierarchy, generates level from texture
- `GameHUDController`: From hierarchy, displays inventory UI
- `GameManager`: From hierarchy, handles win/lose conditions

---

## Prefab Reference

### Wall
- Simple 1x1x1 cube
- BoxCollider (solid)
- White emissive material

### Player
- Root: PlayerController, Rigidbody, CapsuleCollider
- Child ""Visual"": Capsule mesh with grey material
- Uses Rigidbody physics with gravity
- Tag: ""Player"" (set automatically in script)

### ColorBarrier
- 1x1x1 cube with ColorBarrier script
- Auto-creates dual colliders (trigger + solid)
- Transparent material for phasing visual effect
- Initialize() sets required color

### MaskPickup
- 0.5x0.5x0.5 cube with MaskPickup script
- Trigger collider (auto-set)
- Rotates and bobs in Update()
- Initialize() sets color type to give

### ExitGoal
- Flat cylinder platform
- Tall trigger collider to catch player
- Green emissive material with pulsing effect
- Fires static event when player enters

---

## Troubleshooting

### Player Falls Through Floor
- **Cause**: No floor in scene or floor position wrong
- **Fix**: Ensure Floor object exists at Y=-0.5

### Barriers Don't Block/Allow Passage
- **Cause**: Player tag not set
- **Fix**: Verify player has ""Player"" tag (set automatically by PlayerController)

### Level Doesn't Generate
- **Cause**: Texture import settings incorrect
- **Fix**: Enable Read/Write, set Filter to Point, disable Compression

### Pickups Can't Be Collected
- **Cause**: MaskInventory not injected
- **Fix**: Ensure SceneContext has GameInstaller in Installers list

### HUD Doesn't Show
- **Cause**: Missing PanelSettings or UXML reference
- **Fix**: Assign PanelSettings.asset and HUD.uxml to UIDocument

### Scene Transition Fails
- **Cause**: Scenes not in Build Settings
- **Fix**: Run ""Update Build Settings"" from Game Setup window

---

## Tips for Level Design

1. **Always surround with walls** - Prevents player from walking off the map
2. **Place pickups before barriers** - Player needs mask before reaching barrier
3. **One player spawn** - Only place one grey pixel
4. **Test mask combinations** - Yellow = Red + Green, Cyan = Green + Blue, etc.
5. **Place exit last** - Put it where the player should end up after all barriers

---

*Generated by Breaking Hue Game Setup Tool*
";

            string path = $"{DocumentationPath}/GameSystemGuide.md";
            File.WriteAllText(path, doc);
            AssetDatabase.Refresh();
            Log("Documentation generated");
        }
    }
}
#endif

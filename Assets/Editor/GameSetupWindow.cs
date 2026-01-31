#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using Zenject;
using BreakingHue.Core;
using BreakingHue.Input;
using BreakingHue.Level.Data;
using BreakingHue.Tutorial;
using BreakingHue.UI;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Editor window that generates all game assets for the RYB color puzzle game.
    /// Updated for the new multi-level system with bots, barrels, portals, and checkpoints.
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
        private const string ConfigPath = "Assets/Config";
        private const string ConfigAssetPath = "Assets/Config/GameConfig.asset";

        // Status
        private Vector2 _scrollPosition;
        private List<string> _logMessages = new List<string>();

        [MenuItem("Window/Breaking Hue/Game Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameSetupWindow>("Breaking Hue Setup");
            window.minSize = new Vector2(450, 700);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawCleanupSection();
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
                "RYB Color Puzzle Game Setup\n\n" +
                "This tool generates all assets for the updated game:\n" +
                "• RYB color materials (Red, Yellow, Blue, Orange, Green, Purple, Black)\n" +
                "• All prefabs (Wall, Player, Barrier, Pickup, Barrel, Bot, Portal, HiddenBlock)\n" +
                "• World scene with LevelManager & CheckpointManager\n" +
                "• UI assets with mask toggle support\n" +
                "• Example level data",
                MessageType.Info);
        }

        private void DrawCleanupSection()
        {
            EditorGUILayout.LabelField("Cleanup Old Assets", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Old RGB Assets", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Delete Old Assets",
                    "This will delete:\n• Old Tutorial.png level\n• Old Game.unity scene\n• Old RGB materials\n\nAre you sure?",
                    "Delete", "Cancel"))
                {
                    DeleteOldAssets();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawFullSetupSection()
        {
            EditorGUILayout.LabelField("Full Setup", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Run Full Setup (RYB System)", GUILayout.Height(40)))
            {
                RunFullSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawIndividualSections()
        {
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Generate RYB Materials"))
                GenerateMaterials();

            if (GUILayout.Button("2. Generate All Prefabs"))
                GeneratePrefabs();

            if (GUILayout.Button("3. Generate UI Assets"))
                GenerateUIAssets();

            if (GUILayout.Button("4. Generate World Scene"))
                GenerateScenes();

            if (GUILayout.Button("4b. Generate EndGame Scene"))
                GenerateEndGameScene();

            if (GUILayout.Button("4c. Setup Tutorial & Prompts in World Scene"))
                SetupTutorialAndPromptsInWorldScene();

            if (GUILayout.Button("5. Generate Example Level"))
                GenerateExampleLevel();

            if (GUILayout.Button("6. Generate Game Config"))
                GenerateGameConfig();

            if (GUILayout.Button("7. Update Build Settings"))
                UpdateBuildSettings();

            if (GUILayout.Button("8. Generate Documentation"))
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

        // ==================== CLEANUP ====================

        private void DeleteOldAssets()
        {
            // Delete old level PNG
            DeleteAssetIfExists($"{LevelsPath}/Tutorial.png");
            
            // Delete old Game scene (replaced by World)
            DeleteAssetIfExists($"{ScenesPath}/Game.unity");
            
            // Delete old single-color materials
            DeleteAssetIfExists($"{MaterialsPath}/M_Barrier.mat");
            DeleteAssetIfExists($"{MaterialsPath}/M_Pickup.mat");
            DeleteAssetIfExists($"{MaterialsPath}/M_Exit.mat");
            
            // Delete old ExitGoal prefab (replaced by Portal)
            DeleteAssetIfExists($"{PrefabsPath}/ExitGoal.prefab");
            
            AssetDatabase.Refresh();
            Log("Old assets deleted");
        }

        private void DeleteAssetIfExists(string path)
        {
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
                Log($"Deleted: {path}");
            }
        }

        // ==================== FULL SETUP ====================

        private void RunFullSetup()
        {
            _logMessages.Clear();
            Log("Starting Full Setup (RYB System)...");

            try
            {
                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Creating folders...", 0.05f);
                CreateFolders();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating RYB materials...", 0.15f);
                GenerateMaterials();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating prefabs...", 0.30f);
                GeneratePrefabs();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating UI assets...", 0.45f);
                GenerateUIAssets();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating World scene...", 0.55f);
                GenerateScenes();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating EndGame scene...", 0.62f);
                GenerateEndGameScene();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating example level...", 0.70f);
                GenerateExampleLevel();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating game config...", 0.80f);
                GenerateGameConfig();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Updating build settings...", 0.90f);
                UpdateBuildSettings();

                EditorUtility.DisplayProgressBar("Breaking Hue Setup", "Generating documentation...", 0.95f);
                GenerateDocumentation();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Log("Full Setup Complete!");
                EditorUtility.DisplayDialog("Setup Complete", 
                    "Breaking Hue (RYB System) is now ready!\n\n" +
                    "1. Open MainMenu scene\n" +
                    "2. Press Play\n" +
                    "3. Use Level Editor to create levels\n\n" +
                    "New Controls:\n" +
                    "• 1/2/3: Toggle mask active\n" +
                    "• Q: Drop mask\n" +
                    "• 0: Deactivate all masks", 
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
            CreateFolderIfNeeded(UIPath);
            CreateFolderIfNeeded(ScenesPath);
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

            // Structural materials
            CreateMaterial("M_Wall", Color.white, 0.5f, false);
            CreateMaterial("M_Player", new Color(0.7f, 0.7f, 0.7f), 0.3f, false);
            CreateMaterial("M_Floor", new Color(0.15f, 0.15f, 0.2f), 0f, false);
            CreateMaterial("M_Hidden", new Color(0.25f, 0.25f, 0.25f), 0.1f, false);

            // RYB Primary barrier materials (transparent)
            CreateMaterial("M_Barrier_Red", ColorType.Red.ToColor(), 1.0f, true);
            CreateMaterial("M_Barrier_Yellow", ColorType.Yellow.ToColor(), 1.0f, true);
            CreateMaterial("M_Barrier_Blue", ColorType.Blue.ToColor(), 1.0f, true);

            // RYB Secondary barrier materials
            CreateMaterial("M_Barrier_Orange", ColorType.Orange.ToColor(), 1.0f, true);
            CreateMaterial("M_Barrier_Green", ColorType.Green.ToColor(), 1.0f, true);
            CreateMaterial("M_Barrier_Purple", ColorType.Purple.ToColor(), 1.0f, true);

            // Black barrier
            CreateMaterial("M_Barrier_Black", new Color(0.1f, 0.1f, 0.1f), 0.5f, true);

            // Pickup materials (opaque, high emission)
            CreateMaterial("M_Pickup_Red", ColorType.Red.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Yellow", ColorType.Yellow.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Blue", ColorType.Blue.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Orange", ColorType.Orange.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Green", ColorType.Green.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Purple", ColorType.Purple.ToColor(), 1.5f, false);
            CreateMaterial("M_Pickup_Black", new Color(0.1f, 0.1f, 0.1f), 0.8f, false);

            // Barrel materials (slightly darker, hazardous look)
            CreateMaterial("M_Barrel_Red", ColorType.Red.ToColor() * 0.8f, 0.8f, false);
            CreateMaterial("M_Barrel_Yellow", ColorType.Yellow.ToColor() * 0.8f, 0.8f, false);
            CreateMaterial("M_Barrel_Blue", ColorType.Blue.ToColor() * 0.8f, 0.8f, false);

            // Entity materials
            CreateMaterial("M_Bot", new Color(0.5f, 0.8f, 0.9f), 0.5f, false); // Cyan-ish
            CreateMaterial("M_Portal", new Color(0f, 0.8f, 1f), 2.0f, false);
            CreateMaterial("M_Portal_Checkpoint", new Color(1f, 0.8f, 0f), 2.5f, false); // Gold

            AssetDatabase.Refresh();
            Log("RYB Materials generated");
        }

        private void CreateMaterial(string name, Color baseColor, float emissionIntensity, bool transparent)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Log($"WARNING: URP Lit shader not found, using Standard");
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            
            baseColor.a = 1f;
            mat.SetColor("_BaseColor", baseColor);
            
            if (emissionIntensity > 0)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", baseColor * emissionIntensity);
            }

            if (transparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
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

            CreateFloorPrefab();
            CreateWallPrefab();
            CreatePlayerPrefab();
            CreateBarrierPrefab();
            CreatePickupPrefab();
            CreateBarrelPrefab();
            CreateBotPrefab();
            CreatePortalPrefab();
            CreateHiddenBlockPrefab();
            CreateDroppedMaskPrefab();

            AssetDatabase.Refresh();
            Log("All prefabs generated");
        }

        private void CreateFloorPrefab()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(1f, 0.1f, 1f); // Flat tile
            
            var renderer = floor.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Floor.mat");
            if (mat != null) renderer.sharedMaterial = mat;
            
            // Remove collider - floors don't need collision (player walks on ground plane)
            var collider = floor.GetComponent<BoxCollider>();
            if (collider != null) DestroyImmediate(collider);

            SavePrefab(floor, "Floor");
            DestroyImmediate(floor);
        }

        private void CreateWallPrefab()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            
            var renderer = wall.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Wall.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(wall, "Wall");
            DestroyImmediate(wall);
        }

        private void CreatePlayerPrefab()
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            
            var controller = player.AddComponent<BreakingHue.Gameplay.PlayerController>();
            
            var rb = player.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var capsuleCollider = player.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 1f;
            capsuleCollider.radius = 0.3f;
            capsuleCollider.center = new Vector3(0, 0.5f, 0);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = new Vector3(0, 0.5f, 0);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            
            DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            
            var renderer = visual.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Player.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(player, "Player");
            DestroyImmediate(player);
        }

        private void CreateBarrierPrefab()
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "ColorBarrier";
            
            barrier.AddComponent<BreakingHue.Gameplay.ColorBarrier>();
            
            var renderer = barrier.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Barrier_Red.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(barrier, "ColorBarrier");
            DestroyImmediate(barrier);
        }

        private void CreatePickupPrefab()
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = "MaskPickup";
            pickup.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            pickup.AddComponent<BreakingHue.Gameplay.MaskPickup>();
            
            var collider = pickup.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            
            var renderer = pickup.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Pickup_Red.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(pickup, "MaskPickup");
            DestroyImmediate(pickup);
        }

        private void CreateBarrelPrefab()
        {
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "ExplodingBarrel";
            barrel.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            
            barrel.AddComponent<BreakingHue.Gameplay.ExplodingBarrel>();
            
            var renderer = barrel.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Barrel_Red.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(barrel, "ExplodingBarrel");
            DestroyImmediate(barrel);
        }

        private void CreateBotPrefab()
        {
            GameObject bot = new GameObject("Bot");
            
            var controller = bot.AddComponent<BreakingHue.Gameplay.Bot.BotController>();
            
            var rb = bot.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var capsuleCollider = bot.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 1f;
            capsuleCollider.radius = 0.3f;
            capsuleCollider.center = new Vector3(0, 0.5f, 0);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(bot.transform);
            visual.transform.localPosition = new Vector3(0, 0.5f, 0);
            visual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            
            DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            
            var renderer = visual.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Bot.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(bot, "Bot");
            DestroyImmediate(bot);
        }

        private void CreatePortalPrefab()
        {
            GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.name = "Portal";
            portal.transform.localScale = new Vector3(1f, 2f, 1f);
            portal.transform.rotation = Quaternion.Euler(0, 0, 0);
            
            DestroyImmediate(portal.GetComponent<MeshCollider>());
            var boxCollider = portal.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 1f, 0.5f);
            
            portal.AddComponent<BreakingHue.Gameplay.Portal>();
            
            var renderer = portal.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Portal.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(portal, "Portal");
            DestroyImmediate(portal);
        }

        private void CreateHiddenBlockPrefab()
        {
            GameObject hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hidden.name = "HiddenBlock";
            
            var collider = hidden.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            
            hidden.AddComponent<BreakingHue.Gameplay.HiddenBlock>();
            
            var renderer = hidden.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Hidden.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(hidden, "HiddenBlock");
            DestroyImmediate(hidden);
        }

        private void CreateDroppedMaskPrefab()
        {
            GameObject dropped = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dropped.name = "DroppedMask";
            dropped.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            
            var collider = dropped.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            
            dropped.AddComponent<BreakingHue.Gameplay.DroppedMask>();
            
            var renderer = dropped.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Pickup_Red.mat");
            if (mat != null) renderer.sharedMaterial = mat;

            SavePrefab(dropped, "DroppedMask");
            DestroyImmediate(dropped);
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
            CreateHUDUXML();
            AssetDatabase.Refresh();
            Log("UI assets generated");
        }

        private void CreatePanelSettings()
        {
            string path = $"{UIPath}/PanelSettings.asset";
            
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
            
            string uxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement name=""Root"" style=""flex-grow: 1; justify-content: center; align-items: center; background-color: rgb(20, 20, 35);"">
        <ui:Label text=""BREAKING HUE"" name=""Title"" style=""font-size: 64px; color: rgb(255, 255, 255); margin-bottom: 10px; -unity-font-style: bold;"" />
        <ui:Label text=""RYB Color Puzzle"" name=""Subtitle"" style=""font-size: 24px; color: rgb(255, 200, 100); margin-bottom: 40px;"" />
        <ui:Label text=""Toggle masks to combine colors and pass through barriers"" name=""Description"" style=""font-size: 18px; color: rgb(180, 180, 180); margin-bottom: 60px;"" />
        <ui:Button text=""PLAY"" name=""PlayButton"" style=""font-size: 32px; padding: 20px 80px; background-color: rgb(60, 120, 200); color: white; border-radius: 8px;"" />
        <ui:VisualElement style=""margin-top: 40px;"">
            <ui:Label text=""Controls:"" style=""font-size: 16px; color: white; margin-bottom: 10px;"" />
            <ui:Label text=""WASD - Move  |  1/2/3 - Toggle Masks  |  Q - Drop Mask"" style=""font-size: 14px; color: rgb(150, 150, 150);"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>";

            File.WriteAllText(path, uxml);
        }

        private void CreateHUDUXML()
        {
            string path = $"{UIPath}/HUD.uxml";
            
            string uxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement name=""Root"" style=""flex-grow: 1;"">
        <ui:VisualElement name=""SlotsContainer"" style=""position: absolute; bottom: 20px; left: 0; right: 0; flex-direction: row; justify-content: center;"">
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>";

            File.WriteAllText(path, uxml);
        }

        // ==================== SCENES ====================

        private void GenerateScenes()
        {
            CreateMainMenuScene();
            CreateWorldScene();
            AssetDatabase.Refresh();
            Log("Scenes generated");
        }

        private void GenerateEndGameScene()
        {
            CreateEndGameScene();
            CreateEndGameConfig();
            UpdateBuildSettings();
            AssetDatabase.Refresh();
            Log("EndGame scene and config generated");
        }

        private void CreateEndGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cameraObj.transform.position = new Vector3(0, 0, -10);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            // Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.5f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // UI with EndGameController
            var uiObj = new GameObject("EndGameUI");
            var uiDoc = uiObj.AddComponent<UIDocument>();
            uiObj.AddComponent<BreakingHue.UI.EndGameController>();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            if (panelSettings != null) uiDoc.panelSettings = panelSettings;
            // Note: EndGameController creates UI programmatically, no UXML needed

            // Audio source for completion sounds (optional)
            var audioObj = new GameObject("AudioSource");
            audioObj.AddComponent<AudioSource>();

            // Save scene
            string path = $"{ScenesPath}/EndGame.unity";
            EditorSceneManager.SaveScene(scene, path);
            Log("EndGame scene created");
        }

        private void CreateEndGameConfig()
        {
            string path = $"{ConfigPath}/EndGameConfig.asset";
            
            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<BreakingHue.Core.EndGameConfig>(path);
            if (existing != null)
            {
                Log("EndGameConfig already exists, skipping");
                return;
            }

            var config = ScriptableObject.CreateInstance<BreakingHue.Core.EndGameConfig>();
            config.completionTitle = "Congratulations!";
            config.completionText = "You have completed Breaking Hue!\n\nThank you for playing!";
            config.endGameSceneName = "EndGame";
            config.mainMenuSceneName = "MainMenu";
            config.autoResetProgress = true;
            config.displayDelay = 0.5f;

            AssetDatabase.CreateAsset(config, path);
            Log("EndGameConfig asset created");
        }

        // ==================== TUTORIAL & PROMPTS SETUP ====================

        private void SetupTutorialAndPromptsInWorldScene()
        {
            string worldScenePath = $"{ScenesPath}/World.unity";
            
            if (!File.Exists(worldScenePath))
            {
                Log("ERROR: World scene not found. Please run 'Generate World Scene' first.");
                EditorUtility.DisplayDialog("Error", "World scene not found.\n\nPlease run 'Generate World Scene' first.", "OK");
                return;
            }

            try
            {
                // Open the World scene
                var scene = EditorSceneManager.OpenScene(worldScenePath, OpenSceneMode.Single);
                
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
                
                // Find or create Managers container
                var managersObj = GameObject.Find("Managers");
                if (managersObj == null)
                {
                    managersObj = new GameObject("Managers");
                    Log("Created Managers container");
                }

                // Setup TutorialManager
                SetupTutorialManager(managersObj, panelSettings);
                
                // Find or create UI container
                var uiContainer = GameObject.Find("UI");
                if (uiContainer == null)
                {
                    uiContainer = new GameObject("UI");
                    Log("Created UI container");
                }
                
                // Setup ContextualPromptController
                SetupContextualPromptController(uiContainer, panelSettings);
                
                // Setup PauseMenu
                SetupPauseMenu(uiContainer, panelSettings);
                
                // Setup InputManager
                SetupInputManager(managersObj);
                
                // Setup SelfDestructController
                SetupSelfDestructController(managersObj);
                
                // Setup ControlsBar (shows control hints and self-destruct progress)
                SetupControlsBar(uiContainer, panelSettings);
                
                // Setup EndGameManager (singleton that persists)
                SetupEndGameManager(managersObj);

                // Save the scene
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.Refresh();

                Log("Tutorial & Prompts setup complete in World scene!");
                EditorUtility.DisplayDialog("Setup Complete", 
                    "Tutorial and Contextual Prompts have been added to the World scene!\n\n" +
                    "Components added:\n" +
                    "• TutorialManager (under Managers)\n" +
                    "• TutorialPromptUI with UIDocument\n" +
                    "• ContextualPromptController with UIDocument\n" +
                    "• PauseMenuController with UIDocument\n" +
                    "• InputManager\n" +
                    "• SelfDestructController (R key / B button reset)\n" +
                    "• ControlsBarController (control hints + reset progress bar)\n" +
                    "• EndGameManager (singleton)",
                    "OK");
            }
            catch (System.Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Error", $"Failed to setup tutorial components:\n\n{ex.Message}", "OK");
            }
        }

        private void SetupTutorialManager(GameObject managersObj, PanelSettings panelSettings)
        {
            // Check if TutorialManager already exists
            var existingTutorialManager = Object.FindObjectOfType<TutorialManager>();
            if (existingTutorialManager != null)
            {
                Log("TutorialManager already exists, skipping");
                return;
            }

            // Create TutorialManager GameObject
            var tutorialManagerObj = new GameObject("TutorialManager");
            tutorialManagerObj.transform.SetParent(managersObj.transform);
            tutorialManagerObj.AddComponent<TutorialManager>();
            Log("Added TutorialManager");

            // Create TutorialPromptUI as child with UIDocument
            var tutorialPromptObj = new GameObject("TutorialPromptUI");
            tutorialPromptObj.transform.SetParent(tutorialManagerObj.transform);
            
            var tutorialUIDoc = tutorialPromptObj.AddComponent<UIDocument>();
            if (panelSettings != null)
            {
                tutorialUIDoc.panelSettings = panelSettings;
            }
            
            tutorialPromptObj.AddComponent<TutorialPromptUI>();
            Log("Added TutorialPromptUI with UIDocument");
        }

        private void SetupContextualPromptController(GameObject uiContainer, PanelSettings panelSettings)
        {
            // Check if ContextualPromptController already exists
            var existingController = Object.FindObjectOfType<ContextualPromptController>();
            if (existingController != null)
            {
                Log("ContextualPromptController already exists, skipping");
                return;
            }

            // Create ContextualPrompts GameObject
            var contextualPromptsObj = new GameObject("ContextualPrompts");
            contextualPromptsObj.transform.SetParent(uiContainer.transform);
            
            var contextualUIDoc = contextualPromptsObj.AddComponent<UIDocument>();
            if (panelSettings != null)
            {
                contextualUIDoc.panelSettings = panelSettings;
            }
            
            contextualPromptsObj.AddComponent<ContextualPromptController>();
            Log("Added ContextualPromptController with UIDocument");
        }

        private void SetupPauseMenu(GameObject uiContainer, PanelSettings panelSettings)
        {
            // Check if PauseMenuController already exists
            var existingPauseMenu = Object.FindObjectOfType<PauseMenuController>();
            if (existingPauseMenu != null)
            {
                Log("PauseMenuController already exists, skipping");
                return;
            }

            // Create PauseMenu GameObject
            var pauseMenuObj = new GameObject("PauseMenu");
            pauseMenuObj.transform.SetParent(uiContainer.transform);
            
            var pauseMenuDoc = pauseMenuObj.AddComponent<UIDocument>();
            if (panelSettings != null)
            {
                pauseMenuDoc.panelSettings = panelSettings;
            }
            
            pauseMenuObj.AddComponent<PauseMenuController>();
            Log("Added PauseMenuController with UIDocument");
        }

        private void SetupInputManager(GameObject managersObj)
        {
            // Check if InputManager already exists
            var existingInputManager = Object.FindObjectOfType<BreakingHue.Input.InputManager>();
            if (existingInputManager != null)
            {
                Log("InputManager already exists, skipping");
                return;
            }

            // Create InputManager GameObject
            var inputManagerObj = new GameObject("InputManager");
            inputManagerObj.transform.SetParent(managersObj.transform);
            inputManagerObj.AddComponent<BreakingHue.Input.InputManager>();
            Log("Added InputManager");
        }

        private void SetupSelfDestructController(GameObject managersObj)
        {
            // Check if SelfDestructController already exists
            var existingController = Object.FindObjectOfType<BreakingHue.Gameplay.SelfDestructController>();
            if (existingController != null)
            {
                Log("SelfDestructController already exists, skipping");
                return;
            }

            // Create SelfDestructController GameObject
            var selfDestructObj = new GameObject("SelfDestructController");
            selfDestructObj.transform.SetParent(managersObj.transform);
            selfDestructObj.AddComponent<BreakingHue.Gameplay.SelfDestructController>();
            Log("Added SelfDestructController (R key / B button to reset checkpoint)");
        }

        private void SetupControlsBar(GameObject uiContainer, PanelSettings panelSettings)
        {
            // Check if ControlsBarController already exists
            var existingController = Object.FindObjectOfType<ControlsBarController>();
            if (existingController != null)
            {
                Log("ControlsBarController already exists, skipping");
                return;
            }

            // Create ControlsBar GameObject
            var controlsBarObj = new GameObject("ControlsBar");
            controlsBarObj.transform.SetParent(uiContainer.transform);
            
            var controlsBarDoc = controlsBarObj.AddComponent<UIDocument>();
            if (panelSettings != null)
            {
                controlsBarDoc.panelSettings = panelSettings;
            }
            
            controlsBarObj.AddComponent<ControlsBarController>();
            Log("Added ControlsBarController (shows control hints and self-destruct progress)");
        }

        private void SetupEndGameManager(GameObject managersObj)
        {
            // Check if EndGameManager already exists
            var existingEndGameManager = Object.FindObjectOfType<EndGameManager>();
            if (existingEndGameManager != null)
            {
                Log("EndGameManager already exists, skipping");
                return;
            }

            // Create EndGameManager GameObject
            var endGameManagerObj = new GameObject("EndGameManager");
            endGameManagerObj.transform.SetParent(managersObj.transform);
            endGameManagerObj.AddComponent<EndGameManager>();
            Log("Added EndGameManager");
        }

        private void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.14f);
            cameraObj.transform.position = new Vector3(0, 0, -10);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            var uiObj = new GameObject("UI");
            var uiDoc = uiObj.AddComponent<UIDocument>();
            uiObj.AddComponent<BreakingHue.UI.MainMenuController>();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            var menuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/MainMenu.uxml");
            
            if (panelSettings != null) uiDoc.panelSettings = panelSettings;
            if (menuUxml != null) uiDoc.visualTreeAsset = menuUxml;

            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            string path = $"{ScenesPath}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            Log("MainMenu scene created");
        }

        private void CreateWorldScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera with GameCamera component for letterboxing
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 17f; // For 32x32 grid + padding
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObj.transform.position = new Vector3(0, 30, 0);
            cameraObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<BreakingHue.Camera.GameCamera>();

            // Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Floor (large plane)
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorObj.name = "Floor";
            floorObj.transform.position = new Vector3(0, -0.5f, 0);
            floorObj.transform.localScale = new Vector3(5, 1, 5);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Floor.mat");
            if (floorMat != null) floorObj.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Zenject SceneContext with GameInstaller
            var contextObj = new GameObject("SceneContext");
            var sceneContext = contextObj.AddComponent<SceneContext>();
            var gameInstaller = contextObj.AddComponent<BreakingHue.Installers.GameInstaller>();
            
            // Assign GameInstaller to SceneContext's installers array
            var soContext = new SerializedObject(sceneContext);
            var installersProperty = soContext.FindProperty("_monoInstallers");
            if (installersProperty == null)
            {
                installersProperty = soContext.FindProperty("_installers");
            }
            if (installersProperty != null && installersProperty.isArray)
            {
                installersProperty.arraySize = 1;
                installersProperty.GetArrayElementAtIndex(0).objectReferenceValue = gameInstaller;
                soContext.ApplyModifiedProperties();
            }
            
            // LevelManager
            var levelManagerObj = new GameObject("LevelManager");
            var levelManager = levelManagerObj.AddComponent<BreakingHue.Level.LevelManager>();
            
            // Try to assign existing GameConfig if available
            var existingConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            if (existingConfig != null)
            {
                var soLevelManager = new SerializedObject(levelManager);
                var configProp = soLevelManager.FindProperty("gameConfig");
                if (configProp != null)
                {
                    configProp.objectReferenceValue = existingConfig;
                    soLevelManager.ApplyModifiedProperties();
                }
            }

            // CheckpointManager
            var checkpointObj = new GameObject("CheckpointManager");
            checkpointObj.AddComponent<BreakingHue.Save.CheckpointManager>();

            // SelfDestructController (for R key / B button checkpoint reset)
            var selfDestructObj = new GameObject("SelfDestructController");
            selfDestructObj.AddComponent<BreakingHue.Gameplay.SelfDestructController>();

            // Level Container
            var levelContainer = new GameObject("LevelContainer");

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/HUD.uxml");

            // HUD
            var hudObj = new GameObject("HUD");
            var hudDoc = hudObj.AddComponent<UIDocument>();
            hudObj.AddComponent<BreakingHue.UI.GameHUDController>();
            
            if (panelSettings != null) hudDoc.panelSettings = panelSettings;
            if (hudUxml != null) hudDoc.visualTreeAsset = hudUxml;

            // Pause Menu
            var pauseMenuObj = new GameObject("PauseMenu");
            var pauseMenuDoc = pauseMenuObj.AddComponent<UIDocument>();
            pauseMenuObj.AddComponent<BreakingHue.UI.PauseMenuController>();
            
            if (panelSettings != null) pauseMenuDoc.panelSettings = panelSettings;

            // InputManager (for device detection and input handling)
            var inputManagerObj = new GameObject("InputManager");
            inputManagerObj.AddComponent<BreakingHue.Input.InputManager>();

            // Save scene
            string path = $"{ScenesPath}/World.unity";
            EditorSceneManager.SaveScene(scene, path);
            Log("World scene created");
        }

        // ==================== EXAMPLE LEVEL ====================

        private void GenerateExampleLevel()
        {
            CreateFolderIfNeeded(LevelsPath);
            
            // Create LevelData ScriptableObject
            var levelData = ScriptableObject.CreateInstance<LevelData>();
            levelData.levelName = "Tutorial";
            levelData.levelId = System.Guid.NewGuid().ToString();
            levelData.levelIndex = 0;

            // Initialize layers
            levelData.groundLayer = new GroundLayer { floorTiles = new List<Vector2Int>() };
            levelData.wallLayer = new WallLayer { wallTiles = new List<Vector2Int>() };
            levelData.barrierLayer = new BarrierLayer { barriers = new List<BarrierData>() };
            levelData.pickupLayer = new PickupLayer { pickups = new List<PickupData>() };
            levelData.barrelLayer = new BarrelLayer { barrels = new List<BarrelData>() };
            levelData.botLayer = new BotLayer { bots = new List<BotData>() };
            levelData.portalLayer = new PortalLayer { portals = new List<PortalData>() };
            levelData.hiddenAreaLayer = new HiddenAreaLayer { hiddenBlocks = new List<HiddenBlockData>() };

            // Create a simple tutorial level
            // Floor tiles (10x10 area)
            for (int x = 1; x < 11; x++)
            {
                for (int y = 1; y < 11; y++)
                {
                    levelData.groundLayer.floorTiles.Add(new Vector2Int(x, y));
                }
            }

            // Border walls
            for (int i = 0; i <= 11; i++)
            {
                levelData.wallLayer.wallTiles.Add(new Vector2Int(i, 0));
                levelData.wallLayer.wallTiles.Add(new Vector2Int(i, 11));
                levelData.wallLayer.wallTiles.Add(new Vector2Int(0, i));
                levelData.wallLayer.wallTiles.Add(new Vector2Int(11, i));
            }

            // Player spawn
            levelData.portalLayer.playerSpawnPosition = new Vector2Int(2, 2);

            // Red mask pickup
            levelData.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(4, 3),
                color = ColorType.Red,
                pickupId = System.Guid.NewGuid().ToString()
            });

            // Yellow mask pickup
            levelData.pickupLayer.pickups.Add(new PickupData
            {
                position = new Vector2Int(6, 3),
                color = ColorType.Yellow,
                pickupId = System.Guid.NewGuid().ToString()
            });

            // Red barrier
            levelData.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(5, 6),
                color = ColorType.Red
            });

            // Orange barrier (needs Red + Yellow)
            levelData.barrierLayer.barriers.Add(new BarrierData
            {
                position = new Vector2Int(5, 8),
                color = ColorType.Orange
            });

            // Exit portal
            levelData.portalLayer.portals.Add(new PortalData
            {
                portalId = System.Guid.NewGuid().ToString(),
                position = new Vector2Int(9, 9),
                isCheckpoint = true
            });

            // Save the level data
            string path = $"{LevelsPath}/TutorialLevel.asset";
            AssetDatabase.CreateAsset(levelData, path);
            
            Log("Example level created: TutorialLevel.asset");
        }

        // ==================== GAME CONFIG ====================

        private void GenerateGameConfig()
        {
            CreateFolderIfNeeded(ConfigPath);

            // Create or load existing config
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
            }

            // Ensure prefabs struct exists
            if (config.prefabs == null)
            {
                config.prefabs = new LevelPrefabs();
            }

            // Auto-populate prefabs
            config.prefabs.floorPrefab = LoadPrefabAsset("Floor");
            config.prefabs.wallPrefab = LoadPrefabAsset("Wall");
            config.prefabs.barrierPrefab = LoadPrefabAsset("ColorBarrier");
            config.prefabs.pickupPrefab = LoadPrefabAsset("MaskPickup");
            config.prefabs.barrelPrefab = LoadPrefabAsset("ExplodingBarrel");
            config.prefabs.botPrefab = LoadPrefabAsset("Bot");
            config.prefabs.portalPrefab = LoadPrefabAsset("Portal");
            config.prefabs.hiddenBlockPrefab = LoadPrefabAsset("HiddenBlock");
            config.prefabs.droppedMaskPrefab = LoadPrefabAsset("DroppedMask");
            config.prefabs.playerPrefab = LoadPrefabAsset("Player");

            // Auto-populate levels
            config.allLevels.Clear();
            string[] levelGuids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsPath });
            foreach (string guid in levelGuids)
            {
                string levelPath = AssetDatabase.GUIDToAssetPath(guid);
                var levelData = AssetDatabase.LoadAssetAtPath<LevelData>(levelPath);
                if (levelData != null)
                {
                    config.allLevels.Add(levelData);
                }
            }

            // Set starting level (prefer Tutorial)
            if (config.startingLevel == null && config.allLevels.Count > 0)
            {
                config.startingLevel = config.allLevels.Find(l => 
                    l.levelName.ToLower().Contains("tutorial")) ?? config.allLevels[0];
            }

            // Auto-populate portal links
            config.portalLinks.Clear();
            string[] linkGuids = AssetDatabase.FindAssets("t:EntranceExitLink");
            foreach (string guid in linkGuids)
            {
                string linkPath = AssetDatabase.GUIDToAssetPath(guid);
                var link = AssetDatabase.LoadAssetAtPath<EntranceExitLink>(linkPath);
                if (link != null)
                {
                    config.portalLinks.Add(link);
                }
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            // Update LevelManager in World scene if it exists
            ConnectConfigToWorldScene(config);

            Log($"GameConfig created with {config.allLevels.Count} levels and {config.portalLinks.Count} portal links");
        }

        private GameObject LoadPrefabAsset(string name)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private void ConnectConfigToWorldScene(GameConfig config)
        {
            string worldScenePath = $"{ScenesPath}/World.unity";
            if (!File.Exists(worldScenePath))
            {
                return; // Scene doesn't exist yet
            }

            // Open scene
            var scene = EditorSceneManager.OpenScene(worldScenePath, OpenSceneMode.Single);

            // Find LevelManager and assign config
            var levelManager = Object.FindObjectOfType<BreakingHue.Level.LevelManager>();
            if (levelManager != null)
            {
                var so = new SerializedObject(levelManager);
                var configProp = so.FindProperty("gameConfig");
                if (configProp != null)
                {
                    configProp.objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    Log("Assigned GameConfig to LevelManager");
                }
            }

            // Find SceneContext and ensure GameInstaller is assigned
            var sceneContext = Object.FindObjectOfType<SceneContext>();
            if (sceneContext != null)
            {
                var gameInstaller = sceneContext.GetComponent<BreakingHue.Installers.GameInstaller>();
                if (gameInstaller == null)
                {
                    gameInstaller = sceneContext.gameObject.AddComponent<BreakingHue.Installers.GameInstaller>();
                    Log("Added GameInstaller to SceneContext");
                }

                // Try to assign to installers array
                var so = new SerializedObject(sceneContext);
                var installersProperty = so.FindProperty("_monoInstallers");
                if (installersProperty == null)
                {
                    installersProperty = so.FindProperty("_installers");
                }

                if (installersProperty != null && installersProperty.isArray)
                {
                    bool hasInstaller = false;
                    for (int i = 0; i < installersProperty.arraySize; i++)
                    {
                        if (installersProperty.GetArrayElementAtIndex(i).objectReferenceValue == gameInstaller)
                        {
                            hasInstaller = true;
                            break;
                        }
                    }

                    if (!hasInstaller)
                    {
                        installersProperty.arraySize++;
                        installersProperty.GetArrayElementAtIndex(installersProperty.arraySize - 1).objectReferenceValue = gameInstaller;
                        so.ApplyModifiedProperties();
                        Log("Assigned GameInstaller to SceneContext");
                    }
                }
            }

            EditorSceneManager.SaveScene(scene);
        }

        // ==================== BUILD SETTINGS ====================

        private void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            
            string menuPath = $"{ScenesPath}/MainMenu.unity";
            string worldPath = $"{ScenesPath}/World.unity";
            string endGamePath = $"{ScenesPath}/EndGame.unity";

            if (File.Exists(menuPath))
                scenes.Add(new EditorBuildSettingsScene(menuPath, true));
            
            if (File.Exists(worldPath))
                scenes.Add(new EditorBuildSettingsScene(worldPath, true));

            if (File.Exists(endGamePath))
                scenes.Add(new EditorBuildSettingsScene(endGamePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Log($"Build settings updated ({scenes.Count} scenes: MainMenu, World" + (File.Exists(endGamePath) ? ", EndGame)" : ")"));
        }

        // ==================== DOCUMENTATION ====================

        private void GenerateDocumentation()
        {
            CreateFolderIfNeeded(DocumentationPath);

            string doc = @"# Breaking Hue - RYB Color Puzzle Game

## Overview

A puzzle game using the RYB (Red-Yellow-Blue) color model where players collect and combine masks to pass through color-coded barriers.

## Color System (RYB)

### Primary Colors
- **Red** - Primary
- **Yellow** - Primary  
- **Blue** - Primary

### Secondary Colors (combinations)
- **Orange** = Red + Yellow
- **Green** = Yellow + Blue
- **Purple** = Red + Blue

### Tertiary Color
- **Black** = Red + Yellow + Blue (all primaries)

## New Mechanics

### Multi-Mask Toggle System
- Press 1/2/3 to toggle individual masks active/inactive
- Multiple masks can be active simultaneously
- Combined colors are displayed on the player

### Residue System
When passing through barriers, only the required colors are consumed:
- Example: Purple mask (R+B) passing through Red barrier leaves Blue residue

### Barrels
- Blocks entities without matching mask color
- Explodes when entity with matching color enters
- Player death → return to checkpoint
- Bot death → drops remaining masks

### Bots
- Follow predefined paths
- Pick up masks (only colors they don't have)
- Can pass through barriers using same rules as player
- Stop when colliding with player

### Portals & Checkpoints
- Bidirectional portals connect areas/levels
- Checkpoint portals save game state
- Death returns player to last checkpoint

### Hidden Areas
- Dark gray blocks reveal secrets when entered
- No condition required - just touch them

## Controls

| Key | Action |
|-----|--------|
| WASD / Arrows | Move |
| 1, 2, 3 | Toggle mask slot active |
| Q | Drop first active mask |
| 0 / ` | Deactivate all masks |

## Level Editor

Open via **Window > Breaking Hue > Level Editor**

### Layers
1. Ground - Walkable floor tiles
2. Walls - Solid blocks
3. Barriers - Color-coded barriers
4. Pickups - Mask collectibles
5. Barrels - Exploding hazards
6. Bots - Enemy/helper bots
7. Bot Paths - Waypoint chains
8. Portals - Level connections
9. Hidden Areas - Secret blocks

## File Structure

```
Assets/
├── Scripts/
│   ├── Core/           # ColorType, MaskInventory
│   ├── Gameplay/       # Player, Barrier, Barrel, Bot, Portal
│   ├── Level/          # LevelManager, LevelData
│   ├── Save/           # CheckpointManager
│   ├── Camera/         # GameCamera
│   └── UI/             # HUD, MainMenu
├── Editor/
│   ├── GameSetupWindow.cs
│   └── LevelEditorWindow.cs
├── Materials/          # RYB color materials
├── Prefabs/            # All game prefabs
├── Levels/             # LevelData assets
├── Scenes/             # MainMenu, World
└── UI/                 # UXML, USS files
```

---

*Generated by Breaking Hue Game Setup Tool (RYB System)*
";

            string path = $"{DocumentationPath}/GameSystemGuide.md";
            File.WriteAllText(path, doc);
            AssetDatabase.Refresh();
            Log("Documentation generated");
        }
    }
}
#endif

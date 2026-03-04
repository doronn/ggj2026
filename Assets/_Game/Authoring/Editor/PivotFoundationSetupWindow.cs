using System.Collections.Generic;
using Ggj2026.Game.Shell.Bootstrap;
using Ggj2026.Game.Shell.Camera;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.World;
using Ggj2026.Game.Simulation.Authoring;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public class PivotFoundationSetupWindow : EditorWindow
    {
        private const string RuntimeConfigPath = "Assets/_Game/Authoring/World/RuntimeGameConfig.asset";
        private const string UxmlPath = "Assets/_Game/UI/Editor/UXML/PivotFoundationSetupWindow.uxml";
        private const string UssPath = "Assets/_Game/UI/Editor/USS/EditorWindows.uss";

        [MenuItem("Window/GGJ2026/Persistent World Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<PivotFoundationSetupWindow>("Persistent World Setup");
            window.minSize = new Vector2(440f, 220f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null && !rootVisualElement.styleSheets.Contains(uss))
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            if (uxml != null)
            {
                uxml.CloneTree(rootVisualElement);
            }
            else
            {
                BuildFallbackUi(rootVisualElement);
            }

            VisualElement buttonColumn = rootVisualElement.Q<VisualElement>("SetupButtonColumn");
            buttonColumn.Add(CreateButton("Create Runtime Config", () => CreateRuntimeConfig()));
            buttonColumn.Add(CreateButton("Generate Pivot Scenes", GenerateScenes));
            buttonColumn.Add(CreateButton("Update Build Settings", UpdateBuildSettings));
            buttonColumn.Add(CreateButton("Generate Foundation + UI", GenerateFoundationArtifacts));
        }

        public static void GenerateFoundationArtifacts()
        {
            GenerateScenes();
            UpdateBuildSettings();
            UiPlanInstallerService.ApplyInstall();
        }

        private static RuntimeGameConfig CreateRuntimeConfig()
        {
            RuntimeGameConfig config = AssetDatabase.LoadAssetAtPath<RuntimeGameConfig>(RuntimeConfigPath);
            if (config != null)
            {
                Selection.activeObject = config;
                return config;
            }

            config = CreateInstance<RuntimeGameConfig>();
            AssetDatabase.CreateAsset(config, RuntimeConfigPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            return config;
        }

        private static void GenerateScenes()
        {
            RuntimeGameConfig config = CreateRuntimeConfig();

            CreateBootstrapScene(config);
            CreatePersistentMainMenuScene(config);
            CreateClientWorldScene(config);
            CreateServerWorldScene();
            CreateToolsScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/PersistentMainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/ClientWorld.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/ServerWorld.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Tools.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", false),
                new EditorBuildSettingsScene("Assets/Scenes/World.unity", false),
                new EditorBuildSettingsScene("Assets/Scenes/EndGame.unity", false)
            };

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateBootstrapScene(RuntimeGameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateNetCodeBootstrapOverride("NetCodeBootstrapOverride");

            GameObject root = new GameObject("BootstrapRoot");
            var controller = root.AddComponent<BootstrapFlowController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("runtimeGameConfig").objectReferenceValue = config;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bootstrap.unity");
        }

        private static void CreatePersistentMainMenuScene(RuntimeGameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateNetCodeBootstrapOverride("NetCodeBootstrapOverride");

            GameObject menuRoot = new GameObject("PersistentMainMenuRoot");
            var controller = menuRoot.AddComponent<PersistentMainMenuController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("runtimeGameConfig").objectReferenceValue = config;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PersistentMainMenu.unity");
        }

        private static void CreateClientWorldScene(RuntimeGameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateNetCodeBootstrapOverride("NetCodeBootstrapOverride");

            UnityEngine.Camera camera = Object.FindFirstObjectByType<UnityEngine.Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 42f, -18f);
                camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
                camera.gameObject.AddComponent<StrategicCameraController>();
            }

            GameObject worldRoot = new GameObject("ClientWorldRoot");
            var bootstrap = worldRoot.AddComponent<OfflineWorldBootstrap>();
            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("runtimeGameConfig").objectReferenceValue = config;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            worldRoot.AddComponent<WorldSimulationBootstrapAuthoring>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ClientWorld.unity");
        }

        private static void CreateServerWorldScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateNetCodeBootstrapOverride("NetCodeBootstrapOverride");

            GameObject serverRoot = new GameObject("ServerWorldRoot");
            serverRoot.AddComponent<WorldSimulationBootstrapAuthoring>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ServerWorld.unity");
        }

        private static void CreateToolsScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CreateNetCodeBootstrapOverride("NetCodeBootstrapOverride");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Tools.unity");
        }

        private static void CreateNetCodeBootstrapOverride(string objectName)
        {
            GameObject overrideRoot = new GameObject(objectName);
            var bootstrapOverride = overrideRoot.AddComponent<OverrideAutomaticNetcodeBootstrap>();
            bootstrapOverride.ForceAutomaticBootstrapInScene = NetCodeConfig.AutomaticBootstrapSetting.DisableAutomaticBootstrap;
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            return new Button(onClick) { text = text };
        }

        private static void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1f;
            root.Add(container);
            container.Add(new Label("Persistent World Foundation"));
            container.Add(new VisualElement { name = "SetupButtonColumn" });
        }
    }
}

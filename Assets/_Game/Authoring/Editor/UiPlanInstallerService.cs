using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ggj2026.Game.Authoring.UI;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shell.Bootstrap;
using Ggj2026.Game.Shell.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public static class UiPlanInstallerService
    {
        public const string ManifestPath = "Assets/_Game/UI/UiPlanManifest.asset";
        public const string RuntimePanelSettingsPath = "Assets/_Game/UI/Runtime/PanelSettings/DefaultRuntimePanelSettings.asset";
        public const string LatestReportFileName = "ui-install-latest.json";

        private const string RuntimeHudUxmlPath = "Assets/_Game/UI/Runtime/UXML/ClientWorldHud.uxml";
        private const string RuntimeHudUssPath = "Assets/_Game/UI/Runtime/USS/ClientWorldHud.uss";
        private const string MainMenuUxmlPath = "Assets/_Game/UI/Runtime/UXML/PersistentMainMenu.uxml";
        private const string MainMenuUssPath = "Assets/_Game/UI/Runtime/USS/PersistentMainMenu.uss";
        private const string BootstrapUxmlPath = "Assets/_Game/UI/Runtime/UXML/BootstrapStatus.uxml";
        private const string BootstrapUssPath = "Assets/_Game/UI/Runtime/USS/BootstrapStatus.uss";

        public static UiPlanManifest GetOrCreateManifest()
        {
            UiPlanManifest manifest = AssetDatabase.LoadAssetAtPath<UiPlanManifest>(ManifestPath);
            if (manifest != null)
            {
                EnsureManifestDefaults(manifest);
                EditorUtility.SetDirty(manifest);
                return manifest;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath) ?? "Assets/_Game/UI");

            manifest = ScriptableObject.CreateInstance<UiPlanManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
            EnsureManifestDefaults(manifest);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return manifest;
        }

        public static UiInstallReport PreviewInstall()
        {
            return ExecuteInstall(dryRun: true, allowInteractivePrompts: true);
        }

        public static UiInstallReport ApplyInstall()
        {
            return ExecuteInstall(dryRun: false, allowInteractivePrompts: true);
        }

        public static UiInstallReport PreviewInstallNonInteractive()
        {
            return ExecuteInstall(dryRun: true, allowInteractivePrompts: false);
        }

        public static UiInstallReport ApplyInstallNonInteractive()
        {
            return ExecuteInstall(dryRun: false, allowInteractivePrompts: false);
        }

        public static string GetLatestReportPath()
        {
            return Path.Combine(GetDiagnosticsDirectoryPath(), LatestReportFileName);
        }

        private static UiInstallReport ExecuteInstall(bool dryRun, bool allowInteractivePrompts)
        {
            UiPlanManifest manifest = GetOrCreateManifest();
            EnsureRuntimePanelSettingsAsset(manifest, !dryRun);

            var report = new UiInstallReport
            {
                reportId = Guid.NewGuid().ToString("N"),
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                dryRun = dryRun,
                manifestPath = ManifestPath,
                summary = dryRun ? "Preview generated." : "Install applied."
            };

            var validator = new UiInstallValidationService();
            validator.Validate(report);

            string originalScenePath = SceneManager.GetActiveScene().path;
            bool saveChanges = !dryRun;

            try
            {
                if (saveChanges)
                {
                    if (allowInteractivePrompts)
                    {
                        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    }
                    else if (HasDirtyOpenScenes())
                    {
                        report.summary = "Install aborted because open scenes have unsaved changes.";
                        report.actions.Add(new UiInstallActionRecord
                        {
                            actionId = Guid.NewGuid().ToString("N"),
                            scope = "Preflight",
                            targetPath = string.Empty,
                            succeeded = false,
                            message = report.summary
                        });
                        WriteReport(report);
                        return report;
                    }
                }

                for (int i = 0; i < manifest.sceneBindings.Count; i++)
                {
                    ApplySceneBinding(manifest.sceneBindings[i], dryRun, report);
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScenePath) != null)
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            WriteReport(report);
            return report;
        }

        private static void ApplySceneBinding(UiSceneBindingDefinition sceneBinding, bool dryRun, UiInstallReport report)
        {
            if (string.IsNullOrWhiteSpace(sceneBinding.scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneBinding.scenePath) == null)
            {
                report.actions.Add(new UiInstallActionRecord
                {
                    actionId = Guid.NewGuid().ToString("N"),
                    scope = "SceneBinding",
                    targetPath = sceneBinding.scenePath,
                    succeeded = false,
                    message = "Scene path is missing or does not exist."
                });
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(sceneBinding.scenePath, OpenSceneMode.Single);
            bool sceneChanged = false;

            for (int i = 0; i < sceneBinding.documents.Count; i++)
            {
                UiDocumentBindingDefinition documentBinding = sceneBinding.documents[i];
                bool changed = EnsureDocumentBinding(scene, documentBinding, dryRun, report);
                sceneChanged |= changed;
            }

            if (!dryRun && sceneChanged)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static bool EnsureDocumentBinding(Scene scene, UiDocumentBindingDefinition binding, bool dryRun, UiInstallReport report)
        {
            GameObject host = FindRootObject(binding.hostObjectName);
            bool changed = false;

            if (host == null && !dryRun)
            {
                host = new GameObject(binding.hostObjectName);
                SceneManager.MoveGameObjectToScene(host, scene);
                changed = true;
            }

            if (host == null)
            {
                report.actions.Add(CreateAction("DocumentBinding", binding.scenePath, false, false, $"Host '{binding.hostObjectName}' would be created."));
                return true;
            }

            UIDocument uiDocument = host.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                if (!dryRun)
                {
                    uiDocument = host.AddComponent<UIDocument>();
                }

                changed = true;
                report.actions.Add(CreateAction("UIDocument", $"{binding.scenePath}:{binding.hostObjectName}", true, true, "UIDocument added or required."));
            }

            Type controllerType = ResolveType(binding.controllerTypeName);
            Component controller = controllerType != null ? host.GetComponent(controllerType) : null;
            if (controllerType != null && controller == null)
            {
                if (!dryRun)
                {
                    controller = host.AddComponent(controllerType);
                }

                changed = true;
                report.actions.Add(CreateAction("Controller", $"{binding.scenePath}:{binding.hostObjectName}", true, true, $"Controller '{binding.controllerTypeName}' added or required."));
            }

            if (!dryRun)
            {
                if (uiDocument != null && binding.panelSettings != null)
                {
                    uiDocument.panelSettings = binding.panelSettings;
                }

                if (controller != null)
                {
                    SerializedObject serializedObject = new SerializedObject(controller);
                    SetObjectReference(serializedObject, "visualTreeAsset", binding.visualTreeAsset);
                    SetObjectReference(serializedObject, "styleSheet", binding.styleSheet);
                    SetObjectReference(serializedObject, "panelSettings", binding.panelSettings);
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            report.actions.Add(CreateAction(
                "DocumentBinding",
                $"{binding.scenePath}:{binding.hostObjectName}",
                changed,
                true,
                dryRun ? "Previewed scene/document wiring." : "Applied scene/document wiring."));

            return changed;
        }

        private static UiInstallActionRecord CreateAction(string scope, string targetPath, bool changed, bool succeeded, string message)
        {
            return new UiInstallActionRecord
            {
                actionId = Guid.NewGuid().ToString("N"),
                scope = scope,
                targetPath = targetPath,
                changed = changed,
                succeeded = succeeded,
                message = message
            };
        }

        private static void EnsureManifestDefaults(UiPlanManifest manifest)
        {
            manifest.runtimePanels ??= new List<UiPanelAssetDefinition>();
            manifest.themes ??= new List<UiThemeDefinition>();
            manifest.sceneBindings ??= new List<UiSceneBindingDefinition>();
            manifest.editorWindows ??= new List<UiEditorWindowDefinition>();

            PanelSettings runtimePanelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelSettingsPath);
            VisualTreeAsset bootstrapUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BootstrapUxmlPath);
            StyleSheet bootstrapUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(BootstrapUssPath);
            VisualTreeAsset mainMenuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxmlPath);
            StyleSheet mainMenuUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(MainMenuUssPath);
            VisualTreeAsset hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RuntimeHudUxmlPath);
            StyleSheet hudUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimeHudUssPath);

            UpdatePanelDefinition(GetOrCreatePanel(manifest, "BootstrapStatus"), "Bootstrap Status", BootstrapUxmlPath, BootstrapUssPath, bootstrapUxml, bootstrapUss, runtimePanelSettings);
            UpdatePanelDefinition(GetOrCreatePanel(manifest, "PersistentMainMenu"), "Persistent Main Menu", MainMenuUxmlPath, MainMenuUssPath, mainMenuUxml, mainMenuUss, runtimePanelSettings);
            UpdatePanelDefinition(GetOrCreatePanel(manifest, "ClientWorldHud"), "Client World HUD", RuntimeHudUxmlPath, RuntimeHudUssPath, hudUxml, hudUss, runtimePanelSettings);

            UpdateDocumentBinding(
                GetOrCreateDocumentBinding(GetOrCreateSceneBinding(manifest, "Assets/Scenes/Bootstrap.unity"), "bootstrap-status"),
                "Assets/Scenes/Bootstrap.unity",
                "BootstrapStatusUI",
                typeof(BootstrapStatusPanelController).FullName,
                bootstrapUxml,
                bootstrapUss,
                runtimePanelSettings);

            UpdateDocumentBinding(
                GetOrCreateDocumentBinding(GetOrCreateSceneBinding(manifest, "Assets/Scenes/PersistentMainMenu.unity"), "persistent-main-menu"),
                "Assets/Scenes/PersistentMainMenu.unity",
                "PersistentMainMenuRoot",
                typeof(PersistentMainMenuController).FullName,
                mainMenuUxml,
                mainMenuUss,
                runtimePanelSettings);

            UpdateDocumentBinding(
                GetOrCreateDocumentBinding(GetOrCreateSceneBinding(manifest, "Assets/Scenes/ClientWorld.unity"), "client-world-hud"),
                "Assets/Scenes/ClientWorld.unity",
                "ClientWorldHud",
                typeof(ClientWorldHudController).FullName,
                hudUxml,
                hudUss,
                runtimePanelSettings);

            GetOrCreateEditorWindow(manifest, "ui-installer", "UI Plan Integration", typeof(UiPlanIntegrationWindow).FullName);
            GetOrCreateEditorWindow(manifest, "manual-test-runner", "Manual Test Runner", typeof(ManualTestRunnerWindow).FullName);
        }

        private static UiPanelAssetDefinition GetOrCreatePanel(UiPlanManifest manifest, string panelId)
        {
            for (int i = 0; i < manifest.runtimePanels.Count; i++)
            {
                if (manifest.runtimePanels[i].panel != null && manifest.runtimePanels[i].panel.panelId == panelId)
                {
                    return manifest.runtimePanels[i];
                }
            }

            var panel = new UiPanelAssetDefinition
            {
                panel = new UiPanelDefinition
                {
                    panelId = panelId
                }
            };
            manifest.runtimePanels.Add(panel);
            return panel;
        }

        private static void UpdatePanelDefinition(UiPanelAssetDefinition definition, string displayName, string uxmlPath, string ussPath, VisualTreeAsset uxml, StyleSheet uss, PanelSettings panelSettings)
        {
            definition.panel.displayName = displayName;
            definition.panel.uxmlPath = uxmlPath;
            definition.panel.ussPath = ussPath;
            definition.panel.panelSettingsPath = RuntimePanelSettingsPath;
            definition.visualTreeAsset = uxml;
            definition.styleSheet = uss;
            definition.panelSettings = panelSettings;
        }

        private static UiSceneBindingDefinition GetOrCreateSceneBinding(UiPlanManifest manifest, string scenePath)
        {
            for (int i = 0; i < manifest.sceneBindings.Count; i++)
            {
                if (manifest.sceneBindings[i].scenePath == scenePath)
                {
                    manifest.sceneBindings[i].documents ??= new List<UiDocumentBindingDefinition>();
                    return manifest.sceneBindings[i];
                }
            }

            var binding = new UiSceneBindingDefinition { scenePath = scenePath };
            manifest.sceneBindings.Add(binding);
            return binding;
        }

        private static UiDocumentBindingDefinition GetOrCreateDocumentBinding(UiSceneBindingDefinition sceneBinding, string bindingId)
        {
            for (int i = 0; i < sceneBinding.documents.Count; i++)
            {
                if (sceneBinding.documents[i].bindingId == bindingId)
                {
                    return sceneBinding.documents[i];
                }
            }

            var binding = new UiDocumentBindingDefinition { bindingId = bindingId };
            sceneBinding.documents.Add(binding);
            return binding;
        }

        private static void UpdateDocumentBinding(UiDocumentBindingDefinition binding, string scenePath, string hostObjectName, string controllerTypeName, VisualTreeAsset visualTreeAsset, StyleSheet styleSheet, PanelSettings panelSettings)
        {
            binding.scenePath = scenePath;
            binding.hostObjectName = hostObjectName;
            binding.controllerTypeName = controllerTypeName;
            binding.visualTreeAsset = visualTreeAsset;
            binding.styleSheet = styleSheet;
            binding.panelSettings = panelSettings;
        }

        private static void GetOrCreateEditorWindow(UiPlanManifest manifest, string windowId, string title, string controllerTypeName)
        {
            for (int i = 0; i < manifest.editorWindows.Count; i++)
            {
                if (manifest.editorWindows[i].windowId == windowId)
                {
                    manifest.editorWindows[i].title = title;
                    manifest.editorWindows[i].controllerTypeName = controllerTypeName;
                    return;
                }
            }

            manifest.editorWindows.Add(new UiEditorWindowDefinition
            {
                windowId = windowId,
                title = title,
                controllerTypeName = controllerTypeName
            });
        }

        private static void EnsureRuntimePanelSettingsAsset(UiPlanManifest manifest, bool allowCreate)
        {
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelSettingsPath);
            if (panelSettings == null && allowCreate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RuntimePanelSettingsPath) ?? "Assets/_Game/UI/Runtime/PanelSettings");
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, RuntimePanelSettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (panelSettings == null)
            {
                return;
            }

            EnsureManifestDefaults(manifest);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static GameObject FindRootObject(string objectName)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == objectName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void WriteReport(UiInstallReport report)
        {
            string diagnosticsDirectory = GetDiagnosticsDirectoryPath();
            Directory.CreateDirectory(diagnosticsDirectory);

            string latestPath = Path.Combine(diagnosticsDirectory, LatestReportFileName);
            string sessionPath = Path.Combine(diagnosticsDirectory, $"ui-install-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            string json = JsonUtility.ToJson(report, true);

            File.WriteAllText(latestPath, json);
            File.WriteAllText(sessionPath, json);
        }

        private static string GetDiagnosticsDirectoryPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Logs", "Diagnostics");
        }

        private static bool HasDirtyOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

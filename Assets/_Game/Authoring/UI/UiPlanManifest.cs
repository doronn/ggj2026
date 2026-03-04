using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.UI
{
    [Serializable]
    public class UiThemeDefinition
    {
        public string themeId;
        public StyleSheet sharedStyleSheet;
    }

    [Serializable]
    public class UiPanelAssetDefinition
    {
        public UiPanelDefinition panel = new UiPanelDefinition();
        public VisualTreeAsset visualTreeAsset;
        public StyleSheet styleSheet;
        public PanelSettings panelSettings;
    }

    [Serializable]
    public class UiControllerBindingDefinition
    {
        public string controllerTypeName;
        public string hostObjectName;
    }

    [Serializable]
    public class UiDocumentBindingDefinition
    {
        public string bindingId;
        public string scenePath;
        public string hostObjectName;
        public string controllerTypeName;
        public VisualTreeAsset visualTreeAsset;
        public StyleSheet styleSheet;
        public PanelSettings panelSettings;
    }

    [Serializable]
    public class UiSceneBindingDefinition
    {
        public string scenePath;
        public List<UiDocumentBindingDefinition> documents = new List<UiDocumentBindingDefinition>();
    }

    [Serializable]
    public class UiEditorWindowDefinition
    {
        public string windowId;
        public string title;
        public string controllerTypeName;
    }

    [CreateAssetMenu(fileName = "UiPlanManifest", menuName = "GGJ2026/UI/UI Plan Manifest")]
    public class UiPlanManifest : ScriptableObject
    {
        public string manifestVersion = "1.0";
        public List<UiPanelAssetDefinition> runtimePanels = new List<UiPanelAssetDefinition>();
        public List<UiThemeDefinition> themes = new List<UiThemeDefinition>();
        public List<UiSceneBindingDefinition> sceneBindings = new List<UiSceneBindingDefinition>();
        public List<UiEditorWindowDefinition> editorWindows = new List<UiEditorWindowDefinition>();
    }
}

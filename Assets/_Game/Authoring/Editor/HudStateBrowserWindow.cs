using System.Collections.Generic;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shell.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public sealed class HudStateBrowserWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/_Game/UI/Editor/UXML/HudStateBrowserWindow.uxml";
        private const string UssPath = "Assets/_Game/UI/Editor/USS/EditorWindows.uss";

        private Label _runtimeStatusLabel;
        private Label _selectionSummaryLabel;
        private Label _actionSummaryLabel;
        private Label _warningSummaryLabel;
        private Label _hudJsonLabel;
        private Label _actionsJsonLabel;

        [MenuItem("Window/GGJ2026/QA/HUD State Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<HudStateBrowserWindow>("HUD State Browser");
            window.minSize = new Vector2(720f, 520f);
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

            _runtimeStatusLabel = rootVisualElement.Q<Label>("RuntimeStatusLabel");
            _selectionSummaryLabel = rootVisualElement.Q<Label>("SelectionSummaryLabel");
            _actionSummaryLabel = rootVisualElement.Q<Label>("ActionSummaryLabel");
            _warningSummaryLabel = rootVisualElement.Q<Label>("WarningSummaryLabel");
            _hudJsonLabel = rootVisualElement.Q<Label>("HudJsonLabel");
            _actionsJsonLabel = rootVisualElement.Q<Label>("ActionsJsonLabel");

            VisualElement actionRow = rootVisualElement.Q<VisualElement>("ActionButtonRow");
            actionRow.Clear();
            actionRow.Add(new Button(RefreshNow) { text = "Refresh" });
            actionRow.Add(new Button(CopyHudJson) { text = "Copy HUD JSON" });
            actionRow.Add(new Button(CopyActionsJson) { text = "Copy Actions JSON" });

            rootVisualElement.schedule.Execute(RefreshNow).Every(500);
            RefreshNow();
        }

        private void RefreshNow()
        {
            ClientWorldHudController hud = ClientWorldHudController.Active;
            if (hud == null)
            {
                _runtimeStatusLabel.text = "Runtime HUD is not available. Enter Play Mode in ClientWorld to inspect live state.";
                _selectionSummaryLabel.text = "Selection: none";
                _actionSummaryLabel.text = "Actions: none";
                _warningSummaryLabel.text = "Parity note: MCP gameplay queries are only meaningful while the runtime HUD is active.";
                _hudJsonLabel.text = "{}";
                _actionsJsonLabel.text = "[]";
                return;
            }

            HumanAgentHudState state = hud.BuildHudState();
            List<GameActionDescriptor> actions = hud.BuildAvailableActions();

            _runtimeStatusLabel.text =
                $"Scene: {state.sceneName}\n" +
                $"Context: {state.selection.resolvedContextKind}\n" +
                $"Primary target: {state.selection.primaryTargetKind}\n" +
                $"Telemetry session: {state.sessionId}";
            _selectionSummaryLabel.text =
                $"Selection summary\n" +
                $"Cell: {(state.selection.hasSelectedCell ? state.selection.selectedCell.ToString() : "-")}\n" +
                $"Edge: {(state.selection.hasSelectedEdge ? state.selection.selectedEdge.ToString() : "-")}\n" +
                $"Entity: {(string.IsNullOrWhiteSpace(state.selection.selectedEntityId) ? "-" : state.selection.selectedEntityId)}";
            _actionSummaryLabel.text =
                $"Actions\n" +
                $"Available: {actions.Count}\n" +
                $"Visible context cards: {state.contextInspector.contextCards.Count}\n" +
                $"Visible task cards: {state.contextInspector.taskCards.Count}\n" +
                $"Visible entity cards: {state.contextInspector.entityCards.Count}\n" +
                $"Visible world cards: {state.contextInspector.worldCards.Count}";
            _warningSummaryLabel.text =
                $"Parity note\n" +
                $"This window reads the same HUD/query model that MCP gameplay queries expose.\n" +
                $"Use it to compare human-facing context with agent-facing state before changing commands or UI copy.";
            _hudJsonLabel.text = JsonUtility.ToJson(state, true);
            _actionsJsonLabel.text = JsonUtility.ToJson(new GameActionListWrapper { actions = actions }, true);
        }

        private void CopyHudJson()
        {
            EditorGUIUtility.systemCopyBuffer = _hudJsonLabel != null ? _hudJsonLabel.text : string.Empty;
        }

        private void CopyActionsJson()
        {
            EditorGUIUtility.systemCopyBuffer = _actionsJsonLabel != null ? _actionsJsonLabel.text : string.Empty;
        }

        private static void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.AddToClassList("editor-root");
            root.Add(container);

            container.Add(new Label("HUD State Browser") { name = "WindowTitleLabel" });
            container.Add(new Label("Compare the live human HUD state with the shared command/query surface used by MCP.") { name = "WindowSummaryLabel" });
            container.Add(new VisualElement { name = "ActionButtonRow" });
            container.Add(new Label { name = "RuntimeStatusLabel" });
            container.Add(new Label { name = "SelectionSummaryLabel" });
            container.Add(new Label { name = "ActionSummaryLabel" });
            container.Add(new Label { name = "WarningSummaryLabel" });
            container.Add(new Label { name = "HudJsonLabel" });
            container.Add(new Label { name = "ActionsJsonLabel" });
        }

        [System.Serializable]
        private sealed class GameActionListWrapper
        {
            public List<GameActionDescriptor> actions = new List<GameActionDescriptor>();
        }
    }
}

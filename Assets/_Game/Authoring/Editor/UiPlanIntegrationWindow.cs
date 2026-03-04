using System.IO;
using Ggj2026.Game.Authoring.UI;
using Ggj2026.Game.Shared.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public class UiPlanIntegrationWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/_Game/UI/Editor/UXML/UiPlanIntegrationWindow.uxml";
        private const string UssPath = "Assets/_Game/UI/Editor/USS/EditorWindows.uss";

        private Label _manifestPathLabel;
        private Label _latestReportLabel;
        private Label _reportSummaryLabel;

        [MenuItem("Window/GGJ2026/UI/Apply Planned UI")]
        public static void ShowWindow()
        {
            var window = GetWindow<UiPlanIntegrationWindow>("UI Plan Integration");
            window.minSize = new Vector2(560f, 360f);
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

            _manifestPathLabel = rootVisualElement.Q<Label>("ManifestPathLabel");
            _latestReportLabel = rootVisualElement.Q<Label>("LatestReportLabel");
            _reportSummaryLabel = rootVisualElement.Q<Label>("ReportSummaryLabel");

            VisualElement actionRow = rootVisualElement.Q<VisualElement>("ActionButtonRow");
            actionRow.Add(CreateButton("Create/Refresh Manifest", RefreshManifest));
            actionRow.Add(CreateButton("Dry Run", () => RunInstall(dryRun: true)));
            actionRow.Add(CreateButton("Apply UI", () => RunInstall(dryRun: false)));
            actionRow.Add(CreateButton("Open Latest Report", OpenLatestReport));
            actionRow.Add(CreateButton("Open Manual Tests", ManualTestRunnerWindow.ShowWindow));

            RefreshLabels(null);
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            return new Button(onClick) { text = text };
        }

        private void RefreshManifest()
        {
            UiPlanManifest manifest = UiPlanInstallerService.GetOrCreateManifest();
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            RefreshLabels("Manifest refreshed.");
        }

        private void RunInstall(bool dryRun)
        {
            UiInstallReport report = dryRun ? UiPlanInstallerService.PreviewInstall() : UiPlanInstallerService.ApplyInstall();
            RefreshLabels(report.summary);
        }

        private void OpenLatestReport()
        {
            string path = UiPlanInstallerService.GetLatestReportPath();
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                RefreshLabels("No UI install report has been written yet.");
            }
        }

        private void RefreshLabels(string message)
        {
            _manifestPathLabel.text = UiPlanInstallerService.ManifestPath;

            string latestPath = UiPlanInstallerService.GetLatestReportPath();
            _latestReportLabel.text = latestPath;

            if (!string.IsNullOrWhiteSpace(message))
            {
                _reportSummaryLabel.text = message;
                return;
            }

            if (File.Exists(latestPath))
            {
                _reportSummaryLabel.text = File.ReadAllText(latestPath);
            }
            else
            {
                _reportSummaryLabel.text = "No UI install report has been generated yet.";
            }
        }

        private static void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1f;
            root.Add(container);

            container.Add(new Label("UI Plan Integration"));
            container.Add(new Label { name = "ManifestPathLabel" });
            container.Add(new VisualElement { name = "ActionButtonRow" });
            container.Add(new Label { name = "LatestReportLabel" });
            var scroll = new ScrollView { name = "ReportScroll" };
            scroll.Add(new Label { name = "ReportSummaryLabel" });
            container.Add(scroll);
        }
    }
}

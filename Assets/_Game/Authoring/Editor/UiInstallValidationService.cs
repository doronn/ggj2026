using System;
using Ggj2026.Game.Authoring.UI;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using UnityEditor;

namespace Ggj2026.Game.Authoring.Editor
{
    public sealed class UiInstallValidationService : IUiInstallValidator
    {
        public void Validate(UiInstallReport report)
        {
            UiPlanManifest manifest = UiPlanInstallerService.GetOrCreateManifest();
            if (manifest == null)
            {
                report.validations.Add(CreateCheck("manifest.exists", false, InvariantSeverity.Error, "UI plan manifest is missing."));
                return;
            }

            report.validations.Add(CreateCheck("manifest.exists", true, InvariantSeverity.Info, "UI plan manifest exists."));
            report.validations.Add(CreateCheck("runtimePanels.exists", manifest.runtimePanels.Count > 0, InvariantSeverity.Warning, "Runtime panel definitions should exist."));
            report.validations.Add(CreateCheck("sceneBindings.exists", manifest.sceneBindings.Count > 0, InvariantSeverity.Warning, "Scene bindings should exist."));

            for (int i = 0; i < manifest.runtimePanels.Count; i++)
            {
                UiPanelAssetDefinition panel = manifest.runtimePanels[i];
                bool visualTreeAssigned = panel.visualTreeAsset != null;
                bool styleAssigned = panel.styleSheet != null;
                bool panelSettingsAssigned = panel.panelSettings != null;

                report.validations.Add(CreateCheck($"panel.uxml.{panel.panel.panelId}", visualTreeAssigned, InvariantSeverity.Warning, $"Panel '{panel.panel.panelId}' should have a UXML asset."));
                report.validations.Add(CreateCheck($"panel.uss.{panel.panel.panelId}", styleAssigned, InvariantSeverity.Warning, $"Panel '{panel.panel.panelId}' should have a USS asset."));
                report.validations.Add(CreateCheck($"panel.settings.{panel.panel.panelId}", panelSettingsAssigned, InvariantSeverity.Warning, $"Panel '{panel.panel.panelId}' should have PanelSettings."));
            }

            for (int i = 0; i < manifest.sceneBindings.Count; i++)
            {
                UiSceneBindingDefinition binding = manifest.sceneBindings[i];
                bool sceneExists = !string.IsNullOrWhiteSpace(binding.scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(binding.scenePath) != null;
                report.validations.Add(CreateCheck($"scene.exists.{i}", sceneExists, InvariantSeverity.Warning, $"Scene binding target '{binding.scenePath}' should exist."));
            }
        }

        private static InvariantCheckRecord CreateCheck(string checkId, bool passed, InvariantSeverity severity, string message)
        {
            return new InvariantCheckRecord
            {
                checkId = checkId,
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                passed = passed,
                severity = severity,
                message = message
            };
        }
    }
}

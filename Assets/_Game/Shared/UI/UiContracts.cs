using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Telemetry;

namespace Ggj2026.Game.Shared.UI
{
    [Serializable]
    public class UiPanelDefinition
    {
        public string panelId;
        public string displayName;
        public string uxmlPath;
        public string ussPath;
        public string panelSettingsPath;
    }

    [Serializable]
    public class UiInstallActionRecord
    {
        public string actionId;
        public string scope;
        public string targetPath;
        public string description;
        public bool changed;
        public bool succeeded = true;
        public string message;
    }

    [Serializable]
    public class UiInstallReport
    {
        public string reportId;
        public string generatedAtUtc;
        public bool dryRun;
        public string manifestPath;
        public string summary;
        public List<UiInstallActionRecord> actions = new List<UiInstallActionRecord>();
        public List<InvariantCheckRecord> validations = new List<InvariantCheckRecord>();
    }
}

using System;
using Ggj2026.Game.Shared.UI;
using UnityEditor;
using UnityEngine;

namespace Ggj2026.Game.Authoring.Editor.Mcp
{
    public static class UnityEditorMcpCommandLine
    {
        public static void ApplyUiPlanFromCommandLine()
        {
            bool dryRun = HasFlag("-mcpDryRun");
            UiInstallReport report = dryRun
                ? UiPlanInstallerService.PreviewInstallNonInteractive()
                : UiPlanInstallerService.ApplyInstallNonInteractive();

            string reportPath = UiPlanInstallerService.GetLatestReportPath();
            Debug.Log($"MCP UI plan {(dryRun ? "preview" : "apply")} completed. Latest report: {reportPath}");

            int exitCode = ReportHasFailure(report) ? 1 : 0;
            EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }

        private static bool ReportHasFailure(UiInstallReport report)
        {
            if (report == null)
            {
                return true;
            }

            for (int i = 0; i < report.actions.Count; i++)
            {
                if (!report.actions[i].succeeded)
                {
                    return true;
                }
            }

            for (int i = 0; i < report.validations.Count; i++)
            {
                if (!report.validations[i].passed && report.validations[i].severity == Ggj2026.Game.Shared.Telemetry.InvariantSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Ggj2026.Game.Authoring.Editor
{
    [InitializeOnLoad]
    public static class ProjectCompilationTools
    {
        private const string DiagnosticsDirectoryName = "Diagnostics";
        private const string LatestCompileLogFileName = "compile-latest.log";

        private const string LatestLogPathKey = "Ggj2026.ProjectCompilationTools.LatestLogPath";
        private const string SessionLogPathKey = "Ggj2026.ProjectCompilationTools.SessionLogPath";
        private const string PendingCompilationKey = "Ggj2026.ProjectCompilationTools.PendingCompilation";
        private const string BatchModeExitKey = "Ggj2026.ProjectCompilationTools.BatchModeExit";
        private const string ErrorCountKey = "Ggj2026.ProjectCompilationTools.ErrorCount";
        private const string WarningCountKey = "Ggj2026.ProjectCompilationTools.WarningCount";
        private const string StartedAtKey = "Ggj2026.ProjectCompilationTools.StartedAt";

        static ProjectCompilationTools()
        {
            EnsureDiagnosticsPlaceholder();

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        [MenuItem("Window/GGJ2026/Diagnostics/Refresh Assets And Recompile")]
        public static void RefreshAssetsAndRecompile()
        {
            BeginCompilationRequest(exitOnCompletion: false);
        }

        [MenuItem("Window/GGJ2026/Diagnostics/Open Latest Compile Report")]
        public static void OpenLatestCompileReport()
        {
            string path = GetLatestCompileReportPath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Compile report does not exist yet: {path}");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Window/GGJ2026/Diagnostics/Copy Latest Compile Report Path")]
        public static void CopyLatestCompileReportPath()
        {
            string path = GetLatestCompileReportPath();
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log($"Copied compile report path: {path}");
        }

        public static void RefreshAssetsAndRecompileFromCommandLine()
        {
            BeginCompilationRequest(exitOnCompletion: true);
        }

        private static void BeginCompilationRequest(bool exitOnCompletion)
        {
            InitializeCompilationReport("Explicit refresh/recompile request.", exitOnCompletion);
            WriteLog("INFO", "Refreshing assets and requesting clean script compilation.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            EditorApplication.delayCall += () =>
            {
                WriteLog("INFO", "Calling CompilationPipeline.RequestScriptCompilation(CleanBuildCache).");
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            };
        }

        private static void OnCompilationStarted(object context)
        {
            if (!SessionState.GetBool(PendingCompilationKey, false))
            {
                InitializeCompilationReport("Detected compilation started by Unity.", exitOnCompletion: false);
            }

            WriteLog("COMPILATION", "Compilation started.");
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!SessionState.GetBool(PendingCompilationKey, false))
            {
                return;
            }

            int errors = 0;
            int warnings = 0;

            foreach (CompilerMessage message in messages)
            {
                switch (message.type)
                {
                    case CompilerMessageType.Error:
                        errors++;
                        break;
                    case CompilerMessageType.Warning:
                        warnings++;
                        break;
                }

                string severity = message.type.ToString().ToUpperInvariant();
                WriteLog(severity, $"{message.file}({message.line},{message.column}): {message.message}");
            }

            SessionState.SetInt(ErrorCountKey, SessionState.GetInt(ErrorCountKey, 0) + errors);
            SessionState.SetInt(WarningCountKey, SessionState.GetInt(WarningCountKey, 0) + warnings);

            WriteLog("ASSEMBLY", $"Finished: {assemblyPath} | errors={errors} warnings={warnings}");
        }

        private static void OnCompilationFinished(object context)
        {
            if (!SessionState.GetBool(PendingCompilationKey, false))
            {
                return;
            }

            int totalErrors = SessionState.GetInt(ErrorCountKey, 0);
            int totalWarnings = SessionState.GetInt(WarningCountKey, 0);
            string startedAt = SessionState.GetString(StartedAtKey, string.Empty);

            WriteLog("SUMMARY", $"StartedAt={startedAt}");
            WriteLog("SUMMARY", $"FinishedAt={DateTime.Now:O}");
            WriteLog("SUMMARY", $"errors={totalErrors} warnings={totalWarnings}");
            WriteLog("SUMMARY", totalErrors == 0 ? "Compilation completed successfully." : "Compilation completed with errors.");

            bool exitOnCompletion = SessionState.GetBool(BatchModeExitKey, false);

            SessionState.SetBool(PendingCompilationKey, false);
            SessionState.SetBool(BatchModeExitKey, false);

            if (exitOnCompletion)
            {
                int exitCode = totalErrors == 0 ? 0 : 1;
                WriteLog("SESSION", $"Exiting Unity batchmode with code {exitCode}.");
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }

        private static void InitializeCompilationReport(string reason, bool exitOnCompletion)
        {
            string logDirectory = GetDiagnosticsDirectoryPath();
            Directory.CreateDirectory(logDirectory);

            string latestLogPath = Path.Combine(logDirectory, LatestCompileLogFileName);
            string sessionLogPath = Path.Combine(logDirectory, $"compile-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            SessionState.SetString(LatestLogPathKey, latestLogPath);
            SessionState.SetString(SessionLogPathKey, sessionLogPath);
            SessionState.SetBool(PendingCompilationKey, true);
            SessionState.SetBool(BatchModeExitKey, exitOnCompletion);
            SessionState.SetInt(ErrorCountKey, 0);
            SessionState.SetInt(WarningCountKey, 0);
            SessionState.SetString(StartedAtKey, DateTime.Now.ToString("O"));

            File.WriteAllText(latestLogPath, string.Empty);
            File.WriteAllText(sessionLogPath, string.Empty);

            WriteLog("SESSION", $"Compilation session created at {DateTime.Now:O}");
            WriteLog("INFO", $"Latest compile report: {latestLogPath}");
            WriteLog("INFO", $"Session compile report: {sessionLogPath}");
            WriteLog("INFO", reason);
        }

        private static void WriteLog(string level, string message)
        {
            string latestLogPath = SessionState.GetString(LatestLogPathKey, string.Empty);
            string sessionLogPath = SessionState.GetString(SessionLogPathKey, string.Empty);
            if (string.IsNullOrWhiteSpace(latestLogPath) || string.IsNullOrWhiteSpace(sessionLogPath))
            {
                return;
            }

            string line = $"[{DateTime.Now:O}] [{level}] {message}{Environment.NewLine}";

            File.AppendAllText(latestLogPath, line);
            File.AppendAllText(sessionLogPath, line);
        }

        private static string GetDiagnosticsDirectoryPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Logs", DiagnosticsDirectoryName);
        }

        private static void EnsureDiagnosticsPlaceholder()
        {
            string logDirectory = GetDiagnosticsDirectoryPath();
            Directory.CreateDirectory(logDirectory);

            string latestLogPath = Path.Combine(logDirectory, LatestCompileLogFileName);
            if (File.Exists(latestLogPath))
            {
                return;
            }

            File.WriteAllText(
                latestLogPath,
                $"[{DateTime.Now:O}] [INFO] No compile has been recorded yet.{Environment.NewLine}");
        }

        private static string GetLatestCompileReportPath()
        {
            return Path.Combine(GetDiagnosticsDirectoryPath(), LatestCompileLogFileName);
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ggj2026.Game.Shell.Diagnostics
{
    public sealed class RuntimeLogRecorder : MonoBehaviour
    {
        private static readonly object Sync = new object();
        private static bool _installed;
        private static string _latestLogPath;
        private static string _sessionLogPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;

            var gameObject = new GameObject("RuntimeLogRecorder");
            DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            gameObject.AddComponent<RuntimeLogRecorder>();
        }

        private void Awake()
        {
            InitializePaths();
            PrepareLogFiles();

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            WriteDirect("SESSION", $"Started {DateTime.Now:O}");
            WriteDirect("INFO", $"Latest runtime log: {_latestLogPath}");
            WriteDirect("INFO", $"Session runtime log: {_sessionLogPath}");
            WriteDirect("SCENE", $"Active scene: {SceneManager.GetActiveScene().name}");
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private static void InitializePaths()
        {
            if (!string.IsNullOrEmpty(_latestLogPath))
            {
                return;
            }

#if UNITY_EDITOR
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
            string root = Application.persistentDataPath;
#endif
            string logDirectory = Path.Combine(root, "Logs", "PersistentWorld");
            Directory.CreateDirectory(logDirectory);

            _latestLogPath = Path.Combine(logDirectory, "runtime-latest.log");
            _sessionLogPath = Path.Combine(logDirectory, $"runtime-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }

        private static void PrepareLogFiles()
        {
            lock (Sync)
            {
                File.WriteAllText(_latestLogPath, string.Empty);
                File.WriteAllText(_sessionLogPath, string.Empty);
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            WriteDirect("SCENE", $"Changed from '{previousScene.name}' to '{nextScene.name}'");
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            string entry =
                $"[{DateTime.Now:O}] [{type}] {condition}{Environment.NewLine}" +
                (string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : $"{stackTrace}{Environment.NewLine}") +
                Environment.NewLine;

            lock (Sync)
            {
                File.AppendAllText(_latestLogPath, entry);
                File.AppendAllText(_sessionLogPath, entry);
            }
        }

        private static void WriteDirect(string level, string message)
        {
            string entry = $"[{DateTime.Now:O}] [{level}] {message}{Environment.NewLine}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(_latestLogPath, entry);
                File.AppendAllText(_sessionLogPath, entry);
            }
        }
    }
}

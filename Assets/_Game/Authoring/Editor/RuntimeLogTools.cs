using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ggj2026.Game.Authoring.Editor
{
    public static class RuntimeLogTools
    {
        private const string LatestLogRelativePath = "Logs/PersistentWorld/runtime-latest.log";

        [MenuItem("Window/GGJ2026/Runtime Logs/Reveal Latest Log")]
        public static void RevealLatestLog()
        {
            string path = GetLatestLogPath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Runtime log does not exist yet: {path}");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Window/GGJ2026/Runtime Logs/Copy Latest Log Path")]
        public static void CopyLatestLogPath()
        {
            string path = GetLatestLogPath();
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log($"Copied runtime log path: {path}");
        }

        private static string GetLatestLogPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", LatestLogRelativePath));
        }
    }
}

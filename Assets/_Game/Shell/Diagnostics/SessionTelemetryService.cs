using System;
using System.IO;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ggj2026.Game.Shell.Diagnostics
{
    public sealed class SessionTelemetryService : MonoBehaviour, ISessionTelemetrySink
    {
        public sealed class TelemetryActionScope
        {
            public string actionId;
            public string startedAtUtc;
            public SessionActionKind actionKind;
            public SessionActorKind actorKind;
            public SessionUiSource uiSource;
            public string payloadJson;
            public WorldSnapshot beforeSnapshot;
            public string preSnapshotPath;
            public WorldStateDigest preDigest;
        }

        private readonly object _sync = new object();
        private WorldStateDiffService _worldStateDiffService;
        private RuntimeInvariantService _runtimeInvariantService;
        private string _sessionDirectory;
        private string _latestDirectory;
        private string _snapshotsDirectory;
        private string _diffsDirectory;
        private string _actionsPath;
        private string _uiActionsPath;
        private string _checksPath;
        private string _sessionPath;
        private string _summaryPath;
        private int _actionSequence;
        private TelemetrySessionMetadata _metadata;

        public static SessionTelemetryService Active { get; private set; }
        public string SessionId => _metadata != null ? _metadata.sessionId : string.Empty;
        public string CurrentSessionDirectory => _sessionDirectory;
        public string LastRecordedActionId { get; private set; }
        public string LastRecordedDiffPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Active != null)
            {
                return;
            }

            var gameObject = new GameObject("SessionTelemetryService");
            DontDestroyOnLoad(gameObject);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Active = gameObject.AddComponent<SessionTelemetryService>();
        }

        private void Awake()
        {
            _worldStateDiffService = new WorldStateDiffService();
            _runtimeInvariantService = new RuntimeInvariantService(this);
            InitializePaths();
            InitializeSession();

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Application.quitting += OnApplicationQuitting;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Application.quitting -= OnApplicationQuitting;
            if (Active == this)
            {
                Active = null;
            }
        }

        public TelemetryActionScope BeginWorldAction(SessionActionKind actionKind, SessionActorKind actorKind, SessionUiSource uiSource, string payloadJson, WorldSnapshot snapshot)
        {
            return new TelemetryActionScope
            {
                actionId = $"{++_actionSequence:0000}-{Guid.NewGuid():N}",
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                actionKind = actionKind,
                actorKind = actorKind,
                uiSource = uiSource ?? new SessionUiSource(),
                payloadJson = payloadJson ?? string.Empty,
                beforeSnapshot = CloneSnapshot(snapshot),
                preSnapshotPath = WriteSnapshot("pre", snapshot),
                preDigest = _worldStateDiffService.BuildDigest(snapshot)
            };
        }

        public void CompleteWorldAction(TelemetryActionScope scope, WorldSnapshot afterSnapshot, bool success, string message)
        {
            if (scope == null)
            {
                return;
            }

            WorldSnapshot afterClone = CloneSnapshot(afterSnapshot);
            string postSnapshotPath = WriteSnapshot("post", afterSnapshot);
            WorldStateDiff diff = _worldStateDiffService.CreateDiff(scope.beforeSnapshot, afterClone);
            string diffPath = WriteDiff(scope.actionId, diff);

            var record = new SessionActionRecord
            {
                sessionId = _metadata.sessionId,
                actionId = scope.actionId,
                startedAtUtc = scope.startedAtUtc,
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                actionKind = scope.actionKind,
                actorKind = scope.actorKind,
                result = success ? SessionActionResult.Succeeded : SessionActionResult.Failed,
                uiSource = scope.uiSource,
                payloadJson = scope.payloadJson,
                message = message,
                preSnapshotPath = scope.preSnapshotPath,
                postSnapshotPath = postSnapshotPath,
                diffPath = diffPath,
                preDigest = scope.preDigest,
                postDigest = diff.afterDigest
            };

            RecordAction(record);
            LastRecordedDiffPath = diffPath;
            _runtimeInvariantService.Evaluate(afterSnapshot, scope.actionId);
        }

        public void RecordUiAction(SessionActionKind actionKind, SessionActorKind actorKind, SessionUiSource uiSource, string payloadJson, string message, WorldSnapshot snapshot = null)
        {
            var digest = _worldStateDiffService.BuildDigest(snapshot);
            var record = new SessionActionRecord
            {
                sessionId = _metadata.sessionId,
                actionId = $"{++_actionSequence:0000}-{Guid.NewGuid():N}",
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                finishedAtUtc = DateTime.UtcNow.ToString("O"),
                actionKind = actionKind,
                actorKind = actorKind,
                result = SessionActionResult.Succeeded,
                uiSource = uiSource ?? new SessionUiSource(),
                payloadJson = payloadJson ?? string.Empty,
                message = message ?? string.Empty,
                preDigest = digest,
                postDigest = digest
            };

            RecordAction(record);
            WriteJsonLine(_uiActionsPath, record);
            LastRecordedDiffPath = string.Empty;
        }

        public void RecordAction(SessionActionRecord record)
        {
            if (record == null)
            {
                return;
            }

            _metadata.actionCount++;
            LastRecordedActionId = record.actionId;
            if (record.result == SessionActionResult.Failed)
            {
                _metadata.failedActionCount++;
            }

            WriteJsonLine(_actionsPath, record);
            WriteSessionFile();
            WriteSummaryFile(record.postDigest);
        }

        public void RecordInvariant(InvariantCheckRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (!record.passed)
            {
                if (record.severity == InvariantSeverity.Error)
                {
                    _metadata.invariantErrorCount++;
                }
                else
                {
                    _metadata.invariantWarningCount++;
                }
            }

            WriteJsonLine(_checksPath, record);
            WriteSessionFile();
        }

        public void RecordLifecycle(SessionActionKind actionKind, string payloadJson, string message, WorldSnapshot snapshot = null)
        {
            RecordUiAction(actionKind, SessionActorKind.System, new SessionUiSource { controller = nameof(SessionTelemetryService) }, payloadJson, message, snapshot);
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            _metadata.activeScene = nextScene.name;
            RecordLifecycle(SessionActionKind.SceneChanged, $"{{\"from\":\"{previousScene.name}\",\"to\":\"{nextScene.name}\"}}", $"Scene changed from {previousScene.name} to {nextScene.name}.");
        }

        private void OnApplicationQuitting()
        {
            _metadata.endedAtUtc = DateTime.UtcNow.ToString("O");
            RecordLifecycle(SessionActionKind.SessionEnded, string.Empty, "Session ended.");
            WriteSessionFile();
        }

        private void InitializePaths()
        {
            string root = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                : Application.persistentDataPath;
            string telemetryRoot = Path.Combine(root, "Logs", "Telemetry");

            Directory.CreateDirectory(telemetryRoot);
            _latestDirectory = Path.Combine(telemetryRoot, "session-latest");
            _sessionDirectory = Path.Combine(telemetryRoot, $"session-{DateTime.Now:yyyyMMdd-HHmmss}");
            _snapshotsDirectory = Path.Combine(_sessionDirectory, "snapshots");
            _diffsDirectory = Path.Combine(_sessionDirectory, "diffs");
            _actionsPath = Path.Combine(_sessionDirectory, "actions.jsonl");
            _uiActionsPath = Path.Combine(_sessionDirectory, "ui-actions.jsonl");
            _checksPath = Path.Combine(_sessionDirectory, "checks.jsonl");
            _sessionPath = Path.Combine(_sessionDirectory, "session.json");
            _summaryPath = Path.Combine(_sessionDirectory, "summary.md");

            Directory.CreateDirectory(_sessionDirectory);
            Directory.CreateDirectory(_latestDirectory);
            Directory.CreateDirectory(_snapshotsDirectory);
            Directory.CreateDirectory(_diffsDirectory);
        }

        private void InitializeSession()
        {
            _metadata = new TelemetrySessionMetadata
            {
                sessionId = Guid.NewGuid().ToString("N"),
                sessionKind = Application.isEditor ? "EditorPlayMode" : "BuildRuntime",
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                activeScene = SceneManager.GetActiveScene().name
            };

            WriteSessionFile();
            RecordLifecycle(SessionActionKind.SessionStarted, string.Empty, "Session started.");
        }

        private void WriteSessionFile()
        {
            lock (_sync)
            {
                string json = JsonUtility.ToJson(_metadata, true);
                File.WriteAllText(_sessionPath, json);
                File.WriteAllText(Path.Combine(_latestDirectory, "session.json"), json);
            }
        }

        private void WriteSummaryFile(WorldStateDigest finalDigest)
        {
            var summary = new TelemetrySessionSummary
            {
                sessionId = _metadata.sessionId,
                summaryText = "Structured telemetry session summary.",
                totalActions = _metadata.actionCount,
                totalFailures = _metadata.failedActionCount,
                totalWarnings = _metadata.invariantWarningCount,
                totalErrors = _metadata.invariantErrorCount,
                finalDigest = finalDigest ?? new WorldStateDigest()
            };

            string markdown =
                $"# Session Summary{Environment.NewLine}{Environment.NewLine}" +
                $"- Session: `{summary.sessionId}`{Environment.NewLine}" +
                $"- Actions: `{summary.totalActions}`{Environment.NewLine}" +
                $"- Failures: `{summary.totalFailures}`{Environment.NewLine}" +
                $"- Invariant warnings: `{summary.totalWarnings}`{Environment.NewLine}" +
                $"- Invariant errors: `{summary.totalErrors}`{Environment.NewLine}" +
                $"- Final tick: `{summary.finalDigest.simulationTick}`{Environment.NewLine}";

            lock (_sync)
            {
                File.WriteAllText(_summaryPath, markdown);
                File.WriteAllText(Path.Combine(_latestDirectory, "summary.md"), markdown);
            }
        }

        private string WriteSnapshot(string prefix, WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            string fileName = $"{_actionSequence:0000}-{prefix}.json";
            string fullPath = Path.Combine(_snapshotsDirectory, fileName);
            string json = JsonUtility.ToJson(snapshot, true);

            lock (_sync)
            {
                File.WriteAllText(fullPath, json);
                File.WriteAllText(Path.Combine(_latestDirectory, "snapshot-latest.json"), json);
            }

            return fullPath;
        }

        private string WriteDiff(string actionId, WorldStateDiff diff)
        {
            string fileName = $"{_actionSequence:0000}-{actionId}.json";
            string fullPath = Path.Combine(_diffsDirectory, fileName);
            string json = JsonUtility.ToJson(diff, true);

            lock (_sync)
            {
                File.WriteAllText(fullPath, json);
                File.WriteAllText(Path.Combine(_latestDirectory, "diff-latest.json"), json);
            }

            return fullPath;
        }

        private static WorldSnapshot CloneSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(snapshot));
        }

        private void WriteJsonLine(string path, object payload)
        {
            if (string.IsNullOrEmpty(path) || payload == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(payload);
            lock (_sync)
            {
                File.AppendAllText(path, json + Environment.NewLine);
                string latestPath = Path.Combine(_latestDirectory, Path.GetFileName(path));
                File.AppendAllText(latestPath, json + Environment.NewLine);
            }
        }
    }
}

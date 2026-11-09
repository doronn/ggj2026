using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ggj2026.Game.Authoring.ManualTests;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shell.Diagnostics;
using Ggj2026.Game.Shell.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using ApiTestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;

namespace Ggj2026.Game.Authoring.Editor.Mcp
{
    [InitializeOnLoad]
    public static class UnityEditorMcpBridge
    {
        private const double PollIntervalSeconds = 0.20d;
        private const double HeartbeatIntervalSeconds = 1.0d;
        private const string BridgeVersion = "1.0.0";
        private const string ActiveTestRequestIdKey = "Ggj2026.Mcp.ActiveTest.RequestId";
        private const string ActiveTestPlatformKey = "Ggj2026.Mcp.ActiveTest.Platform";
        private const string ActiveTestStartedAtKey = "Ggj2026.Mcp.ActiveTest.StartedAtUtc";
        private const string ActiveTestResultsPathKey = "Ggj2026.Mcp.ActiveTest.ResultsPath";
        private const string ActiveTestJobIdKey = "Ggj2026.Mcp.ActiveTest.JobId";
        private const string PendingPlayModeRequestIdKey = "Ggj2026.Mcp.PendingPlayMode.RequestId";
        private const string PendingPlayModeTargetKey = "Ggj2026.Mcp.PendingPlayMode.Target";
        private const string PendingPlayModeStartedAtKey = "Ggj2026.Mcp.PendingPlayMode.StartedAt";
        private const double OrphanedTestRecoveryDelaySeconds = 2.0d;
        private const double OrphanedTestRecoveryTimeoutSeconds = 30.0d;
        private const double PlayModeTransitionTimeoutSeconds = 20.0d;

        private static readonly string ProjectRoot;
        private static readonly string BridgeRoot;
        private static readonly string RequestsDirectory;
        private static readonly string ResponsesDirectory;
        private static readonly string StatusPath;
        private static readonly string TestResultsDirectory;
        private static readonly bool IsAuxiliaryProcess;

        private static double _nextPollAt;
        private static double _nextHeartbeatAt;
        private static EditorMcpTestRunCallbacks _activeTestCallbacks;

        static UnityEditorMcpBridge()
        {
            IsAuxiliaryProcess = DetectAuxiliaryProcess();
            if (IsAuxiliaryProcess)
            {
                return;
            }

            ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            BridgeRoot = Path.Combine(ProjectRoot, "Logs", "McpBridge");
            RequestsDirectory = Path.Combine(BridgeRoot, "requests");
            ResponsesDirectory = Path.Combine(BridgeRoot, "responses");
            StatusPath = Path.Combine(BridgeRoot, "status.json");
            TestResultsDirectory = Path.Combine(ProjectRoot, "Logs", "TestResults");

            EnsureDirectories();
            WriteStatus();

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorUpdate()
        {
            if (IsAuxiliaryProcess)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;

            if (now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + HeartbeatIntervalSeconds;
                WriteStatus();
            }

            ProcessPendingPlayModeTransition(now);

            if (now >= _nextPollAt)
            {
                _nextPollAt = now + PollIntervalSeconds;
                ProcessPendingRequests();
            }

            RecoverOrphanedTestRunIfNeeded();
        }

        private static void OnEditorQuitting()
        {
            if (IsAuxiliaryProcess)
            {
                return;
            }

            WriteStatus(isShuttingDown: true);
        }

        private static void ProcessPendingRequests()
        {
            string[] requestPaths = Directory.GetFiles(RequestsDirectory, "*.json");
            Array.Sort(requestPaths, StringComparer.Ordinal);

            for (int i = 0; i < requestPaths.Length; i++)
            {
                string requestPath = requestPaths[i];
                EditorMcpBridgeRequest request = null;

                try
                {
                    request = JsonUtility.FromJson<EditorMcpBridgeRequest>(File.ReadAllText(requestPath));
                    if (request == null || string.IsNullOrWhiteSpace(request.requestId) || string.IsNullOrWhiteSpace(request.command))
                    {
                        WriteResponse(new EditorMcpBridgeResponse
                        {
                            requestId = request != null ? request.requestId : string.Empty,
                            success = false,
                            error = "Malformed MCP bridge request.",
                            respondedAtUtc = DateTime.UtcNow.ToString("O")
                        });
                    }
                    else
                    {
                        EditorMcpBridgeResponse response = HandleRequest(request);
                        if (response != null)
                        {
                            WriteResponse(response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(new EditorMcpBridgeResponse
                    {
                        requestId = request != null ? request.requestId : Path.GetFileNameWithoutExtension(requestPath),
                        success = false,
                        error = ex.ToString(),
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                }
                finally
                {
                    if (File.Exists(requestPath))
                    {
                        File.Delete(requestPath);
                    }
                }
            }
        }

        private static EditorMcpBridgeResponse HandleRequest(EditorMcpBridgeRequest request)
        {
            switch (request.command)
            {
                case "status":
                    return Success(request, BuildStatusPayloadJson());

                case "refresh_recompile":
                    if (EditorApplication.isCompiling)
                    {
                        return Failure(request, "Unity is already compiling.");
                    }

                    ProjectCompilationTools.RefreshAssetsAndRecompile();
                    return Success(request, BuildStatusPayloadJson(), "Compilation requested.");

                case "apply_ui_plan":
                {
                    if (HasDirtyOpenScenes())
                    {
                        return Failure(request, "Cannot apply the UI plan while open scenes have unsaved changes.");
                    }

                    EditorMcpApplyUiPayload payload = ParsePayload<EditorMcpApplyUiPayload>(request.payloadJson);
                    UiInstallReport report = payload != null && payload.dryRun
                        ? UiPlanInstallerService.PreviewInstallNonInteractive()
                        : UiPlanInstallerService.ApplyInstallNonInteractive();
                    return Success(request, JsonUtility.ToJson(report, true), report.summary);
                }

                case "polygon_visual_maintenance":
                {
                    EditorMcpPolygonVisualMaintenancePayload payload = ParsePayload<EditorMcpPolygonVisualMaintenancePayload>(request.payloadJson);
                    bool importPackages = payload != null && payload.importPackages;
                    bool repairMaterials = payload == null || payload.repairMaterials || !PayloadContainsObject(request.payloadJson, "repairMaterials");
                    bool refreshVisualLibrary = payload == null || payload.refreshVisualLibrary || !PayloadContainsObject(request.payloadJson, "refreshVisualLibrary");
                    bool assignLibraryToRenderers = payload == null || payload.assignLibraryToRenderers || !PayloadContainsObject(request.payloadJson, "assignLibraryToRenderers");
                    string sourceRoot = payload != null ? payload.sourceRoot : string.Empty;

                    PolygonAssetIntegrationWindow.PolygonMaintenanceReport report =
                        PolygonAssetIntegrationWindow.RunMaintenanceNonInteractive(
                            importPackages,
                            repairMaterials,
                            refreshVisualLibrary,
                            assignLibraryToRenderers,
                            sourceRoot);

                    string reportJson = JsonUtility.ToJson(report, true);
                    if (!report.success)
                    {
                        return Failure(request, report.summary, reportJson);
                    }

                    return Success(request, reportJson, report.summary);
                }

                case "open_scene":
                {
                    if (HasDirtyOpenScenes())
                    {
                        return Failure(request, "Cannot open a scene while open scenes have unsaved changes.");
                    }

                    EditorMcpScenePayload payload = ParsePayload<EditorMcpScenePayload>(request.payloadJson);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.scenePath))
                    {
                        return Failure(request, "scenePath is required.");
                    }

                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(payload.scenePath) == null)
                    {
                        return Failure(request, $"Scene does not exist: {payload.scenePath}");
                    }

                    Scene scene = EditorSceneManager.OpenScene(payload.scenePath, OpenSceneMode.Single);
                    return Success(request, JsonUtility.ToJson(new EditorMcpSceneResult
                    {
                        scenePath = scene.path,
                        sceneName = scene.name
                    }, true), $"Opened scene {scene.name}.");
                }

                case "run_tests":
                {
                    EditorMcpRunTestsPayload payload = ParsePayload<EditorMcpRunTestsPayload>(request.payloadJson) ?? new EditorMcpRunTestsPayload();
                    return StartTestRun(request, payload);
                }

                case "cancel_tests":
                    return CancelActiveTestRun(request);

                case "enter_play_mode":
                {
                    if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        if (EditorApplication.isPaused)
                        {
                            EditorApplication.isPaused = false;
                        }

                        return Success(request, BuildStatusPayloadJson(), "Editor is already in play mode.");
                    }

                    if (HasDirtyOpenScenes())
                    {
                        return Failure(request, "Cannot enter play mode while open scenes have unsaved changes.");
                    }

                    EditorMcpScenePayload payload = ParsePayload<EditorMcpScenePayload>(request.payloadJson);
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.scenePath))
                    {
                        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(payload.scenePath) == null)
                        {
                            return Failure(request, $"Scene does not exist: {payload.scenePath}");
                        }

                        EditorSceneManager.OpenScene(payload.scenePath, OpenSceneMode.Single);
                    }

                    EditorApplication.isPaused = false;
                    SetPendingPlayModeTransition(request.requestId, true, EditorApplication.timeSinceStartup);
                    EditorApplication.isPlaying = true;
                    return null;
                }

                case "exit_play_mode":
                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        return Success(request, BuildStatusPayloadJson(), "Editor is not in play mode.");
                    }

                    SetPendingPlayModeTransition(request.requestId, false, EditorApplication.timeSinceStartup);
                    EditorApplication.isPlaying = false;
                    return null;

                case "list_manual_tests":
                    return Success(request, BuildManualTestsPayloadJson());

                case "manual_test_status":
                    return Success(request, BuildManualTestStatusPayloadJson());

                case "start_manual_test":
                {
                    EditorMcpManualTestStartPayload payload = ParsePayload<EditorMcpManualTestStartPayload>(request.payloadJson);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.scenarioId))
                    {
                        return Failure(request, "Manual test scenarioId is required.");
                    }

                    if (!ManualTestExecutionService.TryStartRun(payload.scenarioId, out ManualTestExecutionService.ManualTestExecutionStatus status, out string error))
                    {
                        return Failure(request, error);
                    }

                    return Success(request, JsonUtility.ToJson(status, true), $"Started manual test {payload.scenarioId}.");
                }

                case "record_manual_test_step":
                {
                    EditorMcpManualTestStepPayload payload = ParsePayload<EditorMcpManualTestStepPayload>(request.payloadJson);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.result))
                    {
                        return Failure(request, "Manual test step result is required.");
                    }

                    if (!ManualTestExecutionService.TryRecordCurrentStep(payload.result, payload.note ?? string.Empty, out ManualTestExecutionService.ManualTestExecutionStatus status, out string error))
                    {
                        return Failure(request, error);
                    }

                    return Success(request, JsonUtility.ToJson(status, true), $"Recorded manual test step as {payload.result}.");
                }

                case "finish_manual_test":
                    if (!ManualTestExecutionService.TryFinishRun(out ManualTestRunRecord completedRun, out string finishError))
                    {
                        return Failure(request, finishError);
                    }

                    return Success(request, JsonUtility.ToJson(completedRun, true), $"Finished manual test {completedRun.scenarioId}.");

                case "game_hud_state":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(hud.BuildHudState(), true), "Read runtime HUD state.");
                }

                case "game_world_summary":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(new EditorMcpGameWorldSummaryPayload
                    {
                        worldBar = hud.BuildHudState().worldBar
                    }, true), "Read world summary.");
                }

                case "game_selection":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(hud.BuildSelectionState(), true), "Read gameplay selection state.");
                }

                case "game_hover":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    GameSelectionState selection = hud.BuildSelectionState();
                    return Success(request, JsonUtility.ToJson(new EditorMcpGameHoverPayload
                    {
                        hasHoveredCell = selection.hasHoveredCell,
                        hoveredCell = selection.hoveredCell,
                        hasHoveredEdge = selection.hasHoveredEdge,
                        hoveredEdge = selection.hoveredEdge
                    }, true), "Read gameplay hover state.");
                }

                case "game_list_entities":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(new EditorMcpGameEntitiesPayload
                    {
                        entities = hud.BuildEntitySummaries()
                    }, true), "Listed gameplay entities.");
                }

                case "game_list_actions":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(new EditorMcpGameActionsPayload
                    {
                        actions = hud.BuildAvailableActions()
                    }, true), "Listed gameplay actions.");
                }

                case "game_action_schema":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    EditorMcpGameActionSchemaPayload payload = ParsePayload<EditorMcpGameActionSchemaPayload>(request.payloadJson);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.actionId) || !Enum.TryParse(payload.actionId, true, out GameActionId actionId))
                    {
                        return Failure(request, "A valid actionId is required.");
                    }

                    return Success(request, JsonUtility.ToJson(hud.GetActionSchema(actionId), true), $"Read schema for {actionId}.");
                }

                case "game_blockers":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    return Success(request, JsonUtility.ToJson(new EditorMcpGameBlockersPayload
                    {
                        blockers = hud.BuildBlockerSummaries()
                    }, true), "Read gameplay blockers.");
                }

                case "game_recent_events":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    HumanAgentHudState hudState = hud.BuildHudState();
                    return Success(request, JsonUtility.ToJson(new EditorMcpGameRecentEventsPayload
                    {
                        eventStrip = hudState.eventStrip,
                        sessionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.SessionId : string.Empty,
                        latestActionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.LastRecordedActionId : string.Empty,
                        latestDiffPath = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.LastRecordedDiffPath : string.Empty
                    }, true), "Read recent gameplay events.");
                }

                case "game_execute_action":
                {
                    if (!TryGetRuntimeHud(out ClientWorldHudController hud, out string hudError))
                    {
                        return Failure(request, hudError);
                    }

                    EditorMcpGameActionPayload payload = ParsePayload<EditorMcpGameActionPayload>(request.payloadJson);
                    if (payload == null)
                    {
                        return Failure(request, "Gameplay action payload is required.");
                    }

                    if (!TryBuildGameActionEnvelope(payload, request.payloadJson, out GameActionEnvelope envelope, out string actionError))
                    {
                        return Failure(request, actionError);
                    }

                    if (envelope.actionId == GameActionId.SetRealtimeRunning && envelope.desiredRunningState && EditorApplication.isPaused)
                    {
                        EditorApplication.isPaused = false;
                    }

                    GameActionResult result = hud.ExecuteActionAsync(envelope).GetAwaiter().GetResult();
                    string resultJson = JsonUtility.ToJson(result, true);
                    if (result.success)
                        return Success(request, resultJson, result.message);
                    return Failure(request, result.message, resultJson);
                }

                default:
                    return Failure(request, $"Unsupported bridge command: {request.command}");
            }
        }

        private static EditorMcpBridgeResponse CancelActiveTestRun(EditorMcpBridgeRequest request)
        {
            string jobId = SessionState.GetString(ActiveTestJobIdKey, string.Empty);
            bool canceled = false;

            if (!string.IsNullOrWhiteSpace(jobId))
            {
                canceled = TestRunnerApi.CancelTestRun(jobId);
            }

            if (_activeTestCallbacks != null)
            {
                TestRunnerApi.UnregisterTestCallback(_activeTestCallbacks);
                UnityEngine.Object.DestroyImmediate(_activeTestCallbacks);
                _activeTestCallbacks = null;
            }

            ClearActiveTestRunState();
            return Success(
                request,
                JsonUtility.ToJson(new EditorMcpCancelTestsResult
                {
                    hadJobId = !string.IsNullOrWhiteSpace(jobId),
                    canceled = canceled
                }, true),
                canceled ? "Canceled active Unity test run." : "Cleared active Unity test run state.");
        }

        private static EditorMcpBridgeResponse StartTestRun(EditorMcpBridgeRequest request, EditorMcpRunTestsPayload payload)
        {
            if (IsTestRunActive())
            {
                string platform = SessionState.GetString(ActiveTestPlatformKey, "unknown");
                return Failure(request, $"A test run is already active for {platform}.");
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return Failure(request, "Cannot start tests while Unity is compiling or updating.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Failure(request, "Cannot start a bridge test run while the editor is already in play mode or transitioning.");
            }

            ApiTestMode testMode;
            switch ((payload.platform ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "editmode":
                    testMode = ApiTestMode.EditMode;
                    break;

                case "playmode":
                    testMode = ApiTestMode.PlayMode;
                    break;

                default:
                    return Failure(request, $"Unsupported test platform: {payload.platform}");
            }

            Directory.CreateDirectory(TestResultsDirectory);
            string resultsPath = Path.Combine(TestResultsDirectory, $"{payload.platform.ToLowerInvariant()}-results.xml");
            var filter = new Filter
            {
                testMode = testMode
            };

            if (!string.IsNullOrWhiteSpace(payload.filter))
            {
                if (LooksLikeRegex(payload.filter))
                {
                    filter.groupNames = new[] { payload.filter };
                }
                else
                {
                    filter.testNames = new[] { payload.filter };
                }
            }

            var settings = new ExecutionSettings(filter);
            var callback = ScriptableObject.CreateInstance<EditorMcpTestRunCallbacks>();
            callback.hideFlags = HideFlags.HideAndDontSave;
            callback.Initialize(request.requestId, payload.platform.ToLowerInvariant(), resultsPath);
            _activeTestCallbacks = callback;

            SessionState.SetString(ActiveTestRequestIdKey, request.requestId);
            SessionState.SetString(ActiveTestPlatformKey, payload.platform.ToLowerInvariant());
            SessionState.SetString(ActiveTestStartedAtKey, DateTime.UtcNow.ToString("O"));
            SessionState.SetString(ActiveTestResultsPathKey, resultsPath);
            SessionState.EraseString(ActiveTestJobIdKey);

            try
            {
                TestRunnerApi.RegisterTestCallback(callback);
                TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
                string jobId = api.Execute(settings);
                SessionState.SetString(ActiveTestJobIdKey, jobId ?? string.Empty);
                WriteStatus();
                return null;
            }
            catch (Exception ex)
            {
                TestRunnerApi.UnregisterTestCallback(callback);
                UnityEngine.Object.DestroyImmediate(callback);
                _activeTestCallbacks = null;
                ClearActiveTestRunState();
                return Failure(request, $"Failed to start test run: {ex}");
            }
        }

        private static bool LooksLikeRegex(string filter)
        {
            return filter.IndexOfAny(new[] { '^', '$', '*', '+', '?', '[', ']', '(', ')', '{', '}', '|', '\\' }) >= 0;
        }

        private static bool IsTestRunActive()
        {
            return !string.IsNullOrWhiteSpace(SessionState.GetString(ActiveTestRequestIdKey, string.Empty));
        }

        private static void RecoverOrphanedTestRunIfNeeded()
        {
            if (!IsTestRunActive() || _activeTestCallbacks != null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            DateTime startedAtUtc = ParseSessionTimestamp(SessionState.GetString(ActiveTestStartedAtKey, string.Empty));
            double ageSeconds = Math.Max(0d, (DateTime.UtcNow - startedAtUtc).TotalSeconds);
            if (ageSeconds < OrphanedTestRecoveryDelaySeconds)
            {
                return;
            }

            string requestId = SessionState.GetString(ActiveTestRequestIdKey, string.Empty);
            string platform = SessionState.GetString(ActiveTestPlatformKey, string.Empty);
            string requestedResultsPath = SessionState.GetString(ActiveTestResultsPathKey, string.Empty);

            if (TryFindRecoveryResultsPath(startedAtUtc, requestedResultsPath, platform, out string recoveryResultsPath))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(requestedResultsPath) &&
                        !PathsEqual(requestedResultsPath, recoveryResultsPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(requestedResultsPath) ?? TestResultsDirectory);
                        File.Copy(recoveryResultsPath, requestedResultsPath, true);
                    }

                    string effectiveResultsPath = string.IsNullOrWhiteSpace(requestedResultsPath)
                        ? recoveryResultsPath
                        : requestedResultsPath;
                    EditorMcpTestRunPayload payload = BuildTestRunPayloadFromXml(effectiveResultsPath, platform);
                    WriteResponse(new EditorMcpBridgeResponse
                    {
                        requestId = requestId,
                        success = true,
                        payloadJson = JsonUtility.ToJson(payload, true),
                        message = $"{platform} tests finished (recovered from editor state).",
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                }
                catch (Exception ex)
                {
                    WriteResponse(new EditorMcpBridgeResponse
                    {
                        requestId = requestId,
                        success = false,
                        error = $"Recovered orphaned test run but failed to parse results: {ex}",
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                }
                finally
                {
                    ClearActiveTestRunState();
                }

                return;
            }

            if (ageSeconds >= OrphanedTestRecoveryTimeoutSeconds)
            {
                WriteResponse(new EditorMcpBridgeResponse
                {
                    requestId = requestId,
                    success = false,
                    error = $"Unity test run for {platform} became orphaned and no results file was found.",
                    respondedAtUtc = DateTime.UtcNow.ToString("O")
                });
                ClearActiveTestRunState();
            }
        }

        private static void ClearActiveTestRunState()
        {
            SessionState.EraseString(ActiveTestRequestIdKey);
            SessionState.EraseString(ActiveTestPlatformKey);
            SessionState.EraseString(ActiveTestStartedAtKey);
            SessionState.EraseString(ActiveTestResultsPathKey);
            SessionState.EraseString(ActiveTestJobIdKey);
            WriteStatus();
        }

        private static EditorMcpTestRunPayload BuildTestRunPayload(ITestResultAdaptor testResults, string platform, string resultsPath)
        {
            var payload = new EditorMcpTestRunPayload
            {
                platform = platform,
                resultsPath = resultsPath,
                total = testResults.PassCount + testResults.FailCount + testResults.SkipCount + testResults.InconclusiveCount,
                passed = testResults.PassCount,
                failed = testResults.FailCount,
                skipped = testResults.SkipCount,
                inconclusive = testResults.InconclusiveCount,
                resultState = testResults.ResultState,
                durationSeconds = testResults.Duration,
                startedAtUtc = testResults.StartTime.ToUniversalTime().ToString("O"),
                finishedAtUtc = testResults.EndTime.ToUniversalTime().ToString("O")
            };

            CollectFailedLeafResults(testResults, payload.failures);
            return payload;
        }

        private static void CollectFailedLeafResults(ITestResultAdaptor result, List<EditorMcpFailedTestInfo> failures)
        {
            if (result == null)
            {
                return;
            }

            if (!result.HasChildren && result.ResultState.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(new EditorMcpFailedTestInfo
                {
                    fullName = result.FullName,
                    name = result.Name,
                    message = result.Message,
                    stackTrace = result.StackTrace
                });
                return;
            }

            if (!result.HasChildren || result.Children == null)
            {
                return;
            }

            foreach (ITestResultAdaptor child in result.Children)
            {
                CollectFailedLeafResults(child, failures);
            }
        }

        private static string BuildStatusPayloadJson()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            var payload = new EditorMcpStatusPayload
            {
                bridgeVersion = BridgeVersion,
                unityVersion = Application.unityVersion,
                projectPath = ProjectRoot,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isRunningTests = IsTestRunActive(),
                activeTestPlatform = SessionState.GetString(ActiveTestPlatformKey, string.Empty),
                activeTestRequestId = SessionState.GetString(ActiveTestRequestIdKey, string.Empty),
                activeTestStartedAtUtc = SessionState.GetString(ActiveTestStartedAtKey, string.Empty),
                activeSceneName = activeScene.name,
                activeScenePath = activeScene.path,
                latestCompileReportPath = Path.Combine(ProjectRoot, "Logs", "Diagnostics", "compile-latest.log"),
                latestUiInstallReportPath = UiPlanInstallerService.GetLatestReportPath(),
                latestEditModeResultsPath = Path.Combine(TestResultsDirectory, "editmode-results.xml"),
                latestPlayModeResultsPath = Path.Combine(TestResultsDirectory, "playmode-results.xml"),
                latestTelemetrySessionPath = Path.Combine(ProjectRoot, "Logs", "Telemetry", "session-latest"),
                respondedAtUtc = DateTime.UtcNow.ToString("O")
            };

            return JsonUtility.ToJson(payload, true);
        }

        private static string BuildManualTestsPayloadJson()
        {
            var payload = new EditorMcpManualTestsPayload();
            List<ManualTestScenarioDefinition> scenarios = ManualTestExecutionService.LoadScenarios();
            for (int i = 0; i < scenarios.Count; i++)
            {
                ManualTestScenarioDefinition scenario = scenarios[i];
                string assetPath = AssetDatabase.GetAssetPath(scenario);

                payload.tests.Add(new EditorMcpManualTestInfo
                {
                    scenarioId = scenario.scenarioId,
                    displayName = scenario.displayName,
                    category = scenario.category,
                    milestone = scenario.milestone,
                    assetPath = assetPath,
                    stepCount = scenario.steps != null ? scenario.steps.Count : 0
                });
            }

            return JsonUtility.ToJson(payload, true);
        }

        private static string BuildManualTestStatusPayloadJson()
        {
            return ManualTestExecutionService.ToJson(ManualTestExecutionService.GetStatus());
        }

        private static bool TryGetRuntimeHud(out ClientWorldHudController hudController, out string error)
        {
            hudController = null;
            if (!EditorApplication.isPlaying)
            {
                error = "Gameplay MCP commands require Unity to be in play mode.";
                return false;
            }

            hudController = UnityEngine.Object.FindFirstObjectByType<ClientWorldHudController>();
            if (hudController == null)
            {
                error = "ClientWorldHudController was not found in play mode. The gameplay scene may still be initializing.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryBuildGameActionEnvelope(EditorMcpGameActionPayload payload, string rawPayloadJson, out GameActionEnvelope envelope, out string error)
        {
            envelope = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(payload.actionId) || !Enum.TryParse(payload.actionId, true, out GameActionId actionId))
            {
                error = "A valid actionId is required.";
                return false;
            }

            if (!TryParseEnum(payload.floorPurposeKind, FloorPurposeKind.None, out FloorPurposeKind floorPurposeKind))
            {
                error = $"Invalid floorPurposeKind '{payload.floorPurposeKind}'.";
                return false;
            }

            if (!TryParseEnum(payload.edgeFeatureKind, EdgeFeatureKind.None, out EdgeFeatureKind edgeFeatureKind))
            {
                error = $"Invalid edgeFeatureKind '{payload.edgeFeatureKind}'.";
                return false;
            }

            if (!TryParseEnum(payload.moduleKind, ModuleKind.None, out ModuleKind moduleKind))
            {
                error = $"Invalid moduleKind '{payload.moduleKind}'.";
                return false;
            }

            if (!TryParseEnum(payload.landmarkKind, LandmarkKind.None, out LandmarkKind landmarkKind))
            {
                error = $"Invalid landmarkKind '{payload.landmarkKind}'.";
                return false;
            }

            if (!TryParseEnum(payload.overlayMode, GameOverlayMode.Default, out GameOverlayMode overlayMode))
            {
                error = $"Invalid overlayMode '{payload.overlayMode}'.";
                return false;
            }

            if (!TryParseEnum(payload.inspectorTab, InspectorTabKind.Context, out InspectorTabKind inspectorTab))
            {
                error = $"Invalid inspectorTab '{payload.inspectorTab}'.";
                return false;
            }

            if (!TryParseEnum(payload.source, GameActionSource.McpAgent, out GameActionSource source))
            {
                error = $"Invalid source '{payload.source}'.";
                return false;
            }

            bool hasTargetCell = payload.hasTargetCell || PayloadContainsObject(rawPayloadJson, "targetCell");
            bool hasTargetEdge = payload.hasTargetEdge || PayloadContainsObject(rawPayloadJson, "targetEdge");
            HexCoord targetCell = payload.targetCell;
            HexEdgeCoord targetEdge = payload.targetEdge;

            ApplyRawHexOverrides(rawPayloadJson, ref targetCell, ref targetEdge);

            envelope = new GameActionEnvelope
            {
                actionId = actionId,
                source = source,
                uiSource = new SessionUiSource
                {
                    panelId = string.IsNullOrWhiteSpace(payload.panelId) ? "McpGameplay" : payload.panelId,
                    elementId = string.IsNullOrWhiteSpace(payload.elementId) ? actionId.ToString() : payload.elementId,
                    controller = nameof(UnityEditorMcpBridge)
                },
                targetCell = targetCell,
                hasTargetCell = hasTargetCell,
                targetEdge = targetEdge,
                hasTargetEdge = hasTargetEdge,
                targetEntityId = payload.targetEntityId ?? string.Empty,
                targetConstructionId = payload.targetConstructionId ?? string.Empty,
                floorPurposeKind = floorPurposeKind,
                edgeFeatureKind = edgeFeatureKind,
                moduleKind = moduleKind,
                landmarkKind = landmarkKind,
                overlayMode = overlayMode,
                inspectorTab = inspectorTab,
                tickCount = payload.tickCount,
                speedMultiplier = payload.speedMultiplier,
                desiredRunningState = payload.desiredRunningState,
                note = payload.note ?? string.Empty,
                payloadJson = JsonUtility.ToJson(payload)
            };

            return true;
        }

        private static bool PayloadContainsObject(string rawPayloadJson, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(rawPayloadJson) || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            return rawPayloadJson.IndexOf($"\"{propertyName}\"", StringComparison.Ordinal) >= 0;
        }

        private static void ApplyRawHexOverrides(string rawPayloadJson, ref HexCoord targetCell, ref HexEdgeCoord targetEdge)
        {
            if (string.IsNullOrWhiteSpace(rawPayloadJson))
            {
                return;
            }

            EditorMcpGameActionPayloadLowercase rawPayload = JsonUtility.FromJson<EditorMcpGameActionPayloadLowercase>(rawPayloadJson);
            if (rawPayload == null)
            {
                return;
            }

            if (rawPayload.targetCell != null)
            {
                targetCell = new HexCoord(rawPayload.targetCell.GetQ(), rawPayload.targetCell.GetR());
            }

            EditorMcpHexCoordLowercase edgeA = rawPayload.targetEdge?.GetA();
            EditorMcpHexCoordLowercase edgeB = rawPayload.targetEdge?.GetB();
            if (edgeA != null && edgeB != null)
            {
                targetEdge = new HexEdgeCoord(
                    new HexCoord(edgeA.GetQ(), edgeA.GetR()),
                    new HexCoord(edgeB.GetQ(), edgeB.GetR()));
            }
        }

        private static void ProcessPendingPlayModeTransition(double now)
        {
            if (!TryGetPendingPlayModeTransition(out PendingPlayModeTransition pendingTransition))
            {
                return;
            }

            bool reachedTarget = pendingTransition.targetPlayMode
                ? EditorApplication.isPlaying
                : !EditorApplication.isPlaying;

            if (reachedTarget)
            {
                WriteResponse(new EditorMcpBridgeResponse
                {
                    requestId = pendingTransition.requestId,
                    success = true,
                    payloadJson = BuildStatusPayloadJson(),
                    message = pendingTransition.targetPlayMode ? "Entered play mode." : "Exited play mode.",
                    respondedAtUtc = DateTime.UtcNow.ToString("O")
                });
                ClearPendingPlayModeTransition();
                return;
            }

            if (now - pendingTransition.startedAtRealtime < PlayModeTransitionTimeoutSeconds)
            {
                return;
            }

            WriteResponse(new EditorMcpBridgeResponse
            {
                requestId = pendingTransition.requestId,
                success = false,
                error = pendingTransition.targetPlayMode
                    ? "Timed out waiting for Unity to enter play mode."
                    : "Timed out waiting for Unity to exit play mode.",
                respondedAtUtc = DateTime.UtcNow.ToString("O")
            });
            ClearPendingPlayModeTransition();
        }

        private static void SetPendingPlayModeTransition(string requestId, bool targetPlayMode, double startedAtRealtime)
        {
            SessionState.SetString(PendingPlayModeRequestIdKey, requestId ?? string.Empty);
            SessionState.SetBool(PendingPlayModeTargetKey, targetPlayMode);
            SessionState.SetString(PendingPlayModeStartedAtKey, startedAtRealtime.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool TryGetPendingPlayModeTransition(out PendingPlayModeTransition pendingTransition)
        {
            string requestId = SessionState.GetString(PendingPlayModeRequestIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(requestId))
            {
                pendingTransition = null;
                return false;
            }

            string startedAtRaw = SessionState.GetString(PendingPlayModeStartedAtKey, string.Empty);
            double.TryParse(startedAtRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double startedAtRealtime);
            pendingTransition = new PendingPlayModeTransition
            {
                requestId = requestId,
                targetPlayMode = SessionState.GetBool(PendingPlayModeTargetKey, false),
                startedAtRealtime = startedAtRealtime
            };
            return true;
        }

        private static void ClearPendingPlayModeTransition()
        {
            SessionState.EraseString(PendingPlayModeRequestIdKey);
            SessionState.EraseString(PendingPlayModeStartedAtKey);
            SessionState.SetBool(PendingPlayModeTargetKey, false);
        }

        private static bool TryParseEnum<TEnum>(string rawValue, TEnum fallback, out TEnum value) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                value = fallback;
                return true;
            }

            return Enum.TryParse(rawValue, true, out value);
        }

        private static T ParsePayload<T>(string payloadJson) where T : class
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return null;
            }

            return JsonUtility.FromJson<T>(payloadJson);
        }

        private static bool HasDirtyOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isDirty)
                {
                    return true;
                }
            }

            return false;
        }

        private static EditorMcpBridgeResponse Success(EditorMcpBridgeRequest request, string payloadJson, string message = "")
        {
            return new EditorMcpBridgeResponse
            {
                requestId = request.requestId,
                success = true,
                payloadJson = payloadJson ?? string.Empty,
                message = message ?? string.Empty,
                respondedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static EditorMcpBridgeResponse Failure(EditorMcpBridgeRequest request, string error)
        {
            return new EditorMcpBridgeResponse
            {
                requestId = request.requestId,
                success = false,
                error = error ?? "Unknown error.",
                respondedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static EditorMcpBridgeResponse Failure(EditorMcpBridgeRequest request, string error, string payloadJson)
        {
            return new EditorMcpBridgeResponse
            {
                requestId = request.requestId,
                success = false,
                error = error ?? "Unknown error.",
                payloadJson = payloadJson ?? string.Empty,
                respondedAtUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static void EnsureDirectories()
        {
            if (IsAuxiliaryProcess)
            {
                return;
            }

            Directory.CreateDirectory(BridgeRoot);
            Directory.CreateDirectory(RequestsDirectory);
            Directory.CreateDirectory(ResponsesDirectory);
            Directory.CreateDirectory(TestResultsDirectory);
        }

        private static void WriteStatus(bool isShuttingDown = false)
        {
            if (IsAuxiliaryProcess)
            {
                return;
            }

            EnsureDirectories();

            Scene activeScene = SceneManager.GetActiveScene();
            var status = new EditorMcpBridgeStatusFile
            {
                bridgeVersion = BridgeVersion,
                unityVersion = Application.unityVersion,
                projectPath = ProjectRoot,
                editorProcessId = Process.GetCurrentProcess().Id,
                isAvailable = !isShuttingDown,
                isPlaying = EditorApplication.isPlaying,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isRunningTests = IsTestRunActive(),
                activeTestPlatform = SessionState.GetString(ActiveTestPlatformKey, string.Empty),
                activeTestRequestId = SessionState.GetString(ActiveTestRequestIdKey, string.Empty),
                activeTestStartedAtUtc = SessionState.GetString(ActiveTestStartedAtKey, string.Empty),
                activeSceneName = activeScene.name,
                activeScenePath = activeScene.path,
                lastHeartbeatUtc = DateTime.UtcNow.ToString("O")
            };

            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
        }

        private static void WriteResponse(EditorMcpBridgeResponse response)
        {
            if (IsAuxiliaryProcess)
            {
                return;
            }

            EnsureDirectories();
            string responsePath = Path.Combine(ResponsesDirectory, $"{response.requestId}.json");
            File.WriteAllText(responsePath, JsonUtility.ToJson(response, true));
        }

        private static bool DetectAuxiliaryProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (arg.IndexOf("AssetImportWorker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (string.Equals(arg, "-adb2", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-batchMode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-batchmode", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static DateTime ParseSessionTimestamp(string value)
        {
            if (DateTime.TryParse(value, out DateTime parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static bool TryFindRecoveryResultsPath(DateTime startedAtUtc, string requestedResultsPath, string platform, out string resultsPath)
        {
            resultsPath = string.Empty;

            foreach (string candidatePath in EnumerateRecoveryResultsCandidates(requestedResultsPath))
            {
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(candidatePath);
                if (lastWriteUtc >= startedAtUtc.AddSeconds(-1d) && DoesResultsFileMatchPlatform(candidatePath, platform))
                {
                    resultsPath = candidatePath;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateRecoveryResultsCandidates(string requestedResultsPath)
        {
            if (!string.IsNullOrWhiteSpace(requestedResultsPath))
            {
                yield return requestedResultsPath;
            }

            string appSupportRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (string root in GetUnityResultsSearchRoots(appSupportRoot))
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                string defaultCompanyRoot = Path.Combine(root, "DefaultCompany");
                if (Directory.Exists(defaultCompanyRoot))
                {
                    foreach (string candidate in Directory.GetFiles(defaultCompanyRoot, "TestResults.xml", SearchOption.AllDirectories)
                                 .OrderByDescending(File.GetLastWriteTimeUtc))
                    {
                        yield return candidate;
                    }
                }
            }

            foreach (string candidate in Directory.GetFiles(TestResultsDirectory, "*-results.xml", SearchOption.TopDirectoryOnly)
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> GetUnityResultsSearchRoots(string applicationDataRoot)
        {
            if (!string.IsNullOrWhiteSpace(applicationDataRoot))
            {
                yield return applicationDataRoot;
            }

            string personalRoot = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (!string.IsNullOrWhiteSpace(personalRoot))
            {
                yield return Path.Combine(personalRoot, "Library", "Application Support");
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool DoesResultsFileMatchPlatform(string xmlPath, string platform)
        {
            try
            {
                XDocument document = XDocument.Load(xmlPath);
                XElement root = document.Root;
                if (root == null)
                {
                    return false;
                }

                foreach (XElement propertyElement in root.Descendants().Where(element => string.Equals(element.Name.LocalName, "property", StringComparison.OrdinalIgnoreCase)))
                {
                    XAttribute nameAttribute = propertyElement.Attribute("name");
                    XAttribute valueAttribute = propertyElement.Attribute("value");
                    if (nameAttribute == null || valueAttribute == null)
                    {
                        continue;
                    }

                    if (string.Equals(nameAttribute.Value, "platform", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(platform, "editmode", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(valueAttribute.Value, "EditMode", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (string.Equals(platform, "playmode", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(valueAttribute.Value, "PlayMode", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static EditorMcpTestRunPayload BuildTestRunPayloadFromXml(string xmlPath, string platform)
        {
            XDocument document = XDocument.Load(xmlPath);
            XElement root = document.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "test-run", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unexpected NUnit XML structure in {xmlPath}.");
            }

            var payload = new EditorMcpTestRunPayload
            {
                platform = platform,
                resultsPath = xmlPath,
                total = GetIntAttribute(root, "total"),
                passed = GetIntAttribute(root, "passed"),
                failed = GetIntAttribute(root, "failed"),
                skipped = GetIntAttribute(root, "skipped"),
                inconclusive = GetIntAttribute(root, "inconclusive"),
                resultState = GetStringAttribute(root, "result"),
                durationSeconds = GetDoubleAttribute(root, "duration"),
                startedAtUtc = GetStringAttribute(root, "start-time"),
                finishedAtUtc = GetStringAttribute(root, "end-time")
            };

            foreach (XElement testCase in root.Descendants().Where(element => string.Equals(element.Name.LocalName, "test-case", StringComparison.OrdinalIgnoreCase)))
            {
                string result = GetStringAttribute(testCase, "result");
                if (!result.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                XElement failureElement = testCase.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "failure", StringComparison.OrdinalIgnoreCase));
                string message = failureElement?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "message", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
                string stackTrace = failureElement?.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "stack-trace", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

                payload.failures.Add(new EditorMcpFailedTestInfo
                {
                    fullName = GetStringAttribute(testCase, "fullname"),
                    name = GetStringAttribute(testCase, "name"),
                    message = message,
                    stackTrace = stackTrace
                });
            }

            return payload;
        }

        private static string GetStringAttribute(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value ?? string.Empty;
        }

        private static int GetIntAttribute(XElement element, string attributeName)
        {
            int.TryParse(GetStringAttribute(element, attributeName), out int value);
            return value;
        }

        private static double GetDoubleAttribute(XElement element, string attributeName)
        {
            double.TryParse(GetStringAttribute(element, attributeName), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value);
            return value;
        }

        [Serializable]
        private class EditorMcpBridgeRequest
        {
            public string requestId;
            public string command;
            public string payloadJson;
            public string createdAtUtc;
        }

        [Serializable]
        private class EditorMcpBridgeResponse
        {
            public string requestId;
            public bool success;
            public string payloadJson;
            public string message;
            public string error;
            public string respondedAtUtc;
        }

        [Serializable]
        private class PendingPlayModeTransition
        {
            public string requestId;
            public bool targetPlayMode;
            public double startedAtRealtime;
        }

        [Serializable]
        private class EditorMcpBridgeStatusFile
        {
            public string bridgeVersion;
            public string unityVersion;
            public string projectPath;
            public int editorProcessId;
            public bool isAvailable;
            public bool isPlaying;
            public bool isCompiling;
            public bool isUpdating;
            public bool isRunningTests;
            public string activeTestPlatform;
            public string activeTestRequestId;
            public string activeTestStartedAtUtc;
            public string activeSceneName;
            public string activeScenePath;
            public string lastHeartbeatUtc;
        }

        [Serializable]
        private class EditorMcpApplyUiPayload
        {
            public bool dryRun;
        }

        [Serializable]
        private class EditorMcpPolygonVisualMaintenancePayload
        {
            public bool importPackages;
            public bool repairMaterials;
            public bool refreshVisualLibrary;
            public bool assignLibraryToRenderers;
            public string sourceRoot;
        }

        [Serializable]
        private class EditorMcpScenePayload
        {
            public string scenePath;
        }

        [Serializable]
        private class EditorMcpRunTestsPayload
        {
            public string platform;
            public string filter;
        }

        [Serializable]
        private class EditorMcpManualTestStartPayload
        {
            public string scenarioId;
        }

        [Serializable]
        private class EditorMcpManualTestStepPayload
        {
            public string result;
            public string note;
        }

        [Serializable]
        private class EditorMcpGameActionPayload
        {
            public string actionId;
            public string source = nameof(GameActionSource.McpAgent);
            public HexCoord targetCell;
            public bool hasTargetCell;
            public HexEdgeCoord targetEdge;
            public bool hasTargetEdge;
            public string targetEntityId;
            public string targetConstructionId;
            public string floorPurposeKind;
            public string edgeFeatureKind;
            public string moduleKind;
            public string landmarkKind;
            public string overlayMode;
            public string inspectorTab;
            public int tickCount;
            public float speedMultiplier;
            public bool desiredRunningState;
            public string note;
            public string panelId;
            public string elementId;
        }

        [Serializable]
        private class EditorMcpGameActionSchemaPayload
        {
            public string actionId;
        }

        [Serializable]
        private class EditorMcpGameActionPayloadLowercase
        {
            public EditorMcpHexCoordLowercase targetCell;
            public EditorMcpHexEdgeCoordLowercase targetEdge;
        }

        [Serializable]
        private class EditorMcpHexCoordLowercase
        {
            public int q;
            public int r;
            public int s;
            public int Q;
            public int R;
            public int S;

            public int GetQ() => q != 0 || Q == 0 ? q : Q;
            public int GetR() => r != 0 || R == 0 ? r : R;
        }

        [Serializable]
        private class EditorMcpHexEdgeCoordLowercase
        {
            public EditorMcpHexCoordLowercase a;
            public EditorMcpHexCoordLowercase b;
            public EditorMcpHexCoordLowercase A;
            public EditorMcpHexCoordLowercase B;

            public EditorMcpHexCoordLowercase GetA() => a ?? A;
            public EditorMcpHexCoordLowercase GetB() => b ?? B;
        }

        [Serializable]
        private class EditorMcpSceneResult
        {
            public string scenePath;
            public string sceneName;
        }

        [Serializable]
        private class EditorMcpCancelTestsResult
        {
            public bool hadJobId;
            public bool canceled;
        }

        [Serializable]
        private class EditorMcpStatusPayload
        {
            public string bridgeVersion;
            public string unityVersion;
            public string projectPath;
            public bool isPlaying;
            public bool isPaused;
            public bool isCompiling;
            public bool isUpdating;
            public bool isRunningTests;
            public string activeTestPlatform;
            public string activeTestRequestId;
            public string activeTestStartedAtUtc;
            public string activeSceneName;
            public string activeScenePath;
            public string latestCompileReportPath;
            public string latestUiInstallReportPath;
            public string latestEditModeResultsPath;
            public string latestPlayModeResultsPath;
            public string latestTelemetrySessionPath;
            public string respondedAtUtc;
        }

        [Serializable]
        private class EditorMcpManualTestsPayload
        {
            public List<EditorMcpManualTestInfo> tests = new List<EditorMcpManualTestInfo>();
        }

        [Serializable]
        private class EditorMcpManualTestInfo
        {
            public string scenarioId;
            public string displayName;
            public string category;
            public string milestone;
            public string assetPath;
            public int stepCount;
        }

        [Serializable]
        private class EditorMcpGameWorldSummaryPayload
        {
            public WorldBarState worldBar = new WorldBarState();
        }

        [Serializable]
        private class EditorMcpGameHoverPayload
        {
            public bool hasHoveredCell;
            public HexCoord hoveredCell;
            public bool hasHoveredEdge;
            public HexEdgeCoord hoveredEdge;
        }

        [Serializable]
        private class EditorMcpGameEntitiesPayload
        {
            public List<EntitySummaryState> entities = new List<EntitySummaryState>();
        }

        [Serializable]
        private class EditorMcpGameActionsPayload
        {
            public List<GameActionDescriptor> actions = new List<GameActionDescriptor>();
        }

        [Serializable]
        private class EditorMcpGameBlockersPayload
        {
            public List<BlockerSummaryState> blockers = new List<BlockerSummaryState>();
        }

        [Serializable]
        private class EditorMcpGameRecentEventsPayload
        {
            public EventStripState eventStrip = new EventStripState();
            public string sessionId;
            public string latestActionId;
            public string latestDiffPath;
        }

        [Serializable]
        private class EditorMcpTestRunPayload
        {
            public string platform;
            public string resultsPath;
            public int total;
            public int passed;
            public int failed;
            public int skipped;
            public int inconclusive;
            public string resultState;
            public double durationSeconds;
            public string startedAtUtc;
            public string finishedAtUtc;
            public List<EditorMcpFailedTestInfo> failures = new List<EditorMcpFailedTestInfo>();
        }

        [Serializable]
        private class EditorMcpFailedTestInfo
        {
            public string fullName;
            public string name;
            public string message;
            public string stackTrace;
        }

        private sealed class EditorMcpTestRunCallbacks : ScriptableObject, IErrorCallbacks
        {
            [SerializeField] private string _requestId;
            [SerializeField] private string _platform;
            [SerializeField] private string _resultsPath;
            [SerializeField] private bool _completed;

            public void Initialize(string requestId, string platform, string resultsPath)
            {
                _requestId = requestId;
                _platform = platform;
                _resultsPath = resultsPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (_completed)
                {
                    return;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(_resultsPath))
                    {
                        TestRunnerApi.SaveResultToFile(result, _resultsPath);
                    }

                    EditorMcpTestRunPayload payload = BuildTestRunPayload(result, _platform, _resultsPath);
                    Complete(new EditorMcpBridgeResponse
                    {
                        requestId = _requestId,
                        success = true,
                        payloadJson = JsonUtility.ToJson(payload, true),
                        message = $"{_platform} tests finished.",
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                }
                catch (Exception ex)
                {
                    Complete(new EditorMcpBridgeResponse
                    {
                        requestId = _requestId,
                        success = false,
                        error = ex.ToString(),
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public void OnError(string message)
            {
                if (_completed)
                {
                    return;
                }

                if (TryRecoverResultsAfterError(message))
                {
                    return;
                }

                Complete(new EditorMcpBridgeResponse
                {
                    requestId = _requestId,
                    success = false,
                    error = string.IsNullOrWhiteSpace(message) ? "Unity test run failed." : message,
                    respondedAtUtc = DateTime.UtcNow.ToString("O")
                });
            }

            private bool TryRecoverResultsAfterError(string message)
            {
                if (string.IsNullOrWhiteSpace(message) ||
                    message.IndexOf("Multiple unexpected errors happened while running tests.", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }

                DateTime startedAtUtc = ParseSessionTimestamp(SessionState.GetString(ActiveTestStartedAtKey, string.Empty));
                string requestedResultsPath = SessionState.GetString(ActiveTestResultsPathKey, _resultsPath ?? string.Empty);
                if (!TryFindRecoveryResultsPath(startedAtUtc, requestedResultsPath, _platform, out string recoveryResultsPath))
                {
                    return false;
                }

                try
                {
                    string effectiveResultsPath = recoveryResultsPath;
                    if (!string.IsNullOrWhiteSpace(requestedResultsPath) &&
                        !PathsEqual(requestedResultsPath, recoveryResultsPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(requestedResultsPath) ?? TestResultsDirectory);
                        File.Copy(recoveryResultsPath, requestedResultsPath, true);
                        effectiveResultsPath = requestedResultsPath;
                    }

                    EditorMcpTestRunPayload payload = BuildTestRunPayloadFromXml(effectiveResultsPath, _platform);
                    Complete(new EditorMcpBridgeResponse
                    {
                        requestId = _requestId,
                        success = true,
                        payloadJson = JsonUtility.ToJson(payload, true),
                        message = $"{_platform} tests finished (recovered after cleanup error).",
                        respondedAtUtc = DateTime.UtcNow.ToString("O")
                    });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private void Complete(EditorMcpBridgeResponse response)
            {
                _completed = true;
                try
                {
                    WriteResponse(response);
                }
                finally
                {
                    TestRunnerApi.UnregisterTestCallback(this);
                    ClearActiveTestRunState();
                    _activeTestCallbacks = null;
                    UnityEngine.Object.DestroyImmediate(this);
                }
            }
        }
    }
}

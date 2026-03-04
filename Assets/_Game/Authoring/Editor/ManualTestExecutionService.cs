using System;
using System.Collections.Generic;
using System.IO;
using Ggj2026.Game.Authoring.ManualTests;
using Ggj2026.Game.Shared.Telemetry;
using UnityEditor;
using UnityEngine;

namespace Ggj2026.Game.Authoring.Editor
{
    public static class ManualTestExecutionService
    {
        private const string ScenarioDirectory = "Assets/_Game/Authoring/ManualTests";
        private const string ActiveScenarioIdKey = "GGJ2026.ManualTests.ActiveScenarioId";
        private const string CurrentStepIndexKey = "GGJ2026.ManualTests.CurrentStepIndex";
        private const string ActiveRunJsonKey = "GGJ2026.ManualTests.ActiveRunJson";

        [Serializable]
        public class ManualTestExecutionStatus
        {
            public bool hasActiveRun;
            public string scenarioId;
            public string scenarioTitle;
            public string sessionId;
            public string runId;
            public string result;
            public int currentStepIndex;
            public int totalSteps;
            public string currentStepId;
            public string currentStepTitle;
            public string currentInstruction;
            public string currentExpectedResult;
            public string currentSanityChecks;
        }

        public static List<ManualTestScenarioDefinition> LoadScenarios(bool ensureSeedScenarios = true)
        {
            if (ensureSeedScenarios)
            {
                EnsureSeedScenarios();
            }

            var scenarios = new List<ManualTestScenarioDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:ManualTestScenarioDefinition", new[] { ScenarioDirectory });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                ManualTestScenarioDefinition scenario = AssetDatabase.LoadAssetAtPath<ManualTestScenarioDefinition>(assetPath);
                if (scenario != null)
                {
                    scenarios.Add(scenario);
                }
            }

            scenarios.Sort((left, right) => string.CompareOrdinal(GetScenarioDisplayName(left), GetScenarioDisplayName(right)));
            return scenarios;
        }

        public static void EnsureSeedScenarios()
        {
            Directory.CreateDirectory(ScenarioDirectory);
            EnsureScenarioAsset(
                "FoundationUiSanity.asset",
                "foundation-ui-sanity",
                "Foundation UI Sanity",
                "ui-toolkit-foundation",
                "UI sanity",
                "Confirms the runtime HUD, input blocking, and telemetry surfaces are active.",
                new ManualTestStepDefinition
                {
                    stepId = "open-bootstrap",
                    title = "Open Bootstrap",
                    instruction = "Enter Play Mode from Bootstrap and confirm the Persistent Main Menu loads.",
                    requiredScene = "Bootstrap",
                    expectation = new ManualTestExpectationDefinition
                    {
                        expectedResult = "Main menu appears through a UIDocument-backed panel.",
                        sanityChecks = "No IMGUI overlay appears and no world scene loads until you choose an action."
                    }
                },
                new ManualTestStepDefinition
                {
                    stepId = "hud-click-blocking",
                    title = "Verify HUD input capture",
                    instruction = "Enter ClientWorld, click tool buttons, and confirm those clicks do not mutate the world behind the HUD.",
                    requiredScene = "ClientWorld",
                    expectation = new ManualTestExpectationDefinition
                    {
                        expectedResult = "Tool selection changes, but cells are not painted until the pointer leaves the HUD.",
                        sanityChecks = "No accidental selection or paint occurs while clicking or scrolling in the HUD."
                    }
                });

            EnsureScenarioAsset(
                "PlacementAndShelterSanity.asset",
                "placement-and-shelter-sanity",
                "Placement And Shelter Sanity",
                "hex-colony-foundation",
                "Placement sanity",
                "Confirms module purpose matching, shelter requirements, and landmark placement constraints.",
                new ManualTestStepDefinition
                {
                    stepId = "storage-floor-and-rack",
                    title = "Storage floor accepts storage module",
                    instruction = "Paint an outdoor Storage floor, build it, and place a Rack on it.",
                    requiredScene = "ClientWorld",
                    expectation = new ManualTestExpectationDefinition
                    {
                        expectedResult = "The Rack queues successfully on Storage floor.",
                        sanityChecks = "The same cell should reject Residential or Workshop-only modules."
                    }
                },
                new ManualTestStepDefinition
                {
                    stepId = "bed-requires-sheltered-residential",
                    title = "Bed requires sheltered Residential space",
                    instruction = "Try placing a Bed on an open Residential floor and then inside an enclosed Residential room.",
                    requiredScene = "ClientWorld",
                    expectation = new ManualTestExpectationDefinition
                    {
                        expectedResult = "Open placement is rejected; enclosed residential placement is accepted.",
                        sanityChecks = "Failure reason mentions shelter or residential purpose."
                    }
                },
                new ManualTestStepDefinition
                {
                    stepId = "gatehouse-requires-wall-connection",
                    title = "Gatehouse requires wall connections",
                    instruction = "Attempt to place a Gatehouse without walls, then build supporting wall edges and place it again.",
                    requiredScene = "ClientWorld",
                    expectation = new ManualTestExpectationDefinition
                    {
                        expectedResult = "Unconnected placement is rejected and connected placement succeeds.",
                        sanityChecks = "The gate passage is created only after the landmark completes."
                    }
                });
        }

        public static bool TryStartRun(string scenarioId, out ManualTestExecutionStatus status, out string error)
        {
            status = null;
            error = string.Empty;

            ManualTestScenarioDefinition scenario = FindScenarioById(scenarioId);
            if (scenario == null)
            {
                error = $"Manual test scenario not found: {scenarioId}";
                return false;
            }

            var run = new ManualTestRunRecord
            {
                runId = Guid.NewGuid().ToString("N"),
                sessionId = TryReadLatestSessionId(),
                scenarioId = scenario.scenarioId,
                scenarioTitle = GetScenarioDisplayName(scenario),
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                result = "InProgress"
            };

            SessionState.SetString(ActiveScenarioIdKey, scenario.scenarioId);
            SessionState.SetInt(CurrentStepIndexKey, 0);
            SessionState.SetString(ActiveRunJsonKey, JsonUtility.ToJson(run));

            status = BuildStatus(scenario, run, 0);
            return true;
        }

        public static bool TryRecordCurrentStep(string result, string note, out ManualTestExecutionStatus status, out string error)
        {
            status = null;
            error = string.Empty;

            if (!TryLoadActiveRun(out ManualTestScenarioDefinition scenario, out ManualTestRunRecord run, out int currentStepIndex))
            {
                error = "No active manual test run.";
                return false;
            }

            if (scenario.steps == null || scenario.steps.Count == 0)
            {
                error = "Active manual test scenario has no steps.";
                return false;
            }

            if (currentStepIndex >= scenario.steps.Count)
            {
                error = "All manual test steps are already recorded.";
                return false;
            }

            ManualTestStepDefinition step = scenario.steps[currentStepIndex];
            run.steps.Add(new ManualTestStepRecord
            {
                stepId = step.stepId,
                title = step.title,
                instruction = step.instruction,
                result = result,
                note = note ?? string.Empty
            });

            int nextStepIndex = Mathf.Min(currentStepIndex + 1, scenario.steps.Count);
            SessionState.SetInt(CurrentStepIndexKey, nextStepIndex);
            SessionState.SetString(ActiveRunJsonKey, JsonUtility.ToJson(run));

            status = BuildStatus(scenario, run, nextStepIndex);
            return true;
        }

        public static bool TryFinishRun(out ManualTestRunRecord completedRun, out string error)
        {
            completedRun = null;
            error = string.Empty;

            if (!TryLoadActiveRun(out ManualTestScenarioDefinition scenario, out ManualTestRunRecord run, out _))
            {
                error = "No active manual test run.";
                return false;
            }

            run.finishedAtUtc = DateTime.UtcNow.ToString("O");
            run.result = DetermineOverallResult(run.steps);
            WriteRunArtifacts(run);
            ClearActiveRun();
            completedRun = run;
            return true;
        }

        public static ManualTestExecutionStatus GetStatus()
        {
            if (!TryLoadActiveRun(out ManualTestScenarioDefinition scenario, out ManualTestRunRecord run, out int currentStepIndex))
            {
                return new ManualTestExecutionStatus();
            }

            return BuildStatus(scenario, run, currentStepIndex);
        }

        public static string ToJson(ManualTestExecutionStatus status)
        {
            return JsonUtility.ToJson(status ?? new ManualTestExecutionStatus(), true);
        }

        private static bool TryLoadActiveRun(out ManualTestScenarioDefinition scenario, out ManualTestRunRecord run, out int currentStepIndex)
        {
            scenario = null;
            run = null;
            currentStepIndex = 0;

            string scenarioId = SessionState.GetString(ActiveScenarioIdKey, string.Empty);
            string runJson = SessionState.GetString(ActiveRunJsonKey, string.Empty);
            if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(runJson))
            {
                return false;
            }

            scenario = FindScenarioById(scenarioId);
            if (scenario == null)
            {
                ClearActiveRun();
                return false;
            }

            run = JsonUtility.FromJson<ManualTestRunRecord>(runJson);
            if (run == null)
            {
                ClearActiveRun();
                return false;
            }

            currentStepIndex = Mathf.Max(0, SessionState.GetInt(CurrentStepIndexKey, 0));
            return true;
        }

        private static ManualTestExecutionStatus BuildStatus(ManualTestScenarioDefinition scenario, ManualTestRunRecord run, int currentStepIndex)
        {
            var status = new ManualTestExecutionStatus
            {
                hasActiveRun = run != null,
                scenarioId = scenario != null ? scenario.scenarioId : string.Empty,
                scenarioTitle = scenario != null ? GetScenarioDisplayName(scenario) : string.Empty,
                sessionId = run != null ? run.sessionId : string.Empty,
                runId = run != null ? run.runId : string.Empty,
                result = run != null ? run.result : string.Empty,
                currentStepIndex = currentStepIndex,
                totalSteps = scenario?.steps != null ? scenario.steps.Count : 0
            };

            if (scenario != null && scenario.steps != null && currentStepIndex >= 0 && currentStepIndex < scenario.steps.Count)
            {
                ManualTestStepDefinition step = scenario.steps[currentStepIndex];
                status.currentStepId = step.stepId;
                status.currentStepTitle = step.title;
                status.currentInstruction = step.instruction;
                status.currentExpectedResult = step.expectation != null ? step.expectation.expectedResult : string.Empty;
                status.currentSanityChecks = step.expectation != null ? step.expectation.sanityChecks : string.Empty;
            }

            return status;
        }

        private static ManualTestScenarioDefinition FindScenarioById(string scenarioId)
        {
            List<ManualTestScenarioDefinition> scenarios = LoadScenarios();
            for (int i = 0; i < scenarios.Count; i++)
            {
                if (string.Equals(scenarios[i].scenarioId, scenarioId, StringComparison.Ordinal))
                {
                    return scenarios[i];
                }
            }

            return null;
        }

        private static void EnsureScenarioAsset(
            string fileName,
            string scenarioId,
            string displayName,
            string milestone,
            string category,
            string summary,
            params ManualTestStepDefinition[] steps)
        {
            string assetPath = $"{ScenarioDirectory}/{fileName}";
            ManualTestScenarioDefinition existing = AssetDatabase.LoadAssetAtPath<ManualTestScenarioDefinition>(assetPath);
            if (existing != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<ManualTestScenarioDefinition>();
            asset.scenarioId = scenarioId;
            asset.displayName = displayName;
            asset.milestone = milestone;
            asset.category = category;
            asset.summary = summary;
            asset.steps.AddRange(steps);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string GetScenarioDisplayName(ManualTestScenarioDefinition scenario)
        {
            return string.IsNullOrWhiteSpace(scenario.displayName) ? scenario.name : scenario.displayName;
        }

        private static string DetermineOverallResult(List<ManualTestStepRecord> recordedSteps)
        {
            bool hasFailure = false;
            bool hasBlocked = false;

            for (int i = 0; i < recordedSteps.Count; i++)
            {
                switch (recordedSteps[i].result)
                {
                    case "Fail":
                        hasFailure = true;
                        break;
                    case "Blocked":
                        hasBlocked = true;
                        break;
                }
            }

            if (hasFailure)
            {
                return "Fail";
            }

            if (hasBlocked)
            {
                return "Blocked";
            }

            return "Pass";
        }

        private static void ClearActiveRun()
        {
            SessionState.EraseString(ActiveScenarioIdKey);
            SessionState.EraseString(ActiveRunJsonKey);
            SessionState.EraseInt(CurrentStepIndexKey);
        }

        private static void WriteRunArtifacts(ManualTestRunRecord run)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string manualTestDirectory = Path.Combine(projectRoot, "Logs", "Telemetry", "manual-tests");
            Directory.CreateDirectory(manualTestDirectory);

            string latestPath = Path.Combine(manualTestDirectory, "manual-test-latest.json");
            string sessionPath = Path.Combine(manualTestDirectory, $"manual-test-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            string json = JsonUtility.ToJson(run, true);

            File.WriteAllText(latestPath, json);
            File.WriteAllText(sessionPath, json);

            string sessionLatestDirectory = Path.Combine(projectRoot, "Logs", "Telemetry", "session-latest");
            if (Directory.Exists(sessionLatestDirectory))
            {
                File.WriteAllText(Path.Combine(sessionLatestDirectory, "manual-test.json"), json);
            }
        }

        private static string TryReadLatestSessionId()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string latestSessionPath = Path.Combine(projectRoot, "Logs", "Telemetry", "session-latest", "session.json");
            if (!File.Exists(latestSessionPath))
            {
                return string.Empty;
            }

            TelemetrySessionMetadata metadata = JsonUtility.FromJson<TelemetrySessionMetadata>(File.ReadAllText(latestSessionPath));
            return metadata != null ? metadata.sessionId : string.Empty;
        }
    }
}

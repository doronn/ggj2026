using System.IO;
using System.Linq;
using Ggj2026.Game.Authoring.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class ManualTestExecutionServiceTests
    {
        [Test]
        public void EnsureSeedScenarios_ProvidesFoundationScenarios()
        {
            ManualTestExecutionService.EnsureSeedScenarios();

            var scenarios = ManualTestExecutionService.LoadScenarios();

            Assert.That(scenarios.Any(scenario => scenario.scenarioId == "foundation-ui-sanity"), Is.True);
            Assert.That(scenarios.Any(scenario => scenario.scenarioId == "placement-and-shelter-sanity"), Is.True);
        }

        [Test]
        public void StartRecordFinishRun_PersistsManualTestArtifacts()
        {
            ManualTestExecutionService.EnsureSeedScenarios();

            Assert.That(ManualTestExecutionService.TryStartRun("foundation-ui-sanity", out var startStatus, out string startError), Is.True, startError);
            Assert.That(startStatus.hasActiveRun, Is.True);
            Assert.That(startStatus.currentStepTitle, Is.Not.Empty);

            Assert.That(ManualTestExecutionService.TryRecordCurrentStep("Pass", "step 1 ok", out var nextStatus, out string firstStepError), Is.True, firstStepError);
            Assert.That(nextStatus.currentStepIndex, Is.EqualTo(1));

            Assert.That(ManualTestExecutionService.TryRecordCurrentStep("Pass", "step 2 ok", out var finishedStatus, out string secondStepError), Is.True, secondStepError);
            Assert.That(finishedStatus.currentStepIndex, Is.EqualTo(finishedStatus.totalSteps));

            Assert.That(ManualTestExecutionService.TryFinishRun(out var completedRun, out string finishError), Is.True, finishError);
            Assert.That(completedRun.result, Is.EqualTo("Pass"));
            Assert.That(completedRun.steps.Count, Is.EqualTo(2));

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string latestArtifactPath = Path.Combine(projectRoot, "Logs", "Telemetry", "manual-tests", "manual-test-latest.json");
            Assert.That(File.Exists(latestArtifactPath), Is.True);
            Assert.That(File.ReadAllText(latestArtifactPath), Does.Contain("foundation-ui-sanity"));
        }
    }
}

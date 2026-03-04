using System.Collections;
using System.IO;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shell.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ggj2026.Game.Tests.PlayMode
{
    public class SessionTelemetryPlayModeTests
    {
        [UnityTest]
        public IEnumerator ActiveService_WritesUiActionLog()
        {
            yield return null;

            Assert.That(SessionTelemetryService.Active, Is.Not.Null);

            SessionTelemetryService.Active.RecordUiAction(
                SessionActionKind.ButtonClicked,
                SessionActorKind.AutomatedTest,
                new SessionUiSource
                {
                    panelId = "TestPanel",
                    elementId = "TestButton",
                    controller = nameof(SessionTelemetryPlayModeTests)
                },
                "{\"action\":\"test\"}",
                "Play mode telemetry test.");

            yield return null;

            string uiActionPath = Path.Combine(SessionTelemetryService.Active.CurrentSessionDirectory, "ui-actions.jsonl");
            Assert.That(File.Exists(uiActionPath), Is.True);
            Assert.That(File.ReadAllText(uiActionPath).Contains("TestButton"), Is.True);
        }
    }
}

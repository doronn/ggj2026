using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ggj2026.Game.Authoring.ManualTests
{
    [Serializable]
    public class ManualTestExpectationDefinition
    {
        [TextArea(2, 5)]
        public string expectedResult = "Expected result.";

        [TextArea(2, 5)]
        public string sanityChecks = "Sanity checks.";

        public bool requiresTelemetryArtifact = true;
        public bool requiresScreenshot;
    }

    [Serializable]
    public class ManualTestStepDefinition
    {
        public string stepId = "step-1";
        public string title = "Step";
        [TextArea(3, 8)]
        public string instruction = "Perform an action.";
        public string requiredScene = "ClientWorld";
        public ManualTestExpectationDefinition expectation = new ManualTestExpectationDefinition();
    }

    [CreateAssetMenu(fileName = "ManualTestScenario", menuName = "GGJ2026/QA/Manual Test Scenario")]
    public class ManualTestScenarioDefinition : ScriptableObject
    {
        public string scenarioId = "manual-test";
        public string milestone = "foundation";
        public string category = "UI sanity";
        public string displayName = "Manual Test Scenario";
        [TextArea(2, 6)]
        public string summary = "Guided scenario.";
        public List<ManualTestStepDefinition> steps = new List<ManualTestStepDefinition>();
    }
}

using System;
using System.Collections.Generic;
using Ggj2026.Game.Authoring.ManualTests;
using Ggj2026.Game.Shared.Interfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public class ManualTestRunnerWindow : EditorWindow, IManualTestRunner
    {
        private const string UxmlPath = "Assets/_Game/UI/Editor/UXML/ManualTestRunnerWindow.uxml";
        private const string UssPath = "Assets/_Game/UI/Editor/USS/EditorWindows.uss";

        private readonly List<ManualTestScenarioDefinition> _scenarios = new List<ManualTestScenarioDefinition>();
        private readonly List<string> _scenarioNames = new List<string>();

        private PopupField<string> _scenarioPopup;
        private Label _scenarioSummaryLabel;
        private Label _stepTitleLabel;
        private Label _stepInstructionLabel;
        private Label _stepExpectationLabel;
        private TextField _stepNoteField;
        private Label _runLogLabel;

        private ManualTestScenarioDefinition _currentScenario;

        [MenuItem("Window/GGJ2026/QA/Manual Test Runner")]
        public static void ShowWindow()
        {
            var window = GetWindow<ManualTestRunnerWindow>("Manual Test Runner");
            window.minSize = new Vector2(620f, 480f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null && !rootVisualElement.styleSheets.Contains(uss))
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            if (uxml != null)
            {
                uxml.CloneTree(rootVisualElement);
            }
            else
            {
                BuildFallbackUi(rootVisualElement);
            }

            _scenarioSummaryLabel = rootVisualElement.Q<Label>("ScenarioSummaryLabel");
            _stepTitleLabel = rootVisualElement.Q<Label>("StepTitleLabel");
            _stepInstructionLabel = rootVisualElement.Q<Label>("StepInstructionLabel");
            _stepExpectationLabel = rootVisualElement.Q<Label>("StepExpectationLabel");
            _stepNoteField = rootVisualElement.Q<TextField>("StepNoteField");
            _runLogLabel = rootVisualElement.Q<Label>("RunLogLabel");

            VisualElement selectionRow = rootVisualElement.Q<VisualElement>("ScenarioSelectionRow");
            selectionRow.Clear();

            LoadScenarioAssets();
            _scenarioPopup = CreateScenarioPopup();
            _scenarioPopup.RegisterValueChangedCallback(_ => SelectScenario());
            selectionRow.Add(_scenarioPopup);
            selectionRow.Add(CreateButton("Refresh", LoadAndRefresh));
            selectionRow.Add(CreateButton("Create Sample", CreateSampleScenario));
            selectionRow.Add(CreateButton("Start Run", StartSelectedScenario));

            VisualElement stepActionRow = rootVisualElement.Q<VisualElement>("StepActionRow");
            stepActionRow.Add(CreateButton("Pass", () => RecordStepOutcome("Pass")));
            stepActionRow.Add(CreateButton("Fail", () => RecordStepOutcome("Fail")));
            stepActionRow.Add(CreateButton("Blocked", () => RecordStepOutcome("Blocked")));
            stepActionRow.Add(CreateButton("Skip", () => RecordStepOutcome("Skipped")));
            stepActionRow.Add(CreateButton("Finish Run", FinishRun));

            SelectScenario();
        }

        public void StartRun(string scenarioId, string scenarioTitle)
        {
            ManualTestExecutionService.TryStartRun(scenarioId, out _, out _);
            UpdateView();
        }

        public void RecordStep(string stepId, string title, string instruction, string result, string note)
        {
            ManualTestExecutionService.TryRecordCurrentStep(result, note, out _, out _);
        }

        public void FinishRun()
        {
            if (ManualTestExecutionService.TryFinishRun(out Ggj2026.Game.Shared.Telemetry.ManualTestRunRecord completedRun, out string error))
            {
                _runLogLabel.text = JsonUtility.ToJson(completedRun, true);
            }
            else
            {
                _runLogLabel.text = error;
            }
        }

        private void LoadAndRefresh()
        {
            LoadScenarioAssets();
            SelectScenario();
        }

        private void LoadScenarioAssets()
        {
            _scenarios.Clear();
            _scenarioNames.Clear();

            List<ManualTestScenarioDefinition> loadedScenarios = ManualTestExecutionService.LoadScenarios();
            for (int i = 0; i < loadedScenarios.Count; i++)
            {
                ManualTestScenarioDefinition scenario = loadedScenarios[i];
                _scenarios.Add(scenario);
                _scenarioNames.Add(string.IsNullOrWhiteSpace(scenario.displayName) ? scenario.name : scenario.displayName);
            }

            if (_scenarioPopup != null)
            {
                List<string> choices = _scenarioNames.Count > 0 ? _scenarioNames : new List<string> { "No scenarios found" };
                _scenarioPopup.choices.Clear();
                for (int i = 0; i < choices.Count; i++)
                {
                    _scenarioPopup.choices.Add(choices[i]);
                }
                if (_scenarioNames.Count > 0)
                {
                    _scenarioPopup.value = _scenarioNames[0];
                }
                else
                {
                    _scenarioPopup.value = choices[0];
                }
            }
        }

        private void SelectScenario()
        {
            if (_scenarioNames.Count == 0 || _scenarioPopup == null)
            {
                _currentScenario = null;
                UpdateView();
                return;
            }

            int index = Mathf.Max(0, _scenarioNames.IndexOf(_scenarioPopup.value));
            if (index >= _scenarios.Count)
            {
                index = 0;
            }

            _currentScenario = _scenarios.Count > 0 ? _scenarios[index] : null;
            UpdateView();
        }

        private void StartSelectedScenario()
        {
            if (_currentScenario == null)
            {
                return;
            }

            StartRun(_currentScenario.scenarioId, _currentScenario.displayName);
        }

        private void RecordStepOutcome(string outcome)
        {
            if (_currentScenario == null)
            {
                return;
            }

            RecordStep(string.Empty, string.Empty, string.Empty, outcome, _stepNoteField.value);
            _stepNoteField.value = string.Empty;
            UpdateView();
        }

        private void UpdateView()
        {
            if (_currentScenario == null)
            {
                _scenarioSummaryLabel.text = "No manual test scenario found. Create a sample scenario or add assets under Assets/_Game/Authoring/ManualTests.";
                _stepTitleLabel.text = "No step";
                _stepInstructionLabel.text = string.Empty;
                _stepExpectationLabel.text = string.Empty;
                return;
            }

            _scenarioSummaryLabel.text = $"Milestone: {_currentScenario.milestone}\nCategory: {_currentScenario.category}\nSummary: {_currentScenario.summary}";

            ManualTestExecutionService.ManualTestExecutionStatus status = ManualTestExecutionService.GetStatus();
            if (_currentScenario.steps.Count == 0)
            {
                _stepTitleLabel.text = "No steps";
                _stepInstructionLabel.text = "Scenario has no steps.";
                _stepExpectationLabel.text = string.Empty;
                _runLogLabel.text = status.hasActiveRun ? ManualTestExecutionService.ToJson(status) : "No active run.";
                return;
            }

            int clampedIndex = status.hasActiveRun && status.scenarioId == _currentScenario.scenarioId
                ? Mathf.Clamp(status.currentStepIndex, 0, _currentScenario.steps.Count - 1)
                : 0;
            ManualTestStepDefinition step = _currentScenario.steps[clampedIndex];
            _stepTitleLabel.text = $"{clampedIndex + 1}/{_currentScenario.steps.Count}: {step.title}";
            _stepInstructionLabel.text = $"Scene: {step.requiredScene}\nInstruction: {step.instruction}";
            _stepExpectationLabel.text =
                $"Expected: {step.expectation.expectedResult}\n" +
                $"Sanity: {step.expectation.sanityChecks}\n" +
                $"Needs telemetry: {step.expectation.requiresTelemetryArtifact}\n" +
                $"Needs screenshot: {step.expectation.requiresScreenshot}";

            _runLogLabel.text = status.hasActiveRun ? ManualTestExecutionService.ToJson(status) : "No active run.";
        }

        private void CreateSampleScenario()
        {
            ManualTestExecutionService.EnsureSeedScenarios();
            LoadAndRefresh();
        }

        private static Button CreateButton(string text, Action onClick)
        {
            return new Button(onClick) { text = text };
        }

        private static void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexGrow = 1f;
            root.Add(container);
            container.Add(new Label("Manual Test Runner"));
            container.Add(new VisualElement { name = "ScenarioSelectionRow" });
            container.Add(new Label { name = "ScenarioSummaryLabel" });
            container.Add(new Label { name = "StepTitleLabel" });
            container.Add(new Label { name = "StepInstructionLabel" });
            container.Add(new Label { name = "StepExpectationLabel" });
            container.Add(new TextField { name = "StepNoteField" });
            container.Add(new VisualElement { name = "StepActionRow" });
            container.Add(new Label { name = "RunLogLabel" });
        }

        private PopupField<string> CreateScenarioPopup()
        {
            List<string> choices = _scenarioNames.Count > 0 ? _scenarioNames : new List<string> { "No scenarios found" };
            return new PopupField<string>("Scenario", choices, 0);
        }
    }
}

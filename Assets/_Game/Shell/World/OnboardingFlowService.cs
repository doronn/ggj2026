using System.Collections.Generic;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.UI;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    public sealed class OnboardingFlowService
    {
        private const string CompletedKey = "Ggj2026.Onboarding.Completed";

        private readonly List<OnboardingDefinition> _steps = new List<OnboardingDefinition>();
        private bool _isActive;
        private bool _skippedThisSession;
        private int _stepIndex;

        public OnboardingFlowService()
        {
            _steps.Add(new OnboardingDefinition(
                "welcome",
                "Welcome",
                "This short guide shows only the parts you need first. You can skip it for this session at any time.",
                "Start with the world and the few actions that matter right now.",
                "This is a learning overlay. It hides advanced options until the guide reaches them.",
                OnboardingFocusArea.WorldView,
                new[] { "simulation" },
                new[] { InspectorTabKind.World },
                worldMarkerCell: HexCoord.Zero));
            _steps.Add(new OnboardingDefinition(
                "world-bar",
                "Read The Top Bar",
                "The top bar tells you the current tick, simulation state, workers, growth, blockers, and resources.",
                "If the colony stops, read Blockers first.",
                "Green or neutral numbers are healthy. Warning blocks mean the colony is waiting on something.",
                OnboardingFocusArea.WorldTopBar,
                new[] { "simulation", "view", "context" },
                new[] { InspectorTabKind.World }));
            _steps.Add(new OnboardingDefinition(
                "command-panel",
                "Use The Command Panel",
                "Choose only a few actions first: Play Real Time, Floors, Edges, and the Next panel. Then act on the world.",
                "Use Play Real Time, then finish rooms by painting floors and closing them with walls and doors.",
                "The Next group shows the best action for what you selected, so you do not need to hunt for every tool.",
                OnboardingFocusArea.OperationsRail,
                new[] { "simulation", "view", "context", "floors", "edges" },
                new[] { InspectorTabKind.World },
                tryItActionId: GameActionId.SetFloorTool));
            _steps.Add(new OnboardingDefinition(
                "inspect-world",
                "Inspect What You Selected",
                "The right panel explains the selected tile, edge, worker, room, or world summary. Click the world first, then read this panel.",
                "Selection explains the game.",
                "Context explains the selected thing. Tasks shows urgent work. World shows colony health and the biggest blocker.",
                OnboardingFocusArea.ContextInspector,
                new[] { "simulation", "context", "floors", "edges", "modules", "cleanup" },
                new[] { InspectorTabKind.Context, InspectorTabKind.Tasks, InspectorTabKind.World },
                worldMarkerCell: HexCoord.Zero,
                tryItActionId: GameActionId.SelectCell));
            _steps.Add(new OnboardingDefinition(
                "event-strip",
                "Watch The Bottom Strip",
                "The bottom strip tells you your current tool, what you are hovering, the last action result, and the main blocker.",
                "If you are confused, read the last action and the blocker line.",
                "This is the fastest place to understand why a click did or did not work.",
                OnboardingFocusArea.EventStrip,
                new[] { "simulation", "context", "floors", "edges", "modules", "cleanup", "world" },
                new[] { InspectorTabKind.Context, InspectorTabKind.Tasks, InspectorTabKind.World },
                tryItActionId: GameActionId.PaintFloor));
            _steps.Add(new OnboardingDefinition(
                "systems",
                "Your First Good Run",
                "Start realtime. Finish a reachable residential room with a bed, then finish the workshop, then build weaponry, then train defenders.",
                "Reachable room first, growth second, production third, defense last.",
                "If workers stop, the top blocker usually tells you why: no door, no path, or no source.",
                OnboardingFocusArea.WorldView,
                new[] { "simulation", "view", "context", "floors", "edges", "modules", "landmarks", "cleanup", "world" },
                new[] { InspectorTabKind.Context, InspectorTabKind.Tasks, InspectorTabKind.Entities, InspectorTabKind.World },
                worldMarkerCell: HexCoord.Zero));
            _steps.Add(new OnboardingDefinition(
                "done",
                "Guide Complete",
                "You can now explore freely. If you forget something, restart the guide from the World section.",
                "Use blockers and context before guessing.",
                "Next time the guide will stay hidden unless you start it again.",
                OnboardingFocusArea.None,
                new[] { "simulation", "view", "context", "floors", "edges", "modules", "landmarks", "cleanup", "world" },
                new[] { InspectorTabKind.Context, InspectorTabKind.Tasks, InspectorTabKind.Entities, InspectorTabKind.World }));

            _isActive = !IsCompleted();
            _stepIndex = 0;
        }

        public bool IsActive => _isActive;

        public void Start()
        {
            _skippedThisSession = false;
            _isActive = true;
            _stepIndex = 0;
        }

        public bool TryGoBack(out string message)
        {
            if (!_isActive)
            {
                message = "Guide is not active.";
                return false;
            }

            if (_stepIndex <= 0)
            {
                message = "Already at the first guide step.";
                return false;
            }

            _stepIndex--;
            message = $"Guide step {_stepIndex + 1} of {_steps.Count}.";
            return true;
        }

        public bool TryAdvance(out string message)
        {
            if (!_isActive)
            {
                message = "Guide is not active.";
                return false;
            }

            if (_stepIndex >= _steps.Count - 1)
            {
                Complete();
                message = "Guide complete.";
                return true;
            }

            _stepIndex++;
            if (_stepIndex >= _steps.Count - 1)
            {
                message = "Final guide step.";
                return true;
            }

            message = $"Guide step {_stepIndex + 1} of {_steps.Count}.";
            return true;
        }

        public bool TrySkip(out string message)
        {
            if (!_isActive)
            {
                message = "Guide is already hidden.";
                return false;
            }

            _skippedThisSession = true;
            _isActive = false;
            message = "Guide skipped for this session.";
            return true;
        }

        public void Complete()
        {
            _isActive = false;
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.Save();
        }

        public OnboardingStepViewModel BuildViewModel()
        {
            if (!_isActive || _skippedThisSession || _steps.Count == 0)
            {
                return new OnboardingStepViewModel
                {
                    isActive = false,
                    stepCount = _steps.Count
                };
            }

            OnboardingDefinition step = _steps[Mathf.Clamp(_stepIndex, 0, _steps.Count - 1)];
            return new OnboardingStepViewModel
            {
                isActive = true,
                stepId = step.StepId,
                title = step.Title,
                body = step.Body,
                emphasis = step.Emphasis,
                tip = step.Tip,
                focusArea = step.FocusArea,
                stepIndex = _stepIndex,
                stepCount = _steps.Count,
                canGoBack = _stepIndex > 0,
                canSkip = true,
                isFinalStep = _stepIndex >= _steps.Count - 1,
                progressText = $"Guide {_stepIndex + 1}/{_steps.Count}",
                visibleOperationGroups = new List<string>(step.VisibleOperationGroups),
                visibleInspectorTabs = new List<InspectorTabKind>(step.VisibleInspectorTabs),
                hasWorldMarkerCell = step.WorldMarkerCell.HasValue,
                worldMarkerCell = step.WorldMarkerCell ?? HexCoord.Zero,
                tryItActionId = step.TryItActionId ?? GameActionId.None,
                hasTryItAction = step.TryItActionId.HasValue
            };
        }

        public bool IsOperationGroupVisible(string groupId)
        {
            if (!_isActive || _skippedThisSession)
            {
                return true;
            }

            OnboardingDefinition step = _steps[Mathf.Clamp(_stepIndex, 0, _steps.Count - 1)];
            return step.VisibleOperationGroups.Contains(groupId);
        }

        public bool IsInspectorTabVisible(InspectorTabKind tabKind)
        {
            if (!_isActive || _skippedThisSession)
            {
                return true;
            }

            OnboardingDefinition step = _steps[Mathf.Clamp(_stepIndex, 0, _steps.Count - 1)];
            return step.VisibleInspectorTabs.Contains(tabKind);
        }

        public void NotifyActionPerformed(GameActionId actionId)
        {
            if (!_isActive || _skippedThisSession || actionId == GameActionId.None)
            {
                return;
            }

            OnboardingDefinition step = _steps[Mathf.Clamp(_stepIndex, 0, _steps.Count - 1)];
            if (!step.TryItActionId.HasValue || step.TryItActionId.Value != actionId)
            {
                return;
            }

            TryAdvance(out _);
        }

        public string GetPanelTooltip(string panelId)
        {
            return panelId switch
            {
                "WorldTopBar" => "World status. Read ticks, workers, growth, blockers, and resources here.",
                "OperationsRailPanel" => "Command panel. Choose a tool or a colony action here, then act on the world.",
                "ContextInspectorPanel" => "Inspector. This explains the selected tile, edge, worker, room, or world state.",
                "EventStripPanel" => "Live hints. Read your current tool, hover details, last action result, and blocker here.",
                _ => string.Empty
            };
        }

        private static bool IsCompleted()
        {
            return PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        }

        private sealed class OnboardingDefinition
        {
            public OnboardingDefinition(
                string stepId,
                string title,
                string body,
                string emphasis,
                string tip,
                OnboardingFocusArea focusArea,
                IReadOnlyList<string> visibleOperationGroups,
                IReadOnlyList<InspectorTabKind> visibleInspectorTabs,
                HexCoord? worldMarkerCell = null,
                GameActionId? tryItActionId = null)
            {
                StepId = stepId;
                Title = title;
                Body = body;
                Emphasis = emphasis;
                Tip = tip;
                FocusArea = focusArea;
                VisibleOperationGroups = new List<string>(visibleOperationGroups);
                VisibleInspectorTabs = new List<InspectorTabKind>(visibleInspectorTabs);
                WorldMarkerCell = worldMarkerCell;
                TryItActionId = tryItActionId;
            }

            public string StepId { get; }
            public string Title { get; }
            public string Body { get; }
            public string Emphasis { get; }
            public string Tip { get; }
            public OnboardingFocusArea FocusArea { get; }
            public List<string> VisibleOperationGroups { get; }
            public List<InspectorTabKind> VisibleInspectorTabs { get; }
            public HexCoord? WorldMarkerCell { get; }
            public GameActionId? TryItActionId { get; }
        }
    }
}

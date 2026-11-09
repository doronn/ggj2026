using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ggj2026.Game.Shell.World
{
    public sealed class GameQueryService : IGameQueryService, ISelectionContextResolver, IContextInspectorModelBuilder, IWorldBarModelBuilder, IEventStripModelBuilder
    {
        private readonly LocalColonyGameplayService _gameplayService;
        private readonly LocalWorldDebugRenderer _renderer;
        private readonly RealTimeSimulationController _realTimeSimulationController;
        private readonly GameActionService _actionService;
        private readonly OnboardingFlowService _onboardingFlowService;

        public GameQueryService(LocalColonyGameplayService gameplayService, LocalWorldDebugRenderer renderer, RealTimeSimulationController realTimeSimulationController, GameActionService actionService, OnboardingFlowService onboardingFlowService = null)
        {
            _gameplayService = gameplayService;
            _renderer = renderer;
            _realTimeSimulationController = realTimeSimulationController;
            _actionService = actionService;
            _onboardingFlowService = onboardingFlowService;
        }

        public HumanAgentHudState BuildHudState()
        {
            GameSelectionState selection = BuildSelectionState();
            List<BlockerSummaryState> blockers = BuildBlockerSummaries();
            return new HumanAgentHudState
            {
                sessionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.SessionId : string.Empty,
                sceneName = SceneManager.GetActiveScene().name,
                onboarding = _onboardingFlowService != null ? _onboardingFlowService.BuildViewModel() : new OnboardingStepViewModel(),
                selection = selection,
                worldBar = BuildWorldBar(blockers),
                operationsRail = BuildOperationsRail(selection),
                contextInspector = Build(selection, blockers),
                eventStrip = BuildEventStrip(selection, blockers),
                entities = BuildEntitySummaries(),
                blockers = blockers,
                availableActions = BuildAvailableActions(selection),
                milestones = _gameplayService != null ? _gameplayService.GetMilestoneStates() : new List<MilestoneState>()
            };
        }

        public HumanAgentHudState BuildRuntimeHudState()
        {
            GameSelectionState selection = BuildSelectionState();
            List<BlockerSummaryState> blockers = BuildBlockerSummaries();
            return new HumanAgentHudState
            {
                sessionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.SessionId : string.Empty,
                sceneName = SceneManager.GetActiveScene().name,
                onboarding = _onboardingFlowService != null ? _onboardingFlowService.BuildViewModel() : new OnboardingStepViewModel(),
                selection = selection,
                worldBar = BuildWorldBar(blockers),
                operationsRail = BuildOperationsRail(selection),
                contextInspector = Build(selection, blockers),
                eventStrip = BuildEventStrip(selection, blockers),
                milestones = _gameplayService != null ? _gameplayService.GetMilestoneStates() : new List<MilestoneState>()
            };
        }

        public GameSelectionState BuildSelectionState()
        {
            var selection = new GameSelectionState();
            if (_renderer == null)
            {
                selection.primaryTargetKind = SelectionTargetKind.World;
                selection.resolvedContextKind = ContextKind.World;
                return selection;
            }

            if (_renderer.SelectedHex.HasValue)
            {
                selection.selectedCell = _renderer.SelectedHex.Value;
                selection.hasSelectedCell = true;
            }

            if (_renderer.SelectedEdge.HasValue)
            {
                selection.selectedEdge = _renderer.SelectedEdge.Value;
                selection.hasSelectedEdge = true;
            }

            if (_renderer.HoverHex.HasValue)
            {
                selection.hoveredCell = _renderer.HoverHex.Value;
                selection.hasHoveredCell = true;
            }

            if (_renderer.HoverEdge.HasValue)
            {
                selection.hoveredEdge = _renderer.HoverEdge.Value;
                selection.hasHoveredEdge = true;
            }

            if (_actionService != null && _actionService.SelectedEntityUid != EntityUid.None)
            {
                selection.selectedEntityId = _actionService.SelectedEntityUid.ToString();
                selection.primaryTargetKind = SelectionTargetKind.Entity;
                selection.resolvedContextKind = ContextKind.Unit;
                return selection;
            }

            if (selection.hasSelectedCell)
            {
                ResolveCellDerivedSelection(selection, selection.selectedCell);
                if (selection.primaryTargetKind != SelectionTargetKind.None)
                {
                    return selection;
                }
            }

            if (selection.hasSelectedEdge)
            {
                selection.primaryTargetKind = SelectionTargetKind.Edge;
                selection.resolvedContextKind = ContextKind.Edge;
                return selection;
            }

            if (selection.hasHoveredCell)
            {
                ResolveCellDerivedSelection(selection, selection.hoveredCell);
                if (selection.primaryTargetKind == SelectionTargetKind.None)
                {
                    selection.primaryTargetKind = SelectionTargetKind.Cell;
                    selection.resolvedContextKind = ContextKind.Cell;
                }

                return selection;
            }

            if (selection.hasHoveredEdge)
            {
                selection.primaryTargetKind = SelectionTargetKind.Edge;
                selection.resolvedContextKind = ContextKind.Edge;
                return selection;
            }

            selection.primaryTargetKind = SelectionTargetKind.World;
            selection.resolvedContextKind = ContextKind.World;
            return selection;
        }

        public ContextInspectorViewModel BuildContextInspectorState()
        {
            return Build(BuildSelectionState(), BuildBlockerSummaries());
        }

        public ContextInspectorViewModel Build(GameSelectionState selectionState)
        {
            return Build(selectionState, BuildBlockerSummaries());
        }

        public ContextInspectorViewModel Build(GameSelectionState selectionState, List<BlockerSummaryState> blockers = null)
        {
            blockers ??= BuildBlockerSummaries();
            var inspector = new ContextInspectorViewModel
            {
                activeTab = _actionService != null ? _actionService.ActiveInspectorTab : InspectorTabKind.World,
                contextKind = selectionState != null ? selectionState.resolvedContextKind : ContextKind.World,
                title = "World",
                subtitle = "No target selected.",
                summary = "Select a tile, edge, or worker to inspect it."
            };

            if (_onboardingFlowService != null && !_onboardingFlowService.IsInspectorTabVisible(inspector.activeTab))
            {
                if (_onboardingFlowService.IsInspectorTabVisible(InspectorTabKind.World))
                {
                    inspector.activeTab = InspectorTabKind.World;
                }
                else if (_onboardingFlowService.IsInspectorTabVisible(InspectorTabKind.Context))
                {
                    inspector.activeTab = InspectorTabKind.Context;
                }
                else if (_onboardingFlowService.IsInspectorTabVisible(InspectorTabKind.Tasks))
                {
                    inspector.activeTab = InspectorTabKind.Tasks;
                }
                else
                {
                    inspector.activeTab = InspectorTabKind.Entities;
                }
            }

            if (_gameplayService == null || _gameplayService.Snapshot == null)
            {
                inspector.worldCards.Add(new ContextCardViewModel
                {
                    cardId = "world-empty",
                    title = "World",
                    subtitle = "No world loaded",
                    summary = "Start or load a world to see context."
                });
                return inspector;
            }

            switch (selectionState.resolvedContextKind)
            {
                case ContextKind.Unit:
                    BuildUnitContext(inspector, selectionState.selectedEntityId);
                    inspector.activeTab = InspectorTabKind.Context;
                    break;

                case ContextKind.Module:
                case ContextKind.Stack:
                case ContextKind.Landmark:
                case ContextKind.Cell:
                    BuildCellDerivedContext(inspector, selectionState.hasSelectedCell ? selectionState.selectedCell : selectionState.hoveredCell);
                    if (selectionState.hasSelectedCell)
                    {
                        inspector.activeTab = InspectorTabKind.Context;
                    }
                    break;

                case ContextKind.Edge:
                    BuildEdgeContext(inspector, selectionState.hasSelectedEdge ? selectionState.selectedEdge : selectionState.hoveredEdge);
                    if (selectionState.hasSelectedEdge)
                    {
                        inspector.activeTab = InspectorTabKind.Context;
                    }
                    break;

                default:
                    BuildWorldContext(inspector, blockers);
                    break;
            }

            switch (inspector.activeTab)
            {
                case InspectorTabKind.Tasks:
                    inspector.taskCards.AddRange(BuildTaskCards(blockers));
                    break;

                case InspectorTabKind.Entities:
                    inspector.entityCards.AddRange(BuildEntityCards());
                    break;

                case InspectorTabKind.World:
                    inspector.worldCards.AddRange(BuildWorldCards(selectionState));
                    break;
            }

            return inspector;
        }

        public List<EntitySummaryState> BuildEntitySummaries()
        {
            var summaries = new List<EntitySummaryState>();
            if (_gameplayService?.Snapshot?.units == null)
            {
                return summaries;
            }

            EntityUid selectedEntity = _actionService != null ? _actionService.SelectedEntityUid : EntityUid.None;
            for (int i = 0; i < _gameplayService.Snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _gameplayService.Snapshot.units[i];
                summaries.Add(new EntitySummaryState
                {
                    entityId = unit.entityUid.ToString(),
                    displayName = $"{unit.unitRole} {unit.entityUid.ToString().Substring(0, 6)}",
                    status = DescribeWorkerState(unit),
                    detail = DescribeWorkerDetail(unit),
                    isSelected = unit.entityUid == selectedEntity,
                    focusAction = _actionService.Describe(_actionService.CreateEnvelope(
                        GameActionId.SelectEntity,
                        targetEntityId: unit.entityUid.ToString()))
                });
            }

            return summaries;
        }

        public List<BlockerSummaryState> BuildBlockerSummaries()
        {
            var blockers = new List<BlockerSummaryState>();
            if (_gameplayService?.Snapshot == null)
            {
                return blockers;
            }

            ColonySnapshot colony = _gameplayService.GetPrimaryColony();
            if (colony != null && colony.populationGrowth != null && !string.IsNullOrEmpty(colony.populationGrowth.blockedReason))
            {
                blockers.Add(new BlockerSummaryState
                {
                    title = "Growth blocked",
                    detail = colony.populationGrowth.blockedReason,
                    severity = "warning"
                });
            }

            if (colony?.soldierTraining != null &&
                colony.soldierTraining.queuedCount > 0 &&
                !string.IsNullOrEmpty(colony.soldierTraining.blockedReason))
            {
                blockers.Add(new BlockerSummaryState
                {
                    title = "Soldier training",
                    detail = colony.soldierTraining.blockedReason,
                    severity = "warning"
                });
            }

            if (colony?.threatAlert != null && colony.threatAlert.threatLevel > Ggj2026.Game.Shared.Combat.ThreatLevel.None)
            {
                blockers.Add(new BlockerSummaryState
                {
                    title = "Threat",
                    detail = colony.threatAlert.summary,
                    severity = colony.threatAlert.threatLevel >= Ggj2026.Game.Shared.Combat.ThreatLevel.High ? "critical" : "warning"
                });
            }

            if (colony?.raidEvent != null && colony.raidEvent.isActive)
            {
                blockers.Add(new BlockerSummaryState
                {
                    title = "Raid",
                    detail = colony.raidEvent.summary,
                    severity = "critical"
                });
            }

            var missingByResource = new Dictionary<ResourceType, int>();
            for (int i = 0; i < _gameplayService.Snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _gameplayService.Snapshot.constructionFeatures[i];
                if (construction == null || construction.isCompleted)
                {
                    continue;
                }

                for (int j = 0; j < construction.deliveredMaterials.Count; j++)
                {
                    MaterialDeliveryState delivery = construction.deliveredMaterials[j];
                    int missing = Mathf.Max(0, delivery.requiredAmount - delivery.deliveredAmount);
                    if (missing <= 0)
                    {
                        continue;
                    }

                    if (!missingByResource.TryAdd(delivery.resourceType, missing))
                    {
                        missingByResource[delivery.resourceType] += missing;
                    }
                }
            }

            foreach (KeyValuePair<ResourceType, int> pair in missingByResource)
            {
                int freeAmount = CountFreeResource(pair.Key);
                int stagedAmount = CountReservedConstructionResource(pair.Key);
                int nodeAmount = CountResourceNodeYield(pair.Key);
                string severity = freeAmount + nodeAmount > 0 ? "warning" : "critical";
                string detail = freeAmount + nodeAmount > 0
                    ? $"Need {pair.Value}. Free {freeAmount}, staged {stagedAmount}, nodes {nodeAmount}."
                    : $"Need {pair.Value}. No reachable source. Staged {stagedAmount}.";
                blockers.Add(new BlockerSummaryState
                {
                    title = pair.Key.ToString(),
                    detail = detail,
                    severity = severity
                });
            }

            if (_gameplayService.Snapshot.cachedSpaces != null)
            {
                int inactiveSpacesAdded = 0;
                for (int i = 0; i < _gameplayService.Snapshot.cachedSpaces.Count && inactiveSpacesAdded < 3; i++)
                {
                    SpaceRegionState space = _gameplayService.Snapshot.cachedSpaces[i];
                    if (space == null ||
                        space.activationState != SpaceActivationState.Blocked ||
                        space.cells == null ||
                        space.cells.Count == 0)
                    {
                        continue;
                    }

                    if (colony != null && space.cells[0].DistanceTo(colony.center) > 12)
                    {
                        continue;
                    }

                    string detail = _gameplayService.DescribeSpaceActivation(space.cells[0]);
                    blockers.Add(new BlockerSummaryState
                    {
                        title = $"{space.purposeKind} inactive",
                        detail = $"{space.cells[0]}: {detail}",
                        severity = "warning"
                    });
                    inactiveSpacesAdded++;
                }
            }

            int plannedShelterAdded = 0;
            for (int i = 0; i < _gameplayService.Snapshot.constructionFeatures.Count && plannedShelterAdded < 3; i++)
            {
                ConstructionState construction = _gameplayService.Snapshot.constructionFeatures[i];
                if (construction == null ||
                    construction.isCompleted ||
                    construction.blueprint.featureKind != ConstructionFeatureKind.Floor)
                {
                    continue;
                }

                if (colony != null && construction.targetCell.DistanceTo(colony.center) > 12)
                {
                    continue;
                }

                string detail = _gameplayService.DescribeSpaceActivation(construction.targetCell);
                if (!detail.StartsWith("Needs enclosed ", StringComparison.Ordinal))
                {
                    continue;
                }

                blockers.Add(new BlockerSummaryState
                {
                    title = $"{construction.blueprint.floorPurposeKind} planned",
                    detail = $"{construction.targetCell}: {detail}",
                    severity = "warning"
                });
                plannedShelterAdded++;
            }

            int notFormedShelterAdded = 0;
            if (_gameplayService.Snapshot.chunks != null)
            {
                for (int i = 0; i < _gameplayService.Snapshot.chunks.Count && notFormedShelterAdded < 3; i++)
                {
                    List<HexCellState> cells = _gameplayService.Snapshot.chunks[i].modifiedCells;
                    for (int j = 0; j < cells.Count && notFormedShelterAdded < 3; j++)
                    {
                        if (cells[j].floor == null || !cells[j].floor.isBuilt || _gameplayService.GetSpaceAtCell(cells[j].coord) != null)
                        {
                            continue;
                        }

                        if (colony != null && cells[j].coord.DistanceTo(colony.center) > 12)
                        {
                            continue;
                        }

                        string detail = _gameplayService.DescribeSpaceActivation(cells[j].coord);
                        if (!detail.StartsWith("Needs enclosed ", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        blockers.Add(new BlockerSummaryState
                        {
                            title = $"{cells[j].floor.purposeKind} inactive",
                            detail = $"{cells[j].coord}: {detail}",
                            severity = "warning"
                        });
                        notFormedShelterAdded++;
                    }
                }
            }

            int moduleAlertsAdded = 0;
            if (_gameplayService.Snapshot.chunks != null)
            {
                for (int i = 0; i < _gameplayService.Snapshot.chunks.Count && moduleAlertsAdded < 3; i++)
                {
                    List<HexCellState> cells = _gameplayService.Snapshot.chunks[i].modifiedCells;
                    for (int j = 0; j < cells.Count && moduleAlertsAdded < 3; j++)
                    {
                        if (cells[j].module == null || !cells[j].module.isBuilt)
                        {
                            continue;
                        }

                        string moduleStatus = _gameplayService.DescribeModuleStatus(cells[j].coord);
                        if (!moduleStatus.StartsWith("Waiting for ", StringComparison.Ordinal) &&
                            !moduleStatus.StartsWith("Needs ", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        blockers.Add(new BlockerSummaryState
                        {
                            title = $"{cells[j].module.moduleKind} waiting",
                            detail = $"{cells[j].coord}: {moduleStatus}",
                            severity = "warning"
                        });
                        moduleAlertsAdded++;
                    }
                }
            }

            if (blockers.Count == 0)
            {
                blockers.Add(new BlockerSummaryState
                {
                    title = "No blockers",
                    detail = "Current queues have usable supply paths.",
                    severity = "ok"
                });
            }

            return blockers;
        }

        public List<GameActionDescriptor> BuildAvailableActions()
        {
            return BuildAvailableActions(BuildSelectionState());
        }

        private List<GameActionDescriptor> BuildAvailableActions(GameSelectionState selection)
        {
            var actions = new List<GameActionDescriptor>();
            actions.AddRange(BuildOperationsActions());
            actions.AddRange(_actionService.GetContextActions(selection));
            return actions;
        }

        WorldBarState IWorldBarModelBuilder.Build()
        {
            return BuildWorldBar(BuildBlockerSummaries());
        }

        EventStripState IEventStripModelBuilder.Build()
        {
            return BuildEventStrip(BuildSelectionState(), BuildBlockerSummaries());
        }

        private WorldBarState BuildWorldBar(List<BlockerSummaryState> blockers)
        {
            var state = new WorldBarState
            {
                worldName = _gameplayService?.Snapshot != null ? _gameplayService.Snapshot.worldId.ToString() : "No World",
                tickText = _gameplayService?.Snapshot != null ? $"Tick {_gameplayService.Snapshot.simulationTick}" : "Tick -",
                simulationText = _realTimeSimulationController == null
                    ? "Realtime unavailable"
                    : _realTimeSimulationController.IsRunning
                        ? $"Running x{_realTimeSimulationController.SpeedMultiplier:0.##}"
                        : $"Paused x{_realTimeSimulationController.SpeedMultiplier:0.##}",
                saveStateText = _gameplayService?.Snapshot != null ? "Local persistent world" : "No save",
                workerText = BuildWorkerText(),
                growthText = BuildGrowthText(),
                blockerText = BuildPrimaryBlockerText(blockers),
                telemetryText = SessionTelemetryService.Active != null ? "Telemetry active" : "Telemetry offline",
                overlayText = _renderer != null ? $"Overlay: {_renderer.OverlayMode}" : "Overlay -"
            };

            FactionSnapshot faction = _gameplayService != null ? _gameplayService.GetPlayerFaction() : null;
            if (faction != null)
            {
                IReadOnlyList<ResourceStack> summary = _gameplayService.GetDerivedResourceSummary(faction.factionId);
                for (int i = 0; i < summary.Count; i++)
                {
                    state.metrics.Add(new MetricState
                    {
                        label = summary[i].resourceType.ToString(),
                        value = summary[i].amount.ToString(),
                        tone = summary[i].amount > 0 ? "normal" : "muted"
                    });
                }
            }

            return state;
        }

        private EventStripState BuildEventStrip(GameSelectionState selectionState, List<BlockerSummaryState> blockers)
        {
            var strip = new EventStripState
            {
                currentToolText = BuildToolInstruction(),
                hoverText = BuildHoverText(selectionState),
                lastActionText = _actionService != null ? _actionService.LastActionMessage : "Choose a tool and act on the world.",
                blockerText = BuildPrimaryBlockerText(blockers),
                warningText = BuildWarningText(selectionState, blockers)
            };

            if (_gameplayService?.RecentEvents != null)
            {
                for (int i = 0; i < _gameplayService.RecentEvents.Count; i++)
                    strip.recentEvents.Add(_gameplayService.RecentEvents[i]);
            }

            return strip;
        }

        private OperationsRailViewModel BuildOperationsRail(GameSelectionState selection)
        {
            var viewModel = new OperationsRailViewModel
            {
                activeToolLabel = _actionService != null ? BuildActiveToolLabel() : "Active tool: none"
            };

            TryAddOperationsGroup(viewModel, "simulation", "Simulation", BuildSimulationActions());
            TryAddOperationsGroup(viewModel, "view", "View", BuildViewActions());
            List<GameActionDescriptor> nextActions = BuildContextualOperations(selection);
            if (nextActions.Count > 0)
            {
                TryAddOperationsGroup(viewModel, "context", "Next", nextActions);
            }

            bool showFloors = ShouldShowFloorGroup(selection);
            bool showEdges = ShouldShowEdgeGroup(selection);
            bool showModules = ShouldShowModuleGroup(selection);
            bool showLandmarks = ShouldShowLandmarkGroup(selection);
            bool showCleanup = ShouldShowCleanupGroup(selection);

            if (_actionService != null && _actionService.ActiveToolMode != WorldToolMode.None)
            {
                showFloors = _actionService.ActiveToolMode == WorldToolMode.PaintFloor;
                showEdges = _actionService.ActiveToolMode == WorldToolMode.BuildEdge;
                showModules = _actionService.ActiveToolMode == WorldToolMode.PlaceModule;
                showLandmarks = _actionService.ActiveToolMode == WorldToolMode.PlaceLandmark;
                showCleanup = _actionService.ActiveToolMode == WorldToolMode.ClearCell || _actionService.ActiveToolMode == WorldToolMode.ClearEdge;
            }

            if (showFloors)
            {
                TryAddOperationsGroup(viewModel, "floors", "Floors", BuildFloorActions());
            }

            if (showEdges)
            {
                TryAddOperationsGroup(viewModel, "edges", "Edges", BuildEdgeActions());
            }

            if (showModules)
            {
                TryAddOperationsGroup(viewModel, "modules", "Modules", BuildModuleActions());
            }

            if (showLandmarks)
            {
                TryAddOperationsGroup(viewModel, "landmarks", "Landmarks", BuildLandmarkActions());
            }

            if (showCleanup)
            {
                TryAddOperationsGroup(viewModel, "cleanup", "Cleanup", BuildCleanupActions());
            }

            TryAddOperationsGroup(viewModel, "world", "World", BuildWorldActions());
            return viewModel;
        }

        private void TryAddOperationsGroup(OperationsRailViewModel viewModel, string groupId, string title, List<GameActionDescriptor> actions)
        {
            if (_onboardingFlowService != null && !_onboardingFlowService.IsOperationGroupVisible(groupId))
            {
                return;
            }

            if (actions == null || actions.Count == 0)
            {
                return;
            }

            viewModel.groups.Add(new OperationsGroupViewModel
            {
                groupId = groupId,
                title = title,
                actions = actions
            });
        }

        private void ResolveCellDerivedSelection(GameSelectionState selection, HexCoord cell)
        {
            HexCellState state = _gameplayService.GetCellState(cell);
            List<LandmarkState> landmarks = _gameplayService.GetLandmarksAtCell(cell);
            List<ItemStackEntity> stacks = _gameplayService.GetItemStacksAtCell(cell);

            if (state.module != null && state.module.moduleUid != EntityUid.None && state.module.moduleKind != ModuleKind.None)
            {
                selection.selectedModuleId = state.module.moduleUid.ToString();
                selection.primaryTargetKind = SelectionTargetKind.Module;
                selection.resolvedContextKind = ContextKind.Module;
                return;
            }

            if (landmarks.Count > 0)
            {
                selection.selectedLandmarkId = landmarks[0].landmarkId.ToString();
                selection.primaryTargetKind = SelectionTargetKind.Landmark;
                selection.resolvedContextKind = ContextKind.Landmark;
                return;
            }

            if (stacks.Count > 0)
            {
                selection.selectedStackId = stacks[0].itemStackId.ToString();
                selection.primaryTargetKind = SelectionTargetKind.Stack;
                selection.resolvedContextKind = ContextKind.Stack;
                return;
            }

            selection.primaryTargetKind = SelectionTargetKind.Cell;
            selection.resolvedContextKind = ContextKind.Cell;
        }

        private void BuildCellDerivedContext(ContextInspectorViewModel inspector, HexCoord cell)
        {
            HexCellState state = _gameplayService.GetCellState(cell);
            SpaceRegionState space = _gameplayService.GetSpaceAtCell(cell);
            EnclosedRoomState room = _gameplayService.GetRoomAtCell(cell);
            List<ItemStackEntity> stacks = _gameplayService.GetItemStacksAtCell(cell);
            List<UnitSnapshot> units = _gameplayService.GetUnitsAtCell(cell);
            List<ResourceNodeState> nodes = _gameplayService.GetResourceNodesAtCell(cell);
            List<LandmarkState> landmarks = _gameplayService.GetLandmarksAtCell(cell);
            List<ConstructionState> construction = _gameplayService.GetConstructionAtCell(cell);
            OccupancyEvaluation occupancy = _gameplayService.GetOccupancy(cell);

            inspector.title = $"Tile {cell}";
            inspector.subtitle = DescribeCellSubtitle(state, space);
            inspector.summary = BuildCellSummary(space, construction.Count, stacks.Count, units.Count);

            var card = new ContextCardViewModel
            {
                cardId = $"cell-{cell.Q}-{cell.R}",
                title = "Tile",
                subtitle = cell.ToString(),
                summary = BuildCellSummary(space, construction.Count, stacks.Count, units.Count),
                emphasis = space != null ? space.activationState.ToString() : ResolveBiome(cell).ToString(),
                tone = occupancy.isHardBlocked ? "danger" : "normal"
            };

            card.rows.Add(Row("Terrain", ResolveBiome(cell).ToString()));
            card.rows.Add(Row("Floor", state.floor != null && state.floor.isBuilt ? state.floor.purposeKind.ToString() : "None"));
            card.rows.Add(Row("Module", state.module != null && state.module.isBuilt ? state.module.moduleKind.ToString() : "None"));
            card.rows.Add(Row("Occupancy", $"{occupancy.usedGroundSmallCapacity}/{occupancy.availableGroundSmallCapacity}"));
            card.rows.Add(Row("Move cost", occupancy.moveCost.ToString("0.00")));
            if (space != null)
            {
                card.rows.Add(Row("Space", $"{space.purposeKind} [{space.activationState}]"));
                card.rows.Add(Row("Activation", _gameplayService.DescribeSpaceActivation(cell)));
            }
            else if (state.floor != null && state.floor.isBuilt)
            {
                card.rows.Add(Row("Space", $"{state.floor.purposeKind} [Not formed]"));
                card.rows.Add(Row("Activation", _gameplayService.DescribeSpaceActivation(cell)));
            }
            else
            {
                for (int i = 0; i < construction.Count; i++)
                {
                    if (construction[i].blueprint.featureKind == ConstructionFeatureKind.Floor)
                    {
                        card.rows.Add(Row("Space", $"{construction[i].blueprint.floorPurposeKind} [Planned]"));
                        card.rows.Add(Row("Activation", _gameplayService.DescribeSpaceActivation(cell)));
                        break;
                    }
                }
            }

            if (room != null)
            {
                card.rows.Add(Row("Room", room.isFunctional ? "Functional" : "Planned"));
                card.rows.Add(Row("Shelter", space != null && space.isEnclosed ? "Sheltered" : "Open"));
            }

            if (state.module != null && state.module.moduleKind != ModuleKind.None)
            {
                card.rows.Add(Row("Module status", _gameplayService.DescribeModuleStatus(cell)));
            }

            if (construction.Count > 0)
            {
                card.rows.Add(Row("Construction", $"{construction.Count} pending"));
            }

            if (nodes.Count > 0)
            {
                ResourceNodeState node = nodes[0];
                card.rows.Add(Row("Source", $"{node.resourceNodeKind} {node.resourceType} ({node.remainingYield})"));
            }

            if (stacks.Count > 0)
            {
                ItemStackEntity stack = stacks[0];
                card.rows.Add(Row("Top stack", $"{stack.resourceType} x{stack.amount} [{stack.bulkClass}]"));
            }

            if (landmarks.Count > 0)
            {
                card.rows.Add(Row("Landmark", landmarks[0].landmarkKind.ToString()));
            }

            if (units.Count > 0)
            {
                card.rows.Add(Row("Workers", units.Count.ToString()));
            }

            card.actions.AddRange(_actionService.GetCellContextActions(cell));
            inspector.contextCards.Add(card);
        }

        private void BuildEdgeContext(ContextInspectorViewModel inspector, HexEdgeCoord edge)
        {
            HexEdgeState state = _gameplayService.GetEdgeState(edge);
            List<ConstructionState> construction = _gameplayService.GetConstructionAtEdge(edge);
            inspector.title = "Edge";
            inspector.subtitle = edge.ToString();
            inspector.summary = state.isBuilt
                ? $"{state.featureKind} {(state.isPassable ? "passable" : "blocking")}"
                : construction.Count > 0
                    ? $"{construction.Count} edge builds pending"
                    : "No edge feature built.";

            var card = new ContextCardViewModel
            {
                cardId = $"edge-{edge.A.Q}-{edge.A.R}-{edge.B.Q}-{edge.B.R}",
                title = "Edge",
                subtitle = edge.ToString(),
                summary = inspector.summary,
                emphasis = state.isBuilt ? state.featureKind.ToString() : "Blueprint",
                tone = state.isBuilt && !state.isPassable ? "warning" : "normal"
            };
            card.rows.Add(Row("Cells", $"{edge.A} ↔ {edge.B}"));
            card.rows.Add(Row("Feature", state.isBuilt ? state.featureKind.ToString() : "None"));
            card.rows.Add(Row("Passable", state.isPassable ? "Yes" : "No"));
            card.rows.Add(Row("Pending", construction.Count.ToString()));
            card.actions.AddRange(_actionService.GetEdgeContextActions(edge));
            inspector.contextCards.Add(card);
        }

        private void BuildUnitContext(ContextInspectorViewModel inspector, string entityId)
        {
            if (!TryFindUnit(entityId, out UnitSnapshot unit))
            {
                inspector.title = "Unit";
                inspector.subtitle = "Missing";
                inspector.summary = "The selected worker was not found.";
                return;
            }

            inspector.title = unit.unitRole.ToString();
            inspector.subtitle = unit.entityUid.ToString();
            inspector.summary = DescribeWorkerDetail(unit);

            var card = new ContextCardViewModel
            {
                cardId = $"unit-{unit.entityUid}",
                title = $"{unit.unitRole}",
                subtitle = unit.entityUid.ToString(),
                summary = DescribeWorkerDetail(unit),
                emphasis = DescribeWorkerState(unit),
                tone = unit.currentJobType == JobType.Idle ? "warning" : "normal"
            };
            card.rows.Add(Row("Job", unit.currentJobType.ToString()));
            card.rows.Add(Row("Position", unit.hexCoord.ToString()));
            card.rows.Add(Row("Target", unit.targetHex.ToString()));
            card.rows.Add(Row("Carry", unit.carryLoad.amount > 0 ? $"{unit.carryLoad.resourceType} x{unit.carryLoad.amount}" : "Empty"));
            if (unit.assignedConstructionId != ConstructionFeatureId.None)
            {
                card.rows.Add(Row("Construction", unit.assignedConstructionId.ToString()));
            }

            if (unit.assignedModuleUid != EntityUid.None)
            {
                card.rows.Add(Row("Module", unit.assignedModuleUid.ToString()));
            }

            if (unit.assignedResourceNodeId != ResourceNodeId.None)
            {
                card.rows.Add(Row("Source", unit.assignedResourceNodeId.ToString()));
            }

            card.actions.AddRange(_actionService.GetEntityContextActions(unit.entityUid.ToString()));
            inspector.contextCards.Add(card);
        }

        private void BuildWorldContext(ContextInspectorViewModel inspector, List<BlockerSummaryState> blockers)
        {
            inspector.title = "World";
            inspector.subtitle = _gameplayService?.Snapshot != null ? _gameplayService.Snapshot.worldId.ToString() : "No world loaded";
            inspector.summary = _gameplayService?.Snapshot != null ? BuildPrimaryBlockerText(blockers) : "Start or load a world to see live context.";
        }

        private List<ContextCardViewModel> BuildTaskCards(List<BlockerSummaryState> blockers)
        {
            var cards = new List<ContextCardViewModel>();
            for (int i = 0; i < blockers.Count && i < 3; i++)
            {
                cards.Add(new ContextCardViewModel
                {
                    cardId = $"blocker-{i}",
                    title = blockers[i].title,
                    subtitle = blockers[i].severity,
                    summary = blockers[i].detail,
                    tone = blockers[i].severity
                });
            }

            if (_gameplayService?.Snapshot?.constructionFeatures != null)
            {
                var pendingConstruction = new List<ConstructionState>();
                for (int i = 0; i < _gameplayService.Snapshot.constructionFeatures.Count; i++)
                {
                    ConstructionState state = _gameplayService.Snapshot.constructionFeatures[i];
                    if (!state.isCompleted)
                    {
                        pendingConstruction.Add(state);
                    }
                }

                pendingConstruction.Sort(CompareConstructionUrgency);
                int shown = 0;
                for (int i = 0; i < pendingConstruction.Count && shown < 3; i++)
                {
                    ConstructionState state = pendingConstruction[i];

                    var card = new ContextCardViewModel
                    {
                        cardId = $"construction-{state.constructionFeatureId}",
                        title = state.blueprint.featureKind.ToString(),
                        subtitle = state.targetEdge != default && state.blueprint.featureKind == ConstructionFeatureKind.Edge ? state.targetEdge.ToString() : state.targetCell.ToString(),
                        summary = $"{Mathf.RoundToInt(state.buildProgress)} / {Mathf.RoundToInt(state.blueprint.requiredWork)} work",
                        tone = "normal"
                    };
                    card.rows.Add(Row("Priority", state.priority.ToString()));
                    card.rows.Add(Row("Urgency", DescribeConstructionUrgency(state)));
                    for (int j = 0; j < state.deliveredMaterials.Count; j++)
                    {
                        MaterialDeliveryState delivery = state.deliveredMaterials[j];
                        int missing = Mathf.Max(0, delivery.requiredAmount - delivery.deliveredAmount);
                        if (missing > 0)
                        {
                            card.rows.Add(Row(delivery.resourceType.ToString(), $"Need {missing} more"));
                        }
                    }

                    card.actions.AddRange(_actionService.GetConstructionContextActions(state.constructionFeatureId));

                    cards.Add(card);
                    shown++;
                }
            }

            return cards;
        }

        private List<ContextCardViewModel> BuildEntityCards()
        {
            List<EntitySummaryState> summaries = BuildEntitySummaries();
            var cards = new List<ContextCardViewModel>();
            int displayCount = Mathf.Min(summaries.Count, 18);
            for (int i = 0; i < displayCount; i++)
            {
                cards.Add(new ContextCardViewModel
                {
                    cardId = $"entity-{summaries[i].entityId}",
                    title = summaries[i].displayName,
                    subtitle = summaries[i].status,
                    summary = summaries[i].detail,
                    tone = summaries[i].status == "Blocked" ? "warning" : "normal",
                    actions = new List<GameActionDescriptor> { summaries[i].focusAction }
                });
            }

            if (summaries.Count > displayCount)
            {
                cards.Add(new ContextCardViewModel
                {
                    cardId = "entities-overflow",
                    title = "More workers",
                    subtitle = $"{summaries.Count - displayCount} hidden in HUD",
                    summary = "Use MCP entity queries or the HUD state browser for the full list.",
                    tone = "normal"
                });
            }

            return cards;
        }

        private List<ContextCardViewModel> BuildWorldCards(GameSelectionState selection)
        {
            var cards = new List<ContextCardViewModel>();
            if (_gameplayService?.Snapshot == null)
            {
                cards.Add(new ContextCardViewModel
                {
                    cardId = "world-empty",
                    title = "World",
                    subtitle = "No world loaded",
                    summary = "The HUD is ready, but no runtime world is attached yet."
                });
                return cards;
            }

            ColonySnapshot colony = _gameplayService != null ? _gameplayService.GetPrimaryColony() : null;
            cards.Add(new ContextCardViewModel
            {
                cardId = "world-summary",
                title = "World state",
                subtitle = _gameplayService.Snapshot.worldId.ToString(),
                summary = $"Tick {_gameplayService.Snapshot.simulationTick}",
                rows = new List<InfoRowState>
                {
                    Row("Population", colony != null ? colony.population.ToString() : "0"),
                    Row("Housing", colony != null ? colony.housingCapacity.ToString() : "0"),
                    Row("Workforce", colony != null ? colony.workforceCap.ToString() : "0"),
                    Row("Growth", BuildGrowthText()),
                    Row("Defense", BuildDefenseText()),
                    Row("Construction", _gameplayService.Snapshot.constructionFeatures.Count.ToString())
                }
            });
            cards.Add(BuildProductionCard(colony));
            cards.Add(BuildDefenseCard(colony, selection));
            List<BlockerSummaryState> blockers = BuildBlockerSummaries();
            cards.Add(new ContextCardViewModel
            {
                cardId = "world-blockers",
                title = "Top blockers",
                subtitle = blockers.Count > 0 ? blockers[0].title : "No blockers",
                summary = blockers.Count > 0 ? blockers[0].detail : "Current queues have usable supply paths.",
                tone = blockers.Count > 0 ? blockers[0].severity : "ok",
                rows = new List<InfoRowState>
                {
                    Row("Primary", BuildPrimaryBlockerText(blockers)),
                    Row("Workers", BuildWorkerText()),
                    Row("Alerts", BuildWarningText(selection, blockers))
                }
            });
            cards.Add(new ContextCardViewModel
            {
                cardId = "world-telemetry",
                title = "Session",
                subtitle = SessionTelemetryService.Active != null ? "Telemetry active" : "Telemetry offline",
                summary = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.SessionId : "No telemetry session",
                rows = new List<InfoRowState>
                {
                    Row("Last action", _actionService != null ? _actionService.LastActionMessage : "None")
                },
                actions = new List<GameActionDescriptor>
                {
                    _actionService.Describe(_actionService.CreateEnvelope(GameActionId.StartOnboarding)),
                    _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SaveWorld)),
                    _actionService.Describe(_actionService.CreateEnvelope(GameActionId.ResetWorld))
                }
            });
            return cards;
        }

        private ContextCardViewModel BuildProductionCard(ColonySnapshot colony)
        {
            FactionSnapshot faction = _gameplayService.GetPlayerFaction();
            IReadOnlyList<ResourceStack> summary = faction != null
                ? _gameplayService.GetDerivedResourceSummary(faction.factionId)
                : new List<ResourceStack>();

            var card = new ContextCardViewModel
            {
                cardId = "world-production",
                title = "Production chain",
                subtitle = "Ore -> Metal -> Weapons",
                summary = "Tracks the workshop and weaponry loop.",
                tone = "normal",
                rows = new List<InfoRowState>
                {
                    Row("Ore", GetResourceAmount(summary, ResourceType.Ore).ToString()),
                    Row("Metal", GetResourceAmount(summary, ResourceType.Metal).ToString()),
                    Row("Weapons", GetResourceAmount(summary, ResourceType.Weapons).ToString())
                }
            };

            int shown = 0;
            if (_gameplayService.Snapshot.chunks != null)
            {
                for (int i = 0; i < _gameplayService.Snapshot.chunks.Count && shown < 4; i++)
                {
                    List<HexCellState> cells = _gameplayService.Snapshot.chunks[i].modifiedCells;
                    for (int j = 0; j < cells.Count && shown < 4; j++)
                    {
                        ModuleState module = cells[j].module;
                        if (module == null || !module.isBuilt)
                        {
                            continue;
                        }

                        ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(module.moduleKind);
                        if (definition.productionRecipe == null)
                        {
                            continue;
                        }

                        if (colony != null && cells[j].coord.DistanceTo(colony.center) > 16)
                        {
                            continue;
                        }

                        string status = _gameplayService.DescribeModuleStatus(cells[j].coord);
                        card.rows.Add(Row($"{module.moduleKind} {cells[j].coord}", status));
                        if (status.StartsWith("Needs ", StringComparison.Ordinal) ||
                            status.StartsWith("Waiting for ", StringComparison.Ordinal))
                        {
                            card.tone = "warning";
                        }

                        shown++;
                    }
                }
            }

            if (shown == 0)
            {
                card.summary = "No production modules are built yet.";
                card.rows.Add(Row("Next step", "Build a Workshop and a Weaponry room."));
            }

            return card;
        }

        private ContextCardViewModel BuildDefenseCard(ColonySnapshot colony, GameSelectionState selection)
        {
            int soldiers = colony != null ? CountUnitsByRole(colony.colonyId, UnitRole.Soldier) : 0;
            FactionSnapshot faction = _gameplayService.GetPlayerFaction();
            IReadOnlyList<ResourceStack> summary = faction != null
                ? _gameplayService.GetDerivedResourceSummary(faction.factionId)
                : new List<ResourceStack>();
            var actions = new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.TrainSoldier)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.RallyDefenders)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.HoldDefenders)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.PerimeterDefenders))
            };

            if (selection.hasSelectedCell)
            {
                actions.Add(_actionService.Describe(_actionService.CreateEnvelope(GameActionId.DefendArea, targetCell: selection.selectedCell, hasTargetCell: true)));
            }

            return new ContextCardViewModel
            {
                cardId = "world-defense",
                title = "Defense",
                subtitle = "Barracks -> Soldiers",
                summary = colony?.threatAlert != null ? colony.threatAlert.summary : "No threat data.",
                tone = colony?.threatAlert != null && colony.threatAlert.threatLevel >= Ggj2026.Game.Shared.Combat.ThreatLevel.High ? "warning" : "normal",
                rows = new List<InfoRowState>
                {
                    Row("Soldiers", soldiers.ToString()),
                    Row("Order", _gameplayService.DescribeStandingSquadOrder(colony)),
                    Row("Barracks", DescribeBarracksReadiness(colony)),
                    Row("Training", BuildTrainingText(colony)),
                    Row("Threat", colony?.threatAlert != null ? colony.threatAlert.summary : "Frontier quiet."),
                    Row("Raid", colony?.raidEvent != null ? colony.raidEvent.summary : "No active raid."),
                    Row("Weapons", GetResourceAmount(summary, ResourceType.Weapons).ToString())
                },
                actions = actions
            };
        }

        private static int CompareConstructionUrgency(ConstructionState left, ConstructionState right)
        {
            int priorityCompare = GetConstructionPriorityOrder(left.priority).CompareTo(GetConstructionPriorityOrder(right.priority));
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            float leftRemaining = Mathf.Max(0f, left.blueprint.requiredWork - left.buildProgress);
            float rightRemaining = Mathf.Max(0f, right.blueprint.requiredWork - right.buildProgress);
            int workCompare = leftRemaining.CompareTo(rightRemaining);
            if (workCompare != 0)
            {
                return workCompare;
            }

            int leftMissing = CountMissingMaterials(left);
            int rightMissing = CountMissingMaterials(right);
            return leftMissing.CompareTo(rightMissing);
        }

        private static int GetConstructionPriorityOrder(ConstructionPriority priority)
        {
            switch (priority)
            {
                case ConstructionPriority.Critical:
                    return 0;
                case ConstructionPriority.High:
                    return 1;
                case ConstructionPriority.Normal:
                    return 2;
                case ConstructionPriority.Low:
                    return 3;
                default:
                    return 2;
            }
        }

        private static int CountMissingMaterials(ConstructionState state)
        {
            int missing = 0;
            for (int i = 0; i < state.deliveredMaterials.Count; i++)
            {
                missing += Mathf.Max(0, state.deliveredMaterials[i].requiredAmount - state.deliveredMaterials[i].deliveredAmount);
            }

            return missing;
        }

        private static string DescribeConstructionUrgency(ConstructionState state)
        {
            int missingMaterials = CountMissingMaterials(state);
            if (missingMaterials > 0)
            {
                return $"{state.priority} / waiting for {missingMaterials} materials";
            }

            float remainingWork = Mathf.Max(0f, state.blueprint.requiredWork - state.buildProgress);
            return remainingWork > 0f
                ? $"{state.priority} / ready to build"
                : $"{state.priority} / finishing";
        }

        private static int GetResourceAmount(IReadOnlyList<ResourceStack> summary, ResourceType resourceType)
        {
            if (summary == null)
            {
                return 0;
            }

            for (int i = 0; i < summary.Count; i++)
            {
                if (summary[i].resourceType == resourceType)
                {
                    return summary[i].amount;
                }
            }

            return 0;
        }

        private List<GameActionDescriptor> BuildOperationsActions()
        {
            var actions = new List<GameActionDescriptor>();
            actions.AddRange(BuildSimulationActions());
            actions.AddRange(BuildViewActions());
            actions.AddRange(BuildFloorActions());
            actions.AddRange(BuildEdgeActions());
            actions.AddRange(BuildModuleActions());
            actions.AddRange(BuildLandmarkActions());
            actions.AddRange(BuildCleanupActions());
            actions.AddRange(BuildWorldActions());
            return actions;
        }

        private List<GameActionDescriptor> BuildSimulationActions()
        {
            float currentSpeed = _realTimeSimulationController != null ? _realTimeSimulationController.SpeedMultiplier : 1f;
            bool isRunning = _realTimeSimulationController != null && _realTimeSimulationController.IsRunning;
            return new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetRealtimeRunning, desiredRunningState: !isRunning)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 1)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 5)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 25)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.AdjustSimulationSpeed, speedMultiplier: Mathf.Max(0.25f, currentSpeed * 0.5f))),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.AdjustSimulationSpeed, speedMultiplier: Mathf.Min(8f, currentSpeed * 2f)))
            };
        }

        private List<GameActionDescriptor> BuildViewActions()
        {
            return new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetOverlayMode, overlayMode: GameOverlayMode.Default)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetOverlayMode, overlayMode: GameOverlayMode.Occupancy)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetOverlayMode, overlayMode: GameOverlayMode.Spaces))
            };
        }

        private List<GameActionDescriptor> BuildFloorActions()
        {
            var actions = new List<GameActionDescriptor>();
            FloorPurposeKind[] coreFloors =
            {
                FloorPurposeKind.Residential,
                FloorPurposeKind.Storage,
                FloorPurposeKind.Workshop,
                FloorPurposeKind.Weaponry,
                FloorPurposeKind.Barracks,
                FloorPurposeKind.Roadway
            };
            for (int i = 0; i < coreFloors.Length; i++)
            {
                actions.Add(_actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetFloorTool, floorPurposeKind: coreFloors[i])));
            }

            return actions;
        }

        private List<GameActionDescriptor> BuildEdgeActions()
        {
            var actions = new List<GameActionDescriptor>();
            if (_gameplayService == null)
            {
                return actions;
            }

            IReadOnlyList<EdgeFeatureKind> edges = _gameplayService.PlaceableEdges;
            for (int i = 0; i < edges.Count; i++)
            {
                actions.Add(_actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetEdgeTool, edgeFeatureKind: edges[i])));
            }

            return actions;
        }

        private List<GameActionDescriptor> BuildModuleActions()
        {
            var actions = new List<GameActionDescriptor>();
            ModuleKind[] coreModules =
            {
                ModuleKind.Bed,
                ModuleKind.Rack,
                ModuleKind.Workbench,
                ModuleKind.WeaponBench,
                ModuleKind.Bunk,
                ModuleKind.CommandPost
            };
            for (int i = 0; i < coreModules.Length; i++)
            {
                actions.Add(_actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetModuleTool, moduleKind: coreModules[i])));
            }

            return actions;
        }

        private List<GameActionDescriptor> BuildLandmarkActions()
        {
            return new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetLandmarkTool, landmarkKind: LandmarkKind.Tower)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetLandmarkTool, landmarkKind: LandmarkKind.Gatehouse))
            };
        }

        private List<GameActionDescriptor> BuildContextualOperations(GameSelectionState selection)
        {
            var actions = new List<GameActionDescriptor>();
            if (_actionService == null)
            {
                return actions;
            }

            List<GameActionDescriptor> contextActions = _actionService.GetContextActions(selection);
            for (int i = 0; i < contextActions.Count && actions.Count < 6; i++)
            {
                if (contextActions[i].actionId == GameActionId.ExplainContext)
                {
                    continue;
                }

                actions.Add(contextActions[i]);
            }

            return actions;
        }

        private bool ShouldShowEdgeGroup(GameSelectionState selection)
        {
            if (_actionService.ActiveToolMode == WorldToolMode.BuildEdge)
            {
                return true;
            }

            if (selection.hasSelectedEdge || selection.hasHoveredEdge)
            {
                return true;
            }

            if (selection.resolvedContextKind == ContextKind.World)
            {
                return true;
            }

            if (selection.hasSelectedCell && NeedsShelterEdgeForSelection(selection.selectedCell))
            {
                return true;
            }

            return _actionService.ActiveToolMode == WorldToolMode.BuildEdge ||
                   selection.hasSelectedEdge ||
                   selection.hasHoveredEdge;
        }

        private bool ShouldShowModuleGroup(GameSelectionState selection)
        {
            if (_actionService.ActiveToolMode == WorldToolMode.PlaceModule)
            {
                return true;
            }

            if (!selection.hasSelectedCell && !selection.hasHoveredCell)
            {
                return false;
            }

            HexCoord cell = selection.hasSelectedCell ? selection.selectedCell : selection.hoveredCell;
            HexCellState state = _gameplayService.GetCellState(cell);
            return state.floor != null && state.floor.isBuilt;
        }

        private bool ShouldShowFloorGroup(GameSelectionState selection)
        {
            if (_actionService.ActiveToolMode == WorldToolMode.PaintFloor)
            {
                return true;
            }

            if (selection.resolvedContextKind == ContextKind.World)
            {
                return true;
            }

            if (selection.hasSelectedCell)
            {
                return true;
            }

            return selection.resolvedContextKind == ContextKind.Cell ||
                   selection.resolvedContextKind == ContextKind.Stack;
        }

        private bool ShouldShowLandmarkGroup(GameSelectionState selection)
        {
            if (_actionService.ActiveToolMode == WorldToolMode.PlaceLandmark)
            {
                return true;
            }

            if (selection.resolvedContextKind == ContextKind.Landmark)
            {
                return true;
            }

            if (!selection.hasSelectedCell)
            {
                return false;
            }

            return _gameplayService.GetLandmarksAtCell(selection.selectedCell).Count > 0;
        }

        private bool ShouldShowCleanupGroup(GameSelectionState selection)
        {
            if (_actionService.ActiveToolMode == WorldToolMode.ClearCell ||
                _actionService.ActiveToolMode == WorldToolMode.ClearEdge)
            {
                return true;
            }

            if (selection.hasSelectedEdge)
            {
                HexEdgeState edge = _gameplayService.GetEdgeState(selection.selectedEdge);
                return edge.isBuilt || _gameplayService.GetConstructionAtEdge(selection.selectedEdge).Count > 0;
            }

            if (!selection.hasSelectedCell)
            {
                return false;
            }

            HexCellState cellState = _gameplayService.GetCellState(selection.selectedCell);
            if (cellState.floor != null && cellState.floor.isBuilt)
            {
                return true;
            }

            if (cellState.module != null && cellState.module.isBuilt)
            {
                return true;
            }

            return _gameplayService.GetConstructionAtCell(selection.selectedCell).Count > 0 ||
                   _gameplayService.GetItemStacksAtCell(selection.selectedCell).Count > 0;
        }

        private List<GameActionDescriptor> BuildCleanupActions()
        {
            return new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.ClearTool)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetClearCellTool)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SetClearEdgeTool))
            };
        }

        private List<GameActionDescriptor> BuildWorldActions()
        {
            return new List<GameActionDescriptor>
            {
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.TrainSoldier)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.RallyDefenders)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.HoldDefenders)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.PerimeterDefenders)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.SaveWorld)),
                _actionService.Describe(_actionService.CreateEnvelope(GameActionId.ResetWorld))
            };
        }

        private bool NeedsShelterEdgeForSelection(HexCoord cell)
        {
            string activation = _gameplayService.DescribeSpaceActivation(cell);
            return activation.StartsWith("Needs enclosed ", StringComparison.Ordinal);
        }

        private string BuildWorkerText()
        {
            if (_gameplayService?.Snapshot?.units == null)
            {
                return "Workers -";
            }

            int busy = 0;
            int idle = 0;
            int soldiers = 0;
            for (int i = 0; i < _gameplayService.Snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _gameplayService.Snapshot.units[i];
                if (unit.unitRole == UnitRole.Soldier)
                {
                    soldiers++;
                    continue;
                }

                if (unit.currentJobType == JobType.Idle)
                {
                    idle++;
                }
                else
                {
                    busy++;
                }
            }

            return soldiers > 0
                ? $"{busy} busy / {idle} idle / {soldiers} soldiers"
                : $"{busy} busy / {idle} idle";
        }

        private string BuildGrowthText()
        {
            ColonySnapshot colony = _gameplayService != null ? _gameplayService.GetPrimaryColony() : null;
            if (colony?.populationGrowth == null)
            {
                return "Growth unavailable";
            }

            PopulationGrowthState growth = colony.populationGrowth;
            return growth.status switch
            {
                ColonyGrowthStatus.Blocked => string.IsNullOrWhiteSpace(growth.blockedReason) ? "Growth blocked" : growth.blockedReason,
                ColonyGrowthStatus.SpawnQueued => colony.spawnQueue != null && colony.spawnQueue.Count > 0
                    ? $"Incoming worker x{colony.spawnQueue.Count}"
                    : "Incoming worker",
                _ => $"Growing {Mathf.RoundToInt(growth.progress)}/{Mathf.RoundToInt(growth.requiredProgress)}"
            };
        }

        private string BuildDefenseText()
        {
            ColonySnapshot colony = _gameplayService != null ? _gameplayService.GetPrimaryColony() : null;
            if (colony?.threatAlert == null)
            {
                return "Defense unavailable";
            }

            if (colony.raidEvent != null && colony.raidEvent.isActive)
            {
                return colony.raidEvent.summary;
            }

            return $"{_gameplayService.DescribeStandingSquadOrder(colony)} {colony.threatAlert.summary}".Trim();
        }

        private string BuildTrainingText(ColonySnapshot colony)
        {
            if (colony?.soldierTraining == null)
            {
                return "Unavailable";
            }

            SoldierTrainingState training = colony.soldierTraining;
            return training.status switch
            {
                SoldierTrainingStatus.Training => $"Queued {training.queuedCount}, {training.progress:0}/{training.requiredProgress:0}",
                SoldierTrainingStatus.Ready => "Ready to train",
                _ => string.IsNullOrWhiteSpace(training.blockedReason) ? "Training blocked" : training.blockedReason
            };
        }

        private string DescribeBarracksReadiness(ColonySnapshot colony)
        {
            if (colony == null)
            {
                return "No colony";
            }

            if (colony.soldierTraining != null && colony.soldierTraining.blockedReason == "Need active barracks.")
            {
                return "Need active barracks";
            }

            return "Barracks ready";
        }

        private int CountUnitsByRole(ColonyId colonyId, UnitRole role)
        {
            if (_gameplayService?.Snapshot?.units == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _gameplayService.Snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _gameplayService.Snapshot.units[i];
                if (unit.ownerColonyId == colonyId && unit.unitRole == role)
                {
                    count++;
                }
            }

            return count;
        }

        private string BuildPrimaryBlockerText(List<BlockerSummaryState> blockers)
        {
            if (blockers.Count == 0)
            {
                return "No blockers";
            }

            return $"{blockers[0].title}: {blockers[0].detail}";
        }

        private string BuildToolInstruction()
        {
            if (_actionService == null)
            {
                return "Choose a tool.";
            }

            return _actionService.ActiveToolMode switch
            {
                WorldToolMode.PaintFloor => $"Painting {_actionService.ActiveFloor}. Drag across tiles.",
                WorldToolMode.BuildEdge => $"Building {_actionService.ActiveEdge}. Drag across edges.",
                WorldToolMode.PlaceModule => $"Placing {_actionService.ActiveModule}. Click a tile.",
                WorldToolMode.PlaceLandmark => $"Placing {_actionService.ActiveLandmark}. Click a tile.",
                WorldToolMode.ClearCell => "Clearing tiles. Click or drag across cells.",
                WorldToolMode.ClearEdge => "Clearing edges. Click or drag across edges.",
                _ => "Choose a tool, then act on the world."
            };
        }

        private string BuildHoverText(GameSelectionState selectionState)
        {
            if (_actionService != null && _actionService.ActiveToolMode != WorldToolMode.None)
            {
                string preview = BuildActionPreviewText(selectionState);
                if (!string.IsNullOrEmpty(preview))
                {
                    return preview;
                }
            }

            if (selectionState.hasHoveredCell)
            {
                List<UnitSnapshot> units = _gameplayService.GetUnitsAtCell(selectionState.hoveredCell);
                if (units.Count > 0)
                {
                    return $"Click selects {units[0].unitRole}. Right-click selects the nearest edge.";
                }

                HexCellState state = _gameplayService.GetCellState(selectionState.hoveredCell);
                return $"Click selects {DescribeHoverCellTarget(selectionState.hoveredCell, state)}. Right-click selects the nearest edge.";
            }

            if (selectionState.hasHoveredEdge)
            {
                HexEdgeState state = _gameplayService.GetEdgeState(selectionState.hoveredEdge);
                return $"Right-click selects {(state.isBuilt ? state.featureKind.ToString() : "this edge")}.";
            }

            return "Move the pointer over the world for details.";
        }

        private string BuildActionPreviewText(GameSelectionState selectionState)
        {
            GameActionEnvelope previewEnvelope = null;
            switch (_actionService.ActiveToolMode)
            {
                case WorldToolMode.PaintFloor when selectionState.hasHoveredCell:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.PaintFloor, targetCell: selectionState.hoveredCell, hasTargetCell: true, floorPurposeKind: _actionService.ActiveFloor);
                    break;

                case WorldToolMode.BuildEdge when selectionState.hasHoveredEdge:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.BuildEdge, targetEdge: selectionState.hoveredEdge, hasTargetEdge: true, edgeFeatureKind: _actionService.ActiveEdge);
                    break;

                case WorldToolMode.PlaceModule when selectionState.hasHoveredCell:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.PlaceModule, targetCell: selectionState.hoveredCell, hasTargetCell: true, moduleKind: _actionService.ActiveModule);
                    break;

                case WorldToolMode.PlaceLandmark when selectionState.hasHoveredCell:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.PlaceLandmark, targetCell: selectionState.hoveredCell, hasTargetCell: true, landmarkKind: _actionService.ActiveLandmark);
                    break;

                case WorldToolMode.ClearCell when selectionState.hasHoveredCell:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.ClearCell, targetCell: selectionState.hoveredCell, hasTargetCell: true);
                    break;

                case WorldToolMode.ClearEdge when selectionState.hasHoveredEdge:
                    previewEnvelope = _actionService.CreateEnvelope(GameActionId.ClearEdge, targetEdge: selectionState.hoveredEdge, hasTargetEdge: true);
                    break;
            }

            if (previewEnvelope == null)
            {
                return string.Empty;
            }

            GameActionDescriptor descriptor = _actionService.Describe(previewEnvelope);
            return descriptor.isEnabled
                ? $"Click: {descriptor.label}."
                : $"Blocked: {descriptor.blockedReason}";
        }

        private string DescribeHoverCellTarget(HexCoord cell, HexCellState state)
        {
            if (state.module != null && state.module.isBuilt)
            {
                return state.module.moduleKind.ToString();
            }

            List<LandmarkState> landmarks = _gameplayService.GetLandmarksAtCell(cell);
            if (landmarks.Count > 0)
            {
                return landmarks[0].landmarkKind.ToString();
            }

            List<ItemStackEntity> stacks = _gameplayService.GetItemStacksAtCell(cell);
            if (stacks.Count > 0)
            {
                return $"{stacks[0].resourceType} stack";
            }

            if (state.floor != null && state.floor.isBuilt)
            {
                return $"{state.floor.purposeKind} tile";
            }

            return $"{ResolveBiome(cell)} tile";
        }

        private string BuildWarningText(GameSelectionState selectionState, List<BlockerSummaryState> blockers)
        {
            if (blockers.Count > 0 && blockers[0].severity == "critical")
            {
                return blockers[0].detail;
            }

            if (selectionState != null && selectionState.resolvedContextKind == ContextKind.Module && selectionState.hasSelectedCell)
            {
                HexCellState state = _gameplayService.GetCellState(selectionState.selectedCell);
                if (state.module != null && state.module.isBuilt)
                {
                    ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(state.module.moduleKind);
                    SpaceRegionState space = _gameplayService.GetSpaceAtCell(selectionState.selectedCell);
                    if (definition.requiresShelter && (space == null || !space.isEnclosed))
                    {
                        return "This module needs shelter before it can work.";
                    }
                }
            }

            ColonySnapshot colony = _gameplayService != null ? _gameplayService.GetPrimaryColony() : null;
            if (colony?.populationGrowth != null && colony.populationGrowth.status == ColonyGrowthStatus.Blocked)
            {
                return colony.populationGrowth.blockedReason;
            }

            return string.Empty;
        }

        private string BuildActiveToolLabel()
        {
            return _actionService.ActiveToolMode switch
            {
                WorldToolMode.PaintFloor => $"Active tool: Paint {_actionService.ActiveFloor}",
                WorldToolMode.BuildEdge => $"Active tool: Build {_actionService.ActiveEdge}",
                WorldToolMode.PlaceModule => $"Active tool: Place {_actionService.ActiveModule}",
                WorldToolMode.PlaceLandmark => $"Active tool: Place {_actionService.ActiveLandmark}",
                WorldToolMode.ClearCell => "Active tool: Clear Cell",
                WorldToolMode.ClearEdge => "Active tool: Clear Edge",
                _ => "Active tool: None"
            };
        }

        private static InfoRowState Row(string label, string value, string tone = "normal")
        {
            return new InfoRowState
            {
                label = label,
                value = value,
                tone = tone
            };
        }

        private string DescribeCellSubtitle(HexCellState state, SpaceRegionState space)
        {
            if (state.module != null && state.module.isBuilt)
            {
                return state.module.moduleKind.ToString();
            }

            if (space != null)
            {
                return $"{space.purposeKind} [{space.activationState}]";
            }

            if (state.floor != null && state.floor.isBuilt)
            {
                return state.floor.purposeKind.ToString();
            }

            return ResolveBiome(state.coord).ToString();
        }

        private static string BuildCellSummary(SpaceRegionState space, int constructionCount, int stackCount, int unitCount)
        {
            string spaceText = space != null ? $"{space.purposeKind} [{space.activationState}]" : "No space";
            return $"{spaceText}. {constructionCount} builds, {stackCount} stacks, {unitCount} workers.";
        }

        private string DescribeWorkerState(UnitSnapshot unit)
        {
            return unit.currentJobType switch
            {
                JobType.Idle => "Idle",
                JobType.HarvestSource => "Harvesting",
                JobType.HaulToStaging or JobType.HaulToStorage or JobType.HaulToConsumer => "Hauling",
                JobType.BuildFloor or JobType.BuildEdge or JobType.InstallModule or JobType.PlaceLandmark => "Building",
                JobType.OperateModule => "Operating",
                JobType.Garrison => "Garrisoned",
                _ => "Busy"
            };
        }

        private string DescribeWorkerDetail(UnitSnapshot unit)
        {
            if (_gameplayService != null)
            {
                return _gameplayService.DescribeUnitTask(unit);
            }

            string carryText = unit.carryLoad.amount > 0 ? $"{unit.carryLoad.resourceType} x{unit.carryLoad.amount}" : "empty hands";
            return $"{unit.currentJobType} at {unit.hexCoord}, target {unit.targetHex}, carrying {carryText}.";
        }

        private bool TryFindUnit(string entityId, out UnitSnapshot unit)
        {
            unit = null;
            if (string.IsNullOrWhiteSpace(entityId) || _gameplayService?.Snapshot?.units == null)
            {
                return false;
            }

            for (int i = 0; i < _gameplayService.Snapshot.units.Count; i++)
            {
                if (_gameplayService.Snapshot.units[i].entityUid.ToString() == entityId)
                {
                    unit = _gameplayService.Snapshot.units[i];
                    return true;
                }
            }

            return false;
        }

        private int CountFreeResource(ResourceType resourceType)
        {
            int total = 0;
            if (_gameplayService?.Snapshot?.itemStacks == null)
            {
                return total;
            }

            for (int i = 0; i < _gameplayService.Snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _gameplayService.Snapshot.itemStacks[i];
                if (stack.resourceType == resourceType &&
                    stack.reservedByConstructionId == ConstructionFeatureId.None &&
                    stack.reservedByModuleUid == EntityUid.None)
                {
                    total += stack.amount;
                }
            }

            return total;
        }

        private int CountReservedConstructionResource(ResourceType resourceType)
        {
            int total = 0;
            if (_gameplayService?.Snapshot?.itemStacks == null)
            {
                return total;
            }

            for (int i = 0; i < _gameplayService.Snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _gameplayService.Snapshot.itemStacks[i];
                if (stack.resourceType == resourceType && stack.reservedByConstructionId != ConstructionFeatureId.None)
                {
                    total += stack.amount;
                }
            }

            return total;
        }

        private int CountResourceNodeYield(ResourceType resourceType)
        {
            int total = 0;
            if (_gameplayService?.Snapshot?.chunks == null)
            {
                return total;
            }

            for (int i = 0; i < _gameplayService.Snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = _gameplayService.Snapshot.chunks[i].resourceNodes;
                if (nodes == null)
                {
                    continue;
                }

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (!nodes[j].isDepleted && nodes[j].resourceType == resourceType)
                    {
                        total += nodes[j].remainingYield;
                    }
                }
            }

            return total;
        }

        private BiomeKind ResolveBiome(HexCoord cell)
        {
            if (_gameplayService?.Snapshot?.chunks == null)
            {
                return BiomeKind.Plains;
            }

            ChunkCoord chunkCoord = ChunkCoord.FromHex(cell, _gameplayService.Profile.chunkSize);
            for (int i = 0; i < _gameplayService.Snapshot.chunks.Count; i++)
            {
                if (_gameplayService.Snapshot.chunks[i].chunkCoord == chunkCoord)
                {
                    return _gameplayService.Snapshot.chunks[i].dominantBiome;
                }
            }

            return BiomeKind.Plains;
        }
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Diagnostics;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    public sealed class GameActionService : IGameActionService
    {
        private readonly LocalColonyGameplayService _gameplayService;
        private readonly LocalWorldDebugRenderer _renderer;
        private readonly RealTimeSimulationController _realTimeSimulationController;
        private readonly OnboardingFlowService _onboardingFlowService;

        private WorldToolMode _activeToolMode;
        private FloorPurposeKind _activeFloor = FloorPurposeKind.Storage;
        private EdgeFeatureKind _activeEdge = EdgeFeatureKind.Wall;
        private ModuleKind _activeModule = ModuleKind.Bed;
        private LandmarkKind _activeLandmark = LandmarkKind.TownCenterCore;
        private InspectorTabKind _activeInspectorTab = InspectorTabKind.World;
        private EntityUid _selectedEntityUid = EntityUid.None;
        private string _lastActionMessage = "Choose a tool and act on the world.";

        public GameActionService(LocalColonyGameplayService gameplayService, LocalWorldDebugRenderer renderer, RealTimeSimulationController realTimeSimulationController, OnboardingFlowService onboardingFlowService = null)
        {
            _gameplayService = gameplayService;
            _renderer = renderer;
            _realTimeSimulationController = realTimeSimulationController;
            _onboardingFlowService = onboardingFlowService;
        }

        public event Action StateChanged;

        public WorldToolMode ActiveToolMode => _activeToolMode;
        public FloorPurposeKind ActiveFloor => _activeFloor;
        public EdgeFeatureKind ActiveEdge => _activeEdge;
        public ModuleKind ActiveModule => _activeModule;
        public LandmarkKind ActiveLandmark => _activeLandmark;
        public InspectorTabKind ActiveInspectorTab => _activeInspectorTab;
        public EntityUid SelectedEntityUid => _selectedEntityUid;
        public string LastActionMessage => _lastActionMessage;

        public UniTask<GameActionResult> ExecuteAsync(GameActionEnvelope envelope)
        {
            if (envelope == null)
            {
                return UniTask.FromResult(new GameActionResult
                {
                    actionId = GameActionId.None,
                    success = false,
                    blockedReason = "Missing action envelope.",
                    message = "Missing action envelope."
                });
            }

            return ExecuteInternalAsync(envelope);
        }

        public List<GameActionDescriptor> GetGlobalActions()
        {
            return new List<GameActionDescriptor>
            {
                Describe(CreateEnvelope(GameActionId.StartOnboarding)),
                Describe(CreateEnvelope(GameActionId.ClearTool)),
                Describe(CreateEnvelope(GameActionId.SetRealtimeRunning, desiredRunningState: !_realTimeSimulationController.IsRunning)),
                Describe(CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 1)),
                Describe(CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 5)),
                Describe(CreateEnvelope(GameActionId.AdvanceSimulation, tickCount: 25)),
                Describe(CreateEnvelope(GameActionId.AdjustSimulationSpeed, speedMultiplier: Mathf.Max(0.25f, _realTimeSimulationController.SpeedMultiplier * 0.5f))),
                Describe(CreateEnvelope(GameActionId.AdjustSimulationSpeed, speedMultiplier: Mathf.Min(8f, _realTimeSimulationController.SpeedMultiplier * 2f))),
                Describe(CreateEnvelope(GameActionId.TrainSoldier)),
                Describe(CreateEnvelope(GameActionId.RallyDefenders)),
                Describe(CreateEnvelope(GameActionId.HoldDefenders)),
                Describe(CreateEnvelope(GameActionId.PerimeterDefenders)),
                Describe(CreateEnvelope(GameActionId.SaveWorld)),
                Describe(CreateEnvelope(GameActionId.ResetWorld))
            };
        }

        public List<GameActionDescriptor> GetContextActions(GameSelectionState selectionState)
        {
            var actions = new List<GameActionDescriptor>();

            if (selectionState != null && selectionState.hasSelectedCell)
            {
                actions.AddRange(GetCellContextActions(selectionState.selectedCell));
            }

            if (selectionState != null && selectionState.hasSelectedEdge)
            {
                actions.AddRange(GetEdgeContextActions(selectionState.selectedEdge));
            }

            if (selectionState != null && !selectionState.hasSelectedCell && selectionState.hasHoveredCell)
            {
                actions.AddRange(GetCellContextActions(selectionState.hoveredCell));
            }

            if (selectionState != null && !selectionState.hasSelectedEdge && selectionState.hasHoveredEdge)
            {
                actions.AddRange(GetEdgeContextActions(selectionState.hoveredEdge));
            }

            if (!string.IsNullOrWhiteSpace(selectionState?.selectedEntityId))
            {
                actions.AddRange(GetEntityContextActions(selectionState.selectedEntityId));
            }

            actions.Add(Describe(CreateEnvelope(GameActionId.ExplainContext)));
            return actions;
        }

        public List<GameActionDescriptor> GetCellContextActions(HexCoord cell)
        {
            var actions = new List<GameActionDescriptor>();
            if (_gameplayService == null)
            {
                return actions;
            }

            HexCellState state = _gameplayService.GetCellState(cell);
            List<ConstructionState> construction = _gameplayService.GetConstructionAtCell(cell);
            bool hasPendingFloor = HasPendingConstruction(construction, ConstructionFeatureKind.Floor);
            bool hasPendingModule = HasPendingConstruction(construction, ConstructionFeatureKind.Module);
            bool hasBuiltFloor = state.floor != null && state.floor.isBuilt;

            if (!hasBuiltFloor && !hasPendingFloor)
            {
                actions.Add(Describe(CreateEnvelope(GameActionId.PaintFloor, targetCell: cell, hasTargetCell: true, floorPurposeKind: FloorPurposeKind.Residential)));
                actions.Add(Describe(CreateEnvelope(GameActionId.PaintFloor, targetCell: cell, hasTargetCell: true, floorPurposeKind: FloorPurposeKind.Storage)));
                actions.Add(Describe(CreateEnvelope(GameActionId.PaintFloor, targetCell: cell, hasTargetCell: true, floorPurposeKind: FloorPurposeKind.Workshop)));
                actions.Add(Describe(CreateEnvelope(GameActionId.PaintFloor, targetCell: cell, hasTargetCell: true, floorPurposeKind: FloorPurposeKind.Weaponry)));
            }
            else if (hasBuiltFloor)
            {
                switch (state.floor.purposeKind)
                {
                    case FloorPurposeKind.Residential:
                        if (!HasBuiltModule(state, ModuleKind.Bed) && !hasPendingModule)
                        {
                            actions.Add(Describe(CreateEnvelope(GameActionId.PlaceModule, targetCell: cell, hasTargetCell: true, moduleKind: ModuleKind.Bed)));
                        }

                        break;

                    case FloorPurposeKind.Storage:
                        if (!HasStorageModule(state) && !hasPendingModule)
                        {
                            actions.Add(Describe(CreateEnvelope(GameActionId.PlaceModule, targetCell: cell, hasTargetCell: true, moduleKind: ModuleKind.Rack)));
                        }

                        break;

                    case FloorPurposeKind.Workshop:
                        if (!HasBuiltModule(state, ModuleKind.Workbench) && !hasPendingModule)
                        {
                            actions.Add(Describe(CreateEnvelope(GameActionId.PlaceModule, targetCell: cell, hasTargetCell: true, moduleKind: ModuleKind.Workbench)));
                        }

                        break;

                    case FloorPurposeKind.Weaponry:
                        if (!HasBuiltModule(state, ModuleKind.WeaponBench) && !hasPendingModule)
                        {
                            actions.Add(Describe(CreateEnvelope(GameActionId.PlaceModule, targetCell: cell, hasTargetCell: true, moduleKind: ModuleKind.WeaponBench)));
                        }

                        break;

                    case FloorPurposeKind.Barracks:
                        if (!HasBuiltModule(state, ModuleKind.Bunk) && !hasPendingModule)
                        {
                            actions.Add(Describe(CreateEnvelope(GameActionId.PlaceModule, targetCell: cell, hasTargetCell: true, moduleKind: ModuleKind.Bunk)));
                        }

                        break;
                }
            }

            if (_gameplayService.GetLandmarksAtCell(cell).Count == 0 && !HasPendingConstruction(construction, ConstructionFeatureKind.Landmark))
            {
                actions.Add(Describe(CreateEnvelope(GameActionId.PlaceLandmark, targetCell: cell, hasTargetCell: true, landmarkKind: LandmarkKind.Tower)));
            }

            actions.Add(Describe(CreateEnvelope(GameActionId.DefendArea, targetCell: cell, hasTargetCell: true)));
            actions.Add(Describe(CreateEnvelope(GameActionId.ClearCell, targetCell: cell, hasTargetCell: true)));
            return actions;
        }

        public List<GameActionDescriptor> GetEdgeContextActions(HexEdgeCoord edge)
        {
            return new List<GameActionDescriptor>
            {
                Describe(CreateEnvelope(GameActionId.BuildEdge, targetEdge: edge, hasTargetEdge: true, edgeFeatureKind: EdgeFeatureKind.Wall)),
                Describe(CreateEnvelope(GameActionId.BuildEdge, targetEdge: edge, hasTargetEdge: true, edgeFeatureKind: EdgeFeatureKind.Door)),
                Describe(CreateEnvelope(GameActionId.ClearEdge, targetEdge: edge, hasTargetEdge: true))
            };
        }

        private static bool HasPendingConstruction(List<ConstructionState> construction, ConstructionFeatureKind featureKind)
        {
            if (construction == null)
            {
                return false;
            }

            for (int i = 0; i < construction.Count; i++)
            {
                if (construction[i].blueprint.featureKind == featureKind)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBuiltModule(HexCellState state, ModuleKind moduleKind)
        {
            return state.module != null && state.module.isBuilt && state.module.moduleKind == moduleKind;
        }

        private static bool HasStorageModule(HexCellState state)
        {
            if (state.module == null || !state.module.isBuilt)
            {
                return false;
            }

            return state.module.moduleKind == ModuleKind.Rack ||
                   state.module.moduleKind == ModuleKind.Bin ||
                   state.module.moduleKind == ModuleKind.Pallet;
        }

        public List<GameActionDescriptor> GetEntityContextActions(string entityId)
        {
            return new List<GameActionDescriptor>
            {
                Describe(CreateEnvelope(GameActionId.FocusSelectedTarget, targetEntityId: entityId))
            };
        }

        public List<GameActionDescriptor> GetConstructionContextActions(ConstructionFeatureId constructionFeatureId)
        {
            string constructionId = constructionFeatureId.ToString();
            return new List<GameActionDescriptor>
            {
                Describe(CreateEnvelope(GameActionId.IncreaseConstructionPriority, targetConstructionId: constructionId)),
                Describe(CreateEnvelope(GameActionId.DecreaseConstructionPriority, targetConstructionId: constructionId)),
                Describe(CreateEnvelope(GameActionId.CancelConstruction, targetConstructionId: constructionId))
            };
        }

        public GameActionParameterSchema GetSchema(GameActionId actionId)
        {
            var schema = new GameActionParameterSchema { actionId = actionId };
            switch (actionId)
            {
                case GameActionId.SetFloorTool:
                case GameActionId.PaintFloor:
                    schema.parameters.Add(EnumParameter("floorPurposeKind", true, "Floor purpose to paint."));
                    schema.parameters.Add(CellParameter(actionId == GameActionId.PaintFloor));
                    break;
                case GameActionId.SetEdgeTool:
                case GameActionId.BuildEdge:
                    schema.parameters.Add(EnumParameter("edgeFeatureKind", true, "Edge feature to build."));
                    schema.parameters.Add(EdgeParameter(actionId == GameActionId.BuildEdge));
                    break;
                case GameActionId.SetModuleTool:
                case GameActionId.PlaceModule:
                    schema.parameters.Add(EnumParameter("moduleKind", true, "Module to place."));
                    schema.parameters.Add(CellParameter(actionId == GameActionId.PlaceModule));
                    break;
                case GameActionId.SetLandmarkTool:
                case GameActionId.PlaceLandmark:
                    schema.parameters.Add(EnumParameter("landmarkKind", true, "Landmark to place."));
                    schema.parameters.Add(CellParameter(actionId == GameActionId.PlaceLandmark));
                    break;
                case GameActionId.ClearCell:
                case GameActionId.SelectCell:
                    schema.parameters.Add(CellParameter(true));
                    break;
                case GameActionId.ClearEdge:
                case GameActionId.SelectEdge:
                    schema.parameters.Add(EdgeParameter(true));
                    break;
                case GameActionId.IncreaseConstructionPriority:
                case GameActionId.DecreaseConstructionPriority:
                case GameActionId.CancelConstruction:
                    schema.parameters.Add(ConstructionParameter(true));
                    break;
                case GameActionId.SelectEntity:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "targetEntityId",
                        valueType = "string",
                        isRequired = true,
                        description = "Entity uid to select."
                    });
                    break;
                case GameActionId.AdvanceSimulation:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "tickCount",
                        valueType = "int",
                        isRequired = true,
                        description = "How many simulation ticks to advance."
                    });
                    break;
                case GameActionId.AdjustSimulationSpeed:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "speedMultiplier",
                        valueType = "float",
                        isRequired = true,
                        description = "Realtime simulation speed multiplier."
                    });
                    break;
                case GameActionId.SetRealtimeRunning:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "desiredRunningState",
                        valueType = "bool",
                        isRequired = true,
                        description = "Whether realtime simulation should be running."
                    });
                    break;
                case GameActionId.DefendArea:
                    schema.parameters.Add(CellParameter(true));
                    break;
                case GameActionId.SetOverlayMode:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "overlayMode",
                        valueType = "enum",
                        isRequired = true,
                        description = "World overlay mode.",
                        enumOptions = new List<string> { nameof(GameOverlayMode.Default), nameof(GameOverlayMode.Occupancy), nameof(GameOverlayMode.Spaces) }
                    });
                    break;
                case GameActionId.SetInspectorTab:
                    schema.parameters.Add(new GameActionParameterDefinition
                    {
                        name = "inspectorTab",
                        valueType = "enum",
                        isRequired = true,
                        description = "Context inspector tab.",
                        enumOptions = new List<string> { nameof(InspectorTabKind.Context), nameof(InspectorTabKind.Tasks), nameof(InspectorTabKind.Entities), nameof(InspectorTabKind.World) }
                    });
                    break;
            }

            return schema;
        }

        public GameActionDescriptor Describe(GameActionEnvelope envelope)
        {
            string blockedReason;
            bool enabled = Validate(envelope, out blockedReason);
            return new GameActionDescriptor
            {
                actionId = envelope.actionId,
                category = ResolveCategory(envelope.actionId),
                label = ResolveLabel(envelope),
                description = ResolveDescription(envelope),
                hint = ResolveHint(envelope),
                isEnabled = enabled,
                isActive = IsActive(envelope),
                blockedReason = enabled ? string.Empty : blockedReason,
                badgeText = ResolveBadge(envelope),
                envelope = envelope,
                parameterSchema = GetSchema(envelope.actionId)
            };
        }

        public void SetInspectorTab(InspectorTabKind tabKind)
        {
            if (_activeInspectorTab == tabKind)
            {
                return;
            }

            _activeInspectorTab = tabKind;
            NotifyStateChanged();
        }

        public void ClearEntitySelection()
        {
            if (_selectedEntityUid == EntityUid.None)
            {
                return;
            }

            _selectedEntityUid = EntityUid.None;
            NotifyStateChanged();
        }

        private async UniTask<GameActionResult> ExecuteInternalAsync(GameActionEnvelope envelope)
        {
            string blockedReason;
            if (!Validate(envelope, out blockedReason))
            {
                return Finish(envelope, false, blockedReason, blockedReason);
            }

            switch (envelope.actionId)
            {
                case GameActionId.StartOnboarding:
                    _onboardingFlowService?.Start();
                    return Finish(envelope, true, "Guide restarted.");

                case GameActionId.PreviousOnboardingStep:
                {
                    if (_onboardingFlowService == null)
                    {
                        return Finish(envelope, false, "Guide is unavailable.", "Guide is unavailable.");
                    }

                    bool success = _onboardingFlowService.TryGoBack(out string onboardingBackMessage);
                    return Finish(envelope, success, onboardingBackMessage, onboardingBackMessage);
                }

                case GameActionId.NextOnboardingStep:
                {
                    if (_onboardingFlowService == null)
                    {
                        return Finish(envelope, false, "Guide is unavailable.", "Guide is unavailable.");
                    }

                    bool success = _onboardingFlowService.TryAdvance(out string onboardingNextMessage);
                    return Finish(envelope, success, onboardingNextMessage, onboardingNextMessage);
                }

                case GameActionId.SkipOnboarding:
                {
                    if (_onboardingFlowService == null)
                    {
                        return Finish(envelope, false, "Guide is unavailable.", "Guide is unavailable.");
                    }

                    bool success = _onboardingFlowService.TrySkip(out string onboardingSkipMessage);
                    return Finish(envelope, success, onboardingSkipMessage, onboardingSkipMessage);
                }

                case GameActionId.ClearTool:
                    _activeToolMode = WorldToolMode.None;
                    return Finish(envelope, true, "Tool cleared.");

                case GameActionId.SetFloorTool:
                    _activeToolMode = WorldToolMode.PaintFloor;
                    _activeFloor = envelope.floorPurposeKind;
                    return Finish(envelope, true, $"Tool set to paint {envelope.floorPurposeKind}.");

                case GameActionId.SetEdgeTool:
                    _activeToolMode = WorldToolMode.BuildEdge;
                    _activeEdge = envelope.edgeFeatureKind;
                    return Finish(envelope, true, $"Tool set to build {envelope.edgeFeatureKind}.");

                case GameActionId.SetModuleTool:
                    _activeToolMode = WorldToolMode.PlaceModule;
                    _activeModule = envelope.moduleKind;
                    return Finish(envelope, true, $"Tool set to place {envelope.moduleKind}.");

                case GameActionId.SetLandmarkTool:
                    _activeToolMode = WorldToolMode.PlaceLandmark;
                    _activeLandmark = envelope.landmarkKind;
                    return Finish(envelope, true, $"Tool set to place {envelope.landmarkKind}.");

                case GameActionId.SetClearCellTool:
                    _activeToolMode = WorldToolMode.ClearCell;
                    return Finish(envelope, true, "Tool set to clear cells.");

                case GameActionId.SetClearEdgeTool:
                    _activeToolMode = WorldToolMode.ClearEdge;
                    return Finish(envelope, true, "Tool set to clear edges.");

                case GameActionId.SetOverlayMode:
                    _renderer.SetOverlayMode(ToRendererOverlayMode(envelope.overlayMode));
                    return Finish(envelope, true, $"Overlay set to {envelope.overlayMode}.");

                case GameActionId.SetRealtimeRunning:
                    _realTimeSimulationController.SetRunning(envelope.desiredRunningState);
                    return Finish(envelope, true, envelope.desiredRunningState ? "Realtime simulation started." : "Realtime simulation paused.");

                case GameActionId.AdvanceSimulation:
                    _gameplayService.AdvanceSimulation(Mathf.Max(1, envelope.tickCount), envelope.uiSource, ToSessionActor(envelope.source));
                    return Finish(envelope, true, $"Advanced simulation by {Mathf.Max(1, envelope.tickCount)} ticks.");

                case GameActionId.AdjustSimulationSpeed:
                    _realTimeSimulationController.SetSpeedMultiplier(envelope.speedMultiplier);
                    return Finish(envelope, true, $"Realtime speed set to {_realTimeSimulationController.SpeedMultiplier:0.##}x.");

                case GameActionId.SaveWorld:
                    SessionTelemetryService.Active?.RecordUiAction(SessionActionKind.SaveWorld, ToSessionActor(envelope.source), envelope.uiSource, envelope.payloadJson ?? "{\"action\":\"SaveWorld\"}", "Save requested.", _gameplayService.Snapshot);
                    await _gameplayService.SaveAsync();
                    return Finish(envelope, true, "World saved.");

                case GameActionId.TrainSoldier:
                    return Finish(
                        envelope,
                        _gameplayService.TryQueueSoldierTraining(out string trainFailure, envelope.uiSource, ToSessionActor(envelope.source)),
                        string.IsNullOrEmpty(trainFailure) ? "Queued soldier training." : trainFailure,
                        trainFailure);

                case GameActionId.RallyDefenders:
                    return Finish(
                        envelope,
                        _gameplayService.TryIssueDefenderOrder(SquadOrderKind.Garrison, default, false, out string rallyFailure, envelope.uiSource, ToSessionActor(envelope.source)),
                        string.IsNullOrEmpty(rallyFailure) ? _gameplayService.DescribeStandingSquadOrder(_gameplayService.GetPrimaryColony()) : rallyFailure,
                        rallyFailure);

                case GameActionId.HoldDefenders:
                    return Finish(
                        envelope,
                        _gameplayService.TryIssueDefenderOrder(SquadOrderKind.Hold, default, false, out string holdFailure, envelope.uiSource, ToSessionActor(envelope.source)),
                        string.IsNullOrEmpty(holdFailure) ? _gameplayService.DescribeStandingSquadOrder(_gameplayService.GetPrimaryColony()) : holdFailure,
                        holdFailure);

                case GameActionId.PerimeterDefenders:
                    return Finish(
                        envelope,
                        _gameplayService.TryIssueDefenderOrder(SquadOrderKind.Perimeter, default, false, out string perimeterFailure, envelope.uiSource, ToSessionActor(envelope.source)),
                        string.IsNullOrEmpty(perimeterFailure) ? _gameplayService.DescribeStandingSquadOrder(_gameplayService.GetPrimaryColony()) : perimeterFailure,
                        perimeterFailure);

                case GameActionId.DefendArea:
                    return Finish(
                        envelope,
                        _gameplayService.TryIssueDefenderOrder(SquadOrderKind.DefendArea, envelope.targetCell, envelope.hasTargetCell, out string defendFailure, envelope.uiSource, ToSessionActor(envelope.source)),
                        string.IsNullOrEmpty(defendFailure) ? _gameplayService.DescribeStandingSquadOrder(_gameplayService.GetPrimaryColony()) : defendFailure,
                        defendFailure);

                case GameActionId.ResetWorld:
                {
                    bool success = await _gameplayService.ResetWorldAsync(envelope.uiSource, ToSessionActor(envelope.source));
                    if (success)
                    {
                        _activeToolMode = WorldToolMode.None;
                    }

                    return Finish(envelope, success, success ? "Local world reset." : "Failed to reset local world.");
                }

                case GameActionId.SelectCell:
                    _renderer.ClearSelectedEdge();
                    _renderer.ClearSelectedEntity();
                    _renderer.SetSelectedHex(envelope.targetCell);
                    _selectedEntityUid = EntityUid.None;
                    _activeInspectorTab = InspectorTabKind.Context;
                    SessionTelemetryService.Active?.RecordUiAction(SessionActionKind.SelectionChanged, ToSessionActor(envelope.source), envelope.uiSource, envelope.payloadJson ?? string.Empty, $"Selected cell {envelope.targetCell}.", _gameplayService.Snapshot);
                    return Finish(envelope, true, $"Selected cell {envelope.targetCell}.");

                case GameActionId.SelectEdge:
                    _renderer.ClearSelectedHex();
                    _renderer.ClearSelectedEntity();
                    _renderer.SetSelectedEdge(envelope.targetEdge);
                    _selectedEntityUid = EntityUid.None;
                    _activeInspectorTab = InspectorTabKind.Context;
                    SessionTelemetryService.Active?.RecordUiAction(SessionActionKind.SelectionChanged, ToSessionActor(envelope.source), envelope.uiSource, envelope.payloadJson ?? string.Empty, $"Selected edge {envelope.targetEdge}.", _gameplayService.Snapshot);
                    return Finish(envelope, true, $"Selected edge {envelope.targetEdge}.");

                case GameActionId.SelectEntity:
                    return Finish(envelope, TrySelectEntity(envelope.targetEntityId, ToSessionActor(envelope.source), out string selectionMessage), selectionMessage, selectionMessage);

                case GameActionId.PaintFloor:
                    return Finish(envelope, _gameplayService.TryPaintFloor(envelope.floorPurposeKind, envelope.targetCell, out string floorFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(floorFailure) ? $"Queued {envelope.floorPurposeKind} floor at {envelope.targetCell}." : floorFailure, floorFailure);

                case GameActionId.BuildEdge:
                    return Finish(envelope, _gameplayService.TryQueueEdge(envelope.edgeFeatureKind, envelope.targetEdge, out string edgeFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(edgeFailure) ? $"Queued {envelope.edgeFeatureKind} at {envelope.targetEdge}." : edgeFailure, edgeFailure);

                case GameActionId.PlaceModule:
                    return Finish(envelope, _gameplayService.TryPlaceModule(envelope.moduleKind, envelope.targetCell, out string moduleFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(moduleFailure) ? $"Queued {envelope.moduleKind} at {envelope.targetCell}." : moduleFailure, moduleFailure);

                case GameActionId.PlaceLandmark:
                    return Finish(envelope, _gameplayService.TryPlaceLandmark(envelope.landmarkKind, envelope.targetCell, out string landmarkFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(landmarkFailure) ? $"Queued {envelope.landmarkKind} at {envelope.targetCell}." : landmarkFailure, landmarkFailure);

                case GameActionId.ClearCell:
                    return Finish(envelope, _gameplayService.TryClearCellFeature(envelope.targetCell, out string clearCellFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(clearCellFailure) ? $"Cleared {envelope.targetCell}." : clearCellFailure, clearCellFailure);

                case GameActionId.ClearEdge:
                    return Finish(envelope, _gameplayService.TryClearEdgeFeature(envelope.targetEdge, out string clearEdgeFailure, envelope.uiSource, ToSessionActor(envelope.source)), string.IsNullOrEmpty(clearEdgeFailure) ? $"Cleared {envelope.targetEdge}." : clearEdgeFailure, clearEdgeFailure);

                case GameActionId.IncreaseConstructionPriority:
                {
                    string priorityFailure = "Invalid construction id.";
                    bool success = TryParseConstructionFeatureId(envelope.targetConstructionId, out ConstructionFeatureId constructionId) &&
                                   _gameplayService.TryAdjustConstructionPriority(constructionId, true, out priorityFailure, envelope.uiSource, ToSessionActor(envelope.source));
                    return Finish(envelope, success, string.IsNullOrEmpty(priorityFailure) ? "Construction priority increased." : priorityFailure, priorityFailure);
                }

                case GameActionId.DecreaseConstructionPriority:
                {
                    string priorityFailure = "Invalid construction id.";
                    bool success = TryParseConstructionFeatureId(envelope.targetConstructionId, out ConstructionFeatureId constructionId) &&
                                   _gameplayService.TryAdjustConstructionPriority(constructionId, false, out priorityFailure, envelope.uiSource, ToSessionActor(envelope.source));
                    return Finish(envelope, success, string.IsNullOrEmpty(priorityFailure) ? "Construction priority decreased." : priorityFailure, priorityFailure);
                }

                case GameActionId.CancelConstruction:
                {
                    string cancelFailure = "Invalid construction id.";
                    bool success = TryParseConstructionFeatureId(envelope.targetConstructionId, out ConstructionFeatureId constructionId) &&
                                   _gameplayService.TryCancelConstruction(constructionId, out cancelFailure, envelope.uiSource, ToSessionActor(envelope.source));
                    return Finish(envelope, success, string.IsNullOrEmpty(cancelFailure) ? "Construction canceled." : cancelFailure, cancelFailure);
                }

                case GameActionId.SetInspectorTab:
                    _activeInspectorTab = envelope.inspectorTab;
                    return Finish(envelope, true, $"Inspector set to {envelope.inspectorTab}.");

                case GameActionId.ExplainContext:
                    return Finish(envelope, true, BuildContextExplanation());

                case GameActionId.FocusSelectedTarget:
                    if (_selectedEntityUid != EntityUid.None && TryFocusEntity(_selectedEntityUid, ToSessionActor(envelope.source), out string focusMessage))
                    {
                        return Finish(envelope, true, focusMessage);
                    }

                    return Finish(envelope, false, "Nothing selected to focus.", "Nothing selected to focus.");
            }

            return Finish(envelope, false, $"Unsupported action {envelope.actionId}.", $"Unsupported action {envelope.actionId}.");
        }

        private bool Validate(GameActionEnvelope envelope, out string blockedReason)
        {
            blockedReason = string.Empty;
            switch (envelope.actionId)
            {
                case GameActionId.None:
                    blockedReason = "No action selected.";
                    return false;

                case GameActionId.StartOnboarding:
                    return true;

                case GameActionId.PreviousOnboardingStep:
                    if (_onboardingFlowService == null || !_onboardingFlowService.IsActive)
                    {
                        blockedReason = "Guide is not active.";
                        return false;
                    }

                    return true;

                case GameActionId.NextOnboardingStep:
                    if (_onboardingFlowService == null || !_onboardingFlowService.IsActive)
                    {
                        blockedReason = "Guide is not active.";
                        return false;
                    }

                    return true;

                case GameActionId.SkipOnboarding:
                    if (_onboardingFlowService == null || !_onboardingFlowService.IsActive)
                    {
                        blockedReason = "Guide is already hidden.";
                        return false;
                    }

                    return true;

                case GameActionId.ClearTool:
                case GameActionId.SetFloorTool:
                case GameActionId.SetEdgeTool:
                case GameActionId.SetModuleTool:
                case GameActionId.SetLandmarkTool:
                case GameActionId.SetClearCellTool:
                case GameActionId.SetClearEdgeTool:
                case GameActionId.ExplainContext:
                case GameActionId.SetInspectorTab:
                    return true;

                case GameActionId.SetOverlayMode:
                    return _renderer != null;

                case GameActionId.SetRealtimeRunning:
                case GameActionId.AdjustSimulationSpeed:
                    if (_realTimeSimulationController == null)
                    {
                        blockedReason = "Realtime simulation is unavailable.";
                        return false;
                    }

                    return true;

                case GameActionId.AdvanceSimulation:
                    if (_gameplayService?.Snapshot == null)
                    {
                        blockedReason = "No world loaded.";
                        return false;
                    }

                    return true;

                case GameActionId.SaveWorld:
                case GameActionId.ResetWorld:
                    return _gameplayService != null;

                case GameActionId.TrainSoldier:
                    return _gameplayService != null && _gameplayService.CanQueueSoldierTraining(out blockedReason);

                case GameActionId.RallyDefenders:
                    return _gameplayService != null && _gameplayService.CanIssueDefenderOrder(SquadOrderKind.Garrison, default, false, out blockedReason);

                case GameActionId.HoldDefenders:
                    return _gameplayService != null && _gameplayService.CanIssueDefenderOrder(SquadOrderKind.Hold, default, false, out blockedReason);

                case GameActionId.PerimeterDefenders:
                    return _gameplayService != null && _gameplayService.CanIssueDefenderOrder(SquadOrderKind.Perimeter, default, false, out blockedReason);

                case GameActionId.DefendArea:
                    return _gameplayService != null && _gameplayService.CanIssueDefenderOrder(SquadOrderKind.DefendArea, envelope.targetCell, envelope.hasTargetCell, out blockedReason);

                case GameActionId.SelectCell:
                    return RequireCell(envelope, out blockedReason);

                case GameActionId.SelectEdge:
                    return RequireEdge(envelope, out blockedReason);

                case GameActionId.SelectEntity:
                    if (string.IsNullOrWhiteSpace(envelope.targetEntityId))
                    {
                        blockedReason = "Entity id is required.";
                        return false;
                    }

                    return true;

                case GameActionId.PaintFloor:
                    return RequireCell(envelope, out blockedReason) && _gameplayService.CanPaintFloor(envelope.floorPurposeKind, envelope.targetCell, out blockedReason);

                case GameActionId.BuildEdge:
                    return RequireEdge(envelope, out blockedReason) && _gameplayService.CanQueueEdge(envelope.edgeFeatureKind, envelope.targetEdge, out blockedReason);

                case GameActionId.PlaceModule:
                    return RequireCell(envelope, out blockedReason) && _gameplayService.CanPlaceModule(envelope.moduleKind, envelope.targetCell, out blockedReason);

                case GameActionId.PlaceLandmark:
                    return RequireCell(envelope, out blockedReason) && _gameplayService.CanPlaceLandmark(envelope.landmarkKind, envelope.targetCell, out blockedReason);

                case GameActionId.ClearCell:
                    return RequireCell(envelope, out blockedReason) && _gameplayService.CanClearCellFeature(envelope.targetCell, out blockedReason);

                case GameActionId.ClearEdge:
                    return RequireEdge(envelope, out blockedReason) && _gameplayService.CanClearEdgeFeature(envelope.targetEdge, out blockedReason);

                case GameActionId.IncreaseConstructionPriority:
                case GameActionId.DecreaseConstructionPriority:
                    return RequireConstruction(envelope, out blockedReason) &&
                           TryParseConstructionFeatureId(envelope.targetConstructionId, out ConstructionFeatureId prioritizedConstructionId) &&
                           _gameplayService.CanAdjustConstructionPriority(prioritizedConstructionId, out blockedReason);

                case GameActionId.CancelConstruction:
                    return RequireConstruction(envelope, out blockedReason) &&
                           TryParseConstructionFeatureId(envelope.targetConstructionId, out ConstructionFeatureId cancelConstructionId) &&
                           _gameplayService.CanCancelConstruction(cancelConstructionId, out blockedReason);

                case GameActionId.FocusSelectedTarget:
                    if (_selectedEntityUid == EntityUid.None)
                    {
                        blockedReason = "No focused entity.";
                        return false;
                    }

                    return true;
            }

            return true;
        }

        private bool RequireCell(GameActionEnvelope envelope, out string blockedReason)
        {
            if (!envelope.hasTargetCell)
            {
                blockedReason = "Target cell is required.";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private bool RequireEdge(GameActionEnvelope envelope, out string blockedReason)
        {
            if (!envelope.hasTargetEdge)
            {
                blockedReason = "Target edge is required.";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private bool RequireConstruction(GameActionEnvelope envelope, out string blockedReason)
        {
            if (string.IsNullOrWhiteSpace(envelope.targetConstructionId))
            {
                blockedReason = "Construction id is required.";
                return false;
            }

            if (!TryParseConstructionFeatureId(envelope.targetConstructionId, out _))
            {
                blockedReason = "Invalid construction id.";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private bool TrySelectEntity(string entityId, SessionActorKind actorKind, out string message)
        {
            if (!TryParseEntityUid(entityId, out EntityUid entityUid))
            {
                message = "Invalid entity id.";
                return false;
            }

            return TryFocusEntity(entityUid, actorKind, out message);
        }

        private bool TryFocusEntity(EntityUid entityUid, SessionActorKind actorKind, out string message)
        {
            if (_gameplayService?.Snapshot?.units == null)
            {
                message = "No world loaded.";
                return false;
            }

            for (int i = 0; i < _gameplayService.Snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _gameplayService.Snapshot.units[i];
                if (unit.entityUid != entityUid)
                {
                    continue;
                }

                _selectedEntityUid = entityUid;
                _renderer.SetSelectedEntity(entityUid);
                _renderer.SetSelectedHex(unit.hexCoord);
                _activeInspectorTab = InspectorTabKind.Context;
                message = $"Focused {unit.unitRole} {unit.entityUid}.";
                SessionTelemetryService.Active?.RecordUiAction(SessionActionKind.SelectionChanged, actorKind, new SessionUiSource
                {
                    panelId = "ContextInspector",
                    elementId = "SelectEntity",
                    controller = nameof(GameActionService)
                }, $"{{\"entityId\":\"{entityUid}\"}}", message, _gameplayService.Snapshot);
                NotifyStateChanged();
                return true;
            }

            message = "Entity was not found.";
            return false;
        }

        private GameActionResult Finish(GameActionEnvelope envelope, bool success, string message, string blockedReason = "")
        {
            _lastActionMessage = message;

            if (success && _onboardingFlowService != null && _onboardingFlowService.IsActive)
            {
                _onboardingFlowService.NotifyActionPerformed(envelope.actionId);
            }

            NotifyStateChanged();
            return new GameActionResult
            {
                actionId = envelope.actionId,
                source = envelope.source,
                success = success,
                message = message,
                blockedReason = success ? string.Empty : blockedReason,
                telemetryActionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.LastRecordedActionId : string.Empty,
                diffPath = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.LastRecordedDiffPath : string.Empty
            };
        }

        private static SessionActorKind ToSessionActor(GameActionSource source)
        {
            return source switch
            {
                GameActionSource.HumanUi => SessionActorKind.Player,
                GameActionSource.McpAgent => SessionActorKind.McpAgent,
                GameActionSource.ManualQa => SessionActorKind.ManualTester,
                _ => SessionActorKind.System
            };
        }

        private static GameActionCategory ResolveCategory(GameActionId actionId)
        {
            return actionId switch
            {
                GameActionId.StartOnboarding or GameActionId.PreviousOnboardingStep or GameActionId.NextOnboardingStep or GameActionId.SkipOnboarding => GameActionCategory.World,
                GameActionId.SetRealtimeRunning or GameActionId.AdvanceSimulation or GameActionId.AdjustSimulationSpeed => GameActionCategory.Simulation,
                GameActionId.SetOverlayMode => GameActionCategory.View,
                GameActionId.SetFloorTool or GameActionId.PaintFloor => GameActionCategory.Floors,
                GameActionId.SetEdgeTool or GameActionId.BuildEdge => GameActionCategory.Edges,
                GameActionId.SetModuleTool or GameActionId.PlaceModule => GameActionCategory.Modules,
                GameActionId.SetLandmarkTool or GameActionId.PlaceLandmark => GameActionCategory.Landmarks,
                GameActionId.IncreaseConstructionPriority or GameActionId.DecreaseConstructionPriority or GameActionId.CancelConstruction => GameActionCategory.Tasks,
                GameActionId.SetClearCellTool or GameActionId.SetClearEdgeTool or GameActionId.ClearCell or GameActionId.ClearEdge or GameActionId.ClearTool => GameActionCategory.Cleanup,
                GameActionId.SaveWorld or GameActionId.ResetWorld or GameActionId.TrainSoldier or GameActionId.RallyDefenders or GameActionId.HoldDefenders or GameActionId.PerimeterDefenders or GameActionId.DefendArea => GameActionCategory.World,
                GameActionId.SelectEntity or GameActionId.FocusSelectedTarget => GameActionCategory.Entities,
                _ => GameActionCategory.Context
            };
        }

        private string ResolveLabel(GameActionEnvelope envelope)
        {
            return envelope.actionId switch
            {
                GameActionId.StartOnboarding => "Restart Guide",
                GameActionId.PreviousOnboardingStep => "Back",
                GameActionId.NextOnboardingStep => "Next Tip",
                GameActionId.SkipOnboarding => "Skip Guide",
                GameActionId.ClearTool => "Clear Tool",
                GameActionId.SetFloorTool => $"Paint {envelope.floorPurposeKind}",
                GameActionId.SetEdgeTool => $"Build {envelope.edgeFeatureKind}",
                GameActionId.SetModuleTool => $"Place {envelope.moduleKind}",
                GameActionId.SetLandmarkTool => $"Place {envelope.landmarkKind}",
                GameActionId.SetClearCellTool => "Clear Cell Tool",
                GameActionId.SetClearEdgeTool => "Clear Edge Tool",
                GameActionId.PaintFloor => $"Paint {envelope.floorPurposeKind}",
                GameActionId.BuildEdge => $"Build {envelope.edgeFeatureKind}",
                GameActionId.PlaceModule => $"Place {envelope.moduleKind}",
                GameActionId.PlaceLandmark => $"Place {envelope.landmarkKind}",
                GameActionId.ClearCell => "Clear Cell",
                GameActionId.ClearEdge => "Clear Edge",
                GameActionId.IncreaseConstructionPriority => "Raise Priority",
                GameActionId.DecreaseConstructionPriority => "Lower Priority",
                GameActionId.CancelConstruction => "Cancel Build",
                GameActionId.TrainSoldier => "Train Soldier",
                GameActionId.RallyDefenders => "Rally Defenders",
                GameActionId.HoldDefenders => "Hold Defenders",
                GameActionId.PerimeterDefenders => "Perimeter Defenders",
                GameActionId.DefendArea => "Defend Here",
                GameActionId.SetOverlayMode => $"Overlay: {envelope.overlayMode}",
                GameActionId.SetRealtimeRunning => envelope.desiredRunningState ? "Play Real Time" : "Pause",
                GameActionId.AdvanceSimulation => $"Tick +{Mathf.Max(1, envelope.tickCount)}",
                GameActionId.AdjustSimulationSpeed => envelope.speedMultiplier >= _realTimeSimulationController.SpeedMultiplier ? "Speed +" : "Speed -",
                GameActionId.SaveWorld => "Save World",
                GameActionId.ResetWorld => "Reset Local World",
                GameActionId.SelectCell => "Select Cell",
                GameActionId.SelectEdge => "Select Edge",
                GameActionId.SelectEntity => "Focus Entity",
                GameActionId.SetInspectorTab => envelope.inspectorTab.ToString(),
                GameActionId.FocusSelectedTarget => "Focus Selected",
                GameActionId.ExplainContext => "Explain Context",
                _ => envelope.actionId.ToString()
            };
        }

        private static string ResolveDescription(GameActionEnvelope envelope)
        {
            return envelope.actionId switch
            {
                GameActionId.StartOnboarding => "Show the onboarding guide again.",
                GameActionId.PreviousOnboardingStep => "Go back to the previous onboarding tip.",
                GameActionId.NextOnboardingStep => "Move to the next onboarding tip.",
                GameActionId.SkipOnboarding => "Hide the onboarding guide for this session.",
                GameActionId.SetFloorTool => "Set the active floor paint tool.",
                GameActionId.SetEdgeTool => "Set the active edge build tool.",
                GameActionId.SetModuleTool => "Set the active module placement tool.",
                GameActionId.SetLandmarkTool => "Set the active landmark placement tool.",
                GameActionId.PaintFloor => "Queue floor construction at the selected tile.",
                GameActionId.BuildEdge => "Queue edge construction at the selected edge.",
                GameActionId.PlaceModule => "Queue a module at the selected tile.",
                GameActionId.PlaceLandmark => "Queue a landmark footprint at the selected tile.",
                GameActionId.ClearCell => "Clear construction or built content from the selected tile.",
                GameActionId.ClearEdge => "Clear construction or built content from the selected edge.",
                GameActionId.IncreaseConstructionPriority => "Raise a queued build so workers prefer it sooner.",
                GameActionId.DecreaseConstructionPriority => "Lower a queued build so other work can finish first.",
                GameActionId.CancelConstruction => "Cancel a queued build and reclaim staged materials.",
                GameActionId.TrainSoldier => "Train one soldier from the current workforce using one Weapons stack.",
                GameActionId.RallyDefenders => "Send defenders back to the colony rally point.",
                GameActionId.HoldDefenders => "Make defenders hold their current position.",
                GameActionId.PerimeterDefenders => "Spread defenders across the colony perimeter.",
                GameActionId.DefendArea => "Send defenders to hold the selected tile.",
                GameActionId.ExplainContext => "Explain the current selection and blockers.",
                _ => string.Empty
            };
        }

        private string ResolveHint(GameActionEnvelope envelope)
        {
            return envelope.actionId switch
            {
                GameActionId.StartOnboarding => "Reopens the first-session guide.",
                GameActionId.PreviousOnboardingStep or GameActionId.NextOnboardingStep or GameActionId.SkipOnboarding => "These controls manage the onboarding guide only.",
                GameActionId.SetFloorTool or GameActionId.SetEdgeTool or GameActionId.SetModuleTool or GameActionId.SetLandmarkTool or GameActionId.SetClearCellTool or GameActionId.SetClearEdgeTool => "Select then act on the world.",
                GameActionId.PaintFloor or GameActionId.BuildEdge or GameActionId.ClearCell or GameActionId.ClearEdge => "Runs immediately on the selected target.",
                GameActionId.IncreaseConstructionPriority or GameActionId.DecreaseConstructionPriority => "Changes worker preference for this queued build.",
                GameActionId.CancelConstruction => "Returns staged materials to free world stacks.",
                GameActionId.TrainSoldier => "Requires active barracks, a recruit, and available Weapons.",
                GameActionId.RallyDefenders => "Use this to regroup defenders at the colony rally point.",
                GameActionId.HoldDefenders => "Keeps defenders at their current held position.",
                GameActionId.PerimeterDefenders => "Distributes defenders around outer approach lanes.",
                GameActionId.DefendArea => "Requires a selected tile. Future defenders will also go there.",
                GameActionId.SetRealtimeRunning => _realTimeSimulationController.IsRunning ? "Pause realtime simulation." : "Start realtime simulation.",
                _ => string.Empty
            };
        }

        private string ResolveBadge(GameActionEnvelope envelope)
        {
            if (envelope.actionId == GameActionId.SetOverlayMode && _renderer != null && ToRendererOverlayMode(envelope.overlayMode) == _renderer.OverlayMode)
            {
                return "active";
            }

            if (IsActive(envelope))
            {
                return "active";
            }

            return string.Empty;
        }

        private bool IsActive(GameActionEnvelope envelope)
        {
            SquadOrder standingOrder = _gameplayService != null ? _gameplayService.GetStandingSquadOrder() : null;
            return envelope.actionId switch
            {
                GameActionId.StartOnboarding => _onboardingFlowService != null && _onboardingFlowService.IsActive,
                GameActionId.SetFloorTool => _activeToolMode == WorldToolMode.PaintFloor && _activeFloor == envelope.floorPurposeKind,
                GameActionId.SetEdgeTool => _activeToolMode == WorldToolMode.BuildEdge && _activeEdge == envelope.edgeFeatureKind,
                GameActionId.SetModuleTool => _activeToolMode == WorldToolMode.PlaceModule && _activeModule == envelope.moduleKind,
                GameActionId.SetLandmarkTool => _activeToolMode == WorldToolMode.PlaceLandmark && _activeLandmark == envelope.landmarkKind,
                GameActionId.SetClearCellTool => _activeToolMode == WorldToolMode.ClearCell,
                GameActionId.SetClearEdgeTool => _activeToolMode == WorldToolMode.ClearEdge,
                GameActionId.SetInspectorTab => _activeInspectorTab == envelope.inspectorTab,
                GameActionId.RallyDefenders => standingOrder?.orderKind == SquadOrderKind.Garrison,
                GameActionId.HoldDefenders => standingOrder?.orderKind == SquadOrderKind.Hold,
                GameActionId.PerimeterDefenders => standingOrder?.orderKind == SquadOrderKind.Perimeter,
                GameActionId.DefendArea => standingOrder?.orderKind == SquadOrderKind.DefendArea &&
                                           envelope.hasTargetCell &&
                                           standingOrder.targetHex == envelope.targetCell,
                _ => false
            };
        }

        private string BuildContextExplanation()
        {
            if (_renderer.SelectedHex.HasValue)
            {
                return _gameplayService.DescribeSelectedCell(_renderer.SelectedHex.Value);
            }

            if (_renderer.SelectedEdge.HasValue)
            {
                return _gameplayService.DescribeSelectedEdge(_renderer.SelectedEdge.Value);
            }

            return "No world target is currently selected.";
        }

        private GameOverlayMode ToGameOverlayMode(LocalWorldDebugRenderer.RenderOverlayMode overlayMode)
        {
            return overlayMode switch
            {
                LocalWorldDebugRenderer.RenderOverlayMode.Occupancy => GameOverlayMode.Occupancy,
                LocalWorldDebugRenderer.RenderOverlayMode.Spaces => GameOverlayMode.Spaces,
                _ => GameOverlayMode.Default
            };
        }

        private static LocalWorldDebugRenderer.RenderOverlayMode ToRendererOverlayMode(GameOverlayMode overlayMode)
        {
            return overlayMode switch
            {
                GameOverlayMode.Occupancy => LocalWorldDebugRenderer.RenderOverlayMode.Occupancy,
                GameOverlayMode.Spaces => LocalWorldDebugRenderer.RenderOverlayMode.Spaces,
                _ => LocalWorldDebugRenderer.RenderOverlayMode.Default
            };
        }

        private static bool TryParseEntityUid(string entityId, out EntityUid entityUid)
        {
            entityUid = EntityUid.None;
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return false;
            }

            if (!ulong.TryParse(entityId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong value))
            {
                return false;
            }

            entityUid = new EntityUid(value);
            return true;
        }

        private static bool TryParseConstructionFeatureId(string constructionId, out ConstructionFeatureId constructionFeatureId)
        {
            constructionFeatureId = ConstructionFeatureId.None;
            if (string.IsNullOrWhiteSpace(constructionId))
            {
                return false;
            }

            if (!ulong.TryParse(constructionId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong value))
            {
                return false;
            }

            constructionFeatureId = new ConstructionFeatureId(value);
            return true;
        }

        private static GameActionParameterDefinition EnumParameter(string name, bool isRequired, string description)
        {
            return new GameActionParameterDefinition
            {
                name = name,
                valueType = "enum",
                isRequired = isRequired,
                description = description
            };
        }

        private static GameActionParameterDefinition CellParameter(bool required)
        {
            return new GameActionParameterDefinition
            {
                name = "targetCell",
                valueType = "HexCoord",
                isRequired = required,
                description = "Target hex cell."
            };
        }

        private static GameActionParameterDefinition EdgeParameter(bool required)
        {
            return new GameActionParameterDefinition
            {
                name = "targetEdge",
                valueType = "HexEdgeCoord",
                isRequired = required,
                description = "Target hex edge."
            };
        }

        private static GameActionParameterDefinition ConstructionParameter(bool required)
        {
            return new GameActionParameterDefinition
            {
                name = "targetConstructionId",
                valueType = "string",
                isRequired = required,
                description = "Queued construction id."
            };
        }

        public GameActionEnvelope CreateEnvelope(
            GameActionId actionId,
            HexCoord targetCell = default,
            bool hasTargetCell = false,
            HexEdgeCoord targetEdge = default,
            bool hasTargetEdge = false,
            string targetEntityId = null,
            string targetConstructionId = null,
            FloorPurposeKind floorPurposeKind = FloorPurposeKind.None,
            EdgeFeatureKind edgeFeatureKind = EdgeFeatureKind.None,
            ModuleKind moduleKind = ModuleKind.None,
            LandmarkKind landmarkKind = LandmarkKind.None,
            WorldToolMode toolMode = WorldToolMode.None,
            GameOverlayMode overlayMode = GameOverlayMode.Default,
            InspectorTabKind inspectorTab = InspectorTabKind.Context,
            int tickCount = 0,
            float speedMultiplier = 0f,
            bool desiredRunningState = false,
            GameActionSource source = GameActionSource.HumanUi,
            SessionUiSource uiSource = null)
        {
            return new GameActionEnvelope
            {
                actionId = actionId,
                source = source,
                uiSource = uiSource ?? new SessionUiSource
                {
                    panelId = "ClientWorldHud",
                    elementId = actionId.ToString(),
                    controller = nameof(GameActionService)
                },
                targetCell = targetCell,
                hasTargetCell = hasTargetCell,
                targetEdge = targetEdge,
                hasTargetEdge = hasTargetEdge,
                targetEntityId = targetEntityId ?? string.Empty,
                targetConstructionId = targetConstructionId ?? string.Empty,
                floorPurposeKind = floorPurposeKind,
                edgeFeatureKind = edgeFeatureKind,
                moduleKind = moduleKind,
                landmarkKind = landmarkKind,
                toolMode = toolMode,
                overlayMode = overlayMode,
                inspectorTab = inspectorTab,
                tickCount = tickCount,
                speedMultiplier = speedMultiplier,
                desiredRunningState = desiredRunningState
            };
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}

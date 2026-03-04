using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Networking;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Shared.Interfaces
{
    public interface IWorldGenerator
    {
        WorldSnapshot Generate(WorldGenerationProfile profile, WorldSeed seed, WorldId worldId, FactionId playerFactionId);
    }

    public interface IWorldRepository
    {
        UniTask SaveAsync(WorldSnapshot snapshot, CancellationToken cancellationToken = default);
        UniTask<WorldSnapshot> LoadAsync(WorldId worldId, CancellationToken cancellationToken = default);
        UniTask DeleteAsync(WorldId worldId, CancellationToken cancellationToken = default);
    }

    public interface IConstructionPlanner
    {
        bool CanQueueFloor(ColonyId colonyId, FloorPurposeKind purposeKind);
        ConstructionState CreateFloorBlueprint(ColonyId colonyId, FloorPurposeKind purposeKind);
    }

    public interface IJobPlanner
    {
        bool TryReserve(JobRequest request, EntityUid workerEntityUid, long currentTick, out JobReservation reservation);
    }

    public interface ISpaceClassifier
    {
        void Rebuild(WorldSnapshot snapshot);
    }

    public interface IOccupancyResolver
    {
        OccupancyEvaluation EvaluateCell(WorldSnapshot snapshot, HexCoord cell, MovementClassMask movementMask);
    }

    public interface ILogisticsPlanner
    {
        bool TryAssignDelivery(WorldSnapshot snapshot, ResourceType resourceType, ConstructionFeatureId constructionFeatureId, out ItemStackId itemStackId);
    }

    public interface IPathCostProvider
    {
        float GetMoveCost(WorldSnapshot snapshot, HexCoord fromCell, HexCoord toCell, MovementClassMask movementMask);
    }

    public interface ILandmarkPlacementValidator
    {
        bool CanPlace(WorldSnapshot snapshot, LandmarkKind landmarkKind, HexCoord originCell);
    }

    public interface ICombatResolver
    {
        bool CanExecuteOrder(WorldSnapshot snapshot, SquadOrderCommand command);
    }

    public interface ISupplySolver
    {
        int ComputeSupplyRadius(LandmarkKind anchorLandmarkKind, int assignedWorkers);
    }

    public interface ICollisionResolver
    {
        CollisionResolution Resolve(CollisionInstanceConfig instanceConfig);
    }

    public interface IInputCaptureGate
    {
        bool IsWorldInputBlocked();
    }

    public interface IUiDocumentBinder
    {
        void Bind(VisualElement root);
    }

    public interface IUiThemeProvider
    {
        string GetSharedThemePath();
    }

    public interface IUiActionRouter
    {
        void RecordUiAction(SessionActionKind actionKind, SessionActorKind actorKind, SessionUiSource uiSource, string payloadJson, string message);
    }

    public interface IUiInstallValidator
    {
        void Validate(UiInstallReport report);
    }

    public interface ISessionTelemetrySink
    {
        void RecordAction(SessionActionRecord record);
        void RecordInvariant(InvariantCheckRecord record);
    }

    public interface IWorldStateDiffer
    {
        WorldStateDiff CreateDiff(WorldSnapshot beforeSnapshot, WorldSnapshot afterSnapshot);
    }

    public interface IRuntimeInvariantChecker
    {
        void Evaluate(WorldSnapshot snapshot, string actionId = null);
    }

    public interface IManualTestRunner
    {
        void StartRun(string scenarioId, string scenarioTitle);
        void RecordStep(string stepId, string title, string instruction, string result, string note);
        void FinishRun();
    }

    public interface IGameActionService
    {
        UniTask<GameActionResult> ExecuteAsync(GameActionEnvelope envelope);
        List<GameActionDescriptor> GetGlobalActions();
        List<GameActionDescriptor> GetContextActions(GameSelectionState selectionState);
        GameActionParameterSchema GetSchema(GameActionId actionId);
    }

    public interface IGameQueryService
    {
        HumanAgentHudState BuildHudState();
        GameSelectionState BuildSelectionState();
        ContextInspectorViewModel BuildContextInspectorState();
        List<EntitySummaryState> BuildEntitySummaries();
        List<BlockerSummaryState> BuildBlockerSummaries();
        List<GameActionDescriptor> BuildAvailableActions();
    }

    public interface ISelectionContextResolver
    {
        GameSelectionState BuildSelectionState();
    }

    public interface IContextInspectorModelBuilder
    {
        ContextInspectorViewModel Build(GameSelectionState selectionState);
    }

    public interface IWorldBarModelBuilder
    {
        WorldBarState Build();
    }

    public interface IEventStripModelBuilder
    {
        EventStripState Build();
    }

    public interface IPopulationGrowthService
    {
        bool TryAdvanceGrowth(WorldSnapshot snapshot, ColonySnapshot colony);
    }

    public interface IProfileGateway
    {
        UniTask<PlayerProfile> GetLocalProfileAsync(CancellationToken cancellationToken = default);
    }

    public interface IMatchmakingGateway
    {
        UniTask<MatchmakingTicket> QueueCollisionAsync(CollisionMatchRequest matchRequest, CancellationToken cancellationToken = default);
    }
}

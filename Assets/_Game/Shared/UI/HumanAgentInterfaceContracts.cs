using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Telemetry;

namespace Ggj2026.Game.Shared.UI
{
    public enum ColonyMilestone
    {
        None = 0,
        FirstShelter,
        FirstProduction,
        PopulationGrowth,
        FirstSoldier,
        SurvivedFirstRaid,
        WeaponsChain
    }

    public enum GameActionId
    {
        None,
        StartOnboarding,
        PreviousOnboardingStep,
        NextOnboardingStep,
        SkipOnboarding,
        ClearTool,
        SetFloorTool,
        SetEdgeTool,
        SetModuleTool,
        SetLandmarkTool,
        SetClearCellTool,
        SetClearEdgeTool,
        PaintFloor,
        BuildEdge,
        PlaceModule,
        PlaceLandmark,
        ClearCell,
        ClearEdge,
        IncreaseConstructionPriority,
        DecreaseConstructionPriority,
        CancelConstruction,
        TrainSoldier,
        RallyDefenders,
        HoldDefenders,
        DefendArea,
        SetOverlayMode,
        SetRealtimeRunning,
        AdvanceSimulation,
        AdjustSimulationSpeed,
        SaveWorld,
        ResetWorld,
        SelectCell,
        SelectEdge,
        SelectEntity,
        SetInspectorTab,
        FocusSelectedTarget,
        ExplainContext,
        PerimeterDefenders
    }

    public enum GameActionCategory
    {
        Simulation,
        View,
        Floors,
        Edges,
        Modules,
        Landmarks,
        Cleanup,
        World,
        Context,
        Entities,
        Tasks
    }

    public enum GameActionSource
    {
        HumanUi,
        McpAgent,
        ManualQa,
        System
    }

    public enum WorldToolMode
    {
        None,
        PaintFloor,
        BuildEdge,
        PlaceModule,
        PlaceLandmark,
        ClearCell,
        ClearEdge
    }

    public enum GameOverlayMode
    {
        Default,
        Occupancy,
        Spaces
    }

    public enum SelectionTargetKind
    {
        None,
        Cell,
        Edge,
        Entity,
        Stack,
        Module,
        Landmark,
        World
    }

    public enum ContextKind
    {
        None,
        Cell,
        Edge,
        Unit,
        Stack,
        Module,
        Landmark,
        World
    }

    public enum OnboardingFocusArea
    {
        None,
        WorldTopBar,
        OperationsRail,
        WorldView,
        ContextInspector,
        EventStrip
    }

    public enum InspectorTabKind
    {
        Context,
        Tasks,
        Entities,
        World
    }

    [Serializable]
    public class GameActionParameterDefinition
    {
        public string name;
        public string valueType;
        public bool isRequired;
        public string description;
        public List<string> enumOptions = new List<string>();
    }

    [Serializable]
    public class GameActionParameterSchema
    {
        public GameActionId actionId;
        public List<GameActionParameterDefinition> parameters = new List<GameActionParameterDefinition>();
    }

    [Serializable]
    public class GameActionEnvelope
    {
        public GameActionId actionId;
        public GameActionSource source = GameActionSource.HumanUi;
        public SessionUiSource uiSource = new SessionUiSource();
        public HexCoord targetCell;
        public bool hasTargetCell;
        public HexEdgeCoord targetEdge;
        public bool hasTargetEdge;
        public string targetEntityId;
        public string targetConstructionId;
        public FloorPurposeKind floorPurposeKind;
        public EdgeFeatureKind edgeFeatureKind;
        public ModuleKind moduleKind;
        public LandmarkKind landmarkKind;
        public WorldToolMode toolMode;
        public GameOverlayMode overlayMode;
        public InspectorTabKind inspectorTab;
        public int tickCount;
        public float speedMultiplier;
        public bool desiredRunningState;
        public string note;
        public string payloadJson;
    }

    [Serializable]
    public class GameActionDescriptor
    {
        public GameActionId actionId;
        public GameActionCategory category;
        public string label;
        public string description;
        public string hint;
        public bool isEnabled = true;
        public bool isActive;
        public string blockedReason;
        public string badgeText;
        public GameActionEnvelope envelope = new GameActionEnvelope();
        public GameActionParameterSchema parameterSchema = new GameActionParameterSchema();
    }

    [Serializable]
    public class GameActionResult
    {
        public GameActionId actionId;
        public GameActionSource source = GameActionSource.HumanUi;
        public bool success;
        public string message;
        public string blockedReason;
        public string telemetryActionId;
        public string diffPath;
        public GameSelectionState selectionAfter = new GameSelectionState();
    }

    [Serializable]
    public class GameSelectionState
    {
        public SelectionTargetKind primaryTargetKind;
        public ContextKind resolvedContextKind;
        public HexCoord selectedCell;
        public bool hasSelectedCell;
        public HexEdgeCoord selectedEdge;
        public bool hasSelectedEdge;
        public HexCoord hoveredCell;
        public bool hasHoveredCell;
        public HexEdgeCoord hoveredEdge;
        public bool hasHoveredEdge;
        public string selectedEntityId;
        public string selectedStackId;
        public string selectedModuleId;
        public string selectedLandmarkId;
    }

    [Serializable]
    public class InfoRowState
    {
        public string label;
        public string value;
        public string tone;
    }

    [Serializable]
    public class MetricState
    {
        public string label;
        public string value;
        public string tone;
    }

    [Serializable]
    public class BlockerSummaryState
    {
        public string title;
        public string detail;
        public string severity;
    }

    [Serializable]
    public class EntitySummaryState
    {
        public string entityId;
        public string displayName;
        public string status;
        public string detail;
        public bool isSelected;
        public GameActionDescriptor focusAction = new GameActionDescriptor();
    }

    [Serializable]
    public class ContextCardViewModel
    {
        public string cardId;
        public string title;
        public string subtitle;
        public string summary;
        public string emphasis;
        public string tone;
        public List<InfoRowState> rows = new List<InfoRowState>();
        public List<GameActionDescriptor> actions = new List<GameActionDescriptor>();
    }

    [Serializable]
    public class ContextInspectorViewModel
    {
        public InspectorTabKind activeTab = InspectorTabKind.World;
        public ContextKind contextKind = ContextKind.World;
        public string title;
        public string subtitle;
        public string summary;
        public List<ContextCardViewModel> contextCards = new List<ContextCardViewModel>();
        public List<ContextCardViewModel> taskCards = new List<ContextCardViewModel>();
        public List<ContextCardViewModel> entityCards = new List<ContextCardViewModel>();
        public List<ContextCardViewModel> worldCards = new List<ContextCardViewModel>();
    }

    [Serializable]
    public class OperationsGroupViewModel
    {
        public string groupId;
        public string title;
        public List<GameActionDescriptor> actions = new List<GameActionDescriptor>();
    }

    [Serializable]
    public class OperationsRailViewModel
    {
        public string activeToolLabel;
        public List<OperationsGroupViewModel> groups = new List<OperationsGroupViewModel>();
    }

    [Serializable]
    public class WorldBarState
    {
        public string worldName;
        public string tickText;
        public string simulationText;
        public string saveStateText;
        public string workerText;
        public string growthText;
        public string blockerText;
        public string telemetryText;
        public string overlayText;
        public List<MetricState> metrics = new List<MetricState>();
    }

    [Serializable]
    public class OnboardingStepViewModel
    {
        public bool isActive;
        public string stepId;
        public string title;
        public string body;
        public string emphasis;
        public string tip;
        public OnboardingFocusArea focusArea;
        public int stepIndex;
        public int stepCount;
        public bool canGoBack;
        public bool canSkip;
        public bool isFinalStep;
        public string progressText;
        public List<string> visibleOperationGroups = new List<string>();
        public List<InspectorTabKind> visibleInspectorTabs = new List<InspectorTabKind>();
        public bool hasWorldMarkerCell;
        public HexCoord worldMarkerCell;
        public GameActionId tryItActionId;
        public bool hasTryItAction;
    }

    public enum GameEventSeverity { Info, Warning, Critical }
    public enum GameEventCategory { Economy, Construction, Milestone, Raid, Population, System }

    [Serializable]
    public class GameEventRecord
    {
        public long tick;
        public string text;
        public GameEventSeverity severity;
        public GameEventCategory category;
    }

    [Serializable]
    public class EventStripState
    {
        public string currentToolText;
        public string hoverText;
        public string lastActionText;
        public string blockerText;
        public string warningText;
        public List<GameEventRecord> recentEvents = new List<GameEventRecord>();
    }

    [Serializable]
    public class MilestoneState
    {
        public string milestoneId;
        public string title;
        public string description;
        public bool isComplete;
        public int completedTick;
    }

    [Serializable]
    public class HumanAgentHudState
    {
        public string sessionId;
        public string sceneName;
        public OnboardingStepViewModel onboarding = new OnboardingStepViewModel();
        public GameSelectionState selection = new GameSelectionState();
        public WorldBarState worldBar = new WorldBarState();
        public OperationsRailViewModel operationsRail = new OperationsRailViewModel();
        public ContextInspectorViewModel contextInspector = new ContextInspectorViewModel();
        public EventStripState eventStrip = new EventStripState();
        public List<EntitySummaryState> entities = new List<EntitySummaryState>();
        public List<BlockerSummaryState> blockers = new List<BlockerSummaryState>();
        public List<GameActionDescriptor> availableActions = new List<GameActionDescriptor>();
        public List<MilestoneState> milestones = new List<MilestoneState>();
    }
}

using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.UI;

namespace Ggj2026.Game.Shared.Telemetry
{
    public enum SessionActionKind
    {
        SessionStarted,
        SessionEnded,
        SceneChanged,
        LoadWorld,
        RegenerateWorld,
        SaveWorld,
        PanelOpened,
        PanelClosed,
        ButtonClicked,
        DropdownChanged,
        ToolSelected,
        ToolCleared,
        OverlayModeChanged,
        PaintFloor,
        BuildEdge,
        PlaceModule,
        PlaceLandmark,
        ClearCell,
        ClearEdge,
        TrainSoldier,
        AdvanceSimulation,
        MenuAction,
        ManualTestStarted,
        ManualTestStepCompleted,
        UiInstallStarted,
        UiInstallCompleted,
        RuntimeInvariantCheck,
        SelectionChanged,
        GameplayQuery
    }

    public enum SessionActorKind
    {
        System,
        Player,
        McpAgent,
        AutomatedTest,
        ManualTester,
        EditorTool
    }

    public enum SessionActionResult
    {
        Succeeded,
        Failed,
        Blocked
    }

    public enum StateChangeKind
    {
        Added,
        Updated,
        Removed
    }

    public enum InvariantSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public class TelemetrySessionMetadata
    {
        public string sessionId;
        public string sessionKind = "EditorPlayMode";
        public string startedAtUtc;
        public string endedAtUtc;
        public string unityVersion;
        public string activeScene;
        public string projectVersion = "1";
        public string gitBranch;
        public string gitCommit;
        public string worldId;
        public string factionId;
        public string colonyId;
        public int actionCount;
        public int failedActionCount;
        public int invariantWarningCount;
        public int invariantErrorCount;
    }

    [Serializable]
    public class TelemetrySessionSummary
    {
        public string sessionId;
        public string summaryText;
        public int totalActions;
        public int totalFailures;
        public int totalWarnings;
        public int totalErrors;
        public WorldStateDigest finalDigest = new WorldStateDigest();
    }

    [Serializable]
    public class SessionUiSource
    {
        public string panelId;
        public string elementId;
        public string controller;
    }

    [Serializable]
    public class SessionActionRecord
    {
        public string sessionId;
        public string actionId;
        public string parentActionId;
        public string startedAtUtc;
        public string finishedAtUtc;
        public SessionActionKind actionKind;
        public SessionActorKind actorKind;
        public SessionActionResult result;
        public SessionUiSource uiSource = new SessionUiSource();
        public string logicalActionId;
        public string payloadJson;
        public string message;
        public string preSnapshotPath;
        public string postSnapshotPath;
        public string diffPath;
        public string contextSummary;
        public WorldStateDigest preDigest = new WorldStateDigest();
        public WorldStateDigest postDigest = new WorldStateDigest();
        public List<string> relatedLogIds = new List<string>();
        public List<string> relatedCheckIds = new List<string>();
    }

    [Serializable]
    public class WorldStateDigest
    {
        public long simulationTick;
        public int chunkCount;
        public int colonyCount;
        public int unitCount;
        public int itemStackCount;
        public int constructionCount;
        public int landmarkCount;
        public int spaceCount;
        public int roomCount;
        public List<ResourceStack> factionSummary = new List<ResourceStack>();
    }

    [Serializable]
    public class WorldStateDiff
    {
        public WorldStateDigest beforeDigest = new WorldStateDigest();
        public WorldStateDigest afterDigest = new WorldStateDigest();
        public List<CellStateDelta> changedCells = new List<CellStateDelta>();
        public List<EdgeStateDelta> changedEdges = new List<EdgeStateDelta>();
        public List<ItemStackDelta> changedItemStacks = new List<ItemStackDelta>();
        public List<UnitStateDelta> changedUnits = new List<UnitStateDelta>();
        public List<ResourceNodeDelta> changedResourceNodes = new List<ResourceNodeDelta>();
        public List<ConstructionDelta> changedConstruction = new List<ConstructionDelta>();
        public List<LandmarkDelta> changedLandmarks = new List<LandmarkDelta>();
        public List<SpaceDelta> changedSpaces = new List<SpaceDelta>();
        public List<RoomDelta> changedRooms = new List<RoomDelta>();
    }

    [Serializable]
    public class CellStateDelta
    {
        public StateChangeKind changeKind;
        public HexCoord coord;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class EdgeStateDelta
    {
        public StateChangeKind changeKind;
        public HexEdgeCoord edgeCoord;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class ItemStackDelta
    {
        public StateChangeKind changeKind;
        public string itemStackId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class UnitStateDelta
    {
        public StateChangeKind changeKind;
        public string entityUid;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class ResourceNodeDelta
    {
        public StateChangeKind changeKind;
        public string resourceNodeId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class ConstructionDelta
    {
        public StateChangeKind changeKind;
        public string constructionFeatureId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class LandmarkDelta
    {
        public StateChangeKind changeKind;
        public string landmarkId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class SpaceDelta
    {
        public StateChangeKind changeKind;
        public string regionId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class RoomDelta
    {
        public StateChangeKind changeKind;
        public string regionId;
        public string beforeJson;
        public string afterJson;
    }

    [Serializable]
    public class InvariantCheckRecord
    {
        public string sessionId;
        public string actionId;
        public string checkId;
        public string recordedAtUtc;
        public InvariantSeverity severity;
        public bool passed;
        public string message;
        public string relatedObjectId;
    }

    [Serializable]
    public class ManualTestRunRecord
    {
        public string runId;
        public string sessionId;
        public string startedAtUtc;
        public string finishedAtUtc;
        public string scenarioId;
        public string scenarioTitle;
        public string result;
        public List<ManualTestStepRecord> steps = new List<ManualTestStepRecord>();
    }

    [Serializable]
    public class ManualTestStepRecord
    {
        public string stepId;
        public string title;
        public string instruction;
        public string result;
        public string note;
    }

    [Serializable]
    public class UiInstallRunRecord
    {
        public string runId;
        public string startedAtUtc;
        public string finishedAtUtc;
        public bool dryRun;
        public List<string> createdAssets = new List<string>();
        public List<string> updatedAssets = new List<string>();
        public List<string> validationMessages = new List<string>();
    }

    [Serializable]
    public class ContextSnapshotRecord
    {
        public string recordedAtUtc;
        public GameSelectionState selection = new GameSelectionState();
        public ContextKind contextKind;
        public string summary;
    }

    [Serializable]
    public class HudStateDigest
    {
        public string activeTool;
        public string blockerText;
        public string telemetryText;
        public string activeInspectorTab;
        public string worldSummary;
    }

    [Serializable]
    public class GameActionTelemetryRecord
    {
        public string sessionId;
        public string actionId;
        public GameActionId logicalActionId;
        public GameActionSource source;
        public string recordedAtUtc;
        public ContextSnapshotRecord contextBefore = new ContextSnapshotRecord();
        public ContextSnapshotRecord contextAfter = new ContextSnapshotRecord();
        public string diffPath;
    }

    [Serializable]
    public class AgentQueryTelemetryRecord
    {
        public string sessionId;
        public string queryId;
        public string toolName;
        public string recordedAtUtc;
        public string payloadJson;
        public ContextSnapshotRecord context = new ContextSnapshotRecord();
        public HudStateDigest hudDigest = new HudStateDigest();
    }
}

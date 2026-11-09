using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Diagnostics;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    public sealed class LocalColonyGameplayService
    {
        private const int MaxPathSearchRadius = 20;
        private const int PopulationGrowthTicksRequired = 18;
        private const int SoldierTrainingTicksRequired = 12;
        private const int RaidIntervalTicks = 72;
        private const int RaidCountdownTicks = 6;
        private const int RaidLootAmount = 4;
        private const int ColonyOwnershipRadius = 12;
        private const int SoldierPerimeterRadius = 9;

        private static readonly FloorPurposeKind[] FloorTools =
        {
            FloorPurposeKind.Residential,
            FloorPurposeKind.Storage,
            FloorPurposeKind.Workshop,
            FloorPurposeKind.Weaponry,
            FloorPurposeKind.Barracks,
            FloorPurposeKind.Roadway,
            FloorPurposeKind.Farmland,
            FloorPurposeKind.Utility
        };

        private static readonly ModuleKind[] ModuleTools =
        {
            ModuleKind.Bed,
            ModuleKind.Rack,
            ModuleKind.Bin,
            ModuleKind.Pallet,
            ModuleKind.Workbench,
            ModuleKind.WeaponBench,
            ModuleKind.Bunk,
            ModuleKind.CommandPost,
            ModuleKind.DrillPost
        };

        private static readonly EdgeFeatureKind[] EdgeTools =
        {
            EdgeFeatureKind.Wall,
            EdgeFeatureKind.Door
        };

        private static readonly LandmarkKind[] LandmarkTools =
        {
            LandmarkKind.TownCenterCore,
            LandmarkKind.Tower,
            LandmarkKind.Gatehouse
        };

        private readonly LocalWorldSessionService _sessionService;
        private readonly List<ResourceStack> _scratchSummary = new List<ResourceStack>();

        private WorldSnapshot _snapshot;
        private WorldGenerationProfile _profile;
        private readonly HashSet<ColonyMilestone> _completedMilestones = new HashSet<ColonyMilestone>();
        private readonly Dictionary<ColonyMilestone, int> _milestoneCompletedTick = new Dictionary<ColonyMilestone, int>();
        private string _lastMilestoneMessage;

        private const int MaxRecentEvents = 50;
        private readonly List<GameEventRecord> _recentEvents = new List<GameEventRecord>();

        public LocalColonyGameplayService(LocalWorldSessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public event Action<WorldSnapshot> WorldChanged;
        public event Action<ColonyMilestone> MilestoneAchieved;

        public WorldSnapshot Snapshot => _snapshot;
        public WorldGenerationProfile Profile => _profile;
        public string LastMilestoneMessage => _lastMilestoneMessage;
        public IReadOnlyList<GameEventRecord> RecentEvents => _recentEvents;
        public IReadOnlyList<FloorPurposeKind> PaintableFloors => FloorTools;
        public IReadOnlyList<ModuleKind> PlaceableModules => ModuleTools;
        public IReadOnlyList<EdgeFeatureKind> PlaceableEdges => EdgeTools;
        public IReadOnlyList<LandmarkKind> PlaceableLandmarks => LandmarkTools;

        public OccupancyEvaluation GetOccupancy(HexCoord cell, MovementClassMask movementMask = MovementClassMask.GroundSmall)
        {
            return EvaluateCell(cell, movementMask);
        }

        public void Initialize(WorldSnapshot snapshot, WorldGenerationProfile profile)
        {
            _snapshot = snapshot;
            _profile = profile;

            NormalizeWorld();
            RecomputeDerivedState();
            NotifyWorldChanged(false);
        }

        public ColonySnapshot GetPrimaryColony()
        {
            return _snapshot?.colonies != null && _snapshot.colonies.Count > 0
                ? _snapshot.colonies[0]
                : null;
        }

        public FactionSnapshot GetPlayerFaction()
        {
            if (_snapshot?.factions == null)
            {
                return null;
            }

            for (int i = 0; i < _snapshot.factions.Count; i++)
            {
                if (_snapshot.factions[i].isPlayerControlled)
                {
                    return _snapshot.factions[i];
                }
            }

            return _snapshot.factions.Count > 0 ? _snapshot.factions[0] : null;
        }

        public HexCellState GetCellState(HexCoord cell)
        {
            HexCellState state = FindModifiedCell(cell);
            if (state == null)
            {
                state = new HexCellState { coord = cell };
            }

            HexCellState resolved = CloneCell(state);
            resolved.capacityProfile = ResolveCapacityProfile(cell, resolved);
            return resolved;
        }

        public HexEdgeState GetEdgeState(HexEdgeCoord edge)
        {
            HexEdgeState state = FindEdge(edge);
            return state != null
                ? CloneEdge(state)
                : new HexEdgeState { edgeCoord = edge, isPassable = true };
        }

        public List<UnitSnapshot> GetUnitsAtCell(HexCoord cell)
        {
            var result = new List<UnitSnapshot>();
            if (_snapshot?.units == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                if (_snapshot.units[i].hexCoord == cell)
                {
                    result.Add(_snapshot.units[i]);
                }
            }

            return result;
        }

        public List<ItemStackEntity> GetItemStacksAtCell(HexCoord cell)
        {
            var result = new List<ItemStackEntity>();
            if (_snapshot?.itemStacks == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                if (_snapshot.itemStacks[i].occupiedCells.Contains(cell))
                {
                    result.Add(_snapshot.itemStacks[i]);
                }
            }

            return result;
        }

        public List<ResourceNodeState> GetResourceNodesAtCell(HexCoord cell)
        {
            var result = new List<ResourceNodeState>();
            if (_snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = _snapshot.chunks[i].resourceNodes;
                if (nodes == null)
                {
                    continue;
                }

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (nodes[j].cell == cell)
                    {
                        result.Add(nodes[j]);
                    }
                }
            }

            return result;
        }

        public List<LandmarkState> GetLandmarksAtCell(HexCoord cell)
        {
            var result = new List<LandmarkState>();
            if (_snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<LandmarkState> landmarks = _snapshot.chunks[i].landmarks;
                if (landmarks == null)
                {
                    continue;
                }

                for (int j = 0; j < landmarks.Count; j++)
                {
                    if (landmarks[j].occupiedCells.Contains(cell))
                    {
                        result.Add(landmarks[j]);
                    }
                }
            }

            return result;
        }

        public List<ConstructionState> GetConstructionAtCell(HexCoord cell)
        {
            var result = new List<ConstructionState>();
            if (_snapshot?.constructionFeatures == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState state = _snapshot.constructionFeatures[i];
                if (state.targetCell == cell || state.affectedCells.Contains(cell))
                {
                    result.Add(state);
                }
            }

            return result;
        }

        public List<ConstructionState> GetConstructionAtEdge(HexEdgeCoord edge)
        {
            var result = new List<ConstructionState>();
            if (_snapshot?.constructionFeatures == null)
            {
                return result;
            }

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                if (_snapshot.constructionFeatures[i].blueprint.featureKind == ConstructionFeatureKind.Edge &&
                    _snapshot.constructionFeatures[i].targetEdge == edge)
                {
                    result.Add(_snapshot.constructionFeatures[i]);
                }
            }

            return result;
        }

        public SpaceRegionState GetSpaceAtCell(HexCoord cell)
        {
            if (_snapshot?.cachedSpaces == null)
            {
                return null;
            }

            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                if (_snapshot.cachedSpaces[i].cells.Contains(cell))
                {
                    return _snapshot.cachedSpaces[i];
                }
            }

            return null;
        }

        public EnclosedRoomState GetRoomAtCell(HexCoord cell)
        {
            if (_snapshot?.cachedRooms == null)
            {
                return null;
            }

            for (int i = 0; i < _snapshot.cachedRooms.Count; i++)
            {
                if (_snapshot.cachedRooms[i].cells.Contains(cell))
                {
                    return _snapshot.cachedRooms[i];
                }
            }

            return null;
        }

        public IReadOnlyList<ResourceStack> GetDerivedResourceSummary(FactionId factionId)
        {
            _scratchSummary.Clear();
            if (_snapshot == null)
            {
                return _scratchSummary;
            }

            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId == factionId)
                {
                    AddToSummary(_scratchSummary, stack.resourceType, stack.amount);
                }
            }

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                if (unit.ownerFactionId == factionId && unit.carryLoad.amount > 0)
                {
                    AddToSummary(_scratchSummary, unit.carryLoad.resourceType, unit.carryLoad.amount);
                }
            }

            return _scratchSummary;
        }

        public string GetWorkStatusSummary()
        {
            if (_snapshot == null)
            {
                return "No world loaded.";
            }

            int idleWorkers = 0;
            int busyWorkers = 0;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                if (_snapshot.units[i].currentJobType == JobType.Idle)
                {
                    idleWorkers++;
                }
                else
                {
                    busyWorkers++;
                }
            }

            var missingByResource = new Dictionary<ResourceType, int>();
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.isCompleted)
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

            var lines = new List<string>
            {
                $"Workers: {busyWorkers} busy / {idleWorkers} idle",
                $"Pending constructions: {_snapshot.constructionFeatures.Count}"
            };

            if (missingByResource.Count == 0)
            {
                lines.Add("All queued construction has materials.");
                return string.Join("\n", lines);
            }

            lines.Add("Supply blockers:");
            foreach (KeyValuePair<ResourceType, int> pair in missingByResource)
            {
                int freeAmount = GetFreeStackAmount(pair.Key);
                int stagedAmount = GetReservedConstructionStackAmount(pair.Key);
                int nodeAmount = GetHarvestableNodeYield(pair.Key);
                string status = freeAmount + nodeAmount > 0
                    ? "reachable"
                    : stagedAmount > 0
                        ? "only staged elsewhere"
                        : "no source";
                lines.Add($"- {pair.Key}: need {pair.Value}, free {freeAmount}, staged {stagedAmount}, nodes {nodeAmount} [{status}]");
            }

            return string.Join("\n", lines);
        }

        public string DescribeSelectedCell(HexCoord cell)
        {
            HexCellState state = GetCellState(cell);
            OccupancyEvaluation occupancy = EvaluateCell(cell, MovementClassMask.GroundSmall);
            SpaceRegionState space = GetSpaceAtCell(cell);
            EnclosedRoomState room = GetRoomAtCell(cell);
            List<ItemStackEntity> stacks = GetItemStacksAtCell(cell);
            List<UnitSnapshot> units = GetUnitsAtCell(cell);
            List<ResourceNodeState> nodes = GetResourceNodesAtCell(cell);
            List<ConstructionState> construction = GetConstructionAtCell(cell);
            List<LandmarkState> landmarks = GetLandmarksAtCell(cell);

            var lines = new List<string>
            {
                $"Biome: {ResolveBiome(cell)}",
                $"Floor: {(state.floor.isBuilt ? state.floor.purposeKind.ToString() : "None")} / {state.floor.materialKind}",
                $"Capacity: {occupancy.usedGroundSmallCapacity}/{occupancy.availableGroundSmallCapacity} move {occupancy.moveCost:0.00}"
            };

            if (state.module != null && state.module.isBuilt)
            {
                ModuleDefinition moduleDefinition = SimulationCatalog.GetModuleDefinition(state.module.moduleKind);
                if (moduleDefinition.productionRecipe != null)
                {
                    lines.Add($"Module: {state.module.moduleKind} progress={state.module.operationProgress:0.0}/{moduleDefinition.productionRecipe.requiredWork:0.0}");
                }
                else
                {
                    lines.Add($"Module: {state.module.moduleKind}");
                }
            }
            else
            {
                lines.Add("Module: None");
            }
            lines.Add($"ModuleStatus: {DescribeModuleStatus(cell)}");

            if (space != null)
            {
                lines.Add($"Space: {space.purposeKind} [{space.activationState}] enclosed={space.isEnclosed}");
                lines.Add($"Activation: {DescribeSpaceActivation(space, room)}");
            }

            if (room != null)
            {
                lines.Add($"Room: beds={room.bedCount} racks={room.rackCount} workbenches={room.workbenchCount} weaponBenches={room.weaponBenchCount}");
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                string reservationSuffix = stacks[i].reservedByConstructionId != ConstructionFeatureId.None
                    ? $" reservedByBuild={stacks[i].reservedByConstructionId}"
                    : stacks[i].reservedByModuleUid != EntityUid.None
                        ? $" reservedByModule={stacks[i].reservedByModuleUid}"
                        : string.Empty;
                lines.Add($"Stack: {stacks[i].resourceType} x{stacks[i].amount} [{stacks[i].bulkClass}] policy={stacks[i].storagePolicy}{reservationSuffix}");
            }

            for (int i = 0; i < units.Count; i++)
            {
                lines.Add($"Unit: {units[i].unitRole} {DescribeUnitTask(units[i])}");
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                lines.Add($"Node: {nodes[i].resourceNodeKind} {nodes[i].resourceType} yield={nodes[i].remainingYield}");
            }

            for (int i = 0; i < construction.Count; i++)
            {
                lines.Add($"Build: {DescribeConstruction(construction[i])}");
            }

            for (int i = 0; i < landmarks.Count; i++)
            {
                lines.Add($"Landmark: {landmarks[i].landmarkKind} hp={landmarks[i].hitPoints}");
            }

            return string.Join("\n", lines);
        }

        public string DescribeUnitTask(UnitSnapshot unit)
        {
            if (unit == null)
            {
                return "unavailable.";
            }

            string carryText = unit.carryLoad.amount > 0
                ? $"{unit.carryLoad.resourceType} x{unit.carryLoad.amount}"
                : "empty hands";

            if (unit.currentJobType == JobType.Idle)
            {
                return $"{DescribeIdleUnitReason(unit)} Carrying {carryText}.";
            }

            return $"{DescribeActiveUnitTask(unit)} Carrying {carryText}.";
        }

        private string DescribeActiveUnitTask(UnitSnapshot unit)
        {
            return unit.currentJobType switch
            {
                JobType.HarvestSource => $"Harvesting near {unit.targetHex}.",
                JobType.HaulToStaging => $"Hauling to construction staging at {unit.targetHex}.",
                JobType.HaulToStorage => $"Hauling to storage at {unit.targetHex}.",
                JobType.HaulToConsumer => $"Hauling to module input at {unit.targetHex}.",
                JobType.BuildFloor or JobType.BuildEdge or JobType.InstallModule or JobType.PlaceLandmark => $"Building at {unit.targetHex}.",
                JobType.OperateModule => $"Operating module at {unit.targetHex}.",
                JobType.Garrison => $"Holding position at {unit.targetHex}.",
                _ => $"{unit.currentJobType} at {unit.targetHex}."
            };
        }

        private string DescribeIdleUnitReason(UnitSnapshot unit)
        {
            if (_snapshot == null)
            {
                return "No world loaded.";
            }

            if (unit.carryLoad.amount > 0)
            {
                return "Waiting to drop carried resources.";
            }

            if (TryGetMostUrgentConstructionNeed(unit.hexCoord, out ResourceType constructionResource, out int freeAmount, out int stagedAmount, out int nodeYield))
            {
                if (freeAmount + nodeYield <= 0)
                {
                    return stagedAmount > 0
                        ? $"Construction needs {constructionResource}, but materials are staged elsewhere."
                        : $"No source for {constructionResource}.";
                }

                return $"Waiting to haul {constructionResource} to construction.";
            }

            if (TryGetMostUrgentModuleNeed(unit.ownerFactionId, unit.hexCoord, out ResourceType moduleResource, out int moduleFree, out int moduleNodeYield))
            {
                if (moduleFree + moduleNodeYield <= 0)
                {
                    return $"No source for {moduleResource} needed by modules.";
                }

                return $"Waiting to haul {moduleResource} to a module.";
            }

            if (FindNearestRelocatableStack(unit.ownerFactionId, unit.hexCoord) != null)
            {
                return "Waiting for storage organization assignment.";
            }

            return "No pending work.";
        }

        private bool TryGetMostUrgentConstructionNeed(HexCoord origin, out ResourceType resourceType, out int freeAmount, out int stagedAmount, out int nodeYield)
        {
            resourceType = ResourceType.Wood;
            freeAmount = 0;
            stagedAmount = 0;
            nodeYield = 0;

            int bestScore = int.MaxValue;
            bool found = false;

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.isCompleted || HasDeliveredMaterials(construction))
                {
                    continue;
                }

                if (!TryGetMissingResource(construction, out ResourceType missingResource))
                {
                    continue;
                }

                int score = GetSupplyPriorityScore(construction, origin);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                resourceType = missingResource;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            freeAmount = GetFreeStackAmount(resourceType);
            stagedAmount = GetReservedConstructionStackAmount(resourceType);
            nodeYield = GetHarvestableNodeYield(resourceType);
            return true;
        }

        private bool TryGetMostUrgentModuleNeed(FactionId factionId, HexCoord origin, out ResourceType resourceType, out int freeAmount, out int nodeYield)
        {
            resourceType = ResourceType.Wood;
            freeAmount = 0;
            nodeYield = 0;

            int bestDistance = int.MaxValue;
            bool found = false;

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    HexCellState cell = cells[j];
                    if (!TryGetMissingModuleInput(cell.coord, cell.module, out ResourceType missingResource))
                    {
                        continue;
                    }

                    int distance = origin.DistanceTo(cell.coord);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    resourceType = missingResource;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            freeAmount = CountAvailableFactionResource(factionId, resourceType);
            nodeYield = GetHarvestableNodeYield(resourceType);
            return true;
        }

        public string DescribeSelectedEdge(HexEdgeCoord edge)
        {
            HexEdgeState state = GetEdgeState(edge);
            List<ConstructionState> construction = GetConstructionAtEdge(edge);
            var lines = new List<string>
            {
                $"Edge: {edge}",
                $"Feature: {(state.isBuilt ? state.featureKind.ToString() : "None")}",
                $"Passable: {state.isPassable}"
            };

            for (int i = 0; i < construction.Count; i++)
            {
                lines.Add($"Build: {DescribeConstruction(construction[i])}");
            }

            return string.Join("\n", lines);
        }

        public string DescribeSpaceActivation(HexCoord cell)
        {
            SpaceRegionState space = GetSpaceAtCell(cell);
            if (space != null)
            {
                return DescribeSpaceActivation(space, GetRoomAtCell(cell));
            }

            HexCellState state = GetCellState(cell);
            FloorPurposeKind? pendingPurpose = null;
            List<ConstructionState> construction = GetConstructionAtCell(cell);
            for (int i = 0; i < construction.Count; i++)
            {
                if (construction[i] != null &&
                    !construction[i].isCompleted &&
                    construction[i].blueprint.featureKind == ConstructionFeatureKind.Floor)
                {
                    pendingPurpose = construction[i].blueprint.floorPurposeKind;
                    break;
                }
            }

            if ((!state?.floor?.isBuilt ?? true) && pendingPurpose.HasValue)
            {
                switch (pendingPurpose.Value)
                {
                    case FloorPurposeKind.Storage:
                        return "Storage floor planned.";
                    case FloorPurposeKind.Roadway:
                        return "Road planned.";
                    case FloorPurposeKind.Farmland:
                        return "Farmland planned.";
                    case FloorPurposeKind.Residential:
                    case FloorPurposeKind.Workshop:
                    case FloorPurposeKind.Weaponry:
                    case FloorPurposeKind.Barracks:
                    case FloorPurposeKind.Utility:
                        return $"Needs enclosed {pendingPurpose.Value} room.";
                }
            }

            if (state?.floor == null || !state.floor.isBuilt)
            {
                return "No space.";
            }

            switch (state.floor.purposeKind)
            {
                case FloorPurposeKind.Storage:
                    return "Open storage active.";
                case FloorPurposeKind.Roadway:
                    return "Road active.";
                case FloorPurposeKind.Farmland:
                    return "Farmland active.";
                case FloorPurposeKind.Residential:
                case FloorPurposeKind.Workshop:
                case FloorPurposeKind.Weaponry:
                case FloorPurposeKind.Barracks:
                case FloorPurposeKind.Utility:
                    return $"Needs enclosed {state.floor.purposeKind} room.";
                default:
                    return "No space.";
            }
        }

        public string DescribeModuleStatus(HexCoord cell)
        {
            HexCellState state = GetCellState(cell);
            if (state?.module == null || !state.module.isBuilt)
            {
                return "No module.";
            }

            ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(state.module.moduleKind);
            if (state.floor == null || !state.floor.isBuilt)
            {
                return "Needs built floor.";
            }

            if (state.floor.purposeKind != definition.requiredPurposeKind)
            {
                return $"Requires {definition.requiredPurposeKind} floor.";
            }

            SpaceRegionState space = GetSpaceAtCell(cell);
            if (definition.requiresShelter && (space == null || space.activationState != SpaceActivationState.ShelteredActive))
            {
                return $"Needs enclosed {definition.requiredPurposeKind} room.";
            }

            if (!definition.requiresShelter && space != null && space.activationState == SpaceActivationState.Blocked)
            {
                return $"Needs active {definition.requiredPurposeKind} space.";
            }

            if (definition.productionRecipe == null)
            {
                return "Ready.";
            }

            if (!TryGetProductionRecipe(cell, state.module, out ProductionRecipeDefinition recipe))
            {
                return "Inactive.";
            }

            for (int i = 0; i < recipe.inputCosts.Count; i++)
            {
                int remaining = GetRemainingModuleInputAmount(state.module.moduleUid, recipe.inputCosts[i].resourceType);
                if (remaining > 0)
                {
                    return $"Waiting for {recipe.inputCosts[i].resourceType} x{remaining}.";
                }
            }

            if (state.module.operationProgress > 0f)
            {
                return $"Operating {state.module.operationProgress:0.0}/{recipe.requiredWork:0.0}.";
            }

            return "Ready to operate.";
        }

        public bool CanPaintFloor(FloorPurposeKind purposeKind, HexCoord cell, out string failureReason)
        {
            if (!CanEditCell(cell, out failureReason))
            {
                return false;
            }

            HexCellState existing = FindModifiedCell(cell);
            if (existing != null && existing.floor != null && existing.floor.isBuilt && existing.floor.purposeKind == purposeKind)
            {
                failureReason = "Floor already built with this purpose.";
                return false;
            }

            if (existing?.module != null && existing.module.moduleKind != ModuleKind.None)
            {
                ModuleDefinition moduleDef = SimulationCatalog.GetModuleDefinition(existing.module.moduleKind);
                if (moduleDef.requiredPurposeKind != FloorPurposeKind.None && moduleDef.requiredPurposeKind != purposeKind)
                {
                    failureReason = $"Cell has {moduleDef.displayName} which requires {moduleDef.requiredPurposeKind} floor. Remove the module first.";
                    return false;
                }
            }

            if (HasPendingConstructionAtCell(cell, ConstructionFeatureKind.Floor))
            {
                failureReason = "Floor construction already queued here.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryPaintFloor(FloorPurposeKind purposeKind, HexCoord cell, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.PaintFloor,
                actorKind,
                uiSource,
                $"{{\"purpose\":\"{purposeKind}\",\"cell\":\"{cell}\"}}");

            if (!CanPaintFloor(purposeKind, cell, out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            QueueConstruction(CreateConstructionState(GetPrimaryColony(), GetPlayerFaction(), SimulationCatalog.GetFloorBlueprint(purposeKind), cell));
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Queued {purposeKind} floor at {cell}.");
            return true;
        }

        public bool CanQueueEdge(EdgeFeatureKind edgeFeatureKind, HexEdgeCoord edge, out string failureReason)
        {
            if (HasPendingConstructionAtEdge(edge))
            {
                failureReason = "Edge construction already queued here.";
                return false;
            }

            HexEdgeState existing = FindEdge(edge);
            if (existing != null && existing.isBuilt && existing.featureKind == edgeFeatureKind)
            {
                failureReason = "Edge feature already exists.";
                return false;
            }

            if (edgeFeatureKind == EdgeFeatureKind.Wall && WouldSealReachableSpace(edge, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryQueueEdge(EdgeFeatureKind edgeFeatureKind, HexEdgeCoord edge, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.BuildEdge,
                actorKind,
                uiSource,
                $"{{\"edgeFeature\":\"{edgeFeatureKind}\",\"edge\":\"{edge}\"}}");

            if (!CanQueueEdge(edgeFeatureKind, edge, out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            QueueConstruction(CreateConstructionState(GetPrimaryColony(), GetPlayerFaction(), SimulationCatalog.GetEdgeBlueprint(edgeFeatureKind), edge.A, edge));
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Queued {edgeFeatureKind} at {edge}.");
            return true;
        }

        public bool CanPlaceModule(ModuleKind moduleKind, HexCoord cell, out string failureReason)
        {
            if (!CanEditCell(cell, out failureReason))
            {
                return false;
            }

            HexCellState state = GetCellState(cell);
            if (state.floor == null || !state.floor.isBuilt)
            {
                failureReason = "Modules require a built floor.";
                return false;
            }

            if (state.module != null && state.module.isBuilt)
            {
                failureReason = "Cell already contains a built module.";
                return false;
            }

            if (HasPendingConstructionAtCell(cell, ConstructionFeatureKind.Module))
            {
                failureReason = "Module construction already queued here.";
                return false;
            }

            if (!TryValidateModulePlacement(moduleKind, cell, out failureReason))
            {
                return false;
            }

            if (!CanAnyWorkerReach(cell))
            {
                failureReason = "Workers cannot reach this tile. Keep a door or clear a path first.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryPlaceModule(ModuleKind moduleKind, HexCoord cell, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.PlaceModule,
                actorKind,
                uiSource,
                $"{{\"module\":\"{moduleKind}\",\"cell\":\"{cell}\"}}");

            if (!CanPlaceModule(moduleKind, cell, out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            QueueConstruction(CreateConstructionState(GetPrimaryColony(), GetPlayerFaction(), SimulationCatalog.GetModuleBlueprint(moduleKind), cell));
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Queued {moduleKind} at {cell}.");
            return true;
        }

        public bool CanPlaceLandmark(LandmarkKind landmarkKind, HexCoord originCell, out string failureReason)
        {
            ColonySnapshot colony = GetPrimaryColony();
            FactionSnapshot faction = GetPlayerFaction();
            if (colony == null || faction == null)
            {
                failureReason = "No active colony.";
                return false;
            }

            LandmarkDefinition definition = SimulationCatalog.GetLandmarkDefinition(landmarkKind);
            List<HexCoord> occupiedCells = ResolveFootprint(originCell, definition.footprint);
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                if (!CanEditCell(occupiedCells[i], out failureReason))
                {
                    return false;
                }

                if (GetLandmarksAtCell(occupiedCells[i]).Count > 0)
                {
                    failureReason = "Landmark footprint overlaps another landmark.";
                    return false;
                }

                if (HasPendingConstructionAtCell(occupiedCells[i], ConstructionFeatureKind.Landmark))
                {
                    failureReason = "Landmark construction already queued in this footprint.";
                    return false;
                }
            }

            if (!TryValidateLandmarkPlacement(definition, occupiedCells, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryPlaceLandmark(LandmarkKind landmarkKind, HexCoord originCell, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.PlaceLandmark,
                actorKind,
                uiSource,
                $"{{\"landmark\":\"{landmarkKind}\",\"origin\":\"{originCell}\"}}");
            if (!CanPlaceLandmark(landmarkKind, originCell, out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            ColonySnapshot colony = GetPrimaryColony();
            FactionSnapshot faction = GetPlayerFaction();
            LandmarkDefinition definition = SimulationCatalog.GetLandmarkDefinition(landmarkKind);
            List<HexCoord> occupiedCells = ResolveFootprint(originCell, definition.footprint);
            ConstructionState state = CreateConstructionState(colony, faction, SimulationCatalog.GetLandmarkBlueprint(landmarkKind), originCell);
            state.affectedCells = occupiedCells;
            QueueConstruction(state);
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Queued {landmarkKind} at {originCell}.");
            return true;
        }

        public bool CanClearCellFeature(HexCoord cell, out string failureReason)
        {
            for (int i = _snapshot.constructionFeatures.Count - 1; i >= 0; i--)
            {
                if (_snapshot.constructionFeatures[i].targetCell == cell || _snapshot.constructionFeatures[i].affectedCells.Contains(cell))
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            HexCellState state = FindModifiedCell(cell);
            if (state != null && state.module != null && state.module.isBuilt)
            {
                failureReason = string.Empty;
                return true;
            }

            if (state != null && state.floor != null && state.floor.isBuilt)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = "Nothing to clear on this cell.";
            return false;
        }

        public bool TryClearCellFeature(HexCoord cell, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.ClearCell,
                actorKind,
                uiSource,
                $"{{\"cell\":\"{cell}\"}}");
            for (int i = _snapshot.constructionFeatures.Count - 1; i >= 0; i--)
            {
                if (_snapshot.constructionFeatures[i].targetCell == cell || _snapshot.constructionFeatures[i].affectedCells.Contains(cell))
                {
                    ConstructionFeatureId constructionFeatureId = _snapshot.constructionFeatures[i].constructionFeatureId;
                    bool canceled = TryCancelConstruction(constructionFeatureId, out failureReason, uiSource, actorKind);
                    CompleteTelemetryAction(scope, canceled, canceled ? $"Cleared queued construction at {cell}." : failureReason);
                    return canceled;
                }
            }

            HexCellState state = FindModifiedCell(cell);
            if (state != null && state.module != null && state.module.isBuilt)
            {
                state.module = new ModuleState();
                RecomputeDerivedState();
                NotifyWorldChanged();
                failureReason = string.Empty;
                CompleteTelemetryAction(scope, true, $"Cleared module at {cell}.");
                return true;
            }

            if (state != null && state.floor != null && state.floor.isBuilt)
            {
                state.floor = new FloorState();
                RecomputeDerivedState();
                NotifyWorldChanged();
                failureReason = string.Empty;
                CompleteTelemetryAction(scope, true, $"Cleared floor at {cell}.");
                return true;
            }

            failureReason = "Nothing to clear on this cell.";
            CompleteTelemetryAction(scope, false, failureReason);
            return false;
        }

        public bool CanClearEdgeFeature(HexEdgeCoord edge, out string failureReason)
        {
            for (int i = _snapshot.constructionFeatures.Count - 1; i >= 0; i--)
            {
                if (_snapshot.constructionFeatures[i].blueprint.featureKind == ConstructionFeatureKind.Edge &&
                    _snapshot.constructionFeatures[i].targetEdge == edge)
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            HexEdgeState state = FindEdge(edge);
            if (state != null && state.isBuilt)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = "Nothing to clear on this edge.";
            return false;
        }

        public bool TryClearEdgeFeature(HexEdgeCoord edge, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.ClearEdge,
                actorKind,
                uiSource,
                $"{{\"edge\":\"{edge}\"}}");
            for (int i = _snapshot.constructionFeatures.Count - 1; i >= 0; i--)
            {
                if (_snapshot.constructionFeatures[i].blueprint.featureKind == ConstructionFeatureKind.Edge &&
                    _snapshot.constructionFeatures[i].targetEdge == edge)
                {
                    ConstructionFeatureId constructionFeatureId = _snapshot.constructionFeatures[i].constructionFeatureId;
                    bool canceled = TryCancelConstruction(constructionFeatureId, out failureReason, uiSource, actorKind);
                    CompleteTelemetryAction(scope, canceled, canceled ? $"Cleared queued edge at {edge}." : failureReason);
                    return canceled;
                }
            }

            HexEdgeState state = FindEdge(edge);
            if (state != null && state.isBuilt)
            {
                state.featureKind = EdgeFeatureKind.None;
                state.isBuilt = false;
                state.isPassable = true;
                RecomputeDerivedState();
                NotifyWorldChanged();
                failureReason = string.Empty;
                CompleteTelemetryAction(scope, true, $"Cleared edge at {edge}.");
                return true;
            }

            failureReason = "Nothing to clear on this edge.";
            CompleteTelemetryAction(scope, false, failureReason);
            return false;
        }

        public bool CanAdjustConstructionPriority(ConstructionFeatureId constructionFeatureId, out string failureReason)
        {
            ConstructionState construction = FindConstruction(constructionFeatureId);
            if (construction == null || construction.isCompleted)
            {
                failureReason = "Construction was not found.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryAdjustConstructionPriority(ConstructionFeatureId constructionFeatureId, bool increasePriority, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.MenuAction,
                actorKind,
                uiSource,
                $"{{\"constructionId\":\"{constructionFeatureId}\",\"direction\":\"{(increasePriority ? "up" : "down")}\"}}");

            int index = FindConstructionIndex(constructionFeatureId);
            if (index < 0 || _snapshot.constructionFeatures[index].isCompleted)
            {
                failureReason = "Construction was not found.";
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            ConstructionState construction = _snapshot.constructionFeatures[index];
            ConstructionPriority updatedPriority = increasePriority
                ? RaisePriority(construction.priority)
                : LowerPriority(construction.priority);

            if (updatedPriority == construction.priority)
            {
                failureReason = increasePriority
                    ? "Construction is already at highest priority."
                    : "Construction is already at lowest priority.";
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            construction.priority = updatedPriority;
            _snapshot.constructionFeatures[index] = construction;
            NotifyWorldChanged();
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Set {DescribeConstruction(construction)} to {updatedPriority} priority.");
            return true;
        }

        public bool CanCancelConstruction(ConstructionFeatureId constructionFeatureId, out string failureReason)
        {
            ConstructionState construction = FindConstruction(constructionFeatureId);
            if (construction == null || construction.isCompleted)
            {
                failureReason = "Construction was not found.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryCancelConstruction(ConstructionFeatureId constructionFeatureId, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.MenuAction,
                actorKind,
                uiSource,
                $"{{\"constructionId\":\"{constructionFeatureId}\",\"action\":\"cancel\"}}");

            int index = FindConstructionIndex(constructionFeatureId);
            if (index < 0 || _snapshot.constructionFeatures[index].isCompleted)
            {
                failureReason = "Construction was not found.";
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            ConstructionState construction = _snapshot.constructionFeatures[index];
            ReclaimConstructionMaterials(construction);
            ClearConstructionAssignments(constructionFeatureId);
            _snapshot.constructionFeatures.RemoveAt(index);
            RecomputeDerivedState();
            NotifyWorldChanged();
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, $"Canceled {DescribeConstruction(construction)} and reclaimed staged materials.");
            return true;
        }

        public bool CanQueueSoldierTraining(out string failureReason)
        {
            return CanQueueSoldierTrainingForColony(GetPrimaryColony(), GetPrimaryColony()?.soldierTraining?.queuedCount ?? 0, out failureReason);
        }

        public bool TryQueueSoldierTraining(out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.TrainSoldier,
                actorKind,
                uiSource,
                "{\"action\":\"TrainSoldier\"}");

            if (!CanQueueSoldierTraining(out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            ColonySnapshot colony = GetPrimaryColony();
            UnitSnapshot recruit = FindRecruitCandidate(colony.colonyId);
            if (recruit == null)
            {
                failureReason = "No recruit available.";
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            if (!TryConsumeFactionResource(colony.ownerFactionId, ResourceType.Weapons, 1))
            {
                failureReason = "Need Weapons.";
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            colony.soldierTraining.queuedCount++;
            colony.soldierTraining.assignedRecruitUid = EntityUid.None;
            colony.soldierTraining.status = SoldierTrainingStatus.Training;
            colony.soldierTraining.blockedReason = string.Empty;
            if (recruit.carryLoad != null && recruit.carryLoad.amount > 0)
            {
                DropCarryLoad(recruit, recruit.hexCoord, StoragePolicy.None, ConstructionFeatureId.None);
            }
            _snapshot.units.Remove(recruit);
            NotifyWorldChanged();
            CompleteTelemetryAction(scope, true, "Queued soldier training.");
            failureReason = string.Empty;
            return true;
        }

        public SquadOrder GetStandingSquadOrder()
        {
            ColonySnapshot colony = GetPrimaryColony();
            if (colony == null)
            {
                return null;
            }

            return EnsureStandingSquad(colony).currentOrder;
        }

        public bool CanIssueDefenderOrder(SquadOrderKind orderKind, HexCoord targetCell, bool hasTargetCell, out string failureReason)
        {
            ColonySnapshot colony = GetPrimaryColony();
            if (colony == null)
            {
                failureReason = "No colony loaded.";
                return false;
            }

            if (orderKind == SquadOrderKind.DefendArea && !hasTargetCell)
            {
                failureReason = "Target cell is required.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryIssueDefenderOrder(SquadOrderKind orderKind, HexCoord targetCell, bool hasTargetCell, out string failureReason, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.MenuAction,
                actorKind,
                uiSource,
                $"{{\"action\":\"IssueDefenderOrder\",\"order\":\"{orderKind}\",\"targetCell\":\"{targetCell}\"}}");

            if (!CanIssueDefenderOrder(orderKind, targetCell, hasTargetCell, out failureReason))
            {
                CompleteTelemetryAction(scope, false, failureReason);
                return false;
            }

            ColonySnapshot colony = GetPrimaryColony();
            SquadTemplate squad = EnsureStandingSquad(colony);
            SquadOrder order = squad.currentOrder ?? new SquadOrder();
            order.squadId = squad.squadId;
            order.orderKind = orderKind;
            order.issuedAtTime = _snapshot != null ? _snapshot.simulationTick : 0f;

            switch (orderKind)
            {
                case SquadOrderKind.Hold:
                    order.targetHex = TryFindSoldierHoldCell(colony, out HexCoord holdCell) ? holdCell : colony.center;
                    break;
                case SquadOrderKind.DefendArea:
                    order.targetHex = targetCell;
                    break;
                case SquadOrderKind.Perimeter:
                    order.targetHex = TryFindPerimeterAnchorCell(colony, 0, out HexCoord perimeterAnchor)
                        ? perimeterAnchor
                        : (TryFindSoldierRallyCell(colony.colonyId, out HexCoord perimeterFallback) ? perimeterFallback : colony.center);
                    break;
                case SquadOrderKind.Garrison:
                default:
                    order.targetHex = TryFindSoldierRallyCell(colony.colonyId, out HexCoord rallyCell) ? rallyCell : colony.center;
                    order.orderKind = SquadOrderKind.Garrison;
                    break;
            }

            squad.currentOrder = order;
            NotifyWorldChanged();
            failureReason = string.Empty;
            CompleteTelemetryAction(scope, true, DescribeStandingSquadOrder(colony));
            return true;
        }

        public void AdvanceSimulation(int ticks, SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            if (_snapshot == null)
            {
                return;
            }

            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.AdvanceSimulation,
                actorKind,
                uiSource,
                $"{{\"ticks\":{ticks}}}");

            int clampedTicks = Mathf.Clamp(ticks, 1, 200);
            for (int i = 0; i < clampedTicks; i++)
            {
                _snapshot.simulationTick++;
                RegenerateResourceNodes();
                SimulateWorkers();
                DetectAndBreakDeadlock();
                ApplyConstructionCompletions();
                RecomputeDerivedState();
                if (SimulatePopulationGrowth())
                {
                    RecomputeDerivedState();
                }

                if (SimulateSoldierTraining())
                {
                    RecomputeDerivedState();
                }

                if (SimulateRaidPressure())
                {
                    RecomputeDerivedState();
                }

                CheckMilestones();
            }

            NotifyWorldChanged();
            CompleteTelemetryAction(scope, true, $"Advanced simulation by {clampedTicks} ticks.");
        }

        public UniTask SaveAsync()
        {
            return _sessionService != null && _snapshot != null
                ? _sessionService.SaveWorldAsync(_snapshot)
                : UniTask.CompletedTask;
        }

        public async UniTask<bool> ResetWorldAsync(SessionUiSource uiSource = null, SessionActorKind actorKind = SessionActorKind.Player)
        {
            if (_sessionService == null || _profile == null)
            {
                return false;
            }

            SessionTelemetryService.TelemetryActionScope scope = BeginTelemetryAction(
                SessionActionKind.RegenerateWorld,
                actorKind,
                uiSource,
                "{\"action\":\"ResetLocalWorld\"}");

            try
            {
                WorldSnapshot snapshot = await _sessionService.ResetLocalWorldAsync();
                Initialize(snapshot, _profile);
                CompleteTelemetryAction(scope, true, "Local world reset and regenerated.");
                return true;
            }
            catch (Exception exception)
            {
                CompleteTelemetryAction(scope, false, $"Failed to reset local world: {exception.Message}");
                return false;
            }
        }

        public List<MilestoneState> GetMilestoneStates()
        {
            var states = new List<MilestoneState>();
            AddMilestoneState(states, ColonyMilestone.FirstShelter, "First Shelter", "Build an enclosed residential room.");
            AddMilestoneState(states, ColonyMilestone.FirstProduction, "First Production", "Activate a workshop and produce metal.");
            AddMilestoneState(states, ColonyMilestone.PopulationGrowth, "Population Growth", "Grow population to 10.");
            AddMilestoneState(states, ColonyMilestone.FirstSoldier, "First Soldier", "Train a soldier.");
            AddMilestoneState(states, ColonyMilestone.SurvivedFirstRaid, "Survived First Raid", "Survive the first raid.");
            AddMilestoneState(states, ColonyMilestone.WeaponsChain, "Weapons Chain", "Complete Ore to Metal to Weapons chain.");
            return states;
        }

        private void AddMilestoneState(List<MilestoneState> list, ColonyMilestone milestone, string title, string description)
        {
            bool complete = _completedMilestones.Contains(milestone);
            list.Add(new MilestoneState
            {
                milestoneId = milestone.ToString(),
                title = title,
                description = description,
                isComplete = complete,
                completedTick = complete && _milestoneCompletedTick.TryGetValue(milestone, out int tick) ? tick : 0
            });
        }

        private void RecordEvent(string text, GameEventSeverity severity, GameEventCategory category)
        {
            _recentEvents.Add(new GameEventRecord
            {
                tick = _snapshot?.simulationTick ?? 0,
                text = text,
                severity = severity,
                category = category
            });
            while (_recentEvents.Count > MaxRecentEvents) _recentEvents.RemoveAt(0);
        }

        private void CheckMilestones()
        {
            ColonySnapshot colony = GetPrimaryColony();
            FactionSnapshot faction = GetPlayerFaction();
            if (colony == null || faction == null || _snapshot == null)
            {
                return;
            }

            TryAwardMilestone(ColonyMilestone.FirstShelter, () => HasShelteredResidentialSpace(colony));
            TryAwardMilestone(ColonyMilestone.FirstProduction, () => CountAvailableFactionResource(faction.factionId, ResourceType.Metal) > 0);
            TryAwardMilestone(ColonyMilestone.PopulationGrowth, () => colony.population >= 10);
            TryAwardMilestone(ColonyMilestone.FirstSoldier, () => CountUnitsForColony(colony.colonyId, UnitRole.Soldier) > 0);
            TryAwardMilestone(ColonyMilestone.SurvivedFirstRaid, () => colony.raidEvent != null && colony.raidEvent.lastResolvedTick > 0);
            TryAwardMilestone(ColonyMilestone.WeaponsChain, () => CountAvailableFactionResource(faction.factionId, ResourceType.Weapons) > 0);
        }

        private void TryAwardMilestone(ColonyMilestone milestone, Func<bool> condition)
        {
            if (_completedMilestones.Contains(milestone))
            {
                return;
            }

            if (!condition())
            {
                return;
            }

            _completedMilestones.Add(milestone);
            int tick = (int)(_snapshot?.simulationTick ?? 0);
            _milestoneCompletedTick[milestone] = tick;
            string title = milestone switch
            {
                ColonyMilestone.FirstShelter => "First Shelter",
                ColonyMilestone.FirstProduction => "First Production",
                ColonyMilestone.PopulationGrowth => "Population Growth",
                ColonyMilestone.FirstSoldier => "First Soldier",
                ColonyMilestone.SurvivedFirstRaid => "Survived First Raid",
                ColonyMilestone.WeaponsChain => "Weapons Chain",
                _ => milestone.ToString()
            };
            _lastMilestoneMessage = $"Milestone: {title}!";
            RecordEvent(_lastMilestoneMessage, GameEventSeverity.Info, GameEventCategory.Milestone);
            MilestoneAchieved?.Invoke(milestone);
        }

        private void NormalizeWorld()
        {
            _snapshot.snapshotVersion = 10;
            _snapshot.chunks ??= new List<ChunkSnapshot>();
            _snapshot.factions ??= new List<FactionSnapshot>();
            _snapshot.colonies ??= new List<ColonySnapshot>();
            _snapshot.units ??= new List<UnitSnapshot>();
            _snapshot.itemStacks ??= new List<ItemStackEntity>();
            _snapshot.constructionFeatures ??= new List<ConstructionState>();
            _snapshot.cachedSpaces ??= new List<SpaceRegionState>();
            _snapshot.cachedRooms ??= new List<EnclosedRoomState>();

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                _snapshot.chunks[i].modifiedCells ??= new List<HexCellState>();
                _snapshot.chunks[i].modifiedEdges ??= new List<HexEdgeState>();
                _snapshot.chunks[i].resourceNodes ??= new List<ResourceNodeState>();
                _snapshot.chunks[i].landmarks ??= new List<LandmarkState>();
            }

            EnsureFactionSummaryLists();
            EnsureUnitDefinitions();
            EnsureColonyDefaults();
        }

        private void EnsureFactionSummaryLists()
        {
            for (int i = 0; i < _snapshot.factions.Count; i++)
            {
                _snapshot.factions[i].derivedResourceSummary ??= new List<ResourceStack>();
            }
        }

        private void EnsureUnitDefinitions()
        {
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                _snapshot.units[i].movementClassMask = _snapshot.units[i].movementClassMask == MovementClassMask.None
                    ? MovementClassMask.GroundSmall
                    : _snapshot.units[i].movementClassMask;
                _snapshot.units[i].occupancyClassMask = _snapshot.units[i].occupancyClassMask == OccupancyClassMask.None
                    ? OccupancyClassMask.Worker
                    : _snapshot.units[i].occupancyClassMask;
                _snapshot.units[i].carryLoad ??= new CarryLoad();
            }
        }

        private void EnsureColonyDefaults()
        {
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                ColonySnapshot colony = _snapshot.colonies[i];
                colony.population = Mathf.Max(colony.population, CountUnitsForColony(colony.colonyId));
                colony.workforceCap = Mathf.Max(1, colony.population);
                colony.populationGrowth ??= new PopulationGrowthState();
                colony.populationGrowth.requiredProgress = colony.populationGrowth.requiredProgress <= 0f
                    ? PopulationGrowthTicksRequired
                    : colony.populationGrowth.requiredProgress;
                colony.soldierTraining ??= new SoldierTrainingState();
                colony.soldierTraining.requiredProgress = colony.soldierTraining.requiredProgress <= 0f
                    ? SoldierTrainingTicksRequired
                    : colony.soldierTraining.requiredProgress;
                colony.threatAlert ??= new ThreatAlertState();
                colony.raidEvent ??= new RaidEventState();
                if (colony.raidEvent.nextRaidTick <= 0)
                {
                    colony.raidEvent.nextRaidTick = _snapshot.simulationTick + RaidIntervalTicks;
                }
                colony.spawnQueue ??= new List<UnitSpawnRequest>();
                colony.standingSquads ??= new List<SquadTemplate>();
            }
        }

        private SessionTelemetryService.TelemetryActionScope BeginTelemetryAction(SessionActionKind actionKind, SessionActorKind actorKind, SessionUiSource uiSource, string payloadJson)
        {
            return SessionTelemetryService.Active?.BeginWorldAction(
                actionKind,
                uiSource == null ? SessionActorKind.System : actorKind,
                uiSource,
                payloadJson,
                _snapshot);
        }

        private void CompleteTelemetryAction(SessionTelemetryService.TelemetryActionScope scope, bool success, string message)
        {
            SessionTelemetryService.Active?.CompleteWorldAction(scope, _snapshot, success, message);
        }

        private void QueueConstruction(ConstructionState state)
        {
            InitializeDeliveryState(state);
            _snapshot.constructionFeatures.Add(state);
            NotifyWorldChanged();
        }

        private static ConstructionState CreateConstructionState(ColonySnapshot colony, FactionSnapshot faction, ConstructionBlueprint blueprint, HexCoord targetCell, HexEdgeCoord targetEdge = default)
        {
            return new ConstructionState
            {
                constructionFeatureId = ConstructionFeatureId.NewId(),
                ownerColonyId = colony != null ? colony.colonyId : ColonyId.None,
                ownerFactionId = faction != null ? faction.factionId : FactionId.None,
                priority = ConstructionPriority.Normal,
                blueprint = blueprint,
                targetCell = targetCell,
                targetEdge = targetEdge,
                stagingCell = targetCell,
                buildProgress = 0f,
                isCompleted = false,
                affectedCells = new List<HexCoord> { targetCell },
                deliveredMaterials = new List<MaterialDeliveryState>(),
                reservations = new List<StagingReservation>()
            };
        }

        private static void InitializeDeliveryState(ConstructionState state)
        {
            state.deliveredMaterials.Clear();
            for (int i = 0; i < state.blueprint.buildCost.Count; i++)
            {
                ResourceStack cost = state.blueprint.buildCost[i];
                state.deliveredMaterials.Add(new MaterialDeliveryState
                {
                    resourceType = cost.resourceType,
                    requiredAmount = cost.amount,
                    deliveredAmount = 0
                });
            }
        }

        private void RegenerateResourceNodes()
        {
            long tick = _snapshot.simulationTick;
            for (int c = 0; c < _snapshot.chunks.Count; c++)
            {
                List<ResourceNodeState> nodes = _snapshot.chunks[c].resourceNodes;
                if (nodes == null) continue;

                for (int n = 0; n < nodes.Count; n++)
                {
                    ResourceNodeState node = nodes[n];
                    if (node.remainingYield >= node.maxYield) continue;

                    int interval = node.resourceNodeKind switch
                    {
                        ResourceNodeKind.Tree => 8,
                        ResourceNodeKind.FertileSoil => 10,
                        ResourceNodeKind.StoneOutcrop => 15,
                        ResourceNodeKind.OreSeam => 20,
                        _ => 12
                    };

                    if (tick % interval != 0) continue;

                    node.remainingYield = Mathf.Min(node.remainingYield + 1, node.maxYield);
                    if (node.remainingYield > 0 && node.isDepleted)
                    {
                        node.isDepleted = false;
                    }
                }
            }
        }

        private void DetectAndBreakDeadlock()
        {
            if (_snapshot.constructionFeatures == null || _snapshot.constructionFeatures.Count == 0) return;
            if (_snapshot.units == null || _snapshot.units.Count == 0) return;

            int idleWorkers = 0;
            int totalWorkers = 0;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                if (unit.unitRole != UnitRole.Worker || unit.lifecycleState != UnitLifecycleState.Active) continue;
                totalWorkers++;
                if (unit.currentJobType == JobType.Idle) idleWorkers++;
            }

            if (totalWorkers == 0 || idleWorkers < totalWorkers) return;

            int pendingUnsupplied = 0;
            ConstructionState lowestPriority = null;
            int lowestRank = -1;
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState c = _snapshot.constructionFeatures[i];
                if (c.isCompleted) continue;
                if (HasDeliveredMaterials(c)) return;
                if (HasAnyPartialDelivery(c)) return; // partially supplied -- not a true deadlock

                pendingUnsupplied++;
                int rank = GetConstructionPriorityRank(c);
                if (rank > lowestRank)
                {
                    lowestRank = rank;
                    lowestPriority = c;
                }
            }

            if (pendingUnsupplied == 0 || lowestPriority == null) return;

            string desc = DescribeConstruction(lowestPriority);
            TryCancelConstruction(lowestPriority.constructionFeatureId, out _);
            string msg = $"Auto-cancelled {desc} -- not enough materials";
            RecordEvent(msg, GameEventSeverity.Warning, GameEventCategory.Economy);
            Debug.Log($"[DeadlockBreaker] {msg} (all {totalWorkers} workers idle, {pendingUnsupplied} unsupplied constructions)");
        }

        private static bool HasAnyPartialDelivery(ConstructionState construction)
        {
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                if (construction.deliveredMaterials[i].deliveredAmount > 0)
                    return true;
            }
            return false;
        }

        private void SimulateWorkers()
        {
            if (_snapshot.units == null)
            {
                return;
            }

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                ResolveUnitBehavior(unit);
            }
        }

        private void ResolveUnitBehavior(UnitSnapshot unit)
        {
            if (unit.unitRole == UnitRole.Soldier)
            {
                ResolveSoldierBehavior(unit);
                return;
            }

            if (unit.carryLoad != null && unit.carryLoad.amount > 0)
            {
                if (unit.assignedConstructionId != ConstructionFeatureId.None)
                {
                    ConstructionState construction = FindConstruction(unit.assignedConstructionId);
                    if (construction != null && !construction.isCompleted)
                    {
                        unit.targetHex = construction.stagingCell;
                        MoveUnitToward(unit, unit.targetHex);
                        if (unit.hexCoord == construction.stagingCell)
                        {
                            DeliverCarryLoad(unit, construction);
                        }

                        return;
                    }
                }

                if (unit.assignedModuleUid != EntityUid.None &&
                    TryFindModule(unit.assignedModuleUid, out ModuleState assignedModule, out HexCoord moduleCell))
                {
                    unit.currentJobType = JobType.HaulToConsumer;
                    unit.targetHex = moduleCell;
                    MoveUnitToward(unit, moduleCell);
                    if (unit.hexCoord == moduleCell)
                    {
                        DeliverCarryLoadToModule(unit, assignedModule, moduleCell);
                    }

                    return;
                }

                FindBestStorageTarget(unit.ownerFactionId, unit.carryLoad.resourceType, unit.carryLoad.amount, out HexCoord storageTarget, out StoragePolicy storagePolicy);
                unit.currentJobType = JobType.HaulToStorage;
                unit.targetHex = storageTarget;
                MoveUnitToward(unit, storageTarget);
                if (unit.hexCoord == storageTarget)
                {
                    DropCarryLoad(unit, unit.hexCoord, storagePolicy, ConstructionFeatureId.None);
                }

                return;
            }

            if (TryContinueAssignedJob(unit))
            {
                return;
            }

            if (TryAssignBuildJob(unit))
            {
                ExecuteBuildBehavior(unit);
                return;
            }

            if (TryAssignSupplyJob(unit))
            {
                ExecuteSupplyBehavior(unit);
                return;
            }

            if (TryAssignConsumerJob(unit))
            {
                ExecuteConsumerBehavior(unit);
                return;
            }

            if (TryAssignStorageOrganizationJob(unit))
            {
                ExecuteStorageOrganizationBehavior(unit);
                return;
            }

            if (TryAssignOperateModuleJob(unit))
            {
                ExecuteOperateModuleBehavior(unit);
                return;
            }

            unit.currentJobType = JobType.Idle;
            unit.assignedConstructionId = ConstructionFeatureId.None;
            unit.assignedModuleUid = EntityUid.None;
            unit.assignedResourceNodeId = ResourceNodeId.None;
            unit.targetHex = unit.hexCoord;
        }

        private void ResolveSoldierBehavior(UnitSnapshot unit)
        {
            unit.assignedConstructionId = ConstructionFeatureId.None;
            unit.assignedModuleUid = EntityUid.None;
            unit.assignedResourceNodeId = ResourceNodeId.None;
            unit.currentJobType = JobType.Garrison;
            ColonySnapshot colony = FindColony(unit.ownerColonyId);
            SquadOrder order = colony != null ? EnsureStandingSquad(colony).currentOrder : null;
            HexCoord targetHex = unit.hexCoord;

            if (order != null)
            {
                switch (order.orderKind)
                {
                    case SquadOrderKind.Hold:
                    case SquadOrderKind.DefendArea:
                        targetHex = order.targetHex;
                        break;
                    case SquadOrderKind.Perimeter:
                        if (!TryFindPerimeterTargetCell(unit, colony, out targetHex) &&
                            !TryFindSoldierRallyCell(unit.ownerColonyId, out targetHex))
                        {
                            targetHex = unit.hexCoord;
                        }

                        break;
                    case SquadOrderKind.Garrison:
                    default:
                        if (!TryFindSoldierRallyCell(unit.ownerColonyId, out targetHex))
                        {
                            targetHex = unit.hexCoord;
                        }
                        break;
                }
            }
            else if (!TryFindSoldierRallyCell(unit.ownerColonyId, out targetHex))
            {
                targetHex = unit.hexCoord;
            }

            unit.targetHex = targetHex;
            if (unit.hexCoord != targetHex)
            {
                MoveUnitToward(unit, targetHex);
            }
        }

        private bool TryContinueAssignedJob(UnitSnapshot unit)
        {
            switch (unit.currentJobType)
            {
                case JobType.BuildFloor:
                case JobType.BuildEdge:
                case JobType.InstallModule:
                case JobType.PlaceLandmark:
                {
                    ConstructionState construction = FindConstruction(unit.assignedConstructionId);
                    if (construction == null || construction.isCompleted || !HasDeliveredMaterials(construction))
                    {
                        break;
                    }

                    ExecuteBuildBehavior(unit);
                    return true;
                }
                case JobType.HarvestSource:
                case JobType.HaulToStaging:
                {
                    if (unit.assignedConstructionId != ConstructionFeatureId.None)
                    {
                        ConstructionState construction = FindConstruction(unit.assignedConstructionId);
                        if (construction != null &&
                            !construction.isCompleted &&
                            (unit.carryLoad.amount > 0 || TryGetMissingResource(construction, out _)))
                        {
                            ExecuteSupplyBehavior(unit);
                            return true;
                        }
                    }

                    if (unit.assignedModuleUid != EntityUid.None &&
                        TryFindModule(unit.assignedModuleUid, out _, out _))
                    {
                        ExecuteConsumerBehavior(unit);
                        return true;
                    }

                    break;
                }
                case JobType.HaulToConsumer:
                {
                    if (unit.assignedModuleUid != EntityUid.None &&
                        TryFindModule(unit.assignedModuleUid, out _, out _))
                    {
                        ExecuteConsumerBehavior(unit);
                        return true;
                    }

                    break;
                }
                case JobType.HaulToStorage:
                {
                    ExecuteStorageOrganizationBehavior(unit);
                    return true;
                }
                case JobType.OperateModule:
                {
                    if (unit.assignedModuleUid != EntityUid.None &&
                        TryFindModule(unit.assignedModuleUid, out _, out _))
                    {
                        ExecuteOperateModuleBehavior(unit);
                        return true;
                    }

                    break;
                }
            }

            return false;
        }

        private bool TryAssignBuildJob(UnitSnapshot unit)
        {
            ConstructionState best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState candidate = _snapshot.constructionFeatures[i];
                if (candidate.isCompleted || !HasDeliveredMaterials(candidate))
                {
                    continue;
                }

                int distance = unit.hexCoord.DistanceTo(candidate.targetCell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return false;
            }

            unit.assignedConstructionId = best.constructionFeatureId;
            unit.assignedModuleUid = EntityUid.None;
            unit.assignedResourceNodeId = ResourceNodeId.None;
            unit.currentJobType = best.blueprint.featureKind switch
            {
                ConstructionFeatureKind.Floor => JobType.BuildFloor,
                ConstructionFeatureKind.Edge => JobType.BuildEdge,
                ConstructionFeatureKind.Module => JobType.InstallModule,
                ConstructionFeatureKind.Landmark => JobType.PlaceLandmark,
                _ => JobType.Idle
            };
            unit.targetHex = best.targetCell;
            return true;
        }

        private bool TryAssignSupplyJob(UnitSnapshot unit)
        {
            ConstructionState best = null;
            ResourceType missingType = default;
            int bestPriority = int.MaxValue;

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState candidate = _snapshot.constructionFeatures[i];
                if (candidate.isCompleted || HasDeliveredMaterials(candidate))
                {
                    continue;
                }

                if (!TryGetMissingResource(candidate, out ResourceType resourceType))
                {
                    continue;
                }

                int priority = GetSupplyPriorityScore(candidate, unit.hexCoord);
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    best = candidate;
                    missingType = resourceType;
                }
            }

            if (best == null)
            {
                return false;
            }

            unit.assignedConstructionId = best.constructionFeatureId;
            unit.assignedModuleUid = EntityUid.None;

            ItemStackEntity availableStack = FindNearestSupplyStackForConstruction(best, missingType, unit.hexCoord);
            if (availableStack != null)
            {
                unit.currentJobType = JobType.HaulToStaging;
                unit.assignedResourceNodeId = ResourceNodeId.None;
                unit.targetHex = availableStack.occupiedCells.Count > 0 ? availableStack.occupiedCells[0] : best.stagingCell;
                return true;
            }

            ResourceNodeState node = FindNearestHarvestableNode(missingType, unit.hexCoord);
            if (node == null)
            {
                return false;
            }

            unit.currentJobType = JobType.HarvestSource;
            unit.assignedResourceNodeId = node.resourceNodeId;
            unit.targetHex = node.cell;
            return true;
        }

        private bool TryAssignConsumerJob(UnitSnapshot unit)
        {
            ModuleState bestModule = null;
            HexCoord bestCell = HexCoord.Zero;
            ResourceType missingType = default;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    HexCellState cellState = cells[j];
                    if (!TryGetMissingModuleInput(cellState.coord, cellState.module, out ResourceType candidateMissing))
                    {
                        continue;
                    }

                    int distance = unit.hexCoord.DistanceTo(cellState.coord);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestModule = cellState.module;
                        bestCell = cellState.coord;
                        missingType = candidateMissing;
                    }
                }
            }

            if (bestModule == null)
            {
                return false;
            }

            unit.assignedConstructionId = ConstructionFeatureId.None;
            unit.assignedModuleUid = bestModule.moduleUid;

            ItemStackEntity availableStack = FindNearestAvailableStack(unit.ownerFactionId, missingType, unit.hexCoord);
            if (availableStack != null)
            {
                unit.currentJobType = JobType.HaulToConsumer;
                unit.assignedResourceNodeId = ResourceNodeId.None;
                unit.targetHex = availableStack.occupiedCells.Count > 0 ? availableStack.occupiedCells[0] : bestCell;
                return true;
            }

            ResourceNodeState node = FindNearestHarvestableNode(missingType, unit.hexCoord);
            if (node == null)
            {
                unit.assignedModuleUid = EntityUid.None;
                return false;
            }

            unit.currentJobType = JobType.HarvestSource;
            unit.assignedResourceNodeId = node.resourceNodeId;
            unit.targetHex = node.cell;
            return true;
        }

        private bool TryAssignOperateModuleJob(UnitSnapshot unit)
        {
            ModuleState bestModule = null;
            HexCoord bestCell = HexCoord.Zero;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    HexCellState cellState = cells[j];
                    if (!IsModuleProductionReady(cellState.coord, cellState.module))
                    {
                        continue;
                    }

                    int distance = unit.hexCoord.DistanceTo(cellState.coord);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestModule = cellState.module;
                        bestCell = cellState.coord;
                    }
                }
            }

            if (bestModule == null)
            {
                return false;
            }

            unit.assignedConstructionId = ConstructionFeatureId.None;
            unit.assignedModuleUid = bestModule.moduleUid;
            unit.assignedResourceNodeId = ResourceNodeId.None;
            unit.currentJobType = JobType.OperateModule;
            unit.targetHex = bestCell;
            return true;
        }

        private bool TryAssignStorageOrganizationJob(UnitSnapshot unit)
        {
            ItemStackEntity stack = FindNearestRelocatableStack(unit.ownerFactionId, unit.hexCoord);
            if (stack == null || stack.occupiedCells == null || stack.occupiedCells.Count == 0)
            {
                return false;
            }

            unit.assignedConstructionId = ConstructionFeatureId.None;
            unit.assignedModuleUid = EntityUid.None;
            unit.assignedResourceNodeId = ResourceNodeId.None;
            unit.currentJobType = JobType.HaulToStorage;
            unit.targetHex = stack.occupiedCells[0];
            return true;
        }

        private void ExecuteBuildBehavior(UnitSnapshot unit)
        {
            ConstructionState construction = FindConstruction(unit.assignedConstructionId);
            if (construction == null || construction.isCompleted)
            {
                unit.currentJobType = JobType.Idle;
                unit.assignedConstructionId = ConstructionFeatureId.None;
                return;
            }

            MoveUnitToward(unit, construction.targetCell);
            if (unit.hexCoord != construction.targetCell)
            {
                return;
            }

            construction.buildProgress += 2f;
            if (construction.buildProgress >= construction.blueprint.requiredWork)
            {
                construction.isCompleted = true;
            }
        }

        private void ExecuteSupplyBehavior(UnitSnapshot unit)
        {
            ConstructionState construction = FindConstruction(unit.assignedConstructionId);
            if (construction == null || construction.isCompleted)
            {
                unit.currentJobType = JobType.Idle;
                unit.assignedConstructionId = ConstructionFeatureId.None;
                return;
            }

            if (unit.currentJobType == JobType.HarvestSource)
            {
                ResourceNodeState node = FindResourceNode(unit.assignedResourceNodeId);
                if (node == null || node.isDepleted)
                {
                    unit.currentJobType = JobType.Idle;
                    unit.assignedResourceNodeId = ResourceNodeId.None;
                    return;
                }

                MoveUnitToward(unit, node.cell);
                if (unit.hexCoord != node.cell)
                {
                    return;
                }

                int amount = Mathf.Min(4, node.remainingYield);
                node.remainingYield -= amount;
                bool wasDepleted = node.isDepleted;
                node.isDepleted = node.remainingYield <= 0;
                if (node.isDepleted && !wasDepleted)
                    RecordEvent($"{node.resourceNodeKind} depleted ({node.resourceType})", GameEventSeverity.Warning, GameEventCategory.Economy);
                unit.carryLoad.resourceType = node.resourceType;
                unit.carryLoad.amount = amount;
                unit.carryLoad.bulkClass = ResolveBulkClass(amount);
                unit.currentJobType = JobType.HaulToStaging;
                unit.targetHex = construction.stagingCell;
                return;
            }

            if (unit.currentJobType != JobType.HaulToStaging)
            {
                return;
            }

            if (unit.carryLoad.amount == 0)
            {
                if (!TryGetMissingResource(construction, out ResourceType resourceType))
                {
                    unit.currentJobType = JobType.Idle;
                    return;
                }

                ItemStackEntity stack = FindNearestSupplyStackForConstruction(construction, resourceType, unit.hexCoord);
                if (stack == null)
                {
                    unit.currentJobType = JobType.Idle;
                    return;
                }

                HexCoord pickupCell = stack.occupiedCells[0];
                MoveUnitToward(unit, pickupCell);
                if (unit.hexCoord == pickupCell)
                {
                    int remainingRequired = GetRemainingRequiredAmount(construction, resourceType);
                    if (remainingRequired <= 0)
                    {
                        unit.currentJobType = JobType.Idle;
                        unit.assignedConstructionId = ConstructionFeatureId.None;
                        return;
                    }

                    int pickupAmount = Mathf.Min(4, Mathf.Min(stack.amount, remainingRequired));
                    if (pickupAmount <= 0)
                    {
                        unit.currentJobType = JobType.Idle;
                        unit.assignedConstructionId = ConstructionFeatureId.None;
                        return;
                    }

                    if (stack.reservedByConstructionId != ConstructionFeatureId.None &&
                        stack.reservedByConstructionId != construction.constructionFeatureId)
                    {
                        ReleaseReservedMaterial(stack.reservedByConstructionId, stack.resourceType, pickupAmount);
                    }

                    stack.amount -= pickupAmount;
                    unit.carryLoad.resourceType = stack.resourceType;
                    unit.carryLoad.amount = pickupAmount;
                    unit.carryLoad.bulkClass = ResolveBulkClass(pickupAmount);
                    if (stack.amount <= 0)
                    {
                        _snapshot.itemStacks.Remove(stack);
                    }
                }

                return;
            }

            MoveUnitToward(unit, construction.stagingCell);
            if (unit.hexCoord == construction.stagingCell)
            {
                DeliverCarryLoad(unit, construction);
            }
        }

        private void ExecuteConsumerBehavior(UnitSnapshot unit)
        {
            if (!TryFindModule(unit.assignedModuleUid, out ModuleState module, out HexCoord moduleCell))
            {
                unit.currentJobType = JobType.Idle;
                unit.assignedModuleUid = EntityUid.None;
                unit.assignedResourceNodeId = ResourceNodeId.None;
                return;
            }

            if (unit.currentJobType == JobType.HarvestSource)
            {
                ResourceNodeState node = FindResourceNode(unit.assignedResourceNodeId);
                if (node == null || node.isDepleted)
                {
                    unit.currentJobType = JobType.Idle;
                    unit.assignedModuleUid = EntityUid.None;
                    unit.assignedResourceNodeId = ResourceNodeId.None;
                    return;
                }

                MoveUnitToward(unit, node.cell);
                if (unit.hexCoord != node.cell)
                {
                    return;
                }

                int remainingRequired = GetRemainingModuleInputAmount(module.moduleUid, node.resourceType);
                if (remainingRequired <= 0)
                {
                    unit.currentJobType = JobType.Idle;
                    unit.assignedModuleUid = EntityUid.None;
                    unit.assignedResourceNodeId = ResourceNodeId.None;
                    return;
                }

                int amount = Mathf.Min(4, Mathf.Min(node.remainingYield, remainingRequired));
                node.remainingYield -= amount;
                bool wasDepleted2 = node.isDepleted;
                node.isDepleted = node.remainingYield <= 0;
                if (node.isDepleted && !wasDepleted2)
                    RecordEvent($"{node.resourceNodeKind} depleted ({node.resourceType})", GameEventSeverity.Warning, GameEventCategory.Economy);
                unit.carryLoad.resourceType = node.resourceType;
                unit.carryLoad.amount = amount;
                unit.carryLoad.bulkClass = ResolveBulkClass(amount);
                unit.currentJobType = JobType.HaulToConsumer;
                unit.targetHex = moduleCell;
                return;
            }

            if (unit.currentJobType != JobType.HaulToConsumer)
            {
                return;
            }

            if (unit.carryLoad.amount == 0)
            {
                if (!TryGetMissingModuleInput(moduleCell, module, out ResourceType resourceType))
                {
                    unit.currentJobType = JobType.Idle;
                    unit.assignedModuleUid = EntityUid.None;
                    unit.assignedResourceNodeId = ResourceNodeId.None;
                    return;
                }

                ItemStackEntity stack = FindNearestAvailableStack(unit.ownerFactionId, resourceType, unit.hexCoord);
                if (stack == null)
                {
                    ResourceNodeState node = FindNearestHarvestableNode(resourceType, unit.hexCoord);
                    if (node == null)
                    {
                        unit.currentJobType = JobType.Idle;
                        unit.assignedModuleUid = EntityUid.None;
                        unit.assignedResourceNodeId = ResourceNodeId.None;
                        return;
                    }

                    unit.currentJobType = JobType.HarvestSource;
                    unit.assignedResourceNodeId = node.resourceNodeId;
                    unit.targetHex = node.cell;
                    return;
                }

                HexCoord pickupCell = stack.occupiedCells[0];
                MoveUnitToward(unit, pickupCell);
                if (unit.hexCoord == pickupCell)
                {
                    int remainingRequired = GetRemainingModuleInputAmount(module.moduleUid, resourceType);
                    if (remainingRequired <= 0)
                    {
                        unit.currentJobType = JobType.Idle;
                        unit.assignedModuleUid = EntityUid.None;
                        return;
                    }

                    int pickupAmount = Mathf.Min(4, Mathf.Min(stack.amount, remainingRequired));
                    if (pickupAmount <= 0)
                    {
                        unit.currentJobType = JobType.Idle;
                        unit.assignedModuleUid = EntityUid.None;
                        return;
                    }

                    stack.amount -= pickupAmount;
                    unit.carryLoad.resourceType = stack.resourceType;
                    unit.carryLoad.amount = pickupAmount;
                    unit.carryLoad.bulkClass = ResolveBulkClass(pickupAmount);
                    if (stack.amount <= 0)
                    {
                        _snapshot.itemStacks.Remove(stack);
                    }
                }

                return;
            }

            MoveUnitToward(unit, moduleCell);
            if (unit.hexCoord == moduleCell)
            {
                DeliverCarryLoadToModule(unit, module, moduleCell);
            }
        }

        private void ExecuteOperateModuleBehavior(UnitSnapshot unit)
        {
            if (!TryFindModule(unit.assignedModuleUid, out ModuleState module, out HexCoord moduleCell))
            {
                unit.currentJobType = JobType.Idle;
                unit.assignedModuleUid = EntityUid.None;
                return;
            }

            ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(module.moduleKind);
            ProductionRecipeDefinition recipe = definition.productionRecipe;
            if (recipe == null || !IsModuleProductionReady(moduleCell, module))
            {
                unit.currentJobType = JobType.Idle;
                unit.assignedModuleUid = EntityUid.None;
                module.operationProgress = 0f;
                return;
            }

            MoveUnitToward(unit, moduleCell);
            if (unit.hexCoord != moduleCell)
            {
                return;
            }

            module.operationProgress += 2f;
            if (module.operationProgress < recipe.requiredWork)
            {
                return;
            }

            module.operationProgress = 0f;
            ConsumeModuleInputs(module.moduleUid, recipe);
            AddOrMergeItemStack(unit.ownerFactionId, recipe.output.resourceType, recipe.output.amount, moduleCell, StoragePolicy.None, ConstructionFeatureId.None, EntityUid.None);
            unit.currentJobType = JobType.Idle;
            unit.assignedModuleUid = EntityUid.None;
        }

        private void ExecuteStorageOrganizationBehavior(UnitSnapshot unit)
        {
            if (unit.currentJobType != JobType.HaulToStorage)
            {
                return;
            }

            if (unit.carryLoad.amount == 0)
            {
                ItemStackEntity stack = FindNearestRelocatableStack(unit.ownerFactionId, unit.hexCoord);
                if (stack == null || stack.occupiedCells == null || stack.occupiedCells.Count == 0)
                {
                    unit.currentJobType = JobType.Idle;
                    return;
                }

                HexCoord pickupCell = stack.occupiedCells[0];
                MoveUnitToward(unit, pickupCell);
                if (unit.hexCoord != pickupCell)
                {
                    return;
                }

                int pickupAmount = Mathf.Min(4, stack.amount);
                if (pickupAmount <= 0)
                {
                    unit.currentJobType = JobType.Idle;
                    return;
                }

                stack.amount -= pickupAmount;
                unit.carryLoad.resourceType = stack.resourceType;
                unit.carryLoad.amount = pickupAmount;
                unit.carryLoad.bulkClass = ResolveBulkClass(pickupAmount);
                if (stack.amount <= 0)
                {
                    _snapshot.itemStacks.Remove(stack);
                }

                return;
            }

            FindBestStorageTarget(unit.ownerFactionId, unit.carryLoad.resourceType, unit.carryLoad.amount, out HexCoord storageTarget, out StoragePolicy storagePolicy);
            unit.targetHex = storageTarget;
            MoveUnitToward(unit, storageTarget);
            if (unit.hexCoord == storageTarget)
            {
                DropCarryLoad(unit, storageTarget, storagePolicy, ConstructionFeatureId.None);
            }
        }

        private void DeliverCarryLoad(UnitSnapshot unit, ConstructionState construction)
        {
            if (unit.carryLoad.amount <= 0)
            {
                return;
            }

            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                MaterialDeliveryState delivery = construction.deliveredMaterials[i];
                if (delivery.resourceType != unit.carryLoad.resourceType || delivery.deliveredAmount >= delivery.requiredAmount)
                {
                    continue;
                }

                int accepted = Mathf.Min(unit.carryLoad.amount, delivery.requiredAmount - delivery.deliveredAmount);
                delivery.deliveredAmount += accepted;
                construction.deliveredMaterials[i] = delivery;
                AddOrMergeItemStack(construction.ownerFactionId, unit.carryLoad.resourceType, accepted, construction.stagingCell, StoragePolicy.None, construction.constructionFeatureId, EntityUid.None);
                unit.carryLoad.amount -= accepted;
                if (unit.carryLoad.amount <= 0)
                {
                    unit.carryLoad.amount = 0;
                    break;
                }
            }

            if (unit.carryLoad.amount <= 0)
            {
                unit.carryLoad.resourceType = ResourceType.Wood;
                unit.carryLoad.bulkClass = BulkClass.LooseSmall;
                unit.currentJobType = JobType.Idle;
                unit.assignedResourceNodeId = ResourceNodeId.None;
                unit.assignedConstructionId = ConstructionFeatureId.None;
                unit.assignedModuleUid = EntityUid.None;
                return;
            }

            if (GetRemainingRequiredAmount(construction, unit.carryLoad.resourceType) <= 0)
            {
                unit.currentJobType = JobType.HaulToStorage;
                unit.assignedConstructionId = ConstructionFeatureId.None;
                unit.assignedResourceNodeId = ResourceNodeId.None;
            }
        }

        private void DeliverCarryLoadToModule(UnitSnapshot unit, ModuleState module, HexCoord moduleCell)
        {
            if (unit.carryLoad.amount <= 0)
            {
                return;
            }

            int remainingRequired = GetRemainingModuleInputAmount(module.moduleUid, unit.carryLoad.resourceType);
            int accepted = Mathf.Min(unit.carryLoad.amount, remainingRequired);
            if (accepted > 0)
            {
                AddOrMergeItemStack(unit.ownerFactionId, unit.carryLoad.resourceType, accepted, moduleCell, StoragePolicy.None, ConstructionFeatureId.None, module.moduleUid);
                unit.carryLoad.amount -= accepted;
            }

            if (unit.carryLoad.amount <= 0)
            {
                unit.carryLoad.amount = 0;
                unit.carryLoad.resourceType = ResourceType.Wood;
                unit.carryLoad.bulkClass = BulkClass.LooseSmall;
                unit.currentJobType = JobType.Idle;
                unit.assignedResourceNodeId = ResourceNodeId.None;
                unit.assignedModuleUid = EntityUid.None;
                return;
            }

            if (GetRemainingModuleInputAmount(module.moduleUid, unit.carryLoad.resourceType) <= 0)
            {
                unit.currentJobType = JobType.HaulToStorage;
                unit.assignedModuleUid = EntityUid.None;
                unit.assignedResourceNodeId = ResourceNodeId.None;
            }
        }

        private void DropCarryLoad(UnitSnapshot unit, HexCoord cell, StoragePolicy storagePolicy, ConstructionFeatureId reservedByConstructionId)
        {
            if (unit.carryLoad.amount <= 0)
            {
                return;
            }

            AddOrMergeItemStack(unit.ownerFactionId, unit.carryLoad.resourceType, unit.carryLoad.amount, cell, storagePolicy, reservedByConstructionId, EntityUid.None);
            unit.carryLoad.amount = 0;
            unit.carryLoad.resourceType = ResourceType.Wood;
            unit.carryLoad.bulkClass = BulkClass.LooseSmall;
            unit.currentJobType = JobType.Idle;
            unit.assignedModuleUid = EntityUid.None;
        }

        private void ApplyConstructionCompletions()
        {
            for (int i = _snapshot.constructionFeatures.Count - 1; i >= 0; i--)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (!construction.isCompleted)
                {
                    continue;
                }

                RecordEvent($"Built {DescribeConstruction(construction)}", GameEventSeverity.Info, GameEventCategory.Construction);
                ApplyConstruction(construction);
                ConsumeReservedStacks(construction.constructionFeatureId);
                _snapshot.constructionFeatures.RemoveAt(i);
            }
        }

        private void ApplyConstruction(ConstructionState construction)
        {
            switch (construction.blueprint.featureKind)
            {
                case ConstructionFeatureKind.Floor:
                {
                    HexCellState cell = GetOrCreateCell(construction.targetCell);
                    cell.floor = new FloorState
                    {
                        isBuilt = true,
                        purposeKind = construction.blueprint.floorPurposeKind,
                        materialKind = construction.blueprint.floorMaterialKind,
                        surfaceModifiers = ResolveSurfaceModifiers(construction.blueprint.floorPurposeKind)
                    };
                    break;
                }
                case ConstructionFeatureKind.Edge:
                {
                    HexEdgeState edge = GetOrCreateEdge(construction.targetEdge);
                    edge.featureKind = construction.blueprint.edgeFeatureKind;
                    edge.isBuilt = true;
                    edge.ownerFactionId = construction.ownerFactionId;
                    edge.isPassable = construction.blueprint.edgeFeatureKind != EdgeFeatureKind.Wall;
                    break;
                }
                case ConstructionFeatureKind.Module:
                {
                    HexCellState cell = GetOrCreateCell(construction.targetCell);
                    cell.module = new ModuleState
                    {
                        moduleUid = EntityUid.NewId(),
                        moduleKind = construction.blueprint.moduleKind,
                        isBuilt = true,
                        hitPoints = 100
                    };
                    break;
                }
                case ConstructionFeatureKind.Landmark:
                {
                    LandmarkDefinition definition = SimulationCatalog.GetLandmarkDefinition(construction.blueprint.landmarkKind);
                    List<HexCoord> occupiedCells = construction.affectedCells.Count > 0
                        ? new List<HexCoord>(construction.affectedCells)
                        : ResolveFootprint(construction.targetCell, definition.footprint);
                    ChunkSnapshot chunk = EnsureChunkForCell(construction.targetCell);
                    chunk.landmarks.Add(new LandmarkState
                    {
                        landmarkId = LandmarkId.NewId(),
                        ownerFactionId = construction.ownerFactionId,
                        ownerColonyId = construction.ownerColonyId,
                        landmarkKind = construction.blueprint.landmarkKind,
                        originCell = construction.targetCell,
                        hitPoints = definition.maxHitPoints,
                        isBuilt = true,
                        occupiedCells = occupiedCells
                    });

                    if (construction.blueprint.landmarkKind == LandmarkKind.Gatehouse)
                    {
                        HexEdgeState edge = GetOrCreateEdge(new HexEdgeCoord(occupiedCells[0], occupiedCells[1]));
                        edge.featureKind = EdgeFeatureKind.Gate;
                        edge.isBuilt = true;
                        edge.isPassable = true;
                    }

                    break;
                }
            }
        }

        private void ConsumeReservedStacks(ConstructionFeatureId constructionFeatureId)
        {
            for (int i = _snapshot.itemStacks.Count - 1; i >= 0; i--)
            {
                if (_snapshot.itemStacks[i].reservedByConstructionId == constructionFeatureId)
                {
                    _snapshot.itemStacks.RemoveAt(i);
                }
            }
        }

        private void RecomputeDerivedState()
        {
            RebuildFactionSummaries();
            RebuildSpaces();
            RebuildColonySummaries();
        }

        private void RebuildFactionSummaries()
        {
            for (int i = 0; i < _snapshot.factions.Count; i++)
            {
                FactionSnapshot faction = _snapshot.factions[i];
                faction.derivedResourceSummary.Clear();
                IReadOnlyList<ResourceStack> summary = GetDerivedResourceSummary(faction.factionId);
                for (int j = 0; j < summary.Count; j++)
                {
                    faction.derivedResourceSummary.Add(summary[j]);
                }
            }
        }

        private void RebuildSpaces()
        {
            _snapshot.cachedSpaces.Clear();
            _snapshot.cachedRooms.Clear();

            var allBuiltFloors = new Dictionary<HexCoord, HexCellState>();
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                if (cells == null)
                {
                    continue;
                }

                for (int j = 0; j < cells.Count; j++)
                {
                    if (cells[j].floor != null && cells[j].floor.isBuilt && cells[j].floor.purposeKind != FloorPurposeKind.None)
                    {
                        allBuiltFloors[cells[j].coord] = cells[j];
                    }
                }
            }

            var visited = new HashSet<HexCoord>();
            foreach (KeyValuePair<HexCoord, HexCellState> pair in allBuiltFloors)
            {
                if (visited.Contains(pair.Key))
                {
                    continue;
                }

                List<HexCoord> cells = FloodFillFloorRegion(pair.Key, pair.Value.floor.purposeKind, allBuiltFloors, visited);
                bool enclosed = IsRegionEnclosed(cells);
                EnclosedRoomState room = BuildRoomState(cells, pair.Value.floor.purposeKind, enclosed);
                SpaceRegionState space = BuildSpaceState(room);
                _snapshot.cachedSpaces.Add(space);
                if (room.isFunctional || room.cells.Count > 0)
                {
                    _snapshot.cachedRooms.Add(room);
                }
            }
        }

        private void RebuildColonySummaries()
        {
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                ColonySnapshot colony = _snapshot.colonies[i];
                colony.populationGrowth ??= new PopulationGrowthState();
                colony.soldierTraining ??= new SoldierTrainingState();
                colony.threatAlert ??= new ThreatAlertState();
                colony.spawnQueue ??= new List<UnitSpawnRequest>();
                colony.standingSquads ??= new List<SquadTemplate>();
                colony.population = Mathf.Max(colony.population, CountUnitsForColony(colony.colonyId));
                colony.housingCapacity = 0;
                colony.activeOpenSpaces = 0;
                colony.activeShelteredSpaces = 0;

                for (int j = 0; j < _snapshot.cachedSpaces.Count; j++)
                {
                    SpaceRegionState space = _snapshot.cachedSpaces[j];
                    if (!DoesSpaceBelongToColony(colony, space))
                    {
                        continue;
                    }

                    if (space.activationState == SpaceActivationState.OpenActive)
                    {
                        colony.activeOpenSpaces++;
                    }
                    else if (space.activationState == SpaceActivationState.ShelteredActive)
                    {
                        colony.activeShelteredSpaces++;
                    }

                    if (space.purposeKind == FloorPurposeKind.Residential || space.purposeKind == FloorPurposeKind.Barracks)
                    {
                        colony.housingCapacity += space.housingCapacityProvided;
                    }
                }

                LandmarkState townCenter = FindFirstLandmark(colony.colonyId, LandmarkKind.TownCenterCore);
                if (townCenter != null)
                {
                    colony.housingCapacity += SimulationCatalog.GetLandmarkDefinition(LandmarkKind.TownCenterCore).initialStorageBonus / 10;
                }

                colony.workforceCap = Mathf.Max(1, colony.population);
                colony.populationGrowth.requiredProgress = PopulationGrowthTicksRequired;
                colony.soldierTraining.requiredProgress = SoldierTrainingTicksRequired;
                RefreshSoldierTrainingState(colony);
                RefreshThreatAlert(colony);
                RefreshRaidEventState(colony);
            }
        }

        private bool SimulatePopulationGrowth()
        {
            bool spawnedAny = false;
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                ColonySnapshot colony = _snapshot.colonies[i];
                colony.populationGrowth ??= new PopulationGrowthState();
                colony.spawnQueue ??= new List<UnitSpawnRequest>();

                int existingUnits = CountUnitsForColony(colony.colonyId);
                int totalResidents = existingUnits + colony.spawnQueue.Count;
                bool hasShelteredResidential = HasShelteredResidentialSpace(colony);

                if (!hasShelteredResidential)
                {
                    colony.populationGrowth.status = ColonyGrowthStatus.Blocked;
                    colony.populationGrowth.progress = 0f;
                    colony.populationGrowth.blockedReason = "Need sheltered residential room.";
                }
                else if (colony.housingCapacity <= totalResidents)
                {
                    colony.populationGrowth.status = ColonyGrowthStatus.Blocked;
                    colony.populationGrowth.progress = 0f;
                    colony.populationGrowth.blockedReason = "Need more beds.";
                }
                else if (!TryFindSpawnCell(colony, out HexCoord spawnCell))
                {
                    colony.populationGrowth.status = ColonyGrowthStatus.Blocked;
                    colony.populationGrowth.progress = 0f;
                    colony.populationGrowth.blockedReason = "No valid spawn tile.";
                }
                else
                {
                    colony.populationGrowth.status = ColonyGrowthStatus.Growing;
                    colony.populationGrowth.blockedReason = string.Empty;
                    colony.populationGrowth.progress = Mathf.Min(
                        colony.populationGrowth.requiredProgress,
                        colony.populationGrowth.progress + 1f);

                    if (colony.populationGrowth.progress >= colony.populationGrowth.requiredProgress)
                    {
                        colony.populationGrowth.progress = 0f;
                        colony.populationGrowth.status = ColonyGrowthStatus.SpawnQueued;
                        colony.spawnQueue.Add(new UnitSpawnRequest
                        {
                            requestId = EntityUid.NewId(),
                            colonyId = colony.colonyId,
                            ownerFactionId = colony.ownerFactionId,
                            unitRole = DetermineSpawnRole(colony),
                            spawnCell = spawnCell,
                            requestedAtTick = _snapshot.simulationTick
                        });
                    }
                }

                if (ProcessSpawnQueue(colony))
                {
                    int newPop = CountUnitsForColony(colony.colonyId);
                    RecordEvent($"New colonist arrived (pop: {newPop})", GameEventSeverity.Info, GameEventCategory.Population);
                    spawnedAny = true;
                }
            }

            return spawnedAny;
        }

        private bool SimulateSoldierTraining()
        {
            bool changed = false;
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                ColonySnapshot colony = _snapshot.colonies[i];
                colony.soldierTraining ??= new SoldierTrainingState();

                if (colony.soldierTraining.queuedCount <= 0)
                {
                    RefreshSoldierTrainingState(colony);
                    continue;
                }

                if (!CanAdvanceSoldierTraining(colony, out string blockedReason))
                {
                    colony.soldierTraining.status = SoldierTrainingStatus.Blocked;
                    colony.soldierTraining.blockedReason = blockedReason;
                    continue;
                }

                colony.soldierTraining.status = SoldierTrainingStatus.Training;
                colony.soldierTraining.blockedReason = string.Empty;
                colony.soldierTraining.progress = Mathf.Min(
                    colony.soldierTraining.requiredProgress,
                    colony.soldierTraining.progress + 1f);

                if (colony.soldierTraining.progress < colony.soldierTraining.requiredProgress)
                {
                    continue;
                }

                if (!TryCompleteSoldierTraining(colony, out blockedReason))
                {
                    colony.soldierTraining.status = SoldierTrainingStatus.Blocked;
                    colony.soldierTraining.blockedReason = blockedReason;
                    continue;
                }

                colony.soldierTraining.progress = 0f;
                colony.soldierTraining.queuedCount = Mathf.Max(0, colony.soldierTraining.queuedCount - 1);
                RefreshSoldierTrainingState(colony);
                changed = true;
            }

            return changed;
        }

        private List<HexCoord> FloodFillFloorRegion(HexCoord start, FloorPurposeKind purposeKind, Dictionary<HexCoord, HexCellState> allBuiltFloors, HashSet<HexCoord> visited)
        {
            var result = new List<HexCoord>();
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                result.Add(current);

                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = current.Neighbor(direction);
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!allBuiltFloors.TryGetValue(neighbor, out HexCellState neighborState) ||
                        neighborState.floor.purposeKind != purposeKind)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return result;
        }

        private bool IsRegionEnclosed(List<HexCoord> cells)
        {
            var region = new HashSet<HexCoord>(cells);
            foreach (HexCoord cell in cells)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = cell.Neighbor(direction);
                    if (region.Contains(neighbor))
                    {
                        continue;
                    }

                    HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                    HexEdgeState edgeState = FindEdge(edge);
                    if (edgeState == null || !edgeState.isBuilt || (edgeState.featureKind != EdgeFeatureKind.Wall && edgeState.featureKind != EdgeFeatureKind.Door && edgeState.featureKind != EdgeFeatureKind.Gate))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private EnclosedRoomState BuildRoomState(List<HexCoord> cells, FloorPurposeKind purposeKind, bool enclosed)
        {
            var room = new EnclosedRoomState
            {
                regionId = SpaceRegionId.NewId(),
                purposeKind = purposeKind,
                cells = new List<HexCoord>(cells)
            };

            for (int i = 0; i < cells.Count; i++)
            {
                HexCellState cellState = FindModifiedCell(cells[i]);
                if (cellState?.module == null || !cellState.module.isBuilt)
                {
                    continue;
                }

                switch (cellState.module.moduleKind)
                {
                    case ModuleKind.Bed:
                        room.bedCount++;
                        break;
                    case ModuleKind.Rack:
                        room.rackCount++;
                        break;
                    case ModuleKind.Workbench:
                        room.workbenchCount++;
                        break;
                    case ModuleKind.WeaponBench:
                        room.weaponBenchCount++;
                        break;
                }
            }

            room.isFunctional = purposeKind switch
            {
                FloorPurposeKind.Roadway => true,
                FloorPurposeKind.Farmland => true,
                FloorPurposeKind.Storage => true,
                FloorPurposeKind.Residential => enclosed && room.bedCount > 0,
                FloorPurposeKind.Workshop => enclosed && room.workbenchCount > 0,
                FloorPurposeKind.Weaponry => enclosed && room.weaponBenchCount > 0,
                FloorPurposeKind.Barracks => enclosed && CountModules(cells, ModuleKind.Bunk) > 0,
                FloorPurposeKind.Utility => enclosed && CountBuiltModules(cells) > 0,
                _ => false
            };

            return room;
        }

        private SpaceRegionState BuildSpaceState(EnclosedRoomState room)
        {
            SpaceActivationState activation = room.purposeKind switch
            {
                FloorPurposeKind.Roadway => SpaceActivationState.OpenActive,
                FloorPurposeKind.Farmland => SpaceActivationState.OpenActive,
                FloorPurposeKind.Storage => room.isFunctional && IsRegionEnclosed(room.cells) ? SpaceActivationState.ShelteredActive : SpaceActivationState.OpenActive,
                _ => room.isFunctional ? SpaceActivationState.ShelteredActive : SpaceActivationState.Blocked
            };

            int housing = room.bedCount * 2;
            if (room.purposeKind == FloorPurposeKind.Barracks)
            {
                housing += CountModules(room.cells, ModuleKind.Bunk) * 3;
            }

            return new SpaceRegionState
            {
                regionId = room.regionId,
                purposeKind = room.purposeKind,
                isEnclosed = IsRegionEnclosed(room.cells),
                activationState = activation,
                installedModuleCount = CountBuiltModules(room.cells),
                housingCapacityProvided = housing,
                cells = new List<HexCoord>(room.cells)
            };
        }

        private int CountBuiltModules(List<HexCoord> cells)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                HexCellState state = FindModifiedCell(cells[i]);
                if (state?.module != null && state.module.isBuilt)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountModules(List<HexCoord> cells, ModuleKind moduleKind)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                HexCellState state = FindModifiedCell(cells[i]);
                if (state?.module != null && state.module.isBuilt && state.module.moduleKind == moduleKind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddToSummary(List<ResourceStack> summary, ResourceType resourceType, int amount)
        {
            for (int i = 0; i < summary.Count; i++)
            {
                if (summary[i].resourceType != resourceType)
                {
                    continue;
                }

                ResourceStack stack = summary[i];
                stack.amount += amount;
                summary[i] = stack;
                return;
            }

            summary.Add(new ResourceStack { resourceType = resourceType, amount = amount });
        }

        private void AddOrMergeItemStack(
            FactionId ownerFactionId,
            ResourceType resourceType,
            int amount,
            HexCoord cell,
            StoragePolicy storagePolicy,
            ConstructionFeatureId reservedByConstructionId,
            EntityUid reservedByModuleUid)
        {
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId == ownerFactionId &&
                    stack.resourceType == resourceType &&
                    stack.storagePolicy == storagePolicy &&
                    stack.reservedByConstructionId == reservedByConstructionId &&
                    stack.reservedByModuleUid == reservedByModuleUid &&
                    stack.occupiedCells.Count == 1 &&
                    stack.occupiedCells[0] == cell)
                {
                    stack.amount += amount;
                    stack.bulkClass = ResolveStoredBulkClass(stack.amount, storagePolicy);
                    _snapshot.itemStacks[i] = stack;
                    return;
                }
            }

            _snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = ownerFactionId,
                resourceType = resourceType,
                amount = amount,
                bulkClass = ResolveStoredBulkClass(amount, storagePolicy),
                allowedMovementMask = MovementClassMask.GroundSmall | MovementClassMask.Air,
                mergeKey = $"{resourceType}-{storagePolicy}",
                storagePolicy = storagePolicy,
                reservedByConstructionId = reservedByConstructionId,
                reservedByModuleUid = reservedByModuleUid,
                occupiedCells = new List<HexCoord> { cell }
            });
        }

        private static BulkClass ResolveBulkClass(int amount)
        {
            if (amount <= 4)
            {
                return BulkClass.LooseSmall;
            }

            if (amount <= 12)
            {
                return BulkClass.PileMedium;
            }

            return BulkClass.PileLarge;
        }

        private static BulkClass ResolveStoredBulkClass(int amount, StoragePolicy storagePolicy)
        {
            return storagePolicy == StoragePolicy.Containerized
                ? BulkClass.Containerized
                : ResolveBulkClass(amount);
        }

        private ItemStackEntity FindNearestRelocatableStack(FactionId ownerFactionId, HexCoord from)
        {
            ItemStackEntity best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity candidate = _snapshot.itemStacks[i];
                if (candidate.ownerFactionId != ownerFactionId ||
                    candidate.reservedByConstructionId != ConstructionFeatureId.None ||
                    candidate.reservedByModuleUid != EntityUid.None ||
                    candidate.amount <= 0 ||
                    candidate.occupiedCells == null ||
                    candidate.occupiedCells.Count == 0 ||
                    !ShouldRelocateStack(candidate))
                {
                    continue;
                }

                int distance = from.DistanceTo(candidate.occupiedCells[0]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private bool ShouldRelocateStack(ItemStackEntity stack)
        {
            if (stack.storagePolicy == StoragePolicy.None)
            {
                return true;
            }

            if (stack.occupiedCells == null || stack.occupiedCells.Count == 0)
            {
                return false;
            }

            return !CanAcceptStorageAtCell(stack.occupiedCells[0], stack.storagePolicy, stack.resourceType, stack.amount);
        }

        private bool TryValidateModulePlacement(ModuleKind moduleKind, HexCoord cell, out string failureReason)
        {
            ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(moduleKind);
            HexCellState state = GetCellState(cell);
            if (state.floor == null || !state.floor.isBuilt)
            {
                failureReason = "Modules require a built floor.";
                return false;
            }

            if (state.floor.purposeKind != definition.requiredPurposeKind)
            {
                failureReason = $"{definition.displayName} requires a {definition.requiredPurposeKind} floor.";
                return false;
            }

            if (definition.requiresShelter)
            {
                SpaceRegionState space = GetSpaceAtCell(cell);
                if (space == null || !space.isEnclosed)
                {
                    failureReason = $"{definition.displayName} requires a sheltered {definition.requiredPurposeKind} space.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private bool WouldSealReachableSpace(HexEdgeCoord blockedEdge, out string failureReason)
        {
            var visited = new HashSet<HexCoord>();
            var adjacentCells = new List<HexCoord> { blockedEdge.A, blockedEdge.B };

            for (int i = 0; i < adjacentCells.Count; i++)
            {
                HexCoord start = adjacentCells[i];
                if (!IsImportantAccessCell(start) || !visited.Add(start))
                {
                    continue;
                }

                List<HexCoord> region = CollectImportantRegion(start, visited);
                if (CountAccessibleBoundaryEdges(region, blockedEdge) <= 0)
                {
                    failureReason = "This wall would seal a work area with no access. Add a door or keep another opening.";
                    return true;
                }
            }

            failureReason = string.Empty;
            return false;
        }

        private List<HexCoord> CollectImportantRegion(HexCoord start, HashSet<HexCoord> globalVisited)
        {
            var region = new List<HexCoord>();
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                region.Add(current);

                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = current.Neighbor(direction);
                    if (!IsImportantAccessCell(neighbor) || !globalVisited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return region;
        }

        private int CountAccessibleBoundaryEdges(List<HexCoord> region, HexEdgeCoord blockedEdge)
        {
            var regionSet = new HashSet<HexCoord>(region);
            var seenEdges = new HashSet<HexEdgeCoord>();
            int openings = 0;

            for (int i = 0; i < region.Count; i++)
            {
                HexCoord cell = region[i];
                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = cell.Neighbor(direction);
                    if (regionSet.Contains(neighbor))
                    {
                        continue;
                    }

                    HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                    if (!seenEdges.Add(edge))
                    {
                        continue;
                    }

                    if (IsBoundaryEdgeAccessible(edge, blockedEdge))
                    {
                        openings++;
                    }
                }
            }

            return openings;
        }

        private bool IsBoundaryEdgeAccessible(HexEdgeCoord edge, HexEdgeCoord blockedEdge)
        {
            if (edge == blockedEdge)
            {
                return false;
            }

            HexEdgeState existing = FindEdge(edge);
            if (existing != null && existing.isBuilt)
            {
                return existing.isPassable;
            }

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.blueprint.featureKind != ConstructionFeatureKind.Edge || construction.targetEdge != edge)
                {
                    continue;
                }

                return construction.blueprint.edgeFeatureKind == EdgeFeatureKind.Door;
            }

            return true;
        }

        private bool IsImportantAccessCell(HexCoord cell)
        {
            HexCellState state = FindModifiedCell(cell);
            if (state != null)
            {
                bool builtFloor = state.floor != null && state.floor.isBuilt;
                bool builtModule = state.module != null && state.module.isBuilt;
                if (builtFloor || builtModule)
                {
                    return true;
                }
            }

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.targetCell == cell &&
                    (construction.blueprint.featureKind == ConstructionFeatureKind.Floor ||
                     construction.blueprint.featureKind == ConstructionFeatureKind.Module))
                {
                    return true;
                }

                if (construction.affectedCells != null && construction.affectedCells.Contains(cell) &&
                    (construction.blueprint.featureKind == ConstructionFeatureKind.Floor ||
                     construction.blueprint.featureKind == ConstructionFeatureKind.Module))
                {
                    return true;
                }
            }

            return false;
        }

        private List<HexCoord> CollectReachableWorkCells(ColonySnapshot colony)
        {
            var result = new List<HexCoord>();
            var seen = new HashSet<HexCoord>();

            if (_snapshot?.chunks != null)
            {
                for (int i = 0; i < _snapshot.chunks.Count; i++)
                {
                    ChunkSnapshot chunk = _snapshot.chunks[i];
                    if (chunk.modifiedCells == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < chunk.modifiedCells.Count; j++)
                    {
                        HexCellState state = chunk.modifiedCells[j];
                        bool isImportant =
                            (state.floor != null && state.floor.isBuilt) ||
                            (state.module != null && state.module.isBuilt);
                        if (!isImportant || state.coord.DistanceTo(colony.center) > 14 || !seen.Add(state.coord))
                        {
                            continue;
                        }

                        result.Add(state.coord);
                    }
                }
            }

            if (_snapshot?.constructionFeatures != null)
            {
                for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
                {
                    ConstructionState construction = _snapshot.constructionFeatures[i];
                    if (construction.ownerColonyId != colony.colonyId)
                    {
                        continue;
                    }

                    bool isImportant =
                        construction.blueprint.featureKind == ConstructionFeatureKind.Floor ||
                        construction.blueprint.featureKind == ConstructionFeatureKind.Module;
                    if (!isImportant)
                    {
                        continue;
                    }

                    if (construction.targetCell.DistanceTo(colony.center) <= 14 && seen.Add(construction.targetCell))
                    {
                        result.Add(construction.targetCell);
                    }

                    for (int j = 0; j < construction.affectedCells.Count; j++)
                    {
                        HexCoord affectedCell = construction.affectedCells[j];
                        if (affectedCell.DistanceTo(colony.center) <= 14 && seen.Add(affectedCell))
                        {
                            result.Add(affectedCell);
                        }
                    }
                }
            }

            if (seen.Add(colony.center))
            {
                result.Add(colony.center);
            }

            return result;
        }

        private HexCoord ResolveReachabilityAnchor(ColonySnapshot colony, List<HexCoord> importantCells)
        {
            if (_snapshot?.units != null)
            {
                for (int i = 0; i < _snapshot.units.Count; i++)
                {
                    UnitSnapshot unit = _snapshot.units[i];
                    if (unit.ownerColonyId == colony.colonyId)
                    {
                        return unit.hexCoord;
                    }
                }
            }

            for (int i = 0; i < importantCells.Count; i++)
            {
                if (importantCells[i] == colony.center || importantCells[i].DistanceTo(colony.center) <= 1)
                {
                    return importantCells[i];
                }
            }

            return colony.center;
        }

        private bool CanAnyWorkerReach(HexCoord cell)
        {
            ColonySnapshot colony = GetPrimaryColony();
            if (colony == null)
            {
                return false;
            }

            if (_snapshot?.units != null)
            {
                for (int i = 0; i < _snapshot.units.Count; i++)
                {
                    UnitSnapshot unit = _snapshot.units[i];
                    if (unit.ownerColonyId != colony.colonyId)
                    {
                        continue;
                    }

                    if (TryFindPath(unit.hexCoord, cell, out _, default, false))
                    {
                        return true;
                    }
                }
            }

            return TryFindPath(colony.center, cell, out _, default, false);
        }

        private bool TryValidateLandmarkPlacement(LandmarkDefinition definition, List<HexCoord> occupiedCells, out string failureReason)
        {
            if (!definition.placementRule.mayConnectToWalls)
            {
                failureReason = string.Empty;
                return true;
            }

            var cellsWithConnections = new HashSet<HexCoord>();
            var seenEdges = new HashSet<HexEdgeCoord>();
            var occupiedSet = new HashSet<HexCoord>(occupiedCells);

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                HexCoord cell = occupiedCells[i];
                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = cell.Neighbor(direction);
                    if (occupiedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                    if (!seenEdges.Add(edge))
                    {
                        continue;
                    }

                    if (HasWallConnection(edge))
                    {
                        cellsWithConnections.Add(cell);
                    }
                }
            }

            if (cellsWithConnections.Count < occupiedCells.Count)
            {
                failureReason = $"{definition.displayName} must connect to supporting wall edges on each footprint cell.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool HasWallConnection(HexEdgeCoord edge)
        {
            HexEdgeState existing = FindEdge(edge);
            if (existing != null && existing.isBuilt && existing.featureKind == EdgeFeatureKind.Wall)
            {
                return true;
            }

            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.blueprint.featureKind == ConstructionFeatureKind.Edge &&
                    construction.targetEdge == edge &&
                    construction.blueprint.edgeFeatureKind == EdgeFeatureKind.Wall)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanEditCell(HexCoord cell, out string failureReason)
        {
            if (GetPrimaryColony() == null)
            {
                failureReason = "No active colony.";
                return false;
            }

            if (GetPrimaryColony().center.DistanceTo(cell) > 14)
            {
                failureReason = "Selection is outside the colony working radius.";
                return false;
            }

            if (GetLandmarksAtCell(cell).Count > 0)
            {
                failureReason = "Landmark footprint already occupies this cell.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool HasPendingConstructionAtCell(HexCoord cell, ConstructionFeatureKind featureKind)
        {
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                ConstructionState construction = _snapshot.constructionFeatures[i];
                if (construction.blueprint.featureKind == featureKind &&
                    (construction.targetCell == cell || construction.affectedCells.Contains(cell)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPendingConstructionAtEdge(HexEdgeCoord edge)
        {
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                if (_snapshot.constructionFeatures[i].blueprint.featureKind == ConstructionFeatureKind.Edge &&
                    _snapshot.constructionFeatures[i].targetEdge == edge)
                {
                    return true;
                }
            }

            return false;
        }

        private OccupancyEvaluation EvaluateCell(HexCoord cell, MovementClassMask movementMask)
        {
            HexCellState state = GetCellState(cell);
            List<UnitSnapshot> units = GetUnitsAtCell(cell);
            List<ItemStackEntity> stacks = GetItemStacksAtCell(cell);
            bool hasLandmark = GetLandmarksAtCell(cell).Count > 0;
            bool hasLargePile = false;
            int usedCapacity = units.Count;
            float moveCost = ResolveBiomeMoveCost(cell);

            for (int i = 0; i < stacks.Count; i++)
            {
                bool isReservedConstructionStack = stacks[i].reservedByConstructionId != ConstructionFeatureId.None;
                switch (stacks[i].bulkClass)
                {
                    case BulkClass.PileMedium:
                        usedCapacity += isReservedConstructionStack ? 1 : 2;
                        break;
                    case BulkClass.PileLarge:
                        if (isReservedConstructionStack)
                        {
                            usedCapacity += 2;
                        }
                        else
                        {
                            hasLargePile = true;
                        }
                        break;
                }
            }

            TileCapacityProfile capacity = ResolveCapacityProfile(cell, state);
            moveCost += Mathf.Max(0f, usedCapacity - capacity.congestionThreshold) * capacity.congestionMovePenalty;
            moveCost = Mathf.Max(0.35f, moveCost - capacity.roadMoveBonus);

            return new OccupancyEvaluation
            {
                cell = cell,
                movementMask = movementMask,
                isHardBlocked = hasLandmark || hasLargePile || capacity.groundSmallCapacity <= 0,
                usedGroundSmallCapacity = usedCapacity,
                availableGroundSmallCapacity = capacity.groundSmallCapacity,
                moveCost = moveCost,
                reason = hasLandmark ? "Landmark footprint" : hasLargePile ? "Large pile" : string.Empty
            };
        }

        private TileCapacityProfile ResolveCapacityProfile(HexCoord cell, HexCellState state)
        {
            bool hasModule = state.module != null && state.module.isBuilt;
            bool hasLandmark = GetLandmarksAtCell(cell).Count > 0;
            bool hasLargePile = false;
            List<ItemStackEntity> stacks = GetItemStacksAtCell(cell);
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].bulkClass == BulkClass.PileLarge &&
                    stacks[i].reservedByConstructionId == ConstructionFeatureId.None)
                {
                    hasLargePile = true;
                    break;
                }
            }

            return SimulationCatalog.GetDefaultCapacityProfile(state.floor, hasModule, hasLandmark, hasLargePile);
        }

        private void MoveUnitToward(UnitSnapshot unit, HexCoord target)
        {
            unit.targetHex = target;
            if (unit.hexCoord == target)
            {
                return;
            }

            if (TryFindNextStep(unit.hexCoord, target, out HexCoord nextStep, default, false))
            {
                unit.hexCoord = nextStep;
            }
        }

        private bool TryFindNextStep(HexCoord start, HexCoord target, out HexCoord nextStep)
        {
            return TryFindNextStep(start, target, out nextStep, default, false);
        }

        private bool TryFindNextStep(HexCoord start, HexCoord target, out HexCoord nextStep, HexEdgeCoord blockedEdge, bool simulateBlockedEdge)
        {
            nextStep = start;
            if (start == target)
            {
                return true;
            }

            var queue = new Queue<HexCoord>();
            var visited = new HashSet<HexCoord> { start };
            var parent = new Dictionary<HexCoord, HexCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                if (current.DistanceTo(start) > MaxPathSearchRadius)
                {
                    continue;
                }

                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = current.Neighbor(direction);
                    if (visited.Contains(neighbor) || !CanTraverse(current, neighbor, blockedEdge, simulateBlockedEdge))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    parent[neighbor] = current;
                    if (neighbor == target)
                    {
                        nextStep = ReconstructNextStep(start, target, parent);
                        return true;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        private bool TryFindPath(HexCoord start, HexCoord target, out HexCoord nextStep, HexEdgeCoord blockedEdge, bool simulateBlockedEdge)
        {
            return TryFindNextStep(start, target, out nextStep, blockedEdge, simulateBlockedEdge);
        }

        private bool CanTraverse(HexCoord from, HexCoord to)
        {
            return CanTraverse(from, to, default, false);
        }

        private bool CanTraverse(HexCoord from, HexCoord to, HexEdgeCoord blockedEdge, bool simulateBlockedEdge)
        {
            HexEdgeCoord edgeCoord = new HexEdgeCoord(from, to);
            if (simulateBlockedEdge && edgeCoord == blockedEdge)
            {
                return false;
            }

            HexEdgeState edge = FindEdge(edgeCoord);
            if (edge != null && edge.isBuilt && !edge.isPassable)
            {
                return false;
            }

            OccupancyEvaluation occupancy = EvaluateCell(to, MovementClassMask.GroundSmall);
            return !occupancy.isHardBlocked;
        }

        private static HexCoord ReconstructNextStep(HexCoord start, HexCoord target, Dictionary<HexCoord, HexCoord> parent)
        {
            HexCoord current = target;
            while (parent.TryGetValue(current, out HexCoord previous) && previous != start)
            {
                current = previous;
            }

            return current;
        }

        private bool TryGetMissingResource(ConstructionState construction, out ResourceType resourceType)
        {
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                if (construction.deliveredMaterials[i].deliveredAmount < construction.deliveredMaterials[i].requiredAmount)
                {
                    resourceType = construction.deliveredMaterials[i].resourceType;
                    return true;
                }
            }

            resourceType = ResourceType.Wood;
            return false;
        }

        private static bool HasDeliveredMaterials(ConstructionState construction)
        {
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                if (construction.deliveredMaterials[i].deliveredAmount < construction.deliveredMaterials[i].requiredAmount)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetRemainingRequiredAmount(ConstructionState construction, ResourceType resourceType)
        {
            int remaining = 0;
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                if (construction.deliveredMaterials[i].resourceType != resourceType)
                {
                    continue;
                }

                remaining += Mathf.Max(0, construction.deliveredMaterials[i].requiredAmount - construction.deliveredMaterials[i].deliveredAmount);
            }

            return remaining;
        }

        private bool TryFindModule(EntityUid moduleUid, out ModuleState module, out HexCoord cell)
        {
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    ModuleState candidate = cells[j].module;
                    if (candidate != null && candidate.isBuilt && candidate.moduleUid == moduleUid)
                    {
                        module = candidate;
                        cell = cells[j].coord;
                        return true;
                    }
                }
            }

            module = null;
            cell = HexCoord.Zero;
            return false;
        }

        private bool TryGetMissingModuleInput(HexCoord cell, ModuleState module, out ResourceType resourceType)
        {
            resourceType = ResourceType.Wood;
            if (!TryGetProductionRecipe(cell, module, out ProductionRecipeDefinition recipe))
            {
                return false;
            }

            for (int i = 0; i < recipe.inputCosts.Count; i++)
            {
                ResourceStack input = recipe.inputCosts[i];
                if (GetRemainingModuleInputAmount(module.moduleUid, input.resourceType) > 0)
                {
                    resourceType = input.resourceType;
                    return true;
                }
            }

            return false;
        }

        private int GetRemainingModuleInputAmount(EntityUid moduleUid, ResourceType resourceType)
        {
            if (!TryFindModule(moduleUid, out ModuleState module, out HexCoord cell) ||
                !TryGetProductionRecipe(cell, module, out ProductionRecipeDefinition recipe))
            {
                return 0;
            }

            int required = 0;
            for (int i = 0; i < recipe.inputCosts.Count; i++)
            {
                if (recipe.inputCosts[i].resourceType == resourceType)
                {
                    required += recipe.inputCosts[i].amount;
                }
            }

            return Mathf.Max(0, required - GetReservedModuleInputAmount(moduleUid, resourceType));
        }

        private int GetReservedModuleInputAmount(EntityUid moduleUid, ResourceType resourceType)
        {
            int amount = 0;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.reservedByModuleUid == moduleUid &&
                    stack.resourceType == resourceType)
                {
                    amount += stack.amount;
                }
            }

            return amount;
        }

        private bool IsModuleProductionReady(HexCoord cell, ModuleState module)
        {
            if (!TryGetProductionRecipe(cell, module, out ProductionRecipeDefinition recipe))
            {
                return false;
            }

            for (int i = 0; i < recipe.inputCosts.Count; i++)
            {
                if (GetReservedModuleInputAmount(module.moduleUid, recipe.inputCosts[i].resourceType) < recipe.inputCosts[i].amount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetProductionRecipe(HexCoord cell, ModuleState module, out ProductionRecipeDefinition recipe)
        {
            recipe = null;
            if (module == null || !module.isBuilt)
            {
                return false;
            }

            ModuleDefinition definition = SimulationCatalog.GetModuleDefinition(module.moduleKind);
            recipe = definition.productionRecipe;
            if (recipe == null)
            {
                return false;
            }

            SpaceRegionState space = GetSpaceAtCell(cell);
            if (space == null || space.purposeKind != definition.requiredPurposeKind)
            {
                recipe = null;
                return false;
            }

            if (definition.requiresShelter && space.activationState != SpaceActivationState.ShelteredActive)
            {
                recipe = null;
                return false;
            }

            if (!definition.requiresShelter && space.activationState == SpaceActivationState.Blocked)
            {
                recipe = null;
                return false;
            }

            return true;
        }

        private void ConsumeModuleInputs(EntityUid moduleUid, ProductionRecipeDefinition recipe)
        {
            for (int i = 0; i < recipe.inputCosts.Count; i++)
            {
                ResourceStack input = recipe.inputCosts[i];
                int remaining = input.amount;
                for (int j = _snapshot.itemStacks.Count - 1; j >= 0 && remaining > 0; j--)
                {
                    ItemStackEntity stack = _snapshot.itemStacks[j];
                    if (stack.reservedByModuleUid != moduleUid || stack.resourceType != input.resourceType)
                    {
                        continue;
                    }

                    int consumed = Mathf.Min(stack.amount, remaining);
                    stack.amount -= consumed;
                    remaining -= consumed;
                    if (stack.amount <= 0)
                    {
                        _snapshot.itemStacks.RemoveAt(j);
                    }
                    else
                    {
                        stack.bulkClass = ResolveStoredBulkClass(stack.amount, stack.storagePolicy);
                        _snapshot.itemStacks[j] = stack;
                    }
                }
            }
        }

        private ItemStackEntity FindNearestAvailableStack(FactionId factionId, ResourceType resourceType, HexCoord origin)
        {
            ItemStackEntity best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId != factionId ||
                    stack.resourceType != resourceType ||
                    stack.amount <= 0 ||
                    stack.reservedByConstructionId != ConstructionFeatureId.None ||
                    stack.reservedByModuleUid != EntityUid.None)
                {
                    continue;
                }

                int distance = origin.DistanceTo(stack.occupiedCells[0]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = stack;
                }
            }

            return best;
        }

        private bool TryConsumeFactionResource(FactionId factionId, ResourceType resourceType, int amount)
        {
            int remaining = amount;
            for (int i = _snapshot.itemStacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId != factionId ||
                    stack.resourceType != resourceType ||
                    stack.amount <= 0 ||
                    stack.reservedByConstructionId != ConstructionFeatureId.None ||
                    stack.reservedByModuleUid != EntityUid.None)
                {
                    continue;
                }

                int consumed = Mathf.Min(stack.amount, remaining);
                stack.amount -= consumed;
                remaining -= consumed;
                if (stack.amount <= 0)
                {
                    _snapshot.itemStacks.RemoveAt(i);
                }
                else
                {
                    stack.bulkClass = ResolveStoredBulkClass(stack.amount, stack.storagePolicy);
                    _snapshot.itemStacks[i] = stack;
                }
            }

            return remaining <= 0;
        }

        private int CountAvailableFactionResource(FactionId factionId, ResourceType resourceType)
        {
            int total = 0;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId != factionId ||
                    stack.resourceType != resourceType ||
                    stack.amount <= 0 ||
                    stack.reservedByConstructionId != ConstructionFeatureId.None ||
                    stack.reservedByModuleUid != EntityUid.None)
                {
                    continue;
                }

                total += stack.amount;
            }

            return total;
        }

        private ItemStackEntity FindNearestSupplyStackForConstruction(ConstructionState targetConstruction, ResourceType resourceType, HexCoord origin)
        {
            ItemStackEntity freeStack = FindNearestAvailableStack(targetConstruction.ownerFactionId, resourceType, origin);
            if (freeStack != null)
            {
                return freeStack;
            }

            ItemStackEntity best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.ownerFactionId != targetConstruction.ownerFactionId ||
                    stack.resourceType != resourceType ||
                    stack.amount <= 0 ||
                    stack.reservedByConstructionId == ConstructionFeatureId.None ||
                    stack.reservedByConstructionId == targetConstruction.constructionFeatureId ||
                    stack.reservedByModuleUid != EntityUid.None ||
                    stack.occupiedCells == null ||
                    stack.occupiedCells.Count == 0)
                {
                    continue;
                }

                ConstructionState donor = FindConstruction(stack.reservedByConstructionId);
                if (!IsConstructionMaterialReclaimable(targetConstruction, donor))
                {
                    continue;
                }

                int distance = origin.DistanceTo(stack.occupiedCells[0]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = stack;
                }
            }

            return best;
        }

        private ResourceNodeState FindNearestHarvestableNode(ResourceType resourceType, HexCoord origin)
        {
            ResourceNodeState best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = _snapshot.chunks[i].resourceNodes;
                for (int j = 0; j < nodes.Count; j++)
                {
                    ResourceNodeState node = nodes[j];
                    if (node.resourceType != resourceType || node.isDepleted || node.remainingYield <= 0)
                    {
                        continue;
                    }

                    int distance = origin.DistanceTo(node.cell);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = node;
                    }
                }
            }

            return best;
        }

        private int GetFreeStackAmount(ResourceType resourceType)
        {
            int amount = 0;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.resourceType == resourceType &&
                    stack.amount > 0 &&
                    stack.reservedByConstructionId == ConstructionFeatureId.None &&
                    stack.reservedByModuleUid == EntityUid.None)
                {
                    amount += stack.amount;
                }
            }

            return amount;
        }

        private int GetReservedConstructionStackAmount(ResourceType resourceType)
        {
            int amount = 0;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.resourceType == resourceType &&
                    stack.amount > 0 &&
                    stack.reservedByConstructionId != ConstructionFeatureId.None)
                {
                    amount += stack.amount;
                }
            }

            return amount;
        }

        private int GetHarvestableNodeYield(ResourceType resourceType)
        {
            int amount = 0;
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = _snapshot.chunks[i].resourceNodes;
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (nodes[j].resourceType == resourceType &&
                        !nodes[j].isDepleted &&
                        nodes[j].remainingYield > 0)
                    {
                        amount += nodes[j].remainingYield;
                    }
                }
            }

            return amount;
        }

        private static bool IsConstructionMaterialReclaimable(ConstructionState targetConstruction, ConstructionState donorConstruction)
        {
            if (donorConstruction == null || donorConstruction.isCompleted || HasDeliveredMaterials(donorConstruction))
            {
                return false;
            }

            int donorPriority = GetConstructionPriorityRank(donorConstruction);
            int targetPriority = GetConstructionPriorityRank(targetConstruction);
            if (donorPriority > targetPriority)
            {
                return true;
            }

            if (donorPriority < targetPriority)
            {
                return false;
            }

            return GetRemainingMaterialTotal(donorConstruction) > GetRemainingMaterialTotal(targetConstruction);
        }

        private void ReleaseReservedMaterial(ConstructionFeatureId constructionFeatureId, ResourceType resourceType, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ConstructionState construction = FindConstruction(constructionFeatureId);
            if (construction == null)
            {
                return;
            }

            for (int i = 0; i < construction.deliveredMaterials.Count && amount > 0; i++)
            {
                MaterialDeliveryState delivery = construction.deliveredMaterials[i];
                if (delivery.resourceType != resourceType || delivery.deliveredAmount <= 0)
                {
                    continue;
                }

                int released = Mathf.Min(delivery.deliveredAmount, amount);
                delivery.deliveredAmount -= released;
                amount -= released;
                construction.deliveredMaterials[i] = delivery;
            }
        }

        private static int GetSupplyPriorityScore(ConstructionState construction, HexCoord origin)
        {
            return (GetConstructionPriorityRank(construction) * 100000) +
                   (GetMissingMaterialTypeCount(construction) * 10000) +
                   (GetRemainingMaterialTotal(construction) * 100) +
                   Mathf.Min(99, origin.DistanceTo(construction.stagingCell));
        }

        private static int GetConstructionPriorityRank(ConstructionState construction)
        {
            int playerPriority = construction.priority switch
            {
                ConstructionPriority.Critical => 0,
                ConstructionPriority.High => 1,
                ConstructionPriority.Normal => 2,
                ConstructionPriority.Low => 3,
                _ => 2
            };

            int featurePriority = construction.blueprint.featureKind switch
            {
                ConstructionFeatureKind.Floor => 0,
                ConstructionFeatureKind.Edge => 1,
                ConstructionFeatureKind.Module => 2,
                ConstructionFeatureKind.Landmark => 3,
                _ => 4
            };

            return (playerPriority * 10) + featurePriority;
        }

        private static int GetMissingMaterialTypeCount(ConstructionState construction)
        {
            int count = 0;
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                if (construction.deliveredMaterials[i].deliveredAmount < construction.deliveredMaterials[i].requiredAmount)
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetRemainingMaterialTotal(ConstructionState construction)
        {
            int total = 0;
            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                total += Mathf.Max(0, construction.deliveredMaterials[i].requiredAmount - construction.deliveredMaterials[i].deliveredAmount);
            }

            return total;
        }

        private ResourceNodeState FindResourceNode(ResourceNodeId resourceNodeId)
        {
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = _snapshot.chunks[i].resourceNodes;
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (nodes[j].resourceNodeId == resourceNodeId)
                    {
                        return nodes[j];
                    }
                }
            }

            return null;
        }

        private ConstructionState FindConstruction(ConstructionFeatureId constructionFeatureId)
        {
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                if (_snapshot.constructionFeatures[i].constructionFeatureId == constructionFeatureId)
                {
                    return _snapshot.constructionFeatures[i];
                }
            }

            return null;
        }

        private int FindConstructionIndex(ConstructionFeatureId constructionFeatureId)
        {
            for (int i = 0; i < _snapshot.constructionFeatures.Count; i++)
            {
                if (_snapshot.constructionFeatures[i].constructionFeatureId == constructionFeatureId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static ConstructionPriority RaisePriority(ConstructionPriority priority)
        {
            return priority switch
            {
                ConstructionPriority.Low => ConstructionPriority.Normal,
                ConstructionPriority.Normal => ConstructionPriority.High,
                ConstructionPriority.High => ConstructionPriority.Critical,
                _ => ConstructionPriority.Critical
            };
        }

        private static ConstructionPriority LowerPriority(ConstructionPriority priority)
        {
            return priority switch
            {
                ConstructionPriority.Critical => ConstructionPriority.High,
                ConstructionPriority.High => ConstructionPriority.Normal,
                ConstructionPriority.Normal => ConstructionPriority.Low,
                _ => ConstructionPriority.Low
            };
        }

        private void ReclaimConstructionMaterials(ConstructionState construction)
        {
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.reservedByConstructionId != construction.constructionFeatureId)
                {
                    continue;
                }

                stack.reservedByConstructionId = ConstructionFeatureId.None;
                stack.storagePolicy = StoragePolicy.None;
                stack.bulkClass = ResolveStoredBulkClass(stack.amount, stack.storagePolicy);
                _snapshot.itemStacks[i] = stack;
            }

            for (int i = 0; i < construction.deliveredMaterials.Count; i++)
            {
                MaterialDeliveryState delivery = construction.deliveredMaterials[i];
                int reservedAmount = 0;
                for (int j = 0; j < _snapshot.itemStacks.Count; j++)
                {
                    ItemStackEntity stack = _snapshot.itemStacks[j];
                    if (stack.resourceType == delivery.resourceType &&
                        stack.occupiedCells != null &&
                        stack.occupiedCells.Count > 0 &&
                        stack.occupiedCells[0] == construction.stagingCell &&
                        stack.reservedByConstructionId == ConstructionFeatureId.None)
                    {
                        reservedAmount += stack.amount;
                    }
                }

                int missingRecoveredAmount = Mathf.Max(0, delivery.deliveredAmount - reservedAmount);
                if (missingRecoveredAmount > 0)
                {
                    AddOrMergeItemStack(
                        construction.ownerFactionId,
                        delivery.resourceType,
                        missingRecoveredAmount,
                        construction.stagingCell,
                        StoragePolicy.None,
                        ConstructionFeatureId.None,
                        EntityUid.None);
                }
            }
        }

        private void ClearConstructionAssignments(ConstructionFeatureId constructionFeatureId)
        {
            if (_snapshot.units == null)
            {
                return;
            }

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                if (unit.assignedConstructionId != constructionFeatureId)
                {
                    continue;
                }

                unit.assignedConstructionId = ConstructionFeatureId.None;
                if (unit.currentJobType != JobType.HaulToStorage)
                {
                    unit.currentJobType = unit.carryLoad.amount > 0 ? JobType.HaulToStorage : JobType.Idle;
                }

                _snapshot.units[i] = unit;
            }
        }

        private LandmarkState FindFirstLandmark(ColonyId colonyId, LandmarkKind landmarkKind)
        {
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<LandmarkState> landmarks = _snapshot.chunks[i].landmarks;
                for (int j = 0; j < landmarks.Count; j++)
                {
                    if (landmarks[j].ownerColonyId == colonyId && landmarks[j].landmarkKind == landmarkKind)
                    {
                        return landmarks[j];
                    }
                }
            }

            return null;
        }

        private int CountUnitsForColony(ColonyId colonyId)
        {
            int count = 0;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                if (_snapshot.units[i].ownerColonyId == colonyId)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountUnitsForColony(ColonyId colonyId, UnitRole unitRole)
        {
            int count = 0;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                if (_snapshot.units[i].ownerColonyId == colonyId &&
                    _snapshot.units[i].unitRole == unitRole)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountRecruitableUnits(ColonyId colonyId)
        {
            int count = 0;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                if (unit.ownerColonyId != colonyId ||
                    unit.lifecycleState != UnitLifecycleState.Active ||
                    unit.unitRole == UnitRole.Soldier)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool HasActiveBarracks(ColonySnapshot colony)
        {
            return TryFindActiveBarracksSpace(colony, out _);
        }

        private bool TryFindActiveBarracksSpace(ColonySnapshot colony, out SpaceRegionState space)
        {
            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState candidate = _snapshot.cachedSpaces[i];
                if (candidate.purposeKind == FloorPurposeKind.Barracks &&
                    candidate.activationState == SpaceActivationState.ShelteredActive &&
                    DoesSpaceBelongToColony(colony, candidate))
                {
                    space = candidate;
                    return true;
                }
            }

            space = null;
            return false;
        }

        private void RefreshSoldierTrainingState(ColonySnapshot colony)
        {
            colony.soldierTraining ??= new SoldierTrainingState();
            if (colony.soldierTraining.queuedCount > 0)
            {
                if (CanAdvanceSoldierTraining(colony, out string blockedReason))
                {
                    colony.soldierTraining.status = SoldierTrainingStatus.Training;
                    colony.soldierTraining.blockedReason = string.Empty;
                }
                else
                {
                    colony.soldierTraining.status = SoldierTrainingStatus.Blocked;
                    colony.soldierTraining.blockedReason = blockedReason;
                }

                return;
            }

            colony.soldierTraining.progress = 0f;
            colony.soldierTraining.assignedRecruitUid = EntityUid.None;
            if (CanQueueSoldierTrainingForColony(colony, 0, out string readyReason))
            {
                colony.soldierTraining.status = SoldierTrainingStatus.Ready;
                colony.soldierTraining.blockedReason = string.Empty;
            }
            else
            {
                colony.soldierTraining.status = SoldierTrainingStatus.Blocked;
                colony.soldierTraining.blockedReason = readyReason;
            }
        }

        private void RefreshThreatAlert(ColonySnapshot colony)
        {
            colony.threatAlert ??= new ThreatAlertState();
            colony.raidEvent ??= new RaidEventState();
            int soldiers = CountUnitsForColony(colony.colonyId, UnitRole.Soldier);
            int threatScore =
                Mathf.FloorToInt(_snapshot.simulationTick / 24f) +
                colony.population +
                colony.activeShelteredSpaces +
                Mathf.Max(0, _snapshot.constructionFeatures.Count / 3);

            if (colony.raidEvent.isActive)
            {
                threatScore += Mathf.Max(12, colony.raidEvent.strength);
            }

            ThreatLevel level = threatScore switch
            {
                < 10 => ThreatLevel.None,
                < 16 => ThreatLevel.Low,
                < 24 => ThreatLevel.Medium,
                < 32 => ThreatLevel.High,
                _ => ThreatLevel.Critical
            };

            colony.threatAlert.threatScore = threatScore;
            colony.threatAlert.threatLevel = level;
            if (colony.raidEvent.isActive)
            {
                colony.threatAlert.summary = soldiers <= 0
                    ? $"Raid in progress. No trained soldiers."
                    : $"Raid in progress. {soldiers} soldier{(soldiers == 1 ? string.Empty : "s")} defending.";
                return;
            }

            colony.threatAlert.summary = level switch
            {
                ThreatLevel.None => "Frontier quiet.",
                _ when soldiers <= 0 => $"{level} threat. No trained soldiers.",
                _ => $"{level} threat. {soldiers} soldier{(soldiers == 1 ? string.Empty : "s")} ready."
            };
        }

        private bool SimulateRaidPressure()
        {
            bool changed = false;
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                ColonySnapshot colony = _snapshot.colonies[i];
                colony.raidEvent ??= new RaidEventState();
                if (colony.raidEvent.nextRaidTick <= 0)
                {
                    colony.raidEvent.nextRaidTick = _snapshot.simulationTick + RaidIntervalTicks;
                    changed = true;
                }

                if (!colony.raidEvent.isActive)
                {
                    if (_snapshot.simulationTick >= colony.raidEvent.nextRaidTick &&
                        colony.threatAlert != null &&
                        colony.threatAlert.threatLevel >= ThreatLevel.Low)
                    {
                        colony.raidEvent.isActive = true;
                        colony.raidEvent.countdownTicks = RaidCountdownTicks;
                        colony.raidEvent.strength = Mathf.Max(1, colony.threatAlert.threatScore / 8);
                        colony.raidEvent.summary = $"Raiders sighted. Impact in {RaidCountdownTicks} ticks.";
                        RecordEvent(colony.raidEvent.summary, GameEventSeverity.Critical, GameEventCategory.Raid);
                        changed = true;
                    }

                    continue;
                }

                colony.raidEvent.countdownTicks = Mathf.Max(0, colony.raidEvent.countdownTicks - 1);
                if (colony.raidEvent.countdownTicks > 0)
                {
                    colony.raidEvent.summary = $"Raiders attacking in {colony.raidEvent.countdownTicks} ticks.";
                    changed = true;
                    continue;
                }

                colony.raidEvent.isActive = false;
                colony.raidEvent.lastResolvedTick = _snapshot.simulationTick;
                colony.raidEvent.nextRaidTick = _snapshot.simulationTick + RaidIntervalTicks;
                colony.raidEvent.summary = ResolveRaid(colony);
                colony.raidEvent.strength = 0;
                RecordEvent($"Raid resolved: {colony.raidEvent.summary}", GameEventSeverity.Warning, GameEventCategory.Raid);
                changed = true;
            }

            return changed;
        }

        private string ResolveRaid(ColonySnapshot colony)
        {
            int soldiers = CountUnitsForColony(colony.colonyId, UnitRole.Soldier);
            if (soldiers > 0)
            {
                return soldiers == 1
                    ? "Raid repelled by 1 soldier."
                    : $"Raid repelled by {soldiers} soldiers.";
            }

            if (TryConsumeFactionResource(colony.ownerFactionId, ResourceType.Wood, RaidLootAmount))
            {
                return $"Raid struck and looted {RaidLootAmount} Wood.";
            }

            if (TryConsumeFactionResource(colony.ownerFactionId, ResourceType.Food, RaidLootAmount))
            {
                return $"Raid struck and looted {RaidLootAmount} Food.";
            }

            if (TryConsumeFactionResource(colony.ownerFactionId, ResourceType.Stone, RaidLootAmount))
            {
                return $"Raid struck and looted {RaidLootAmount} Stone.";
            }

            return "Raid struck but found no supplies.";
        }

        private void RefreshRaidEventState(ColonySnapshot colony)
        {
            colony.raidEvent ??= new RaidEventState();
            if (colony.raidEvent.nextRaidTick <= 0)
            {
                colony.raidEvent.nextRaidTick = _snapshot.simulationTick + RaidIntervalTicks;
            }

            if (colony.raidEvent.isActive)
            {
                colony.raidEvent.summary = colony.raidEvent.countdownTicks > 0
                    ? $"Raiders attacking in {colony.raidEvent.countdownTicks} ticks."
                    : "Raiders at the gates.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(colony.raidEvent.summary) &&
                colony.raidEvent.lastResolvedTick > 0 &&
                _snapshot.simulationTick - colony.raidEvent.lastResolvedTick <= 12)
            {
                return;
            }

            long ticksUntilRaid = Math.Max(0, colony.raidEvent.nextRaidTick - _snapshot.simulationTick);
            colony.raidEvent.summary = ticksUntilRaid > 0
                ? $"Next raid pressure in {ticksUntilRaid} ticks."
                : "Raid pressure building.";
        }

        private bool CanAdvanceSoldierTraining(ColonySnapshot colony, out string blockedReason)
        {
            if (!CanQueueSoldierTrainingForColony(colony, Mathf.Max(0, colony.soldierTraining.queuedCount - 1), false, out blockedReason))
            {
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private bool CanQueueSoldierTrainingForColony(ColonySnapshot colony, int existingQueuedCount, out string blockedReason)
        {
            return CanQueueSoldierTrainingForColony(colony, existingQueuedCount, true, out blockedReason);
        }

        private bool CanQueueSoldierTrainingForColony(ColonySnapshot colony, int existingQueuedCount, bool requireWeapons, out string blockedReason)
        {
            if (colony == null)
            {
                blockedReason = "No colony loaded.";
                return false;
            }

            if (!HasActiveBarracks(colony))
            {
                blockedReason = "Need active barracks.";
                return false;
            }

            int availableRecruits = Mathf.Max(0, CountRecruitableUnits(colony.colonyId) - Mathf.Max(0, existingQueuedCount));
            if (availableRecruits <= 0)
            {
                blockedReason = "No recruit available.";
                return false;
            }

            int availableWeapons = CountAvailableFactionResource(colony.ownerFactionId, ResourceType.Weapons);
            if (requireWeapons && availableWeapons <= 0)
            {
                blockedReason = "Need Weapons.";
                return false;
            }

            blockedReason = string.Empty;
            return true;
        }

        private bool TryCompleteSoldierTraining(ColonySnapshot colony, out string failureReason)
        {
            if (!TrySpawnFallbackSoldier(colony))
            {
                failureReason = "No rally point for trained soldier.";
                return false;
            }

            colony.soldierTraining.assignedRecruitUid = EntityUid.None;
            failureReason = string.Empty;
            return true;
        }

        private bool TrySpawnFallbackSoldier(ColonySnapshot colony)
        {
            if (!TryFindSoldierRallyCell(colony.colonyId, out HexCoord rallyCell))
            {
                rallyCell = colony.center;
            }

            if (!CanSpawnOnCell(rallyCell))
            {
                if (!TryFindSpawnCell(colony, out rallyCell))
                {
                    return false;
                }
            }

            _snapshot.units.Add(new UnitSnapshot
            {
                entityUid = EntityUid.NewId(),
                ownerColonyId = colony.colonyId,
                ownerFactionId = colony.ownerFactionId,
                unitRole = UnitRole.Soldier,
                lifecycleState = UnitLifecycleState.Active,
                movementClassMask = MovementClassMask.GroundSmall,
                occupancyClassMask = OccupancyClassMask.Soldier,
                hexCoord = rallyCell,
                targetHex = rallyCell,
                currentJobType = JobType.Garrison,
                carryLoad = new CarryLoad(),
                health = 100
            });

            EnsureStandingSquad(colony).assignedUnits = CountUnitsForColony(colony.colonyId, UnitRole.Soldier);
            return true;
        }

        private UnitSnapshot FindRecruitCandidate(ColonyId colonyId)
        {
            UnitSnapshot noCarryFallback = null;
            UnitSnapshot anyFallback = null;
            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot unit = _snapshot.units[i];
                if (unit.ownerColonyId != colonyId ||
                    unit.lifecycleState != UnitLifecycleState.Active ||
                    unit.unitRole == UnitRole.Soldier)
                {
                    continue;
                }

                anyFallback ??= unit;

                int carryAmount = unit.carryLoad != null ? unit.carryLoad.amount : 0;
                if (carryAmount > 0)
                {
                    continue;
                }

                if (unit.currentJobType == JobType.Idle)
                {
                    return unit;
                }

                noCarryFallback ??= unit;
            }

            return noCarryFallback ?? anyFallback;
        }

        private UnitSnapshot GetAssignedRecruit(ColonySnapshot colony)
        {
            colony.soldierTraining ??= new SoldierTrainingState();
            if (colony.soldierTraining.assignedRecruitUid != EntityUid.None)
            {
                UnitSnapshot assigned = FindUnitById(colony.soldierTraining.assignedRecruitUid);
                if (assigned != null &&
                    assigned.ownerColonyId == colony.colonyId &&
                    assigned.lifecycleState == UnitLifecycleState.Active &&
                    assigned.unitRole != UnitRole.Soldier)
                {
                    return assigned;
                }
            }

            UnitSnapshot fallback = FindRecruitCandidate(colony.colonyId);
            if (fallback != null)
            {
                colony.soldierTraining.assignedRecruitUid = fallback.entityUid;
            }

            return fallback;
        }

        private UnitSnapshot FindUnitById(EntityUid entityUid)
        {
            if (entityUid == EntityUid.None)
            {
                return null;
            }

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                if (_snapshot.units[i].entityUid == entityUid)
                {
                    return _snapshot.units[i];
                }
            }

            return null;
        }

        private bool TryFindSoldierRallyCell(ColonyId colonyId, out HexCoord rallyCell)
        {
            ColonySnapshot colony = FindColony(colonyId);
            if (colony != null && TryFindActiveBarracksSpace(colony, out SpaceRegionState barracksSpace) && barracksSpace.cells.Count > 0)
            {
                rallyCell = barracksSpace.cells[0];
                return true;
            }

            LandmarkState townCenter = FindFirstLandmark(colonyId, LandmarkKind.TownCenterCore);
            if (townCenter != null)
            {
                rallyCell = townCenter.originCell;
                return true;
            }

            rallyCell = colony != null ? colony.center : HexCoord.Zero;
            return colony != null;
        }

        private bool TryFindSoldierHoldCell(ColonySnapshot colony, out HexCoord holdCell)
        {
            if (colony != null)
            {
                for (int i = 0; i < _snapshot.units.Count; i++)
                {
                    UnitSnapshot unit = _snapshot.units[i];
                    if (unit.ownerColonyId == colony.colonyId && unit.unitRole == UnitRole.Soldier)
                    {
                        holdCell = unit.hexCoord;
                        return true;
                    }
                }

                SquadOrder currentOrder = EnsureStandingSquad(colony).currentOrder;
                if (currentOrder != null)
                {
                    holdCell = currentOrder.targetHex;
                    return true;
                }

                holdCell = colony.center;
                return true;
            }

            holdCell = HexCoord.Zero;
            return false;
        }

        private bool TryFindPerimeterTargetCell(UnitSnapshot soldier, ColonySnapshot colony, out HexCoord targetCell)
        {
            targetCell = default;
            if (soldier == null || colony == null)
            {
                return false;
            }

            var anchors = new List<HexCoord>(6);
            for (int direction = 0; direction < 6; direction++)
            {
                if (TryFindPerimeterAnchorCell(colony, direction, out HexCoord anchor) && !anchors.Contains(anchor))
                {
                    anchors.Add(anchor);
                }
            }

            if (anchors.Count == 0)
            {
                return false;
            }

            int soldierIndex = GetSortedSoldierIndex(colony.colonyId, soldier.entityUid);
            if (soldierIndex < 0)
            {
                soldierIndex = 0;
            }

            targetCell = anchors[soldierIndex % anchors.Count];
            return true;
        }

        private int GetSortedSoldierIndex(ColonyId colonyId, EntityUid soldierId)
        {
            int index = 0;
            bool found = false;

            for (int i = 0; i < _snapshot.units.Count; i++)
            {
                UnitSnapshot candidate = _snapshot.units[i];
                if (candidate.ownerColonyId != colonyId ||
                    candidate.lifecycleState != UnitLifecycleState.Active ||
                    candidate.unitRole != UnitRole.Soldier)
                {
                    continue;
                }

                if (candidate.entityUid == soldierId)
                {
                    found = true;
                    continue;
                }

                if (candidate.entityUid.Value < soldierId.Value)
                {
                    index++;
                }
            }

            return found ? index : -1;
        }

        private bool TryFindPerimeterAnchorCell(ColonySnapshot colony, int direction, out HexCoord anchorCell)
        {
            if (colony == null)
            {
                anchorCell = HexCoord.Zero;
                return false;
            }

            int clampedDirection = ((direction % 6) + 6) % 6;
            int maxRadius = Mathf.Clamp(SoldierPerimeterRadius, 3, ColonyOwnershipRadius);
            for (int radius = maxRadius; radius >= 3; radius--)
            {
                HexCoord candidate = StepDirection(colony.center, clampedDirection, radius);
                OccupancyEvaluation occupancy = EvaluateCell(candidate, MovementClassMask.GroundSmall);
                if (!occupancy.isHardBlocked)
                {
                    anchorCell = candidate;
                    return true;
                }
            }

            anchorCell = colony.center;
            return false;
        }

        private static HexCoord StepDirection(HexCoord origin, int direction, int steps)
        {
            HexCoord result = origin;
            int clampedDirection = ((direction % 6) + 6) % 6;
            int clampedSteps = Mathf.Max(0, steps);
            for (int i = 0; i < clampedSteps; i++)
            {
                result = result.Neighbor(clampedDirection);
            }

            return result;
        }

        private ColonySnapshot FindColony(ColonyId colonyId)
        {
            for (int i = 0; i < _snapshot.colonies.Count; i++)
            {
                if (_snapshot.colonies[i].colonyId == colonyId)
                {
                    return _snapshot.colonies[i];
                }
            }

            return null;
        }

        private SquadTemplate EnsureStandingSquad(ColonySnapshot colony)
        {
            colony.standingSquads ??= new List<SquadTemplate>();
            if (colony.standingSquads.Count == 0)
            {
                colony.standingSquads.Add(new SquadTemplate
                {
                    squadId = SquadId.NewId(),
                    displayName = "Colony Guard",
                    desiredSize = 6,
                    assignedUnits = 0,
                    currentOrder = new SquadOrder
                    {
                        orderKind = SquadOrderKind.Garrison,
                        targetHex = colony.center
                    }
                });
            }

            SquadTemplate squad = colony.standingSquads[0];
            squad.currentOrder ??= new SquadOrder
            {
                squadId = squad.squadId,
                orderKind = SquadOrderKind.Garrison,
                targetHex = colony.center
            };
            if (squad.currentOrder.squadId == SquadId.None)
            {
                squad.currentOrder.squadId = squad.squadId;
            }

            return squad;
        }

        public string DescribeStandingSquadOrder(ColonySnapshot colony)
        {
            if (colony == null)
            {
                return "No colony.";
            }

            SquadOrder order = EnsureStandingSquad(colony).currentOrder;
            if (order == null)
            {
                return "No defender order.";
            }

            return order.orderKind switch
            {
                SquadOrderKind.Hold => $"Holding {order.targetHex}.",
                SquadOrderKind.DefendArea => $"Defending {order.targetHex}.",
                SquadOrderKind.Perimeter => "Defending the colony perimeter.",
                _ => $"Rallying at {order.targetHex}."
            };
        }

        private bool HasShelteredResidentialSpace(ColonySnapshot colony)
        {
            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState space = _snapshot.cachedSpaces[i];
                if (space.purposeKind == FloorPurposeKind.Residential &&
                    space.activationState == SpaceActivationState.ShelteredActive &&
                    DoesSpaceBelongToColony(colony, space))
                {
                    return true;
                }
            }

            return false;
        }

        private string DescribeSpaceActivation(SpaceRegionState space, EnclosedRoomState room)
        {
            if (space == null)
            {
                return "No space.";
            }

            if (space.activationState == SpaceActivationState.ShelteredActive)
            {
                return "Sheltered active.";
            }

            if (space.activationState == SpaceActivationState.OpenActive)
            {
                return space.purposeKind switch
                {
                    FloorPurposeKind.Storage => room != null && room.rackCount > 0 ? "Sheltered storage not formed yet." : "Open storage active.",
                    FloorPurposeKind.Roadway => "Road active.",
                    FloorPurposeKind.Farmland => "Farmland active.",
                    _ => "Open active."
                };
            }

            if (!space.isEnclosed && RequiresShelter(space.purposeKind))
            {
                return $"Needs enclosed {space.purposeKind} room.";
            }

            return space.purposeKind switch
            {
                FloorPurposeKind.Residential => room != null && room.bedCount <= 0 ? "Needs Bed." : "Residential blocked.",
                FloorPurposeKind.Workshop => room != null && room.workbenchCount <= 0 ? "Needs Workbench." : "Workshop blocked.",
                FloorPurposeKind.Weaponry => room != null && room.weaponBenchCount <= 0 ? "Needs Weapon Bench." : "Weaponry blocked.",
                FloorPurposeKind.Barracks => room != null && CountModules(room.cells, ModuleKind.Bunk) <= 0 ? "Needs Bunk." : "Barracks blocked.",
                FloorPurposeKind.Utility => room != null && CountBuiltModules(room.cells) <= 0 ? "Needs module." : "Utility blocked.",
                _ => "Blocked."
            };
        }

        private static bool RequiresShelter(FloorPurposeKind purposeKind)
        {
            return purposeKind is FloorPurposeKind.Residential or
                FloorPurposeKind.Workshop or
                FloorPurposeKind.Weaponry or
                FloorPurposeKind.Barracks or
                FloorPurposeKind.Utility;
        }

        private bool DoesSpaceBelongToColony(ColonySnapshot colony, SpaceRegionState space)
        {
            if (colony == null || space == null || space.cells == null)
            {
                return false;
            }

            for (int i = 0; i < space.cells.Count; i++)
            {
                if (space.cells[i].DistanceTo(colony.center) <= ColonyOwnershipRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindSpawnCell(ColonySnapshot colony, out HexCoord spawnCell)
        {
            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState space = _snapshot.cachedSpaces[i];
                if (space.purposeKind != FloorPurposeKind.Residential ||
                    space.activationState != SpaceActivationState.ShelteredActive ||
                    !DoesSpaceBelongToColony(colony, space))
                {
                    continue;
                }

                for (int j = 0; j < space.cells.Count; j++)
                {
                    if (CanSpawnOnCell(space.cells[j]))
                    {
                        spawnCell = space.cells[j];
                        return true;
                    }
                }
            }

            LandmarkState townCenter = FindFirstLandmark(colony.colonyId, LandmarkKind.TownCenterCore);
            if (townCenter != null)
            {
                for (int i = 0; i < townCenter.occupiedCells.Count; i++)
                {
                    for (int direction = 0; direction < 6; direction++)
                    {
                        HexCoord candidate = townCenter.occupiedCells[i].Neighbor(direction);
                        if (CanSpawnOnCell(candidate))
                        {
                            spawnCell = candidate;
                            return true;
                        }
                    }
                }
            }

            spawnCell = colony.center;
            return CanSpawnOnCell(spawnCell);
        }

        private bool CanSpawnOnCell(HexCoord cell)
        {
            OccupancyEvaluation occupancy = EvaluateCell(cell, MovementClassMask.GroundSmall);
            return !occupancy.isHardBlocked;
        }

        private bool ProcessSpawnQueue(ColonySnapshot colony)
        {
            if (colony.spawnQueue == null || colony.spawnQueue.Count == 0)
            {
                return false;
            }

            bool spawnedAny = false;
            for (int i = colony.spawnQueue.Count - 1; i >= 0; i--)
            {
                UnitSpawnRequest request = colony.spawnQueue[i];
                if (!TryFindSpawnCell(colony, out HexCoord spawnCell))
                {
                    continue;
                }

                _snapshot.units.Add(new UnitSnapshot
                {
                    entityUid = EntityUid.NewId(),
                    ownerColonyId = colony.colonyId,
                    ownerFactionId = colony.ownerFactionId,
                    unitRole = request.unitRole,
                    lifecycleState = UnitLifecycleState.Active,
                    movementClassMask = MovementClassMask.GroundSmall,
                    occupancyClassMask = OccupancyClassMask.Worker,
                    hexCoord = spawnCell,
                    targetHex = spawnCell,
                    currentJobType = JobType.Idle,
                    carryLoad = new CarryLoad(),
                    health = 100
                });

                colony.spawnQueue.RemoveAt(i);
                colony.population = Mathf.Max(colony.population + 1, CountUnitsForColony(colony.colonyId));
                colony.populationGrowth.status = ColonyGrowthStatus.Growing;
                colony.populationGrowth.blockedReason = string.Empty;
                spawnedAny = true;
            }

            return spawnedAny;
        }

        private UnitRole DetermineSpawnRole(ColonySnapshot colony)
        {
            int currentUnits = CountUnitsForColony(colony.colonyId);
            return (currentUnits % 4) switch
            {
                0 => UnitRole.Builder,
                1 => UnitRole.Hauler,
                2 => UnitRole.Crafter,
                _ => UnitRole.Worker
            };
        }

        private void FindBestStorageTarget(FactionId ownerFactionId, ResourceType resourceType, int amount, out HexCoord targetCell, out StoragePolicy storagePolicy)
        {
            storagePolicy = StoragePolicy.None;
            targetCell = HexCoord.Zero;

            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState space = _snapshot.cachedSpaces[i];
                if (space.purposeKind == FloorPurposeKind.Storage &&
                    TryFindStorageCell(space.cells, resourceType, amount, StoragePolicy.Containerized, out targetCell))
                {
                    storagePolicy = StoragePolicy.Containerized;
                    return;
                }
            }

            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState space = _snapshot.cachedSpaces[i];
                if (space.purposeKind == FloorPurposeKind.Storage &&
                    TryFindStorageCell(space.cells, resourceType, amount, StoragePolicy.ShelteredFloor, out targetCell))
                {
                    storagePolicy = StoragePolicy.ShelteredFloor;
                    return;
                }
            }

            for (int i = 0; i < _snapshot.cachedSpaces.Count; i++)
            {
                SpaceRegionState space = _snapshot.cachedSpaces[i];
                if (space.purposeKind == FloorPurposeKind.Storage &&
                    TryFindStorageCell(space.cells, resourceType, amount, StoragePolicy.OutdoorFloor, out targetCell))
                {
                    storagePolicy = StoragePolicy.OutdoorFloor;
                    return;
                }
            }

            ColonySnapshot colony = GetPrimaryColony();
            targetCell = colony != null ? colony.center : HexCoord.Zero;
        }

        private bool TryFindStorageCell(List<HexCoord> candidateCells, ResourceType resourceType, int amount, StoragePolicy desiredPolicy, out HexCoord targetCell)
        {
            targetCell = HexCoord.Zero;
            for (int i = 0; i < candidateCells.Count; i++)
            {
                HexCoord cell = candidateCells[i];
                if (!CanAcceptStorageAtCell(cell, desiredPolicy, resourceType, amount))
                {
                    continue;
                }

                targetCell = cell;
                return true;
            }

            return false;
        }

        private bool CanAcceptStorageAtCell(HexCoord cell, StoragePolicy desiredPolicy, ResourceType resourceType, int amount)
        {
            SpaceRegionState space = GetSpaceAtCell(cell);
            if (space == null || space.purposeKind != FloorPurposeKind.Storage)
            {
                return false;
            }

            HexCellState state = GetCellState(cell);
            ModuleKind moduleKind = state.module != null && state.module.isBuilt ? state.module.moduleKind : ModuleKind.None;
            bool hasContainerModule = moduleKind == ModuleKind.Rack || moduleKind == ModuleKind.Bin || moduleKind == ModuleKind.Pallet;

            if (desiredPolicy == StoragePolicy.Containerized && !hasContainerModule)
            {
                return false;
            }

            if (desiredPolicy == StoragePolicy.ShelteredFloor && (space.activationState != SpaceActivationState.ShelteredActive || hasContainerModule))
            {
                return false;
            }

            if (desiredPolicy == StoragePolicy.OutdoorFloor && (space.activationState != SpaceActivationState.OpenActive || hasContainerModule))
            {
                return false;
            }

            if (GetLandmarksAtCell(cell).Count > 0)
            {
                return false;
            }

            int projectedAmount = amount;
            for (int i = 0; i < _snapshot.itemStacks.Count; i++)
            {
                ItemStackEntity stack = _snapshot.itemStacks[i];
                if (stack.reservedByConstructionId != ConstructionFeatureId.None ||
                    stack.storagePolicy != desiredPolicy ||
                    stack.resourceType != resourceType ||
                    stack.occupiedCells.Count != 1 ||
                    stack.occupiedCells[0] != cell)
                {
                    continue;
                }

                projectedAmount += stack.amount;
            }

            return IsStoragePolicyAllowedForBulk(desiredPolicy, ResolveBulkClass(projectedAmount));
        }

        private static bool IsStoragePolicyAllowedForBulk(StoragePolicy storagePolicy, BulkClass bulkClass)
        {
            return storagePolicy switch
            {
                StoragePolicy.OutdoorFloor => bulkClass == BulkClass.LooseSmall,
                StoragePolicy.ShelteredFloor => bulkClass == BulkClass.LooseSmall || bulkClass == BulkClass.PileMedium,
                StoragePolicy.Containerized => true,
                _ => true
            };
        }

        private ChunkSnapshot EnsureChunkForCell(HexCoord cell)
        {
            ChunkCoord coord = ChunkCoord.FromHex(cell, _profile.chunkSize);
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                if (_snapshot.chunks[i].chunkCoord == coord)
                {
                    return _snapshot.chunks[i];
                }
            }

            var chunk = new ChunkSnapshot
            {
                chunkCoord = coord,
                dominantBiome = BiomeKind.Plains,
                threatRating = 1,
                resourceRichness = 50,
                isModified = true,
                modifiedCells = new List<HexCellState>(),
                modifiedEdges = new List<HexEdgeState>(),
                resourceNodes = new List<ResourceNodeState>(),
                landmarks = new List<LandmarkState>()
            };
            _snapshot.chunks.Add(chunk);
            return chunk;
        }

        private HexCellState FindModifiedCell(HexCoord cell)
        {
            ChunkCoord coord = ChunkCoord.FromHex(cell, _profile.chunkSize);
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                if (_snapshot.chunks[i].chunkCoord != coord)
                {
                    continue;
                }

                List<HexCellState> cells = _snapshot.chunks[i].modifiedCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    if (cells[j].coord == cell)
                    {
                        return cells[j];
                    }
                }
            }

            return null;
        }

        private HexCellState GetOrCreateCell(HexCoord cell)
        {
            HexCellState state = FindModifiedCell(cell);
            if (state != null)
            {
                return state;
            }

            ChunkSnapshot chunk = EnsureChunkForCell(cell);
            state = new HexCellState
            {
                coord = cell,
                floor = new FloorState(),
                module = new ModuleState()
            };
            chunk.modifiedCells.Add(state);
            return state;
        }

        private HexEdgeState FindEdge(HexEdgeCoord edge)
        {
            ChunkCoord coord = ChunkCoord.FromHex(edge.A, _profile.chunkSize);
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                if (_snapshot.chunks[i].chunkCoord != coord)
                {
                    continue;
                }

                List<HexEdgeState> edges = _snapshot.chunks[i].modifiedEdges;
                for (int j = 0; j < edges.Count; j++)
                {
                    if (edges[j].edgeCoord == edge)
                    {
                        return edges[j];
                    }
                }
            }

            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                List<HexEdgeState> edges = _snapshot.chunks[i].modifiedEdges;
                for (int j = 0; j < edges.Count; j++)
                {
                    if (edges[j].edgeCoord == edge)
                    {
                        return edges[j];
                    }
                }
            }

            return null;
        }

        private HexEdgeState GetOrCreateEdge(HexEdgeCoord edge)
        {
            HexEdgeState state = FindEdge(edge);
            if (state != null)
            {
                return state;
            }

            ChunkSnapshot chunk = EnsureChunkForCell(edge.A);
            state = new HexEdgeState
            {
                edgeCoord = edge,
                featureKind = EdgeFeatureKind.None,
                isBuilt = false,
                isPassable = true
            };
            chunk.modifiedEdges.Add(state);
            return state;
        }

        private BiomeKind ResolveBiome(HexCoord cell)
        {
            ChunkCoord chunkCoord = ChunkCoord.FromHex(cell, _profile.chunkSize);
            for (int i = 0; i < _snapshot.chunks.Count; i++)
            {
                if (_snapshot.chunks[i].chunkCoord == chunkCoord)
                {
                    return _snapshot.chunks[i].dominantBiome;
                }
            }

            return BiomeKind.Plains;
        }

        private float ResolveBiomeMoveCost(HexCoord cell)
        {
            return ResolveBiome(cell) switch
            {
                BiomeKind.Forest => 1.15f,
                BiomeKind.Hills => 1.2f,
                BiomeKind.Wetlands => 1.35f,
                BiomeKind.Desert => 1.1f,
                BiomeKind.Tundra => 1.05f,
                _ => 1f
            };
        }

        private static FloorState CloneFloor(FloorState source)
        {
            return source == null
                ? new FloorState()
                : new FloorState
                {
                    isBuilt = source.isBuilt,
                    purposeKind = source.purposeKind,
                    materialKind = source.materialKind,
                    surfaceModifiers = source.surfaceModifiers
                };
        }

        private static ModuleState CloneModule(ModuleState source)
        {
            return source == null
                ? new ModuleState()
                : new ModuleState
                {
                    moduleUid = source.moduleUid,
                    moduleKind = source.moduleKind,
                    isBuilt = source.isBuilt,
                    hitPoints = source.hitPoints,
                    operationProgress = source.operationProgress
                };
        }

        private static HexCellState CloneCell(HexCellState source)
        {
            return new HexCellState
            {
                coord = source.coord,
                floor = CloneFloor(source.floor),
                module = CloneModule(source.module),
                hasReservation = source.hasReservation,
                capacityProfile = source.capacityProfile
            };
        }

        private static HexEdgeState CloneEdge(HexEdgeState source)
        {
            return new HexEdgeState
            {
                edgeCoord = source.edgeCoord,
                featureKind = source.featureKind,
                isBuilt = source.isBuilt,
                isPassable = source.isPassable,
                hitPoints = source.hitPoints,
                ownerFactionId = source.ownerFactionId
            };
        }

        private static SurfaceModifierMask ResolveSurfaceModifiers(FloorPurposeKind purposeKind)
        {
            return purposeKind switch
            {
                FloorPurposeKind.Roadway => SurfaceModifierMask.Road,
                FloorPurposeKind.Farmland => SurfaceModifierMask.Farmland,
                FloorPurposeKind.Residential or FloorPurposeKind.Workshop or FloorPurposeKind.Weaponry or FloorPurposeKind.Barracks or FloorPurposeKind.Utility
                    => SurfaceModifierMask.Indoor,
                _ => SurfaceModifierMask.None
            };
        }

        private static List<HexCoord> ResolveFootprint(HexCoord originCell, LandmarkFootprint footprint)
        {
            var result = new List<HexCoord>();
            for (int i = 0; i < footprint.occupiedOffsets.Count; i++)
            {
                result.Add(originCell + footprint.occupiedOffsets[i]);
            }

            return result;
        }

        private static string DescribeConstruction(ConstructionState construction)
        {
            string target = construction.blueprint.featureKind == ConstructionFeatureKind.Edge
                ? construction.targetEdge.ToString()
                : construction.targetCell.ToString();
            return $"{construction.blueprint.featureKind} target={target} progress={construction.buildProgress:0.0}/{construction.blueprint.requiredWork:0.0}";
        }

        private void NotifyWorldChanged(bool persist = true)
        {
            WorldChanged?.Invoke(_snapshot);
            if (persist)
            {
                SaveAsync().Forget();
            }
        }
    }
}

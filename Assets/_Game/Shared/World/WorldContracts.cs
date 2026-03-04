using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Units;
using UnityEngine;

namespace Ggj2026.Game.Shared.World
{
    public enum BiomeKind
    {
        Plains,
        Forest,
        Hills,
        Desert,
        Tundra,
        Wetlands
    }

    public enum ResourceNodeKind
    {
        Tree,
        StoneOutcrop,
        OreSeam,
        Salvage,
        FertileSoil
    }

    [Serializable]
    public struct WorldSeed : IEquatable<WorldSeed>
    {
        public int Value;

        public WorldSeed(int value) => Value = value;
        public bool Equals(WorldSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldSeed other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    [Serializable]
    public class BiomeDefinition
    {
        public BiomeKind biomeKind;
        public float fertility = 1f;
        public float danger = 1f;
        public float movementCost = 1f;
        public float resourceDensity = 1f;
    }

    [Serializable]
    public class ResourceNodeDefinition
    {
        public ResourceType resourceType;
        public int minYield = 10;
        public int maxYield = 100;
        public int baseSpawnWeight = 10;
    }

    [Serializable]
    public class WorldGenerationProfile
    {
        public string profileId = "default";
        public int chunkSize = HexMetrics.DefaultChunkSize;
        public int homeWorldSize = HexMetrics.DefaultWorldSize;
        public float hexRadius = HexMetrics.DefaultHexRadius;
        public int aiFactionCount = 1;
        public int neutralThreatScale = 1;
        public List<BiomeDefinition> biomes = new List<BiomeDefinition>();
        public List<ResourceNodeDefinition> resourceNodes = new List<ResourceNodeDefinition>();

        public int GetChunkDimension()
        {
            return Mathf.CeilToInt(homeWorldSize / (float)Mathf.Max(1, chunkSize));
        }
    }

    [Serializable]
    public class HexCellState
    {
        public HexCoord coord;
        public FloorState floor = new FloorState();
        public ModuleState module = new ModuleState();
        public bool hasReservation;
        public TileCapacityProfile capacityProfile;
    }

    [Serializable]
    public class HexEdgeState
    {
        public HexEdgeCoord edgeCoord;
        public EdgeFeatureKind featureKind;
        public bool isBuilt;
        public bool isPassable = true;
        public int hitPoints = 100;
        public FactionId ownerFactionId = FactionId.None;
    }

    [Serializable]
    public class ResourceNodeState
    {
        public ResourceNodeId resourceNodeId = ResourceNodeId.None;
        public ResourceNodeKind resourceNodeKind;
        public ResourceType resourceType;
        public HexCoord cell;
        public int remainingYield;
        public bool isDepleted;
    }

    [Serializable]
    public class ChunkSnapshot
    {
        public ChunkCoord chunkCoord;
        public BiomeKind dominantBiome;
        public int threatRating;
        public int resourceRichness;
        public bool isModified;
        public List<HexCellState> modifiedCells = new List<HexCellState>();
        public List<HexEdgeState> modifiedEdges = new List<HexEdgeState>();
        public List<ResourceNodeState> resourceNodes = new List<ResourceNodeState>();
        public List<LandmarkState> landmarks = new List<LandmarkState>();
    }

    [Serializable]
    public class FactionSnapshot
    {
        public FactionId factionId = FactionId.None;
        public string displayName = "Faction";
        public bool isPlayerControlled;
        public int technologyTier;
        public List<ResourceStack> derivedResourceSummary = new List<ResourceStack>();
    }

    [Serializable]
    public class ColonySnapshot
    {
        public ColonyId colonyId = ColonyId.None;
        public FactionId ownerFactionId = FactionId.None;
        public string displayName = "Colony";
        public HexCoord center;
        public int population;
        public int housingCapacity;
        public int workforceCap;
        public int activeOpenSpaces;
        public int activeShelteredSpaces;
        public PopulationGrowthState populationGrowth = new PopulationGrowthState();
        public SoldierTrainingState soldierTraining = new SoldierTrainingState();
        public ThreatAlertState threatAlert = new ThreatAlertState();
        public RaidEventState raidEvent = new RaidEventState();
        public List<UnitSpawnRequest> spawnQueue = new List<UnitSpawnRequest>();
        public List<SquadTemplate> standingSquads = new List<SquadTemplate>();
    }

    public enum ColonyGrowthStatus
    {
        Blocked,
        Growing,
        SpawnQueued
    }

    public enum SoldierTrainingStatus
    {
        Blocked,
        Ready,
        Training
    }

    public enum UnitLifecycleState
    {
        Active,
        PendingSpawn
    }

    [Serializable]
    public class PopulationGrowthState
    {
        public ColonyGrowthStatus status = ColonyGrowthStatus.Blocked;
        public float progress;
        public float requiredProgress = 18f;
        public string blockedReason = "Need more beds.";
    }

    [Serializable]
    public class SoldierTrainingState
    {
        public SoldierTrainingStatus status = SoldierTrainingStatus.Blocked;
        public int queuedCount;
        public EntityUid assignedRecruitUid = EntityUid.None;
        public float progress;
        public float requiredProgress = 12f;
        public string blockedReason = "Need active barracks.";
    }

    [Serializable]
    public class ThreatAlertState
    {
        public ThreatLevel threatLevel = ThreatLevel.None;
        public int threatScore;
        public string summary = "Frontier quiet.";
    }

    [Serializable]
    public class RaidEventState
    {
        public bool isActive;
        public int countdownTicks;
        public int strength;
        public long nextRaidTick;
        public long lastResolvedTick;
        public string summary = "No active raid.";
    }

    [Serializable]
    public class UnitSpawnRequest
    {
        public EntityUid requestId = EntityUid.None;
        public ColonyId colonyId = ColonyId.None;
        public FactionId ownerFactionId = FactionId.None;
        public UnitRole unitRole = UnitRole.Worker;
        public HexCoord spawnCell;
        public long requestedAtTick;
    }

    [Serializable]
    public class UnitSnapshot
    {
        public EntityUid entityUid = EntityUid.None;
        public ColonyId ownerColonyId = ColonyId.None;
        public FactionId ownerFactionId = FactionId.None;
        public UnitRole unitRole = UnitRole.Worker;
        public UnitLifecycleState lifecycleState = UnitLifecycleState.Active;
        public MovementClassMask movementClassMask = MovementClassMask.GroundSmall;
        public OccupancyClassMask occupancyClassMask = OccupancyClassMask.Worker;
        public HexCoord hexCoord;
        public HexCoord targetHex;
        public JobType currentJobType;
        public ConstructionFeatureId assignedConstructionId = ConstructionFeatureId.None;
        public EntityUid assignedModuleUid = EntityUid.None;
        public ResourceNodeId assignedResourceNodeId = ResourceNodeId.None;
        public CarryLoad carryLoad = new CarryLoad();
        public int health = 100;
    }

    [Serializable]
    public class WorldSnapshot
    {
        public int snapshotVersion = 10;
        public WorldId worldId = WorldId.None;
        public WorldSeed seed;
        public string profileId = "default";
        public long simulationTick;
        public List<ChunkSnapshot> chunks = new List<ChunkSnapshot>();
        public List<FactionSnapshot> factions = new List<FactionSnapshot>();
        public List<ColonySnapshot> colonies = new List<ColonySnapshot>();
        public List<UnitSnapshot> units = new List<UnitSnapshot>();
        public List<ItemStackEntity> itemStacks = new List<ItemStackEntity>();
        public List<ConstructionState> constructionFeatures = new List<ConstructionState>();
        public List<SpaceRegionState> cachedSpaces = new List<SpaceRegionState>();
        public List<EnclosedRoomState> cachedRooms = new List<EnclosedRoomState>();
    }

    [Serializable]
    public class WorldDelta
    {
        public WorldId worldId = WorldId.None;
        public long fromTick;
        public long toTick;
        public List<ChunkSnapshot> changedChunks = new List<ChunkSnapshot>();
        public List<FactionSnapshot> changedFactions = new List<FactionSnapshot>();
        public List<ColonySnapshot> changedColonies = new List<ColonySnapshot>();
        public List<UnitSnapshot> changedUnits = new List<UnitSnapshot>();
        public List<ItemStackEntity> changedStacks = new List<ItemStackEntity>();
    }
}

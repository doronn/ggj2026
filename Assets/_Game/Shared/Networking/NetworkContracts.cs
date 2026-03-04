using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;

namespace Ggj2026.Game.Shared.Networking
{
    [Serializable]
    public class PlayerCommandEnvelope
    {
        public WorldId worldId = WorldId.None;
        public FactionId factionId = FactionId.None;
        public long clientTick;
        public string commandType = string.Empty;
        public string jsonPayload = string.Empty;
    }

    [Serializable]
    public class BuildPlacementCommand
    {
        public ColonyId colonyId = ColonyId.None;
        public ConstructionFeatureKind featureKind;
        public FloorPurposeKind floorPurposeKind;
        public EdgeFeatureKind edgeFeatureKind;
        public ModuleKind moduleKind;
        public LandmarkKind landmarkKind;
        public HexCoord targetCell;
        public HexEdgeCoord targetEdge;
    }

    [Serializable]
    public class SquadOrderCommand
    {
        public SquadOrder squadOrder = new SquadOrder();
    }

    [Serializable]
    public class PolicyUpdateCommand
    {
        public string zoneId = "default-zone";
        public ZonePolicy zonePolicy = new ZonePolicy();
    }

    [Serializable]
    public class SessionJoinRequest
    {
        public string playerName = "Player";
        public WorldId preferredWorldId = WorldId.None;
        public bool offlineMode = true;
    }

    [Serializable]
    public class CollisionMatchRequest
    {
        public WorldDescriptor attackerWorld = new WorldDescriptor();
        public WorldDescriptor defenderWorld = new WorldDescriptor();
        public ThreatLevel desiredThreatLevel = ThreatLevel.Medium;
    }

    [Serializable]
    public class CollisionInstanceConfig
    {
        public string sessionId = Guid.NewGuid().ToString("N");
        public int durationMinutes = 30;
        public int aiThreatScale = 1;
        public ImportedFrontierSlice attackerSlice = new ImportedFrontierSlice();
        public ImportedFrontierSlice defenderSlice = new ImportedFrontierSlice();
    }

    [Serializable]
    public class ImportedFrontierSlice
    {
        public WorldId sourceWorldId = WorldId.None;
        public FactionId sourceFactionId = FactionId.None;
        public List<ChunkSnapshot> importedChunks = new List<ChunkSnapshot>();
        public List<SquadTemplate> importedSquads = new List<SquadTemplate>();
        public List<ResourceStack> importedStockpile = new List<ResourceStack>();
    }

    [Serializable]
    public class CollisionResolution
    {
        public string sessionId = string.Empty;
        public WorldDelta attackerDelta = new WorldDelta();
        public WorldDelta defenderDelta = new WorldDelta();
        public bool attackerWon;
    }
}

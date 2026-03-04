using System;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;

namespace Ggj2026.Game.Shared.Logistics
{
    [Flags]
    public enum MovementClassMask
    {
        None = 0,
        GroundSmall = 1 << 0,
        GroundLarge = 1 << 1,
        Machine = 1 << 2,
        Air = 1 << 3,
        AllGround = GroundSmall | GroundLarge | Machine,
        All = GroundSmall | GroundLarge | Machine | Air
    }

    [Flags]
    public enum OccupancyClassMask
    {
        None = 0,
        Worker = 1 << 0,
        Soldier = 1 << 1,
        Machine = 1 << 2,
        Structure = 1 << 3,
        Item = 1 << 4,
        Landmark = 1 << 5
    }

    [Flags]
    public enum SurfaceModifierMask
    {
        None = 0,
        Road = 1 << 0,
        Farmland = 1 << 1,
        Indoor = 1 << 2,
        Muddy = 1 << 3,
        Rubble = 1 << 4
    }

    public enum BulkClass
    {
        LooseSmall,
        PileMedium,
        PileLarge,
        Containerized
    }

    public enum StoragePolicy
    {
        None,
        OutdoorFloor,
        LooseOutdoor = OutdoorFloor,
        ShelteredFloor,
        Containerized,
        RackOnly = Containerized,
        BinOnly = Containerized,
        LandmarkStorage
    }

    [Serializable]
    public struct TileCapacityProfile
    {
        public int groundSmallCapacity;
        public int groundLargeCapacity;
        public int machineCapacity;
        public int airCapacity;
        public int congestionThreshold;
        public float congestionMovePenalty;
        public float roadMoveBonus;
    }

    [Serializable]
    public struct OccupancyEvaluation
    {
        public HexCoord cell;
        public MovementClassMask movementMask;
        public bool isHardBlocked;
        public int usedGroundSmallCapacity;
        public int availableGroundSmallCapacity;
        public float moveCost;
        public string reason;
    }

    [Serializable]
    public class InfluenceState
    {
        public FactionId ownerFactionId = FactionId.None;
        public HexCoord center;
        public int radius;
    }

    [Serializable]
    public class SupplyNetworkState
    {
        public ColonyId colonyId = ColonyId.None;
        public int suppliedHexCount;
        public int disconnectedHexCount;
        public int effectiveRadius;
    }

    [Serializable]
    public struct RoadEdge
    {
        public HexCoord from;
        public HexCoord to;
        public byte quality;
    }
}

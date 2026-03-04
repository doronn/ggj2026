using System;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;

namespace Ggj2026.Game.Shared.Units
{
    public enum UnitRole
    {
        Civilian,
        Worker,
        Builder,
        Hauler,
        Crafter,
        Scout,
        Soldier
    }

    public enum JobType
    {
        Idle,
        ClearTile,
        HarvestSource,
        HaulToStaging,
        BuildFloor,
        BuildEdge,
        InstallModule,
        PlaceLandmark,
        HaulToStorage,
        HaulToConsumer,
        OperateModule,
        Maintain,
        Garrison
    }

    [Serializable]
    public struct NeedState
    {
        public float hunger;
        public float fatigue;
        public float morale;
        public float safety;
    }

    [Serializable]
    public class CarryLoad
    {
        public ResourceType resourceType;
        public int amount;
        public BulkClass bulkClass = BulkClass.LooseSmall;
    }

    [Serializable]
    public class UnitDefinition
    {
        public string unitId = "worker";
        public UnitRole unitRole = UnitRole.Worker;
        public MovementClassMask movementClassMask = MovementClassMask.GroundSmall;
        public OccupancyClassMask occupancyClassMask = OccupancyClassMask.Worker;
        public float moveSpeed = 4f;
        public int maxHealth = 100;
        public int carryCapacity = 8;
    }

    [Serializable]
    public class JobRequest
    {
        public EntityUid jobId = EntityUid.None;
        public ColonyId colonyId = ColonyId.None;
        public ConstructionFeatureId constructionFeatureId = ConstructionFeatureId.None;
        public ResourceNodeId resourceNodeId = ResourceNodeId.None;
        public JobType jobType;
        public UnitRole preferredRole = UnitRole.Worker;
        public ResourceType resourceType;
        public int requestedAmount;
        public HexCoord targetCell;
        public HexEdgeCoord targetEdge;
        public int priority = 50;
    }

    [Serializable]
    public class JobReservation
    {
        public EntityUid reservationId = EntityUid.None;
        public EntityUid jobId = EntityUid.None;
        public EntityUid workerEntityUid = EntityUid.None;
        public ConstructionFeatureId constructionFeatureId = ConstructionFeatureId.None;
        public JobType jobType;
        public HexCoord targetCell;
        public long expiresAtTick;
    }

    [Serializable]
    public class ZonePolicy
    {
        public string zoneId = "default-zone";
        public bool allowHarvest = true;
        public bool allowBuild = true;
        public bool allowPatrol = true;
        public int desiredWorkerCount = 6;
    }
}

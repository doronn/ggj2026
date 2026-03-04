using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;

namespace Ggj2026.Game.Shared.Economy
{
    public enum ResourceType
    {
        Food,
        Wood,
        Stone,
        Ore,
        Metal,
        Fuel,
        Weapons,
        Supply
    }

    public enum FloorPurposeKind
    {
        None,
        Residential,
        Storage,
        Workshop,
        Weaponry,
        Barracks,
        Roadway,
        Farmland,
        Utility
    }

    public enum FloorMaterialKind
    {
        None,
        PackedEarth,
        Timber,
        Stone
    }

    public enum ModuleKind
    {
        None,
        Bed,
        Rack,
        Bin,
        Pallet,
        Workbench,
        WeaponBench,
        Bunk,
        CommandPost,
        DrillPost
    }

    public enum EdgeFeatureKind
    {
        None,
        Wall,
        Door,
        Gate
    }

    public enum LandmarkKind
    {
        None,
        TownCenterCore,
        Tower,
        Gatehouse
    }

    public enum ConstructionFeatureKind
    {
        None,
        Floor,
        Edge,
        Module,
        Landmark
    }

    public enum ConstructionPriority
    {
        Normal,
        High,
        Critical,
        Low
    }

    public enum SpaceActivationState
    {
        None,
        Planned,
        OpenActive,
        ShelteredActive,
        Blocked
    }

    [Serializable]
    public struct ResourceStack
    {
        public ResourceType resourceType;
        public int amount;
    }

    [Serializable]
    public class FloorState
    {
        public bool isBuilt;
        public FloorPurposeKind purposeKind;
        public FloorMaterialKind materialKind;
        public SurfaceModifierMask surfaceModifiers;
    }

    [Serializable]
    public class ModuleSocketDefinition
    {
        public string socketId = "input";
        public ResourceType acceptedResourceType;
        public int maxAmount;
    }

    [Serializable]
    public class ProductionRecipeDefinition
    {
        public string recipeId = "default-recipe";
        public List<ResourceStack> inputCosts = new List<ResourceStack>();
        public ResourceStack output;
        public float requiredWork = 8f;
    }

    [Serializable]
    public class ModuleDefinition
    {
        public ModuleKind moduleKind;
        public string displayName = "Module";
        public FloorPurposeKind requiredPurposeKind;
        public bool requiresShelter = true;
        public int housingProvided;
        public int storageCapacity;
        public List<ResourceStack> buildCost = new List<ResourceStack>();
        public List<ModuleSocketDefinition> inputSockets = new List<ModuleSocketDefinition>();
        public ProductionRecipeDefinition productionRecipe;
    }

    [Serializable]
    public class ModuleState
    {
        public EntityUid moduleUid = EntityUid.None;
        public ModuleKind moduleKind;
        public bool isBuilt;
        public int hitPoints = 100;
        public float operationProgress;
    }

    [Serializable]
    public class ItemStackEntity
    {
        public ItemStackId itemStackId = ItemStackId.None;
        public FactionId ownerFactionId = FactionId.None;
        public ResourceType resourceType;
        public int amount;
        public BulkClass bulkClass = BulkClass.LooseSmall;
        public MovementClassMask allowedMovementMask = MovementClassMask.GroundSmall | MovementClassMask.Air;
        public string mergeKey = string.Empty;
        public StoragePolicy storagePolicy;
        public ConstructionFeatureId reservedByConstructionId = ConstructionFeatureId.None;
        public EntityUid reservedByModuleUid = EntityUid.None;
        public List<HexCoord> occupiedCells = new List<HexCoord>();
    }

    [Serializable]
    public class LandmarkPlacementRule
    {
        public bool requiresClearCells = true;
        public bool mayConnectToWalls;
        public bool blocksOpenPassage = true;
    }

    [Serializable]
    public class LandmarkFootprint
    {
        public List<HexCoord> occupiedOffsets = new List<HexCoord>();
    }

    [Serializable]
    public class LandmarkDefinition
    {
        public LandmarkKind landmarkKind;
        public string displayName = "Landmark";
        public int maxHitPoints = 400;
        public int commandRadius;
        public int initialStorageBonus;
        public List<ResourceStack> buildCost = new List<ResourceStack>();
        public LandmarkFootprint footprint = new LandmarkFootprint();
        public LandmarkPlacementRule placementRule = new LandmarkPlacementRule();
    }

    [Serializable]
    public class LandmarkState
    {
        public LandmarkId landmarkId = LandmarkId.None;
        public FactionId ownerFactionId = FactionId.None;
        public ColonyId ownerColonyId = ColonyId.None;
        public LandmarkKind landmarkKind;
        public HexCoord originCell;
        public int hitPoints;
        public bool isBuilt;
        public List<HexCoord> occupiedCells = new List<HexCoord>();
    }

    [Serializable]
    public class ConstructionBlueprint
    {
        public ConstructionFeatureKind featureKind;
        public FloorPurposeKind floorPurposeKind;
        public FloorMaterialKind floorMaterialKind;
        public EdgeFeatureKind edgeFeatureKind;
        public ModuleKind moduleKind;
        public LandmarkKind landmarkKind;
        public List<ResourceStack> buildCost = new List<ResourceStack>();
        public float requiredWork = 10f;
    }

    [Serializable]
    public class MaterialDeliveryState
    {
        public ResourceType resourceType;
        public int requiredAmount;
        public int deliveredAmount;
    }

    [Serializable]
    public class StagingReservation
    {
        public ConstructionFeatureId constructionFeatureId = ConstructionFeatureId.None;
        public HexCoord stagingCell;
        public ResourceType resourceType;
        public int reservedAmount;
    }

    [Serializable]
    public class ConstructionState
    {
        public ConstructionFeatureId constructionFeatureId = ConstructionFeatureId.None;
        public FactionId ownerFactionId = FactionId.None;
        public ColonyId ownerColonyId = ColonyId.None;
        public ConstructionPriority priority = ConstructionPriority.Normal;
        public ConstructionBlueprint blueprint = new ConstructionBlueprint();
        public HexCoord targetCell;
        public HexEdgeCoord targetEdge;
        public HexCoord stagingCell;
        public float buildProgress;
        public bool isCompleted;
        public List<HexCoord> affectedCells = new List<HexCoord>();
        public List<MaterialDeliveryState> deliveredMaterials = new List<MaterialDeliveryState>();
        public List<StagingReservation> reservations = new List<StagingReservation>();
    }

    [Serializable]
    public class SpaceRegionState
    {
        public SpaceRegionId regionId = SpaceRegionId.None;
        public FloorPurposeKind purposeKind;
        public bool isEnclosed;
        public SpaceActivationState activationState;
        public int installedModuleCount;
        public int housingCapacityProvided;
        public List<HexCoord> cells = new List<HexCoord>();
    }

    [Serializable]
    public class EnclosedRoomState
    {
        public SpaceRegionId regionId = SpaceRegionId.None;
        public FloorPurposeKind purposeKind;
        public bool isFunctional;
        public int bedCount;
        public int rackCount;
        public int workbenchCount;
        public int weaponBenchCount;
        public List<HexCoord> cells = new List<HexCoord>();
    }
}

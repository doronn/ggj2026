using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class LocalColonyGameplayServiceTests
    {
        [Test]
        public void Initialize_ProducesFunctionalStarterResidentialSpace()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            ColonySnapshot colony = service.GetPrimaryColony();
            SpaceRegionState space = service.GetSpaceAtCell(colony.center.Neighbor(2));
            EnclosedRoomState room = service.GetRoomAtCell(colony.center.Neighbor(2));

            Assert.That(colony, Is.Not.Null);
            Assert.That(space, Is.Not.Null);
            Assert.That(space.purposeKind, Is.EqualTo(FloorPurposeKind.Residential));
            Assert.That(space.activationState, Is.EqualTo(SpaceActivationState.ShelteredActive));
            Assert.That(room, Is.Not.Null);
            Assert.That(room.bedCount, Is.GreaterThan(0));
            Assert.That(colony.housingCapacity, Is.GreaterThan(0));
        }

        [Test]
        public void Initialize_ProducesShelteredStarterStorageSpace()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            ColonySnapshot colony = service.GetPrimaryColony();
            SpaceRegionState space = service.GetSpaceAtCell(colony.center);
            EnclosedRoomState room = service.GetRoomAtCell(colony.center);

            Assert.That(space, Is.Not.Null);
            Assert.That(space.purposeKind, Is.EqualTo(FloorPurposeKind.Storage));
            Assert.That(space.activationState, Is.EqualTo(SpaceActivationState.ShelteredActive));
            Assert.That(room, Is.Not.Null);
            Assert.That(room.rackCount, Is.GreaterThan(0));
        }

        [Test]
        public void QueueOutdoorStorageFloor_RemainsOpenActiveWithoutEnclosure()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord storageCell = new HexCoord(3, -1);
            bool queued = service.TryPaintFloor(FloorPurposeKind.Storage, storageCell, out string failureReason);

            Assert.That(queued, Is.True, failureReason);

            service.AdvanceSimulation(20);

            SpaceRegionState space = service.GetSpaceAtCell(storageCell);
            Assert.That(space, Is.Not.Null);
            Assert.That(space.purposeKind, Is.EqualTo(FloorPurposeKind.Storage));
            Assert.That(space.activationState, Is.EqualTo(SpaceActivationState.OpenActive));
        }

        [Test]
        public void QueueRoadFloor_AdvancesToBuiltFloor()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord buildCell = new HexCoord(2, 1);
            bool queued = service.TryPaintFloor(FloorPurposeKind.Roadway, buildCell, out string failureReason);

            Assert.That(queued, Is.True, failureReason);

            service.AdvanceSimulation(20);

            HexCellState state = service.GetCellState(buildCell);
            Assert.That(state.floor.isBuilt, Is.True);
            Assert.That(state.floor.purposeKind, Is.EqualTo(FloorPurposeKind.Roadway));
        }

        [Test]
        public void QueueTower_BuildsLandmarkFootprint()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord towerCell = new HexCoord(-2, 0);
            bool queued = service.TryPlaceLandmark(LandmarkKind.Tower, towerCell, out string failureReason);

            Assert.That(queued, Is.True, failureReason);

            service.AdvanceSimulation(50);

            Assert.That(service.GetLandmarksAtCell(towerCell).Exists(landmark => landmark.landmarkKind == LandmarkKind.Tower), Is.True);
        }

        [Test]
        public void QueueWallEdge_AllowsStarterWorkshopEnclosure()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord workshopCell = service.GetPrimaryColony().center.Neighbor(4);
            HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(workshopCell, 0);

            Assert.That(service.CanQueueEdge(EdgeFeatureKind.Wall, edge, out string failureReason), Is.True, failureReason);
        }

        [Test]
        public void PlaceModule_FailsOnWrongFloorPurpose()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord openStorageCell = new HexCoord(4, -1);
            Assert.That(service.TryPaintFloor(FloorPurposeKind.Storage, openStorageCell, out string paintFailure), Is.True, paintFailure);
            service.AdvanceSimulation(20);

            bool queued = service.TryPlaceModule(ModuleKind.Bed, openStorageCell, out string failureReason);

            Assert.That(queued, Is.False);
            Assert.That(failureReason, Does.Contain("Residential"));
        }

        [Test]
        public void PlaceModule_FailsWhenShelterIsMissing()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord openResidentialCell = new HexCoord(3, 0);
            Assert.That(service.TryPaintFloor(FloorPurposeKind.Residential, openResidentialCell, out string paintFailure), Is.True, paintFailure);
            service.AdvanceSimulation(20);

            bool queued = service.TryPlaceModule(ModuleKind.Bed, openResidentialCell, out string failureReason);

            Assert.That(queued, Is.False);
            Assert.That(failureReason, Does.Contain("sheltered"));
        }

        [Test]
        public void PlaceStorageModule_SucceedsOnMatchingOpenStorageFloor()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord openStorageCell = new HexCoord(3, -1);
            Assert.That(service.TryPaintFloor(FloorPurposeKind.Storage, openStorageCell, out string paintFailure), Is.True, paintFailure);
            service.AdvanceSimulation(20);

            bool queued = service.TryPlaceModule(ModuleKind.Rack, openStorageCell, out string failureReason);

            Assert.That(queued, Is.True, failureReason);
        }

        [Test]
        public void PlaceGatehouse_FailsWithoutSupportingWallConnections()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord gateOrigin = new HexCoord(-2, 2);
            bool queued = service.TryPlaceLandmark(LandmarkKind.Gatehouse, gateOrigin, out string failureReason);

            Assert.That(queued, Is.False);
            Assert.That(failureReason, Does.Contain("wall"));
        }

        [Test]
        public void PlaceGatehouse_SucceedsWithSupportingWallConnections()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            HexCoord gateOrigin = new HexCoord(7, -1);
            HexCoord secondCell = gateOrigin.Neighbor(0);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(gateOrigin, 2), EdgeFeatureKind.Wall, false);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(secondCell, 5), EdgeFeatureKind.Wall, false);

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            bool queued = service.TryPlaceLandmark(LandmarkKind.Gatehouse, gateOrigin, out string failureReason);

            Assert.That(queued, Is.True, failureReason);
        }

        [Test]
        public void StarterResidentialSharedEdge_IsPassableDoor()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord doorCell = service.GetPrimaryColony().center.Neighbor(3);
            HexEdgeCoord doorEdge = HexEdgeCoord.FromCellAndDirection(doorCell, 0);
            HexEdgeState edgeState = service.GetEdgeState(doorEdge);

            Assert.That(edgeState, Is.Not.Null);
            Assert.That(edgeState.featureKind, Is.EqualTo(EdgeFeatureKind.Door));
            Assert.That(edgeState.isPassable, Is.True);
        }

        [Test]
        public void QueueWallEdge_FailsWhenItWouldSealAccessibleRoom()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            HexCoord roomCell = new HexCoord(3, 1);
            AddBuiltFloor(snapshot, profile, roomCell, FloorPurposeKind.Residential);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(roomCell, 1), EdgeFeatureKind.Wall, false);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(roomCell, 2), EdgeFeatureKind.Wall, false);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(roomCell, 3), EdgeFeatureKind.Wall, false);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(roomCell, 4), EdgeFeatureKind.Wall, false);
            AddBuiltEdge(snapshot, profile, HexEdgeCoord.FromCellAndDirection(roomCell, 5), EdgeFeatureKind.Wall, false);

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            HexEdgeCoord doorEdge = HexEdgeCoord.FromCellAndDirection(roomCell, 0);

            bool queued = service.TryQueueEdge(EdgeFeatureKind.Wall, doorEdge, out string failureReason);

            Assert.That(queued, Is.False);
            Assert.That(failureReason, Does.Contain("seal a work area"));
        }

        [Test]
        public void DescribeSpaceActivation_BlockWorkshopExplainsMissingEnclosure()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            HexCoord workshopCell = new HexCoord(4, 1);
            Assert.That(service.TryPaintFloor(FloorPurposeKind.Workshop, workshopCell, out string paintFailure), Is.True, paintFailure);
            service.AdvanceSimulation(20);

            string detail = service.DescribeSpaceActivation(workshopCell);

            Assert.That(detail, Does.Contain("Needs enclosed Workshop room"));
        }

        [Test]
        public void DescribeModuleStatus_WorkbenchExplainsMissingInputs()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            HexCoord workshopCell = new HexCoord(4, 1);

            CreateFunctionalRoom(snapshot, profile, workshopCell, FloorPurposeKind.Workshop, ModuleKind.Workbench);

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            string detail = service.DescribeModuleStatus(workshopCell);

            Assert.That(detail, Does.Contain("Waiting for"));
        }

        [Test]
        public void DerivedSummary_ComesFromPhysicalStacks()
        {
            WorldSnapshot snapshot = CreateWorld();
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            FactionSnapshot faction = service.GetPlayerFaction();

            Assert.That(faction.derivedResourceSummary.Count, Is.GreaterThan(0));
            Assert.That(faction.derivedResourceSummary.Exists(stack => stack.resourceType == ResourceType.Wood && stack.amount > 0), Is.True);
        }

        [Test]
        public void WorkshopWorkbench_ConsumesInputsAndProducesMetal()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            HexCoord workshopCell = new HexCoord(4, 1);

            CreateFunctionalRoom(snapshot, profile, workshopCell, FloorPurposeKind.Workshop, ModuleKind.Workbench);
            AddItemStack(snapshot, profile, factionId, ResourceType.Ore, 2, workshopCell.Neighbor(3));
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 1, workshopCell.Neighbor(4));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);
            service.AdvanceSimulation(80);

            Assert.That(snapshot.itemStacks.Exists(stack => stack.resourceType == ResourceType.Metal && stack.amount >= 2), Is.True);
        }

        [Test]
        public void WeaponBench_ConsumesInputsAndProducesWeapons()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            HexCoord weaponryCell = new HexCoord(5, 0);

            CreateFunctionalRoom(snapshot, profile, weaponryCell, FloorPurposeKind.Weaponry, ModuleKind.WeaponBench);
            AddItemStack(snapshot, profile, factionId, ResourceType.Metal, 2, weaponryCell.Neighbor(3));
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 1, weaponryCell.Neighbor(4));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);
            service.AdvanceSimulation(90);

            Assert.That(snapshot.itemStacks.Exists(stack => stack.resourceType == ResourceType.Weapons && stack.amount >= 1), Is.True);
        }

        [Test]
        public void WorkshopAndWeaponBench_CompleteOreToWeaponsChain()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            HexCoord workshopCell = new HexCoord(4, 1);
            HexCoord weaponryCell = new HexCoord(5, 0);

            CreateFunctionalRoom(snapshot, profile, workshopCell, FloorPurposeKind.Workshop, ModuleKind.Workbench);
            CreateFunctionalRoom(snapshot, profile, weaponryCell, FloorPurposeKind.Weaponry, ModuleKind.WeaponBench);
            AddItemStack(snapshot, profile, factionId, ResourceType.Ore, 4, workshopCell.Neighbor(3));
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 3, workshopCell.Neighbor(4));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);
            service.AdvanceSimulation(220);

            Assert.That(snapshot.itemStacks.Exists(stack => stack.resourceType == ResourceType.Weapons && stack.amount >= 1), Is.True);
            Assert.That(HasResourceSummaryAmount(service.GetDerivedResourceSummary(factionId), ResourceType.Weapons, 1), Is.True);
        }

        [Test]
        public void ProducedOutput_IsHauledIntoStructuredStorage()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            HexCoord workshopCell = new HexCoord(4, 1);

            CreateFunctionalRoom(snapshot, profile, workshopCell, FloorPurposeKind.Workshop, ModuleKind.Workbench);
            AddItemStack(snapshot, profile, factionId, ResourceType.Ore, 2, workshopCell.Neighbor(3));
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 1, workshopCell.Neighbor(4));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);
            service.AdvanceSimulation(140);

            Assert.That(snapshot.itemStacks.Exists(stack =>
                stack.resourceType == ResourceType.Metal &&
                stack.amount >= 2 &&
                stack.storagePolicy != StoragePolicy.None), Is.True);
        }

        [Test]
        public void ShelteredResidentialExpansion_GrowsColonyAndSpawnsNewWorker()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            ColonySnapshot colony = snapshot.colonies[0];
            int startingUnits = snapshot.units.Count;

            CreateFunctionalRoom(snapshot, profile, new HexCoord(4, 0), FloorPurposeKind.Residential, ModuleKind.Bed);
            CreateFunctionalRoom(snapshot, profile, new HexCoord(5, -1), FloorPurposeKind.Residential, ModuleKind.Bed);

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            Assert.That(colony.housingCapacity, Is.GreaterThan(startingUnits));

            service.AdvanceSimulation(24);

            Assert.That(snapshot.units.Count, Is.GreaterThan(startingUnits));
            Assert.That(snapshot.units.Exists(unit =>
                unit.ownerColonyId == colony.colonyId &&
                unit.lifecycleState == UnitLifecycleState.Active), Is.True);
            Assert.That(colony.populationGrowth.blockedReason, Is.Empty);
        }

        [Test]
        public void ActiveBarracksAndWeapons_AllowQueueingSoldierTraining()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonySnapshot colony = snapshot.colonies[0];
            HexCoord barracksCell = new HexCoord(6, -2);

            CreateFunctionalRoom(snapshot, profile, barracksCell, FloorPurposeKind.Barracks, ModuleKind.Bunk);
            AddItemStack(snapshot, profile, factionId, ResourceType.Weapons, 1, barracksCell.Neighbor(3));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            bool canTrain = service.CanQueueSoldierTraining(out string canTrainFailure);
            if (!canTrain)
            {
                Assert.Fail($"CanQueueSoldierTraining failed: {canTrainFailure}");
            }

            bool queued = service.TryQueueSoldierTraining(out string queueFailure);
            if (!queued)
            {
                Assert.Fail($"TryQueueSoldierTraining failed: {queueFailure}");
            }

            Assert.That(colony.soldierTraining.queuedCount, Is.EqualTo(1));
            Assert.That(colony.soldierTraining.status, Is.EqualTo(SoldierTrainingStatus.Training));
            Assert.That(colony.soldierTraining.blockedReason, Is.Empty);
        }

        [Test]
        public void AdvancingWorld_CompletesSoldierTrainingIntoDefender()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonySnapshot colony = snapshot.colonies[0];
            HexCoord barracksCell = new HexCoord(6, -2);

            CreateFunctionalRoom(snapshot, profile, barracksCell, FloorPurposeKind.Barracks, ModuleKind.Bunk);
            AddItemStack(snapshot, profile, factionId, ResourceType.Weapons, 1, barracksCell.Neighbor(3));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            Assert.That(service.TryQueueSoldierTraining(out string queueFailure), Is.True, queueFailure);
            var timeline = new System.Collections.Generic.List<string>();
            for (int tick = 0; tick < 16; tick++)
            {
                service.AdvanceSimulation(1);
                timeline.Add($"t{tick + 1}:{colony.soldierTraining.status}:{colony.soldierTraining.progress}/{colony.soldierTraining.requiredProgress}:{colony.soldierTraining.blockedReason}");
            }

            Assert.That(snapshot.units.Exists(unit =>
                unit.ownerColonyId == colony.colonyId &&
                unit.unitRole == UnitRole.Soldier &&
                unit.currentJobType == JobType.Garrison), Is.True,
                string.Join(" | ", timeline));
            Assert.That(colony.soldierTraining.queuedCount, Is.EqualTo(0));
            Assert.That(colony.soldierTraining.status, Is.Not.EqualTo(SoldierTrainingStatus.Training));
            Assert.That(snapshot.itemStacks.Exists(stack => stack.resourceType == ResourceType.Weapons), Is.False);
            Assert.That(colony.standingSquads.Count, Is.GreaterThan(0));
            Assert.That(colony.standingSquads[0].assignedUnits, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void AdvancingWorld_RaisesThreatAlertWhenNoDefendersExist()
        {
            WorldSnapshot snapshot = CreateWorld();
            ColonySnapshot colony = snapshot.colonies[0];
            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());

            service.AdvanceSimulation(120);

            Assert.That(colony.threatAlert, Is.Not.Null);
            Assert.That(colony.threatAlert.threatLevel, Is.GreaterThan(ThreatLevel.None));
            Assert.That(colony.threatAlert.summary, Does.Contain("No trained soldiers"));
        }

        [Test]
        public void AdvancingWorld_RaidLootsSuppliesWhenNoDefendersExist()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonySnapshot colony = snapshot.colonies[0];

            colony.population = 12;
            colony.raidEvent.nextRaidTick = 1;
            snapshot.itemStacks.Clear();
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 6, colony.center);

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            service.AdvanceSimulation(8);

            Assert.That(colony.raidEvent.isActive, Is.False);
            Assert.That(colony.raidEvent.summary, Does.Contain("looted"));
            Assert.That(GetResourceSummaryAmount(service.GetDerivedResourceSummary(factionId), ResourceType.Wood), Is.LessThan(6));
        }

        [Test]
        public void AdvancingWorld_RaidIsRepelledWhenSoldierExists()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonySnapshot colony = snapshot.colonies[0];

            colony.population = 12;
            colony.raidEvent.nextRaidTick = 1;
            snapshot.itemStacks.Clear();
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 6, colony.center);
            snapshot.units.Add(new UnitSnapshot
            {
                entityUid = EntityUid.NewId(),
                ownerColonyId = colony.colonyId,
                ownerFactionId = factionId,
                unitRole = UnitRole.Soldier,
                lifecycleState = UnitLifecycleState.Active,
                movementClassMask = MovementClassMask.GroundSmall,
                occupancyClassMask = OccupancyClassMask.Soldier,
                hexCoord = colony.center,
                targetHex = colony.center,
                currentJobType = JobType.Garrison,
                carryLoad = new CarryLoad(),
                health = 100
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            service.AdvanceSimulation(8);

            Assert.That(colony.raidEvent.isActive, Is.False);
            Assert.That(colony.raidEvent.summary, Does.Contain("repelled"));
            Assert.That(GetResourceSummaryAmount(service.GetDerivedResourceSummary(factionId), ResourceType.Wood), Is.EqualTo(6));
            Assert.That(colony.threatAlert.summary, Does.Contain("soldier"));
        }

        [Test]
        public void IssueDefenderOrder_UpdatesStandingSquadOrder()
        {
            WorldSnapshot snapshot = CreateWorld();
            ColonySnapshot colony = snapshot.colonies[0];
            snapshot.units.Add(new UnitSnapshot
            {
                entityUid = EntityUid.NewId(),
                ownerColonyId = colony.colonyId,
                ownerFactionId = colony.ownerFactionId,
                unitRole = UnitRole.Soldier,
                lifecycleState = UnitLifecycleState.Active,
                movementClassMask = MovementClassMask.GroundSmall,
                occupancyClassMask = OccupancyClassMask.Soldier,
                hexCoord = colony.center,
                targetHex = colony.center,
                currentJobType = JobType.Garrison,
                carryLoad = new CarryLoad(),
                health = 100
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());
            HexCoord defendCell = new HexCoord(4, -1);

            Assert.That(service.TryIssueDefenderOrder(SquadOrderKind.DefendArea, defendCell, true, out string defendFailure), Is.True, defendFailure);
            Assert.That(service.GetStandingSquadOrder().orderKind, Is.EqualTo(SquadOrderKind.DefendArea));
            Assert.That(service.GetStandingSquadOrder().targetHex, Is.EqualTo(defendCell));

            Assert.That(service.TryIssueDefenderOrder(SquadOrderKind.Hold, default, false, out string holdFailure), Is.True, holdFailure);
            Assert.That(service.GetStandingSquadOrder().orderKind, Is.EqualTo(SquadOrderKind.Hold));

            Assert.That(service.TryIssueDefenderOrder(SquadOrderKind.Garrison, default, false, out string rallyFailure), Is.True, rallyFailure);
            Assert.That(service.GetStandingSquadOrder().orderKind, Is.EqualTo(SquadOrderKind.Garrison));
        }

        [Test]
        public void AdvancingWorld_SoldierMovesToDefendAreaOrder()
        {
            WorldSnapshot snapshot = CreateWorld();
            ColonySnapshot colony = snapshot.colonies[0];
            snapshot.units.Add(new UnitSnapshot
            {
                entityUid = EntityUid.NewId(),
                ownerColonyId = colony.colonyId,
                ownerFactionId = colony.ownerFactionId,
                unitRole = UnitRole.Soldier,
                lifecycleState = UnitLifecycleState.Active,
                movementClassMask = MovementClassMask.GroundSmall,
                occupancyClassMask = OccupancyClassMask.Soldier,
                hexCoord = colony.center,
                targetHex = colony.center,
                currentJobType = JobType.Garrison,
                carryLoad = new CarryLoad(),
                health = 100
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, CreateProfile());
            HexCoord defendCell = new HexCoord(4, -1);

            Assert.That(service.TryIssueDefenderOrder(SquadOrderKind.DefendArea, defendCell, true, out string defendFailure), Is.True, defendFailure);
            service.AdvanceSimulation(12);

            UnitSnapshot soldier = snapshot.units.Find(unit => unit.unitRole == UnitRole.Soldier);
            Assert.That(soldier, Is.Not.Null);
            Assert.That(soldier.hexCoord, Is.EqualTo(defendCell));
            Assert.That(soldier.targetHex, Is.EqualTo(defendCell));
            Assert.That(soldier.currentJobType, Is.EqualTo(JobType.Garrison));
        }

        [Test]
        public void InProgressSupplyJob_RemainsAssignedUntilItMakesProgress()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonyId colonyId = snapshot.colonies[0].colonyId;
            ConstructionFeatureId harvestConstructionId = ConstructionFeatureId.NewId();
            ConstructionFeatureId stackConstructionId = ConstructionFeatureId.NewId();
            ResourceNodeId nodeId = ResourceNodeId.NewId();

            snapshot.itemStacks.Clear();
            snapshot.constructionFeatures.Clear();
            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                snapshot.chunks[i].resourceNodes.Clear();
            }

            snapshot.units = new System.Collections.Generic.List<UnitSnapshot>
            {
                new UnitSnapshot
                {
                    entityUid = EntityUid.NewId(),
                    ownerColonyId = colonyId,
                    ownerFactionId = factionId,
                    unitRole = UnitRole.Worker,
                    movementClassMask = MovementClassMask.GroundSmall,
                    occupancyClassMask = OccupancyClassMask.Worker,
                    hexCoord = new HexCoord(2, 0),
                    targetHex = new HexCoord(3, -2),
                    currentJobType = JobType.HarvestSource,
                    assignedConstructionId = harvestConstructionId,
                    assignedModuleUid = EntityUid.None,
                    assignedResourceNodeId = nodeId,
                    carryLoad = new CarryLoad()
                }
            };

            snapshot.constructionFeatures.Add(CreateFloorConstruction(harvestConstructionId, factionId, colonyId, new HexCoord(2, 1), 3));
            snapshot.constructionFeatures.Add(CreateFloorConstruction(stackConstructionId, factionId, colonyId, new HexCoord(3, -3), 3));
            AddItemStack(snapshot, profile, factionId, ResourceType.Wood, 3, HexCoord.Zero);
            EnsureChunk(snapshot, profile, new HexCoord(3, -2)).resourceNodes.Add(new ResourceNodeState
            {
                resourceNodeId = nodeId,
                resourceNodeKind = ResourceNodeKind.Tree,
                resourceType = ResourceType.Wood,
                cell = new HexCoord(3, -2),
                remainingYield = 10,
                isDepleted = false
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            service.AdvanceSimulation(1);
            UnitSnapshot unit = snapshot.units[0];
            Assert.That(unit.assignedConstructionId, Is.EqualTo(harvestConstructionId));
            Assert.That(unit.currentJobType, Is.EqualTo(JobType.HarvestSource));
            Assert.That(unit.hexCoord, Is.EqualTo(new HexCoord(3, -1)));

            service.AdvanceSimulation(1);
            unit = snapshot.units[0];
            Assert.That(unit.assignedConstructionId, Is.EqualTo(harvestConstructionId));
            Assert.That(unit.assignedConstructionId, Is.Not.EqualTo(stackConstructionId));
            Assert.That(unit.carryLoad.amount, Is.GreaterThan(0));
            Assert.That(unit.currentJobType, Is.EqualTo(JobType.HaulToStaging));
        }

        [Test]
        public void StalledLandmarkMaterials_AreReclaimedForHigherPriorityFloorConstruction()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonyId colonyId = snapshot.colonies[0].colonyId;
            ConstructionFeatureId floorConstructionId = ConstructionFeatureId.NewId();
            ConstructionFeatureId landmarkConstructionId = ConstructionFeatureId.NewId();
            HexCoord floorCell = new HexCoord(2, 1);
            HexCoord landmarkCell = new HexCoord(3, -3);

            snapshot.itemStacks.Clear();
            snapshot.constructionFeatures.Clear();
            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                snapshot.chunks[i].resourceNodes.Clear();
            }

            snapshot.units = new System.Collections.Generic.List<UnitSnapshot>
            {
                new UnitSnapshot
                {
                    entityUid = EntityUid.NewId(),
                    ownerColonyId = colonyId,
                    ownerFactionId = factionId,
                    unitRole = UnitRole.Worker,
                    movementClassMask = MovementClassMask.GroundSmall,
                    occupancyClassMask = OccupancyClassMask.Worker,
                    hexCoord = new HexCoord(2, 0),
                    targetHex = new HexCoord(2, 0),
                    currentJobType = JobType.Idle,
                    assignedConstructionId = ConstructionFeatureId.None,
                    assignedModuleUid = EntityUid.None,
                    assignedResourceNodeId = ResourceNodeId.None,
                    carryLoad = new CarryLoad()
                }
            };

            snapshot.constructionFeatures.Add(CreateFloorConstruction(floorConstructionId, factionId, colonyId, floorCell, 3));
            snapshot.constructionFeatures.Add(CreateLandmarkConstruction(landmarkConstructionId, factionId, colonyId, landmarkCell, deliveredWood: 6, deliveredStone: 0));
            snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = factionId,
                resourceType = ResourceType.Wood,
                amount = 6,
                bulkClass = BulkClass.PileMedium,
                storagePolicy = StoragePolicy.None,
                mergeKey = "Wood-landmark",
                reservedByConstructionId = landmarkConstructionId,
                occupiedCells = new System.Collections.Generic.List<HexCoord> { landmarkCell }
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);
            service.AdvanceSimulation(20);

            HexCellState state = service.GetCellState(floorCell);
            Assert.That(state.floor.isBuilt, Is.True);

            ConstructionState landmark = snapshot.constructionFeatures.Find(feature => feature.constructionFeatureId == landmarkConstructionId);
            Assert.That(landmark, Is.Not.Null);
            Assert.That(landmark.deliveredMaterials.Find(delivery => delivery.resourceType == ResourceType.Wood).deliveredAmount, Is.LessThan(6));
        }

        [Test]
        public void WorkStatusSummary_ReportsMissingWoodWhenNoWoodSourceExists()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonyId colonyId = snapshot.colonies[0].colonyId;

            snapshot.itemStacks.Clear();
            snapshot.constructionFeatures.Clear();
            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                snapshot.chunks[i].resourceNodes.RemoveAll(node => node.resourceType == ResourceType.Wood);
            }

            snapshot.constructionFeatures.Add(CreateFloorConstruction(ConstructionFeatureId.NewId(), factionId, colonyId, new HexCoord(2, 1), 3));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            string summary = service.GetWorkStatusSummary();

            Assert.That(summary, Does.Contain("Wood"));
            Assert.That(summary, Does.Contain("no source"));
        }

        [Test]
        public void CancelConstruction_ReclaimsReservedStacks()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonyId colonyId = snapshot.colonies[0].colonyId;
            ConstructionFeatureId constructionId = ConstructionFeatureId.NewId();
            HexCoord cell = new HexCoord(2, 1);

            snapshot.constructionFeatures.Clear();
            snapshot.itemStacks.Clear();
            snapshot.constructionFeatures.Add(CreateFloorConstruction(constructionId, factionId, colonyId, cell, 3));
            snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = factionId,
                resourceType = ResourceType.Wood,
                amount = 3,
                bulkClass = BulkClass.PileMedium,
                storagePolicy = StoragePolicy.None,
                mergeKey = "Wood-staged",
                reservedByConstructionId = constructionId,
                occupiedCells = new System.Collections.Generic.List<HexCoord> { cell }
            });

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            bool canceled = service.TryCancelConstruction(constructionId, out string failureReason);

            Assert.That(canceled, Is.True, failureReason);
            Assert.That(snapshot.constructionFeatures.Exists(state => state.constructionFeatureId == constructionId), Is.False);
            Assert.That(snapshot.itemStacks.Exists(stack =>
                stack.resourceType == ResourceType.Wood &&
                stack.amount == 3 &&
                stack.reservedByConstructionId == ConstructionFeatureId.None), Is.True);
        }

        [Test]
        public void IncreasedPriorityFloor_OutranksLandmarkInSupplyScoring()
        {
            WorldGenerationProfile profile = CreateProfile();
            WorldSnapshot snapshot = CreateWorld();
            FactionId factionId = snapshot.factions[0].factionId;
            ColonyId colonyId = snapshot.colonies[0].colonyId;
            ConstructionFeatureId floorConstructionId = ConstructionFeatureId.NewId();
            ConstructionFeatureId landmarkConstructionId = ConstructionFeatureId.NewId();

            snapshot.constructionFeatures.Clear();
            snapshot.constructionFeatures.Add(CreateFloorConstruction(floorConstructionId, factionId, colonyId, new HexCoord(2, 1), 3));
            snapshot.constructionFeatures.Add(CreateLandmarkConstruction(landmarkConstructionId, factionId, colonyId, new HexCoord(3, -3), 0, 0));

            var service = new LocalColonyGameplayService(null);
            service.Initialize(snapshot, profile);

            Assert.That(service.TryAdjustConstructionPriority(landmarkConstructionId, false, out string lowerFailure), Is.True, lowerFailure);
            Assert.That(service.TryAdjustConstructionPriority(floorConstructionId, true, out string raiseFailure), Is.True, raiseFailure);

            ConstructionState floor = snapshot.constructionFeatures.Find(state => state.constructionFeatureId == floorConstructionId);
            ConstructionState landmark = snapshot.constructionFeatures.Find(state => state.constructionFeatureId == landmarkConstructionId);

            Assert.That(floor.priority, Is.EqualTo(ConstructionPriority.High));
            Assert.That(landmark.priority, Is.EqualTo(ConstructionPriority.Low));
        }

        private static WorldSnapshot CreateWorld()
        {
            return new DeterministicHexWorldGenerator().Generate(CreateProfile(), new WorldSeed(4242), new WorldId(1), new FactionId(101));
        }

        private static WorldGenerationProfile CreateProfile()
        {
            return new WorldGenerationProfile
            {
                profileId = "test",
                chunkSize = 24,
                homeWorldSize = 384,
                hexRadius = 1f,
                aiFactionCount = 1,
                neutralThreatScale = 1
            };
        }

        private static void CreateFunctionalRoom(WorldSnapshot snapshot, WorldGenerationProfile profile, HexCoord cell, FloorPurposeKind purposeKind, ModuleKind moduleKind)
        {
            ChunkSnapshot chunk = EnsureChunk(snapshot, profile, cell);
            chunk.modifiedCells.Add(new HexCellState
            {
                coord = cell,
                floor = new FloorState
                {
                    isBuilt = true,
                    purposeKind = purposeKind,
                    materialKind = FloorMaterialKind.Timber
                },
                module = new ModuleState
                {
                    moduleUid = EntityUid.NewId(),
                    moduleKind = moduleKind,
                    isBuilt = true,
                    hitPoints = 100
                }
            });

            for (int direction = 0; direction < 6; direction++)
            {
                HexEdgeCoord edge = HexEdgeCoord.FromCellAndDirection(cell, direction);
                EnsureChunk(snapshot, profile, edge.A).modifiedEdges.Add(new HexEdgeState
                {
                    edgeCoord = edge,
                    featureKind = direction == 0 ? EdgeFeatureKind.Door : EdgeFeatureKind.Wall,
                    isBuilt = true,
                    isPassable = direction == 0
                });
            }
        }

        private static void AddItemStack(WorldSnapshot snapshot, WorldGenerationProfile profile, FactionId factionId, ResourceType resourceType, int amount, HexCoord cell)
        {
            snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = factionId,
                resourceType = resourceType,
                amount = amount,
                bulkClass = amount <= 4 ? BulkClass.LooseSmall : BulkClass.PileMedium,
                storagePolicy = StoragePolicy.None,
                mergeKey = $"{resourceType}-test",
                occupiedCells = new System.Collections.Generic.List<HexCoord> { cell }
            });

            EnsureChunk(snapshot, profile, cell);
        }

        private static void AddBuiltEdge(WorldSnapshot snapshot, WorldGenerationProfile profile, HexEdgeCoord edge, EdgeFeatureKind featureKind, bool isPassable)
        {
            EnsureChunk(snapshot, profile, edge.A).modifiedEdges.Add(new HexEdgeState
            {
                edgeCoord = edge,
                featureKind = featureKind,
                isBuilt = true,
                isPassable = isPassable,
                hitPoints = 100
            });
        }

        private static void AddBuiltFloor(WorldSnapshot snapshot, WorldGenerationProfile profile, HexCoord cell, FloorPurposeKind purposeKind)
        {
            EnsureChunk(snapshot, profile, cell).modifiedCells.Add(new HexCellState
            {
                coord = cell,
                floor = new FloorState
                {
                    isBuilt = true,
                    purposeKind = purposeKind,
                    materialKind = FloorMaterialKind.Timber
                }
            });
        }

        private static ChunkSnapshot EnsureChunk(WorldSnapshot snapshot, WorldGenerationProfile profile, HexCoord cell)
        {
            ChunkCoord chunkCoord = ChunkCoord.FromHex(cell, profile.chunkSize);
            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                if (snapshot.chunks[i].chunkCoord == chunkCoord)
                {
                    return snapshot.chunks[i];
                }
            }

            var chunk = new ChunkSnapshot
            {
                chunkCoord = chunkCoord,
                dominantBiome = BiomeKind.Plains,
                threatRating = 1,
                resourceRichness = 50,
                isModified = true
            };
            snapshot.chunks.Add(chunk);
            return chunk;
        }

        private static bool HasResourceSummaryAmount(System.Collections.Generic.IReadOnlyList<ResourceStack> summary, ResourceType resourceType, int minimumAmount)
        {
            if (summary == null)
            {
                return false;
            }

            for (int i = 0; i < summary.Count; i++)
            {
                if (summary[i].resourceType == resourceType && summary[i].amount >= minimumAmount)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetResourceSummaryAmount(System.Collections.Generic.IReadOnlyList<ResourceStack> summary, ResourceType resourceType)
        {
            if (summary == null)
            {
                return 0;
            }

            for (int i = 0; i < summary.Count; i++)
            {
                if (summary[i].resourceType == resourceType)
                {
                    return summary[i].amount;
                }
            }

            return 0;
        }

        private static ConstructionState CreateFloorConstruction(ConstructionFeatureId constructionId, FactionId factionId, ColonyId colonyId, HexCoord cell, int requiredWood)
        {
            return new ConstructionState
            {
                constructionFeatureId = constructionId,
                ownerFactionId = factionId,
                ownerColonyId = colonyId,
                blueprint = new ConstructionBlueprint
                {
                    featureKind = ConstructionFeatureKind.Floor,
                    floorPurposeKind = FloorPurposeKind.Storage,
                    floorMaterialKind = FloorMaterialKind.Timber,
                    buildCost = new System.Collections.Generic.List<ResourceStack>
                    {
                        new ResourceStack
                        {
                            resourceType = ResourceType.Wood,
                            amount = requiredWood
                        }
                    },
                    requiredWork = 4f
                },
                targetCell = cell,
                stagingCell = cell,
                affectedCells = new System.Collections.Generic.List<HexCoord> { cell },
                deliveredMaterials = new System.Collections.Generic.List<MaterialDeliveryState>
                {
                    new MaterialDeliveryState
                    {
                        resourceType = ResourceType.Wood,
                        requiredAmount = requiredWood,
                        deliveredAmount = 0
                    }
                }
            };
        }

        private static ConstructionState CreateLandmarkConstruction(ConstructionFeatureId constructionId, FactionId factionId, ColonyId colonyId, HexCoord cell, int deliveredWood, int deliveredStone)
        {
            return new ConstructionState
            {
                constructionFeatureId = constructionId,
                ownerFactionId = factionId,
                ownerColonyId = colonyId,
                blueprint = new ConstructionBlueprint
                {
                    featureKind = ConstructionFeatureKind.Landmark,
                    landmarkKind = LandmarkKind.Tower,
                    buildCost = new System.Collections.Generic.List<ResourceStack>
                    {
                        new ResourceStack
                        {
                            resourceType = ResourceType.Wood,
                            amount = 15
                        },
                        new ResourceStack
                        {
                            resourceType = ResourceType.Stone,
                            amount = 20
                        }
                    },
                    requiredWork = 14f
                },
                targetCell = cell,
                stagingCell = cell,
                affectedCells = new System.Collections.Generic.List<HexCoord> { cell },
                deliveredMaterials = new System.Collections.Generic.List<MaterialDeliveryState>
                {
                    new MaterialDeliveryState
                    {
                        resourceType = ResourceType.Wood,
                        requiredAmount = 15,
                        deliveredAmount = deliveredWood
                    },
                    new MaterialDeliveryState
                    {
                        resourceType = ResourceType.Stone,
                        requiredAmount = 20,
                        deliveredAmount = deliveredStone
                    }
                }
            };
        }
    }
}

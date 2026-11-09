using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.UI;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class GameQueryServiceTests
    {
        [Test]
        public void BuildHudState_ProducesOperationsContextAndWorldBar()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(1234), new WorldId(21), new FactionId(7));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryServiceTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            renderer.SetSelectedHex(gameplayService.GetPrimaryColony().center);
            HumanAgentHudState hudState = queryService.BuildHudState();

            Assert.That(hudState.worldBar.worldName, Is.Not.Empty);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.title == "Floors"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.title == "Simulation"), Is.True);
            Assert.That(hudState.contextInspector.contextKind, Is.EqualTo(ContextKind.Module));
            Assert.That(hudState.contextInspector.contextCards.Count, Is.GreaterThan(0));
            Assert.That(hudState.availableActions.Count, Is.GreaterThan(0));

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_WithOnboardingFiltersLaterOperationGroups()
        {
            PlayerPrefs.DeleteKey("Ggj2026.Onboarding.Completed");
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-onboarding-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(12345), new WorldId(201), new FactionId(71));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryOnboardingTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var onboarding = new OnboardingFlowService();
            var actionService = new GameActionService(gameplayService, renderer, realtime, onboarding);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService, onboarding);

            HumanAgentHudState hudState = queryService.BuildHudState();

            Assert.That(hudState.onboarding.isActive, Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "simulation"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "landmarks"), Is.False);

            Object.DestroyImmediate(host);
            PlayerPrefs.DeleteKey("Ggj2026.Onboarding.Completed");
        }

        [Test]
        public void BuildHudState_ContextActionsCarryTargetedEnvelopes()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-target-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(2222), new WorldId(22), new FactionId(8));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryTargetTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            HexCoord selectedCell = gameplayService.GetPrimaryColony().center.Neighbor(2);
            renderer.SetSelectedHex(selectedCell);
            HumanAgentHudState hudState = queryService.BuildHudState();
            ContextCardViewModel card = hudState.contextInspector.contextCards[0];
            GameActionDescriptor clearAction = card.actions.Find(action => action.actionId == GameActionId.ClearCell);

            Assert.That(clearAction, Is.Not.Null);
            Assert.That(clearAction.envelope.hasTargetCell, Is.True);
            Assert.That(clearAction.envelope.targetCell, Is.EqualTo(selectedCell));
            Assert.That(clearAction.blockedReason, Is.Not.EqualTo("Target cell is required."));

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_SelectedResidentialTileAddsContextualNextActions()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-context-group-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(2727), new WorldId(27), new FactionId(13));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryContextGroupTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            renderer.SetSelectedHex(gameplayService.GetPrimaryColony().center.Neighbor(3));
            HumanAgentHudState hudState = queryService.BuildHudState();
            OperationsGroupViewModel contextGroup = hudState.operationsRail.groups.Find(group => group.groupId == "context");

            Assert.That(contextGroup, Is.Not.Null);
            Assert.That(contextGroup.title, Is.EqualTo("Next"));
            Assert.That(contextGroup.actions.Exists(action => action.actionId == GameActionId.PlaceModule), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_WorldContextShowsCoreBuildGroupsAndHidesLandmarks()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-world-groups-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(2828), new WorldId(28), new FactionId(14));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryWorldGroupsTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            HumanAgentHudState hudState = queryService.BuildHudState();

            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "floors"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "edges"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "landmarks"), Is.False);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_SelectedCellWithoutLandmarkHidesLandmarkGroup()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-cell-groups-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(2929), new WorldId(29), new FactionId(15));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryCellGroupsTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            renderer.SetSelectedHex(gameplayService.GetPrimaryColony().center.Neighbor(3));
            HumanAgentHudState hudState = queryService.BuildHudState();

            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "floors"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "landmarks"), Is.False);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_SelectedEdgeShowsEdgeGroupAndHidesUnrelatedBuildGroups()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-edge-groups-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(3030), new WorldId(30), new FactionId(16));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryEdgeGroupsTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            HexCoord center = gameplayService.GetPrimaryColony().center;
            renderer.SetSelectedEdge(HexEdgeCoord.FromCellAndDirection(center, 0));
            HumanAgentHudState hudState = queryService.BuildHudState();

            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "edges"), Is.True);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "modules"), Is.False);
            Assert.That(hudState.operationsRail.groups.Exists(group => group.groupId == "landmarks"), Is.False);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildContextInspectorState_TaskCardsExposeConstructionManagementActions()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-task-test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(3333), new WorldId(23), new FactionId(9));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);
            Assert.That(gameplayService.TryPaintFloor(FloorPurposeKind.Storage, new HexCoord(3, 0), out string failureReason), Is.True, failureReason);

            var host = new GameObject("GameQueryTaskTests");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            actionService.SetInspectorTab(InspectorTabKind.Tasks);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel constructionCard = inspector.taskCards.Find(card => card.cardId.StartsWith("construction-"));

            Assert.That(constructionCard, Is.Not.Null);
            Assert.That(constructionCard.actions.Exists(action => action.actionId == GameActionId.IncreaseConstructionPriority), Is.True);
            Assert.That(constructionCard.actions.Exists(action => action.actionId == GameActionId.DecreaseConstructionPriority), Is.True);
            Assert.That(constructionCard.actions.Exists(action => action.actionId == GameActionId.CancelConstruction), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_BlockedWorkshopShowsActivationReason()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "query-workshop-blocked",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = generator.Generate(profile, new WorldSeed(4444), new WorldId(24), new FactionId(10));
            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);
            HexCoord workshopCell = new HexCoord(4, 1);
            Assert.That(gameplayService.TryPaintFloor(FloorPurposeKind.Workshop, workshopCell, out string failureReason), Is.True, failureReason);
            gameplayService.AdvanceSimulation(20);

            var host = new GameObject("GameQueryWorkshopBlocked");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            renderer.SetSelectedHex(workshopCell);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel card = inspector.contextCards[0];
            InfoRowState activationRow = card.rows.Find(row => row.label == "Activation");

            Assert.That(activationRow, Is.Not.Null);
            Assert.That(activationRow.value, Does.Contain("Needs enclosed Workshop room"));
            Assert.That(queryService.BuildBlockerSummaries().Exists(blocker =>
                blocker.title.Contains("Workshop") &&
                blocker.detail.Contains("Needs enclosed Workshop room")), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_WorkbenchShowsMissingInputStatus()
        {
            var profile = new WorldGenerationProfile
            {
                profileId = "query-module-status",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = new DeterministicHexWorldGenerator().Generate(profile, new WorldSeed(5555), new WorldId(25), new FactionId(11));
            HexCoord workshopCell = new HexCoord(4, 1);
            CreateFunctionalRoom(snapshot, profile, workshopCell, FloorPurposeKind.Workshop, ModuleKind.Workbench);

            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryModuleStatus");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            renderer.SetSelectedHex(workshopCell);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel card = inspector.contextCards[0];
            InfoRowState moduleStatusRow = card.rows.Find(row => row.label == "Module status");

            Assert.That(moduleStatusRow, Is.Not.Null);
            Assert.That(moduleStatusRow.value, Does.Contain("Waiting for"));

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildContextInspectorState_WorldCardsSummarizeProductionChain()
        {
            var profile = new WorldGenerationProfile
            {
                profileId = "query-production-world",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = new DeterministicHexWorldGenerator().Generate(profile, new WorldSeed(6666), new WorldId(26), new FactionId(12));
            CreateFunctionalRoom(snapshot, profile, new HexCoord(4, 1), FloorPurposeKind.Workshop, ModuleKind.Workbench);
            CreateFunctionalRoom(snapshot, profile, new HexCoord(5, 0), FloorPurposeKind.Weaponry, ModuleKind.WeaponBench);

            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryProductionWorld");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            actionService.SetInspectorTab(InspectorTabKind.World);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel productionCard = inspector.worldCards.Find(card => card.cardId == "world-production");

            Assert.That(productionCard, Is.Not.Null);
            Assert.That(productionCard.rows.Exists(row => row.label == "Workbench (4,1,-5)" && row.value.Contains("Waiting for")), Is.True);
            Assert.That(productionCard.rows.Exists(row => row.label == "WeaponBench (5,0,-5)" && row.value.Contains("Waiting for")), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildContextInspectorState_WorldCardsSummarizeDefenseAndTraining()
        {
            var profile = new WorldGenerationProfile
            {
                profileId = "query-defense-world",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = new DeterministicHexWorldGenerator().Generate(profile, new WorldSeed(7777), new WorldId(27), new FactionId(13));
            FactionId factionId = snapshot.factions[0].factionId;
            HexCoord barracksCell = new HexCoord(6, -2);
            CreateFunctionalRoom(snapshot, profile, barracksCell, FloorPurposeKind.Barracks, ModuleKind.Bunk);
            AddItemStack(snapshot, profile, factionId, ResourceType.Weapons, 1, barracksCell.Neighbor(3));

            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryDefenseWorld");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            actionService.SetInspectorTab(InspectorTabKind.World);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel defenseCard = inspector.worldCards.Find(card => card.cardId == "world-defense");

            Assert.That(defenseCard, Is.Not.Null);
            Assert.That(defenseCard.rows.Exists(row => row.label == "Order" && row.value.Contains("Rallying")), Is.True);
            Assert.That(defenseCard.rows.Exists(row => row.label == "Barracks" && row.value.Contains("ready")), Is.True);
            Assert.That(defenseCard.rows.Exists(row => row.label == "Training" && row.value.Contains("Ready to train")), Is.True);
            Assert.That(defenseCard.actions.Exists(action => action.actionId == GameActionId.TrainSoldier), Is.True);
            Assert.That(defenseCard.actions.Exists(action => action.actionId == GameActionId.RallyDefenders), Is.True);
            Assert.That(defenseCard.actions.Exists(action => action.actionId == GameActionId.HoldDefenders), Is.True);
            Assert.That(defenseCard.actions.Exists(action => action.actionId == GameActionId.PerimeterDefenders), Is.True);
            Assert.That(queryService.BuildAvailableActions().Exists(action => action.actionId == GameActionId.TrainSoldier), Is.True);
            Assert.That(queryService.BuildAvailableActions().Exists(action => action.actionId == GameActionId.RallyDefenders), Is.True);
            Assert.That(queryService.BuildAvailableActions().Exists(action => action.actionId == GameActionId.HoldDefenders), Is.True);
            Assert.That(queryService.BuildAvailableActions().Exists(action => action.actionId == GameActionId.PerimeterDefenders), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildBlockerSummaries_SurfaceActiveRaidPressure()
        {
            var profile = new WorldGenerationProfile
            {
                profileId = "query-raid-world",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = new DeterministicHexWorldGenerator().Generate(profile, new WorldSeed(8888), new WorldId(28), new FactionId(14));
            ColonySnapshot colony = snapshot.colonies[0];
            colony.population = 12;
            colony.raidEvent.isActive = true;
            colony.raidEvent.countdownTicks = 3;
            colony.raidEvent.summary = "Raiders attacking in 3 ticks.";

            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryRaidWorld");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);

            Assert.That(queryService.BuildBlockerSummaries().Exists(blocker =>
                blocker.title == "Raid" &&
                blocker.detail.Contains("Raiders attacking")), Is.True);

            actionService.SetInspectorTab(InspectorTabKind.World);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel defenseCard = inspector.worldCards.Find(card => card.cardId == "world-defense");

            Assert.That(defenseCard, Is.Not.Null);
            Assert.That(defenseCard.rows.Exists(row => row.label == "Raid" && row.value.Contains("Raiders attacking")), Is.True);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildHudState_SelectedCellOffersDefendAreaAction()
        {
            var profile = new WorldGenerationProfile
            {
                profileId = "query-defend-area",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldSnapshot snapshot = new DeterministicHexWorldGenerator().Generate(profile, new WorldSeed(9991), new WorldId(29), new FactionId(15));
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
                currentJobType = JobType.Garrison
            });

            var gameplayService = new LocalColonyGameplayService(null);
            gameplayService.Initialize(snapshot, profile);

            var host = new GameObject("GameQueryDefendArea");
            var renderer = host.AddComponent<LocalWorldDebugRenderer>();
            var realtime = host.AddComponent<RealTimeSimulationController>();
            realtime.Initialize(null, gameplayService);

            var actionService = new GameActionService(gameplayService, renderer, realtime);
            var queryService = new GameQueryService(gameplayService, renderer, realtime, actionService);
            HexCoord selectedCell = new HexCoord(3, 0);

            renderer.SetSelectedHex(selectedCell);
            ContextInspectorViewModel inspector = queryService.BuildContextInspectorState();
            ContextCardViewModel card = inspector.contextCards[0];
            GameActionDescriptor defendAction = card.actions.Find(action => action.actionId == GameActionId.DefendArea);

            Assert.That(defendAction, Is.Not.Null);
            Assert.That(defendAction.envelope.hasTargetCell, Is.True);
            Assert.That(defendAction.envelope.targetCell, Is.EqualTo(selectedCell));

            Object.DestroyImmediate(host);
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
                mergeKey = $"{resourceType}-query-test",
                occupiedCells = new System.Collections.Generic.List<HexCoord> { cell }
            });

            EnsureChunk(snapshot, profile, cell);
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
    }
}

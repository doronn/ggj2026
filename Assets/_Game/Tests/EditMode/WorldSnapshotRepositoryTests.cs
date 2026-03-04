using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Persistence;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class WorldSnapshotRepositoryTests
    {
        [Test]
        public void SaveAndLoad_RoundTripsLayeredSnapshot()
        {
            var repository = new JsonWorldRepository();
            WorldId worldId = new WorldId(991);

            var snapshot = new WorldSnapshot
            {
                worldId = worldId,
                seed = new WorldSeed(123),
                profileId = "test",
                simulationTick = 42
            };

            snapshot.factions.Add(new FactionSnapshot
            {
                factionId = new FactionId(5),
                displayName = "Player",
                isPlayerControlled = true
            });

            snapshot.colonies.Add(new ColonySnapshot
            {
                colonyId = new ColonyId(9),
                ownerFactionId = snapshot.factions[0].factionId,
                displayName = "Home",
                center = HexCoord.Zero
            });

            snapshot.chunks.Add(new ChunkSnapshot
            {
                chunkCoord = ChunkCoord.FromHex(HexCoord.Zero, 24),
                dominantBiome = BiomeKind.Plains,
                resourceRichness = 55,
                modifiedCells =
                {
                    new HexCellState
                    {
                        coord = HexCoord.Zero,
                        floor = new FloorState { isBuilt = true, purposeKind = FloorPurposeKind.Storage, materialKind = FloorMaterialKind.Timber }
                    }
                },
                modifiedEdges =
                {
                    new HexEdgeState
                    {
                        edgeCoord = HexEdgeCoord.FromCellAndDirection(HexCoord.Zero, 0),
                        featureKind = EdgeFeatureKind.Wall,
                        isBuilt = true,
                        isPassable = false
                    }
                },
                resourceNodes =
                {
                    new ResourceNodeState
                    {
                        resourceNodeId = ResourceNodeId.NewId(),
                        resourceNodeKind = ResourceNodeKind.Tree,
                        resourceType = ResourceType.Wood,
                        cell = HexCoord.Zero.Neighbor(1),
                        remainingYield = 20
                    }
                },
                landmarks =
                {
                    new LandmarkState
                    {
                        landmarkId = LandmarkId.NewId(),
                        ownerFactionId = snapshot.factions[0].factionId,
                        ownerColonyId = snapshot.colonies[0].colonyId,
                        landmarkKind = LandmarkKind.Tower,
                        originCell = HexCoord.Zero,
                        isBuilt = true,
                        occupiedCells = { HexCoord.Zero }
                    }
                }
            });

            snapshot.units.Add(new UnitSnapshot
            {
                entityUid = EntityUid.NewId(),
                ownerColonyId = snapshot.colonies[0].colonyId,
                ownerFactionId = snapshot.factions[0].factionId,
                unitRole = UnitRole.Worker,
                hexCoord = HexCoord.Zero
            });

            snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = snapshot.factions[0].factionId,
                resourceType = ResourceType.Wood,
                amount = 8,
                occupiedCells = { HexCoord.Zero }
            });

            snapshot.constructionFeatures.Add(new ConstructionState
            {
                constructionFeatureId = ConstructionFeatureId.NewId(),
                ownerColonyId = snapshot.colonies[0].colonyId,
                ownerFactionId = snapshot.factions[0].factionId,
                blueprint = SimulationCatalog.GetFloorBlueprint(FloorPurposeKind.Roadway),
                targetCell = HexCoord.Zero.Neighbor(2),
                stagingCell = HexCoord.Zero.Neighbor(2),
                deliveredMaterials =
                {
                    new MaterialDeliveryState { resourceType = ResourceType.Stone, requiredAmount = 2, deliveredAmount = 1 }
                }
            });

            repository.SaveAsync(snapshot).GetAwaiter().GetResult();
            WorldSnapshot loaded = repository.LoadAsync(worldId).GetAwaiter().GetResult();

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.snapshotVersion, Is.EqualTo(10));
            Assert.That(loaded.chunks.Count, Is.EqualTo(1));
            Assert.That(loaded.chunks[0].modifiedCells.Count, Is.EqualTo(1));
            Assert.That(loaded.chunks[0].modifiedEdges[0].featureKind, Is.EqualTo(EdgeFeatureKind.Wall));
            Assert.That(loaded.itemStacks.Count, Is.EqualTo(1));
            Assert.That(loaded.units.Count, Is.EqualTo(1));
            Assert.That(loaded.constructionFeatures.Count, Is.EqualTo(1));
            Assert.That(loaded.chunks[0].landmarks.Count, Is.EqualTo(1));
        }
    }
}

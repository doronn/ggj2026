using System.Collections.Generic;
using Ggj2026.Game.Shared.Combat;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;

namespace Ggj2026.Game.Shell.World
{
    public sealed class DeterministicHexWorldGenerator : IWorldGenerator
    {
        public WorldSnapshot Generate(WorldGenerationProfile profile, WorldSeed seed, WorldId worldId, FactionId playerFactionId)
        {
            var snapshot = new WorldSnapshot
            {
                worldId = worldId,
                seed = seed,
                profileId = profile.profileId,
                simulationTick = 0,
                snapshotVersion = 10
            };

            GenerateChunkMetadata(snapshot, profile, seed);
            CreateFactions(snapshot, profile, playerFactionId);
            CreateColonies(snapshot);
            SeedPlayerStart(snapshot, profile.chunkSize);
            SeedNaturalResourceNodes(snapshot, profile, seed);
            SeedUnits(snapshot);

            return snapshot;
        }

        private static void GenerateChunkMetadata(WorldSnapshot snapshot, WorldGenerationProfile profile, WorldSeed seed)
        {
            int chunkDimension = profile.GetChunkDimension();
            int halfDimension = chunkDimension / 2;
            for (int y = -halfDimension; y < chunkDimension - halfDimension; y++)
            {
                for (int x = -halfDimension; x < chunkDimension - halfDimension; x++)
                {
                    int hash = DeterministicHash(seed.Value, x, y);
                    snapshot.chunks.Add(new ChunkSnapshot
                    {
                        chunkCoord = new ChunkCoord(x, y),
                        dominantBiome = ResolveBiome(profile, hash),
                        threatRating = 1 + (hash & 3),
                        resourceRichness = 40 + (hash % 61),
                        isModified = false,
                        modifiedCells = new List<HexCellState>(),
                        modifiedEdges = new List<HexEdgeState>(),
                        resourceNodes = new List<ResourceNodeState>(),
                        landmarks = new List<LandmarkState>()
                    });
                }
            }
        }

        private static void CreateFactions(WorldSnapshot snapshot, WorldGenerationProfile profile, FactionId playerFactionId)
        {
            snapshot.factions.Add(new FactionSnapshot
            {
                factionId = playerFactionId,
                displayName = "Player Faction",
                isPlayerControlled = true,
                technologyTier = 1
            });

            for (int i = 0; i < profile.aiFactionCount; i++)
            {
                snapshot.factions.Add(new FactionSnapshot
                {
                    factionId = FactionId.NewId(),
                    displayName = $"Rival {i + 1}",
                    isPlayerControlled = false,
                    technologyTier = 1
                });
            }
        }

        private static void CreateColonies(WorldSnapshot snapshot)
        {
            snapshot.colonies.Add(new ColonySnapshot
            {
                colonyId = ColonyId.NewId(),
                ownerFactionId = snapshot.factions[0].factionId,
                displayName = "Home Colony",
                center = HexCoord.Zero,
                population = 6,
                housingCapacity = 0,
                workforceCap = 6,
                standingSquads = new List<SquadTemplate>
                {
                    new SquadTemplate { squadId = SquadId.NewId(), displayName = "Militia", desiredSize = 4, assignedUnits = 0 }
                }
            });

            if (snapshot.factions.Count > 1)
            {
                snapshot.colonies.Add(new ColonySnapshot
                {
                    colonyId = ColonyId.NewId(),
                    ownerFactionId = snapshot.factions[1].factionId,
                    displayName = "Rival Colony",
                    center = new HexCoord(18, -6),
                    population = 5,
                    workforceCap = 5
                });
            }
        }

        private static void SeedPlayerStart(WorldSnapshot snapshot, int chunkSize)
        {
            ColonySnapshot colony = snapshot.colonies[0];
            FactionId factionId = colony.ownerFactionId;
            HexCoord storageA = colony.center;
            HexCoord storageB = colony.center.Neighbor(0);
            HexCoord residentialA = colony.center.Neighbor(2);
            HexCoord residentialB = colony.center.Neighbor(3);

            StampFloor(snapshot, chunkSize, storageA, FloorPurposeKind.Storage);
            StampFloor(snapshot, chunkSize, storageB, FloorPurposeKind.Storage);
            StampFloor(snapshot, chunkSize, colony.center.Neighbor(1), FloorPurposeKind.Roadway);
            StampFloor(snapshot, chunkSize, residentialA, FloorPurposeKind.Residential);
            StampFloor(snapshot, chunkSize, residentialB, FloorPurposeKind.Residential);
            StampFloor(snapshot, chunkSize, colony.center.Neighbor(4), FloorPurposeKind.Workshop);
            StampFloor(snapshot, chunkSize, colony.center.Neighbor(5), FloorPurposeKind.Roadway);

            StampModule(snapshot, chunkSize, storageA, ModuleKind.Rack);
            StampModule(snapshot, chunkSize, storageB, ModuleKind.Bin);
            StampModule(snapshot, chunkSize, residentialA, ModuleKind.Bed);
            StampModule(snapshot, chunkSize, colony.center.Neighbor(4), ModuleKind.Workbench);

            StampLandmark(snapshot, chunkSize, colony, LandmarkKind.TownCenterCore, colony.center.Neighbor(1));
            StampEnclosure(
                snapshot,
                chunkSize,
                new List<HexCoord> { storageA, storageB },
                HexEdgeCoord.FromCellAndDirection(storageA, 5));
            StampEnclosure(
                snapshot,
                chunkSize,
                new List<HexCoord> { residentialA, residentialB },
                HexEdgeCoord.FromCellAndDirection(residentialB, 0));

            AddItemStack(snapshot, factionId, ResourceType.Wood, 18, storageA, StoragePolicy.Containerized);
            AddItemStack(snapshot, factionId, ResourceType.Stone, 14, storageB, StoragePolicy.Containerized);
        }

        private static void SeedNaturalResourceNodes(WorldSnapshot snapshot, WorldGenerationProfile profile, WorldSeed seed)
        {
            ColonySnapshot colony = snapshot.colonies[0];
            var nodeCells = new List<(HexCoord cell, ResourceNodeKind kind, ResourceType resourceType, int amount)>
            {
                (colony.center + new HexCoord(3, -2), ResourceNodeKind.Tree, ResourceType.Wood, 32),
                (colony.center + new HexCoord(4, -1), ResourceNodeKind.Tree, ResourceType.Wood, 28),
                (colony.center + new HexCoord(-3, 2), ResourceNodeKind.StoneOutcrop, ResourceType.Stone, 26),
                (colony.center + new HexCoord(-4, 1), ResourceNodeKind.OreSeam, ResourceType.Ore, 22),
                (colony.center + new HexCoord(2, 3), ResourceNodeKind.FertileSoil, ResourceType.Food, 16)
            };

            for (int i = 0; i < nodeCells.Count; i++)
            {
                ChunkSnapshot chunk = EnsureChunk(snapshot, ChunkCoord.FromHex(nodeCells[i].cell, profile.chunkSize));
                chunk.resourceNodes.Add(new ResourceNodeState
                {
                    resourceNodeId = ResourceNodeId.NewId(),
                    resourceNodeKind = nodeCells[i].kind,
                    resourceType = nodeCells[i].resourceType,
                    cell = nodeCells[i].cell,
                    remainingYield = nodeCells[i].amount + (DeterministicHash(seed.Value, nodeCells[i].cell.Q, nodeCells[i].cell.R) % 8),
                    isDepleted = false
                });
            }
        }

        private static void SeedUnits(WorldSnapshot snapshot)
        {
            ColonySnapshot colony = snapshot.colonies[0];
            for (int i = 0; i < 6; i++)
            {
                UnitRole role = i switch
                {
                    0 => UnitRole.Builder,
                    1 => UnitRole.Hauler,
                    2 => UnitRole.Worker,
                    3 => UnitRole.Worker,
                    4 => UnitRole.Crafter,
                    _ => UnitRole.Civilian
                };

                snapshot.units.Add(new UnitSnapshot
                {
                    entityUid = EntityUid.NewId(),
                    ownerColonyId = colony.colonyId,
                    ownerFactionId = colony.ownerFactionId,
                    unitRole = role,
                    movementClassMask = MovementClassMask.GroundSmall,
                    occupancyClassMask = OccupancyClassMask.Worker,
                    hexCoord = colony.center,
                    targetHex = colony.center,
                    carryLoad = new CarryLoad(),
                    health = 100
                });
            }
        }

        private static void StampFloor(WorldSnapshot snapshot, int chunkSize, HexCoord cell, FloorPurposeKind purposeKind)
        {
            ChunkSnapshot chunk = EnsureChunk(snapshot, ChunkCoord.FromHex(cell, chunkSize));
            HexCellState state = FindOrCreateCell(chunk, cell);
            state.floor = new FloorState
            {
                isBuilt = true,
                purposeKind = purposeKind,
                materialKind = purposeKind == FloorPurposeKind.Roadway ? FloorMaterialKind.Stone : FloorMaterialKind.Timber,
                surfaceModifiers = purposeKind == FloorPurposeKind.Roadway ? SurfaceModifierMask.Road : SurfaceModifierMask.None
            };
        }

        private static void StampModule(WorldSnapshot snapshot, int chunkSize, HexCoord cell, ModuleKind moduleKind)
        {
            ChunkSnapshot chunk = EnsureChunk(snapshot, ChunkCoord.FromHex(cell, chunkSize));
            HexCellState state = FindOrCreateCell(chunk, cell);
            state.module = new ModuleState
            {
                moduleUid = EntityUid.NewId(),
                moduleKind = moduleKind,
                isBuilt = true,
                hitPoints = 100
            };
        }

        private static void StampEdge(WorldSnapshot snapshot, int chunkSize, HexEdgeCoord edge, EdgeFeatureKind edgeFeatureKind)
        {
            ChunkSnapshot chunk = EnsureChunk(snapshot, ChunkCoord.FromHex(edge.A, chunkSize));
            for (int i = 0; i < chunk.modifiedEdges.Count; i++)
            {
                if (chunk.modifiedEdges[i].edgeCoord != edge)
                {
                    continue;
                }

                chunk.modifiedEdges[i].featureKind = edgeFeatureKind;
                chunk.modifiedEdges[i].isBuilt = true;
                chunk.modifiedEdges[i].isPassable = edgeFeatureKind != EdgeFeatureKind.Wall;
                chunk.modifiedEdges[i].hitPoints = 120;
                return;
            }

            chunk.modifiedEdges.Add(new HexEdgeState
            {
                edgeCoord = edge,
                featureKind = edgeFeatureKind,
                isBuilt = true,
                isPassable = edgeFeatureKind != EdgeFeatureKind.Wall,
                hitPoints = 120
            });
        }

        private static void StampLandmark(WorldSnapshot snapshot, int chunkSize, ColonySnapshot colony, LandmarkKind landmarkKind, HexCoord originCell)
        {
            LandmarkDefinition definition = SimulationCatalog.GetLandmarkDefinition(landmarkKind);
            ChunkSnapshot chunk = EnsureChunk(snapshot, ChunkCoord.FromHex(originCell, chunkSize));
            var occupiedCells = new List<HexCoord>();
            for (int i = 0; i < definition.footprint.occupiedOffsets.Count; i++)
            {
                occupiedCells.Add(originCell + definition.footprint.occupiedOffsets[i]);
            }

            chunk.landmarks.Add(new LandmarkState
            {
                landmarkId = LandmarkId.NewId(),
                ownerFactionId = colony.ownerFactionId,
                ownerColonyId = colony.colonyId,
                landmarkKind = landmarkKind,
                originCell = originCell,
                hitPoints = definition.maxHitPoints,
                isBuilt = true,
                occupiedCells = occupiedCells
            });
        }

        private static void AddItemStack(WorldSnapshot snapshot, FactionId ownerFactionId, ResourceType resourceType, int amount, HexCoord cell, StoragePolicy storagePolicy)
        {
            snapshot.itemStacks.Add(new ItemStackEntity
            {
                itemStackId = ItemStackId.NewId(),
                ownerFactionId = ownerFactionId,
                resourceType = resourceType,
                amount = amount,
                bulkClass = ResolveStoredBulkClass(amount, storagePolicy),
                allowedMovementMask = MovementClassMask.GroundSmall | MovementClassMask.Air,
                mergeKey = $"{resourceType}-{storagePolicy}",
                storagePolicy = storagePolicy,
                occupiedCells = new List<HexCoord> { cell }
            });
        }

        private static void StampEnclosure(WorldSnapshot snapshot, int chunkSize, List<HexCoord> cells, HexEdgeCoord doorEdge)
        {
            var region = new HashSet<HexCoord>(cells);
            var boundaryEdges = new HashSet<HexEdgeCoord>();
            for (int i = 0; i < cells.Count; i++)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord neighbor = cells[i].Neighbor(direction);
                    if (!region.Contains(neighbor))
                    {
                        boundaryEdges.Add(HexEdgeCoord.FromCellAndDirection(cells[i], direction));
                    }
                }
            }

            foreach (HexEdgeCoord edge in boundaryEdges)
            {
                StampEdge(snapshot, chunkSize, edge, edge == doorEdge ? EdgeFeatureKind.Door : EdgeFeatureKind.Wall);
            }
        }

        private static BulkClass ResolveStoredBulkClass(int amount, StoragePolicy storagePolicy)
        {
            return storagePolicy == StoragePolicy.Containerized
                ? BulkClass.Containerized
                : amount <= 4 ? BulkClass.LooseSmall : amount <= 12 ? BulkClass.PileMedium : BulkClass.PileLarge;
        }

        private static ChunkSnapshot EnsureChunk(WorldSnapshot snapshot, ChunkCoord chunkCoord)
        {
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
                isModified = true,
                modifiedCells = new List<HexCellState>(),
                modifiedEdges = new List<HexEdgeState>(),
                resourceNodes = new List<ResourceNodeState>(),
                landmarks = new List<LandmarkState>()
            };
            snapshot.chunks.Add(chunk);
            return chunk;
        }

        private static HexCellState FindOrCreateCell(ChunkSnapshot chunk, HexCoord cell)
        {
            for (int i = 0; i < chunk.modifiedCells.Count; i++)
            {
                if (chunk.modifiedCells[i].coord == cell)
                {
                    return chunk.modifiedCells[i];
                }
            }

            var state = new HexCellState
            {
                coord = cell,
                floor = new FloorState(),
                module = new ModuleState()
            };
            chunk.modifiedCells.Add(state);
            return state;
        }

        private static int DeterministicHash(int seed, int x, int y)
        {
            unchecked
            {
                int value = seed;
                value = (value * 397) ^ x;
                value = (value * 397) ^ y;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }

        private static BiomeKind ResolveBiome(WorldGenerationProfile profile, int hash)
        {
            if (profile.biomes == null || profile.biomes.Count == 0)
            {
                return BiomeKind.Plains;
            }

            return profile.biomes[hash % profile.biomes.Count].biomeKind;
        }
    }
}

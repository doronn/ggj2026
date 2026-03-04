using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Diagnostics;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class WorldStateDiffServiceTests
    {
        [Test]
        public void CreateDiff_DetectsChangedCellAndItemStack()
        {
            var before = new WorldSnapshot
            {
                simulationTick = 10,
                chunks =
                {
                    new ChunkSnapshot
                    {
                        chunkCoord = new ChunkCoord(0, 0),
                        modifiedCells =
                        {
                            new HexCellState
                            {
                                coord = new HexCoord(0, 0),
                                floor = new FloorState { isBuilt = false, purposeKind = FloorPurposeKind.None }
                            }
                        }
                    }
                },
                itemStacks =
                {
                    new ItemStackEntity
                    {
                        itemStackId = new ItemStackId(1),
                        resourceType = ResourceType.Wood,
                        amount = 5,
                        bulkClass = BulkClass.LooseSmall,
                        occupiedCells = { new HexCoord(0, 0) }
                    }
                }
            };

            var after = new WorldSnapshot
            {
                simulationTick = 11,
                chunks =
                {
                    new ChunkSnapshot
                    {
                        chunkCoord = new ChunkCoord(0, 0),
                        modifiedCells =
                        {
                            new HexCellState
                            {
                                coord = new HexCoord(0, 0),
                                floor = new FloorState { isBuilt = true, purposeKind = FloorPurposeKind.Storage }
                            }
                        }
                    }
                },
                itemStacks =
                {
                    new ItemStackEntity
                    {
                        itemStackId = new ItemStackId(1),
                        resourceType = ResourceType.Wood,
                        amount = 9,
                        bulkClass = BulkClass.LooseSmall,
                        occupiedCells = { new HexCoord(0, 0) }
                    }
                }
            };

            var service = new WorldStateDiffService();
            var diff = service.CreateDiff(before, after);

            Assert.That(diff.beforeDigest.simulationTick, Is.EqualTo(10));
            Assert.That(diff.afterDigest.simulationTick, Is.EqualTo(11));
            Assert.That(diff.changedCells.Count, Is.EqualTo(1));
            Assert.That(diff.changedItemStacks.Count, Is.EqualTo(1));
        }
    }
}

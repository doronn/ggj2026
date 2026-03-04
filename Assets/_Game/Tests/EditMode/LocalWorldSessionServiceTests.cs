using System.Threading;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class LocalWorldSessionServiceTests
    {
        [Test]
        public void ResetLocalWorldAsync_DeletesExistingSaveAndGeneratesFreshSnapshot()
        {
            RuntimeGameConfig config = ScriptableObject.CreateInstance<RuntimeGameConfig>();
            var repository = new FakeWorldRepository();
            var generator = new FakeWorldGenerator();
            WorldId worldId = new WorldId(config.LocalWorldId);

            repository.StoredSnapshot = new WorldSnapshot
            {
                worldId = worldId,
                snapshotVersion = 10,
                seed = new WorldSeed(1),
                factions = { new FactionSnapshot { factionId = new FactionId(9), isPlayerControlled = true } },
                colonies = { new ColonySnapshot { colonyId = new ColonyId(5) } },
                chunks = { new ChunkSnapshot() },
                units = { new UnitSnapshot() },
                itemStacks = { new ItemStackEntity() },
                constructionFeatures = { new ConstructionState() }
            };

            var service = new LocalWorldSessionService(config, generator, repository);

            WorldSnapshot reset = service.ResetLocalWorldAsync().GetAwaiter().GetResult();

            Assert.That(repository.DeleteCallCount, Is.EqualTo(1));
            Assert.That(generator.GenerateCallCount, Is.EqualTo(1));
            Assert.That(reset, Is.Not.Null);
            Assert.That(reset.worldId, Is.EqualTo(worldId));
            Assert.That(reset.seed, Is.EqualTo(new WorldSeed(config.LocalWorldSeed)));
        }

        private sealed class FakeWorldRepository : IWorldRepository
        {
            public WorldSnapshot StoredSnapshot;
            public int DeleteCallCount { get; private set; }

            public UniTask SaveAsync(WorldSnapshot snapshot, CancellationToken cancellationToken = default)
            {
                StoredSnapshot = snapshot;
                return UniTask.CompletedTask;
            }

            public UniTask<WorldSnapshot> LoadAsync(WorldId worldId, CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(StoredSnapshot);
            }

            public UniTask DeleteAsync(WorldId worldId, CancellationToken cancellationToken = default)
            {
                DeleteCallCount++;
                StoredSnapshot = null;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeWorldGenerator : IWorldGenerator
        {
            public int GenerateCallCount { get; private set; }

            public WorldSnapshot Generate(WorldGenerationProfile profile, WorldSeed seed, WorldId worldId, FactionId playerFactionId)
            {
                GenerateCallCount++;
                return new WorldSnapshot
                {
                    worldId = worldId,
                    snapshotVersion = 10,
                    seed = seed,
                    profileId = profile.profileId,
                    factions =
                    {
                        new FactionSnapshot
                        {
                            factionId = playerFactionId,
                            isPlayerControlled = true,
                            displayName = "Player"
                        }
                    },
                    colonies =
                    {
                        new ColonySnapshot
                        {
                            colonyId = ColonyId.NewId(),
                            ownerFactionId = playerFactionId,
                            displayName = "Home"
                        }
                    },
                    chunks = { new ChunkSnapshot() },
                    units = { new UnitSnapshot() },
                    itemStacks = { new ItemStackEntity() },
                    constructionFeatures = { new ConstructionState() }
                };
            }
        }
    }
}

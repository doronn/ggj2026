using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class RealTimeSimulationControllerTests
    {
        [Test]
        public void ProcessRealtimeStep_AdvancesOneTickAfterConfiguredInterval()
        {
            RuntimeGameConfig config = ScriptableObject.CreateInstance<RuntimeGameConfig>();
            var gameplay = new LocalColonyGameplayService(null);
            gameplay.Initialize(
                new WorldSnapshot
                {
                    worldId = new WorldId(10),
                    factions = { new FactionSnapshot { factionId = new FactionId(1), isPlayerControlled = true } },
                    colonies = { new ColonySnapshot { colonyId = new ColonyId(1), ownerFactionId = new FactionId(1), displayName = "Home" } },
                    chunks = { new ChunkSnapshot() }
                },
                config.CreateGenerationProfile());

            var go = new GameObject("RealTimeSimulationControllerTests");
            var controller = go.AddComponent<RealTimeSimulationController>();

            try
            {
                controller.Initialize(config, gameplay);
                controller.SetRunning(true);
                controller.ProcessRealtimeStep(config.RealTimeTickDurationSeconds);

                Assert.That(gameplay.Snapshot.simulationTick, Is.EqualTo(1));
                Assert.That(controller.TryGetInterpolationDurationForTick(1, out float duration), Is.True);
                Assert.That(duration, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }
    }
}

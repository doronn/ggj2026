using System;
using Ggj2026.Game.Authoring.Editor;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ggj2026.Game.Tests.EditMode
{
    public class RealTimeSimulationEditorDriverTests
    {
        [Test]
        public void TryComputeFallbackDelta_ReturnsFalseWhileHeartbeatIsFresh()
        {
            var go = new GameObject("RealTimeSimulationEditorDriverTests");
            var controller = go.AddComponent<RealTimeSimulationController>();
            RuntimeGameConfig config = ScriptableObject.CreateInstance<RuntimeGameConfig>();
            LocalColonyGameplayService gameplay = CreateGameplayService(config);
            long now = DateTime.UtcNow.Ticks;
            long lastEditorPump = 0L;

            try
            {
                controller.Initialize(config, gameplay);
                controller.SetRunning(true);

                typeof(RealTimeSimulationController)
                    .GetField("_lastPlayerLoopHeartbeatUtcTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(controller, now);

                bool shouldDrive = RealTimeSimulationEditorDriver.TryComputeFallbackDelta(
                    true,
                    false,
                    controller,
                    now + TimeSpan.FromMilliseconds(100).Ticks,
                    ref lastEditorPump,
                    out float deltaSeconds);

                Assert.That(shouldDrive, Is.False);
                Assert.That(deltaSeconds, Is.EqualTo(0f));
                Assert.That(lastEditorPump, Is.EqualTo(0L));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TryComputeFallbackDelta_ReturnsDeltaWhenHeartbeatIsStale()
        {
            var go = new GameObject("RealTimeSimulationEditorDriverTests");
            var controller = go.AddComponent<RealTimeSimulationController>();
            RuntimeGameConfig config = ScriptableObject.CreateInstance<RuntimeGameConfig>();
            LocalColonyGameplayService gameplay = CreateGameplayService(config);
            long now = DateTime.UtcNow.Ticks;
            long lastEditorPump = 0L;

            try
            {
                controller.Initialize(config, gameplay);
                controller.SetRunning(true);

                typeof(RealTimeSimulationController)
                    .GetField("_lastPlayerLoopHeartbeatUtcTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(controller, now - TimeSpan.FromMilliseconds(500).Ticks);

                bool first = RealTimeSimulationEditorDriver.TryComputeFallbackDelta(
                    true,
                    false,
                    controller,
                    now,
                    ref lastEditorPump,
                    out float firstDelta);

                bool second = RealTimeSimulationEditorDriver.TryComputeFallbackDelta(
                    true,
                    false,
                    controller,
                    now + TimeSpan.FromMilliseconds(100).Ticks,
                    ref lastEditorPump,
                    out float secondDelta);

                Assert.That(first, Is.False);
                Assert.That(firstDelta, Is.EqualTo(0f));
                Assert.That(second, Is.True);
                Assert.That(secondDelta, Is.GreaterThan(0.09f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        private static LocalColonyGameplayService CreateGameplayService(RuntimeGameConfig config)
        {
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
            return gameplay;
        }
    }
}

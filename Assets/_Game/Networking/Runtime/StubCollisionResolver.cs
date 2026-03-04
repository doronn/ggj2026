using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Networking;
using Ggj2026.Game.Shared.World;

namespace Ggj2026.Game.Networking.Runtime
{
    public sealed class StubCollisionResolver : ICollisionResolver
    {
        public CollisionResolution Resolve(CollisionInstanceConfig instanceConfig)
        {
            return new CollisionResolution
            {
                sessionId = instanceConfig.sessionId,
                attackerWon = instanceConfig.attackerSlice.importedSquads.Count >= instanceConfig.defenderSlice.importedSquads.Count,
                attackerDelta = new WorldDelta
                {
                    worldId = instanceConfig.attackerSlice.sourceWorldId,
                    fromTick = 0,
                    toTick = instanceConfig.durationMinutes * 60
                },
                defenderDelta = new WorldDelta
                {
                    worldId = instanceConfig.defenderSlice.sourceWorldId,
                    fromTick = 0,
                    toTick = instanceConfig.durationMinutes * 60
                }
            };
        }
    }
}

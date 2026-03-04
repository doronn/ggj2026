using System;
using Ggj2026.Game.Shared.Ids;

namespace Ggj2026.Game.Shared.Networking
{
    [Serializable]
    public class PlayerProfile
    {
        public string playerId = Guid.NewGuid().ToString("N");
        public string displayName = "Player";
        public WorldDescriptor primaryWorld = new WorldDescriptor();
    }

    [Serializable]
    public class WorldDescriptor
    {
        public WorldId worldId = WorldId.None;
        public string displayName = "Home World";
        public int technologyTier;
        public int threatTier;
    }

    [Serializable]
    public class MatchmakingTicket
    {
        public string ticketId = Guid.NewGuid().ToString("N");
        public WorldId worldId = WorldId.None;
        public DateTime queuedAtUtc = DateTime.UtcNow;
    }

    [Serializable]
    public class SessionDescriptor
    {
        public string sessionId = Guid.NewGuid().ToString("N");
        public string host = "localhost";
        public ushort port = 7979;
        public bool offlineMode = true;
    }
}

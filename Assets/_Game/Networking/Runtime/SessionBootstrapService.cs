using System.Threading;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Networking;

namespace Ggj2026.Game.Networking.Runtime
{
    public sealed class SessionBootstrapService
    {
        public UniTask<SessionDescriptor> CreateOfflineSessionAsync(SessionJoinRequest joinRequest, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new SessionDescriptor
            {
                offlineMode = joinRequest.offlineMode,
                host = "localhost",
                port = 7979
            });
        }
    }
}

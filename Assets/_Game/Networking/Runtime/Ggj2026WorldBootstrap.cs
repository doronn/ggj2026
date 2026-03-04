using Unity.Entities;
using Unity.NetCode;
using UnityEngine.Scripting;

namespace Ggj2026.Game.Networking.Runtime
{
    [Preserve]
    public sealed class Ggj2026WorldBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            if (World.DefaultGameObjectInjectionWorld != null &&
                World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                return true;
            }

            string worldName = string.IsNullOrWhiteSpace(defaultWorldName)
                ? "Ggj2026LocalWorld"
                : defaultWorldName;

            ClientServerBootstrap.CreateLocalWorld(worldName);
            return true;
        }
    }
}

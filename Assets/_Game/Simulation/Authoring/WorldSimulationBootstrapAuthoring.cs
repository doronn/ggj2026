using Unity.Entities;
using UnityEngine;

namespace Ggj2026.Game.Simulation.Authoring
{
    public class WorldSimulationBootstrapAuthoring : MonoBehaviour
    {
        public int chunkSize = 24;
        public int homeWorldSize = 384;

        private sealed class Baker : global::Unity.Entities.Baker<WorldSimulationBootstrapAuthoring>
        {
            public override void Bake(WorldSimulationBootstrapAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WorldSimulationBootstrap
                {
                    ChunkSize = authoring.chunkSize,
                    HomeWorldSize = authoring.homeWorldSize,
                    Initialized = 0
                });
            }
        }
    }
}

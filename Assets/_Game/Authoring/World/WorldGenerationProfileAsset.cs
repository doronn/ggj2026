using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.World;
using UnityEngine;

namespace Ggj2026.Game.Authoring.World
{
    [CreateAssetMenu(fileName = "WorldGenerationProfile", menuName = "GGJ2026/Persistent World/World Generation Profile")]
    public class WorldGenerationProfileAsset : ScriptableObject
    {
        public WorldGenerationProfile profile = new WorldGenerationProfile
        {
            profileId = "default",
            chunkSize = HexMetrics.DefaultChunkSize,
            homeWorldSize = HexMetrics.DefaultWorldSize,
            hexRadius = HexMetrics.DefaultHexRadius,
            aiFactionCount = 1,
            neutralThreatScale = 1
        };
    }
}

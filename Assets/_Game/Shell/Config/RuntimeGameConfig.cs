using System;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.World;
using UnityEngine;

namespace Ggj2026.Game.Shell.Config
{
    [CreateAssetMenu(fileName = "RuntimeGameConfig", menuName = "GGJ2026/Persistent World/Runtime Game Config")]
    public class RuntimeGameConfig : ScriptableObject
    {
        [Header("Scenes")]
        [SerializeField] private string bootstrapSceneName = "Bootstrap";
        [SerializeField] private string mainMenuSceneName = "PersistentMainMenu";
        [SerializeField] private string clientWorldSceneName = "ClientWorld";
        [SerializeField] private string serverWorldSceneName = "ServerWorld";
        [SerializeField] private string toolsSceneName = "Tools";

        [Header("World Defaults")]
        [SerializeField] private string localProfileId = "default";
        [SerializeField] private string localPlayerName = "Player";
        [SerializeField] private int localWorldSeed = 424242;
        [SerializeField] private ulong localWorldId = 1;
        [SerializeField] private int chunkSize = HexMetrics.DefaultChunkSize;
        [SerializeField] private int homeWorldSize = HexMetrics.DefaultWorldSize;
        [SerializeField] private float hexRadius = HexMetrics.DefaultHexRadius;
        [SerializeField] private int aiFactionCount = 1;
        [SerializeField] private int neutralThreatScale = 1;
        [SerializeField] private float realTimeTickDurationSeconds = 1f;
        [SerializeField] private float defaultRealTimeSimulationSpeed = 1f;

        public string BootstrapSceneName => bootstrapSceneName;
        public string MainMenuSceneName => mainMenuSceneName;
        public string ClientWorldSceneName => clientWorldSceneName;
        public string ServerWorldSceneName => serverWorldSceneName;
        public string ToolsSceneName => toolsSceneName;
        public string LocalPlayerName => localPlayerName;
        public int LocalWorldSeed => localWorldSeed;
        public ulong LocalWorldId => localWorldId;
        public float RealTimeTickDurationSeconds => Mathf.Max(0.05f, realTimeTickDurationSeconds);
        public float DefaultRealTimeSimulationSpeed => Mathf.Clamp(defaultRealTimeSimulationSpeed, 0.25f, 8f);

        public WorldGenerationProfile CreateGenerationProfile()
        {
            return new WorldGenerationProfile
            {
                profileId = localProfileId,
                chunkSize = Math.Max(1, chunkSize),
                homeWorldSize = Math.Max(chunkSize, homeWorldSize),
                hexRadius = Mathf.Max(0.1f, hexRadius),
                aiFactionCount = Mathf.Max(1, aiFactionCount),
                neutralThreatScale = Mathf.Max(1, neutralThreatScale),
                biomes =
                {
                    new BiomeDefinition { biomeKind = BiomeKind.Plains, fertility = 1.2f, danger = 0.8f, movementCost = 1f, resourceDensity = 1f },
                    new BiomeDefinition { biomeKind = BiomeKind.Forest, fertility = 1f, danger = 1f, movementCost = 1.1f, resourceDensity = 1.25f },
                    new BiomeDefinition { biomeKind = BiomeKind.Hills, fertility = 0.8f, danger = 1.1f, movementCost = 1.2f, resourceDensity = 1.35f },
                    new BiomeDefinition { biomeKind = BiomeKind.Wetlands, fertility = 1.1f, danger = 1.3f, movementCost = 1.35f, resourceDensity = 0.95f }
                },
                resourceNodes =
                {
                    new ResourceNodeDefinition { resourceType = Ggj2026.Game.Shared.Economy.ResourceType.Food, minYield = 60, maxYield = 120, baseSpawnWeight = 10 },
                    new ResourceNodeDefinition { resourceType = Ggj2026.Game.Shared.Economy.ResourceType.Wood, minYield = 60, maxYield = 140, baseSpawnWeight = 9 },
                    new ResourceNodeDefinition { resourceType = Ggj2026.Game.Shared.Economy.ResourceType.Stone, minYield = 40, maxYield = 100, baseSpawnWeight = 7 },
                    new ResourceNodeDefinition { resourceType = Ggj2026.Game.Shared.Economy.ResourceType.Ore, minYield = 30, maxYield = 90, baseSpawnWeight = 5 }
                }
            };
        }
    }
}

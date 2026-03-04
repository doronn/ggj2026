using System;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Persistence;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using Ggj2026.Game.Shell.Diagnostics;
using UnityEngine;
using Zenject;

namespace Ggj2026.Game.Shell.World
{
    public class OfflineWorldBootstrap : MonoBehaviour
    {
        [SerializeField] private RuntimeGameConfig runtimeGameConfig;
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private bool spawnDebugPresentation = true;

        private LocalWorldSessionService _localWorldSessionService;

        public event Action<WorldSnapshot> WorldLoaded;
        public WorldSnapshot CurrentSnapshot { get; private set; }
        public WorldGenerationProfile CurrentProfile { get; private set; }
        public LocalColonyGameplayService GameplayService { get; private set; }

        public void Construct([InjectOptional] LocalWorldSessionService localWorldSessionService)
        {
            _localWorldSessionService = localWorldSessionService;
        }

        private void Awake()
        {
            if (_localWorldSessionService == null && runtimeGameConfig != null)
            {
                IWorldGenerator generator = new DeterministicHexWorldGenerator();
                IWorldRepository repository = new JsonWorldRepository();
                _localWorldSessionService = new LocalWorldSessionService(runtimeGameConfig, generator, repository);
            }
        }

        private async void Start()
        {
            if (!autoLoadOnStart || _localWorldSessionService == null)
            {
                return;
            }

            CurrentProfile = runtimeGameConfig != null
                ? runtimeGameConfig.CreateGenerationProfile()
                : CreateFallbackProfile();
            var snapshot = await _localWorldSessionService.LoadOrCreateLocalWorldAsync();
            CurrentSnapshot = snapshot;
            GameplayService ??= new LocalColonyGameplayService(_localWorldSessionService);
            GameplayService.Initialize(snapshot, CurrentProfile);
            WorldLoaded?.Invoke(snapshot);
            SessionTelemetryService.Active?.RecordLifecycle(
                SessionActionKind.LoadWorld,
                $"{{\"worldId\":\"{snapshot.worldId}\",\"chunkCount\":{snapshot.chunks.Count},\"colonyCount\":{snapshot.colonies.Count}}}",
                "Persistent world loaded.",
                snapshot);
            Debug.Log($"[OfflineWorldBootstrap] Loaded persistent world {snapshot.worldId} with {snapshot.chunks.Count} chunks and {snapshot.colonies.Count} colonies.");

            if (spawnDebugPresentation)
            {
                EnsureDebugPresentation(snapshot, CurrentProfile, GameplayService);
            }
        }

        private void EnsureDebugPresentation(WorldSnapshot snapshot, WorldGenerationProfile profile, LocalColonyGameplayService gameplayService)
        {
            var renderer = FindFirstObjectByType<LocalWorldDebugRenderer>();
            if (renderer == null)
            {
                renderer = new GameObject("LocalWorldDebugRenderer").AddComponent<LocalWorldDebugRenderer>();
            }

            renderer.Initialize(snapshot, profile, gameplayService);

            var overlay = FindFirstObjectByType<ClientWorldHudController>();
            if (overlay == null)
            {
                overlay = new GameObject("ClientWorldHud").AddComponent<ClientWorldHudController>();
            }

            overlay.Initialize(runtimeGameConfig, snapshot, renderer, gameplayService);
        }

        private static WorldGenerationProfile CreateFallbackProfile()
        {
            return new WorldGenerationProfile
            {
                profileId = "fallback",
                chunkSize = 24,
                homeWorldSize = 384,
                hexRadius = 1f,
                aiFactionCount = 1,
                neutralThreatScale = 1
            };
        }
    }
}

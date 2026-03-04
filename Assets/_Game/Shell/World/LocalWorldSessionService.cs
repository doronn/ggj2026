using System.Threading;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Config;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    public sealed class LocalWorldSessionService
    {
        private readonly RuntimeGameConfig _runtimeGameConfig;
        private readonly IWorldGenerator _worldGenerator;
        private readonly IWorldRepository _worldRepository;

        public LocalWorldSessionService(RuntimeGameConfig runtimeGameConfig, IWorldGenerator worldGenerator, IWorldRepository worldRepository)
        {
            _runtimeGameConfig = runtimeGameConfig;
            _worldGenerator = worldGenerator;
            _worldRepository = worldRepository;
        }

        public async UniTask<WorldSnapshot> LoadOrCreateLocalWorldAsync(CancellationToken cancellationToken = default)
        {
            WorldId worldId = new WorldId(_runtimeGameConfig.LocalWorldId);
            WorldGenerationProfile profile = _runtimeGameConfig.CreateGenerationProfile();
            WorldSnapshot existing = await _worldRepository.LoadAsync(worldId, cancellationToken);

            if (existing != null && IsCompatible(existing))
            {
                Normalize(existing, worldId);
                return existing;
            }

            if (existing != null)
            {
                Debug.LogWarning("[LocalWorldSessionService] Existing local save uses an incompatible prototype snapshot. Regenerating with the hex space-simulation runtime.");
            }

            WorldSnapshot created = _worldGenerator.Generate(
                profile,
                ResolveSeed(existing),
                worldId,
                ResolvePlayerFactionId(existing));

            await _worldRepository.SaveAsync(created, cancellationToken);
            return created;
        }

        public UniTask SaveWorldAsync(WorldSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            return _worldRepository.SaveAsync(snapshot, cancellationToken);
        }

        public async UniTask<WorldSnapshot> ResetLocalWorldAsync(CancellationToken cancellationToken = default)
        {
            WorldId worldId = new WorldId(_runtimeGameConfig.LocalWorldId);
            await _worldRepository.DeleteAsync(worldId, cancellationToken);
            return await LoadOrCreateLocalWorldAsync(cancellationToken);
        }

        private WorldSeed ResolveSeed(WorldSnapshot existing)
        {
            return existing != null && existing.seed.Value != 0
                ? existing.seed
                : new WorldSeed(_runtimeGameConfig.LocalWorldSeed);
        }

        private static FactionId ResolvePlayerFactionId(WorldSnapshot existing)
        {
            if (existing?.factions != null)
            {
                for (int i = 0; i < existing.factions.Count; i++)
                {
                    if (existing.factions[i].isPlayerControlled && existing.factions[i].factionId != FactionId.None)
                    {
                        return existing.factions[i].factionId;
                    }
                }
            }

            return FactionId.NewId();
        }

        private static bool IsCompatible(WorldSnapshot snapshot)
        {
            if (snapshot == null || snapshot.snapshotVersion < 10)
            {
                return false;
            }

            if (snapshot.worldId == WorldId.None || snapshot.chunks == null || snapshot.factions == null || snapshot.colonies == null)
            {
                return false;
            }

            if (snapshot.factions.Count == 0 || snapshot.colonies.Count == 0 || snapshot.chunks.Count == 0)
            {
                return false;
            }

            return snapshot.units != null && snapshot.itemStacks != null && snapshot.constructionFeatures != null;
        }

        private static void Normalize(WorldSnapshot snapshot, WorldId expectedWorldId)
        {
            snapshot.snapshotVersion = 10;
            if (snapshot.worldId == WorldId.None)
            {
                snapshot.worldId = expectedWorldId;
            }

            snapshot.units ??= new System.Collections.Generic.List<UnitSnapshot>();
            snapshot.itemStacks ??= new System.Collections.Generic.List<Ggj2026.Game.Shared.Economy.ItemStackEntity>();
            snapshot.constructionFeatures ??= new System.Collections.Generic.List<Ggj2026.Game.Shared.Economy.ConstructionState>();
            snapshot.cachedSpaces ??= new System.Collections.Generic.List<Ggj2026.Game.Shared.Economy.SpaceRegionState>();
            snapshot.cachedRooms ??= new System.Collections.Generic.List<Ggj2026.Game.Shared.Economy.EnclosedRoomState>();

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                snapshot.chunks[i].modifiedCells ??= new System.Collections.Generic.List<HexCellState>();
                snapshot.chunks[i].modifiedEdges ??= new System.Collections.Generic.List<HexEdgeState>();
                snapshot.chunks[i].resourceNodes ??= new System.Collections.Generic.List<ResourceNodeState>();
                snapshot.chunks[i].landmarks ??= new System.Collections.Generic.List<Ggj2026.Game.Shared.Economy.LandmarkState>();
            }
        }
    }
}

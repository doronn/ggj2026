using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using UnityEngine;

namespace Ggj2026.Game.Shared.Persistence
{
    public sealed class JsonWorldRepository : IWorldRepository
    {
        private readonly string _rootPath;

        public JsonWorldRepository()
        {
            _rootPath = Path.Combine(Application.persistentDataPath, "PersistentWorlds");
        }

        public JsonWorldRepository(string rootPath)
        {
            _rootPath = rootPath;
        }

        public async UniTask SaveAsync(WorldSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_rootPath);
            string path = GetWorldPath(snapshot.worldId);
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(path, json);
            await UniTask.CompletedTask;
        }

        public async UniTask<WorldSnapshot> LoadAsync(WorldId worldId, CancellationToken cancellationToken = default)
        {
            string path = GetWorldPath(worldId);
            if (!File.Exists(path))
            {
                return null;
            }

            string json = File.ReadAllText(path);
            WorldSnapshot snapshot = JsonUtility.FromJson<WorldSnapshot>(json);
            return await UniTask.FromResult(NormalizeSnapshot(snapshot));
        }

        public async UniTask DeleteAsync(WorldId worldId, CancellationToken cancellationToken = default)
        {
            string path = GetWorldPath(worldId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            await UniTask.CompletedTask;
        }

        private string GetWorldPath(WorldId worldId)
        {
            return Path.Combine(_rootPath, $"{worldId}.json");
        }

        private static WorldSnapshot NormalizeSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            if (snapshot.chunks == null) snapshot.chunks = new System.Collections.Generic.List<ChunkSnapshot>();
            if (snapshot.factions == null) snapshot.factions = new System.Collections.Generic.List<FactionSnapshot>();
            if (snapshot.colonies == null) snapshot.colonies = new System.Collections.Generic.List<ColonySnapshot>();
            if (snapshot.units == null) snapshot.units = new System.Collections.Generic.List<UnitSnapshot>();
            if (snapshot.itemStacks == null) snapshot.itemStacks = new System.Collections.Generic.List<ItemStackEntity>();
            if (snapshot.constructionFeatures == null) snapshot.constructionFeatures = new System.Collections.Generic.List<ConstructionState>();
            if (snapshot.cachedSpaces == null) snapshot.cachedSpaces = new System.Collections.Generic.List<SpaceRegionState>();
            if (snapshot.cachedRooms == null) snapshot.cachedRooms = new System.Collections.Generic.List<EnclosedRoomState>();

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                ChunkSnapshot chunk = snapshot.chunks[i];
                if (chunk.modifiedCells == null) chunk.modifiedCells = new System.Collections.Generic.List<HexCellState>();
                if (chunk.modifiedEdges == null) chunk.modifiedEdges = new System.Collections.Generic.List<HexEdgeState>();
                if (chunk.resourceNodes == null) chunk.resourceNodes = new System.Collections.Generic.List<ResourceNodeState>();
                if (chunk.landmarks == null) chunk.landmarks = new System.Collections.Generic.List<LandmarkState>();

                for (int j = 0; j < chunk.modifiedCells.Count; j++)
                {
                    if (chunk.modifiedCells[j].floor == null) chunk.modifiedCells[j].floor = new FloorState();
                    if (chunk.modifiedCells[j].module == null) chunk.modifiedCells[j].module = new ModuleState();
                }
            }

            for (int i = 0; i < snapshot.factions.Count; i++)
            {
                if (snapshot.factions[i].derivedResourceSummary == null)
                {
                    snapshot.factions[i].derivedResourceSummary = new System.Collections.Generic.List<ResourceStack>();
                }
            }

            for (int i = 0; i < snapshot.colonies.Count; i++)
            {
                if (snapshot.colonies[i].standingSquads == null)
                {
                    snapshot.colonies[i].standingSquads = new System.Collections.Generic.List<Ggj2026.Game.Shared.Combat.SquadTemplate>();
                }
            }

            for (int i = 0; i < snapshot.units.Count; i++)
            {
                if (snapshot.units[i].carryLoad == null) snapshot.units[i].carryLoad = new CarryLoad();
            }

            for (int i = 0; i < snapshot.itemStacks.Count; i++)
            {
                if (snapshot.itemStacks[i].occupiedCells == null)
                {
                    snapshot.itemStacks[i].occupiedCells = new System.Collections.Generic.List<HexCoord>();
                }
            }

            for (int i = 0; i < snapshot.constructionFeatures.Count; i++)
            {
                if (snapshot.constructionFeatures[i].blueprint == null) snapshot.constructionFeatures[i].blueprint = new ConstructionBlueprint();
                if (snapshot.constructionFeatures[i].affectedCells == null) snapshot.constructionFeatures[i].affectedCells = new System.Collections.Generic.List<HexCoord>();
                if (snapshot.constructionFeatures[i].deliveredMaterials == null) snapshot.constructionFeatures[i].deliveredMaterials = new System.Collections.Generic.List<MaterialDeliveryState>();
                if (snapshot.constructionFeatures[i].reservations == null) snapshot.constructionFeatures[i].reservations = new System.Collections.Generic.List<StagingReservation>();
            }

            for (int i = 0; i < snapshot.cachedSpaces.Count; i++)
            {
                if (snapshot.cachedSpaces[i].cells == null) snapshot.cachedSpaces[i].cells = new System.Collections.Generic.List<HexCoord>();
            }

            for (int i = 0; i < snapshot.cachedRooms.Count; i++)
            {
                if (snapshot.cachedRooms[i].cells == null) snapshot.cachedRooms[i].cells = new System.Collections.Generic.List<HexCoord>();
            }

            return snapshot;
        }
    }
}

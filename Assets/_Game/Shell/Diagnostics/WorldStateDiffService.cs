using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.World;
using UnityEngine;

namespace Ggj2026.Game.Shell.Diagnostics
{
    public sealed class WorldStateDiffService
    {
        public WorldStateDigest BuildDigest(WorldSnapshot snapshot)
        {
            var digest = new WorldStateDigest();
            if (snapshot == null)
            {
                return digest;
            }

            digest.simulationTick = snapshot.simulationTick;
            digest.chunkCount = snapshot.chunks?.Count ?? 0;
            digest.colonyCount = snapshot.colonies?.Count ?? 0;
            digest.unitCount = snapshot.units?.Count ?? 0;
            digest.itemStackCount = snapshot.itemStacks?.Count ?? 0;
            digest.constructionCount = snapshot.constructionFeatures?.Count ?? 0;
            digest.spaceCount = snapshot.cachedSpaces?.Count ?? 0;
            digest.roomCount = snapshot.cachedRooms?.Count ?? 0;

            if (snapshot.chunks != null)
            {
                for (int i = 0; i < snapshot.chunks.Count; i++)
                {
                    digest.landmarkCount += snapshot.chunks[i].landmarks?.Count ?? 0;
                }
            }

            if (snapshot.factions != null)
            {
                for (int i = 0; i < snapshot.factions.Count; i++)
                {
                    if (!snapshot.factions[i].isPlayerControlled)
                    {
                        continue;
                    }

                    digest.factionSummary = CloneList(snapshot.factions[i].derivedResourceSummary);
                    break;
                }
            }

            return digest;
        }

        public WorldStateDiff CreateDiff(WorldSnapshot beforeSnapshot, WorldSnapshot afterSnapshot)
        {
            var diff = new WorldStateDiff
            {
                beforeDigest = BuildDigest(beforeSnapshot),
                afterDigest = BuildDigest(afterSnapshot)
            };

            DiffByKey(
                EnumerateCells(beforeSnapshot),
                EnumerateCells(afterSnapshot),
                cell => cell.coord.ToString(),
                (kind, before, after) => diff.changedCells.Add(new CellStateDelta
                {
                    changeKind = kind,
                    coord = after != null ? after.coord : before.coord,
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                EnumerateEdges(beforeSnapshot),
                EnumerateEdges(afterSnapshot),
                edge => edge.edgeCoord.ToString(),
                (kind, before, after) => diff.changedEdges.Add(new EdgeStateDelta
                {
                    changeKind = kind,
                    edgeCoord = after != null ? after.edgeCoord : before.edgeCoord,
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                beforeSnapshot?.itemStacks,
                afterSnapshot?.itemStacks,
                stack => stack.itemStackId.ToString(),
                (kind, before, after) => diff.changedItemStacks.Add(new ItemStackDelta
                {
                    changeKind = kind,
                    itemStackId = after != null ? after.itemStackId.ToString() : before.itemStackId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                beforeSnapshot?.units,
                afterSnapshot?.units,
                unit => unit.entityUid.ToString(),
                (kind, before, after) => diff.changedUnits.Add(new UnitStateDelta
                {
                    changeKind = kind,
                    entityUid = after != null ? after.entityUid.ToString() : before.entityUid.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                EnumerateResourceNodes(beforeSnapshot),
                EnumerateResourceNodes(afterSnapshot),
                node => node.resourceNodeId.ToString(),
                (kind, before, after) => diff.changedResourceNodes.Add(new ResourceNodeDelta
                {
                    changeKind = kind,
                    resourceNodeId = after != null ? after.resourceNodeId.ToString() : before.resourceNodeId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                beforeSnapshot?.constructionFeatures,
                afterSnapshot?.constructionFeatures,
                construction => construction.constructionFeatureId.ToString(),
                (kind, before, after) => diff.changedConstruction.Add(new ConstructionDelta
                {
                    changeKind = kind,
                    constructionFeatureId = after != null ? after.constructionFeatureId.ToString() : before.constructionFeatureId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                EnumerateLandmarks(beforeSnapshot),
                EnumerateLandmarks(afterSnapshot),
                landmark => landmark.landmarkId.ToString(),
                (kind, before, after) => diff.changedLandmarks.Add(new LandmarkDelta
                {
                    changeKind = kind,
                    landmarkId = after != null ? after.landmarkId.ToString() : before.landmarkId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                beforeSnapshot?.cachedSpaces,
                afterSnapshot?.cachedSpaces,
                space => space.regionId.ToString(),
                (kind, before, after) => diff.changedSpaces.Add(new SpaceDelta
                {
                    changeKind = kind,
                    regionId = after != null ? after.regionId.ToString() : before.regionId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            DiffByKey(
                beforeSnapshot?.cachedRooms,
                afterSnapshot?.cachedRooms,
                room => room.regionId.ToString(),
                (kind, before, after) => diff.changedRooms.Add(new RoomDelta
                {
                    changeKind = kind,
                    regionId = after != null ? after.regionId.ToString() : before.regionId.ToString(),
                    beforeJson = ToJson(before),
                    afterJson = ToJson(after)
                }));

            return diff;
        }

        private static List<HexCellState> EnumerateCells(WorldSnapshot snapshot)
        {
            var result = new List<HexCellState>();
            if (snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                if (snapshot.chunks[i].modifiedCells != null)
                {
                    result.AddRange(snapshot.chunks[i].modifiedCells);
                }
            }

            return result;
        }

        private static List<HexEdgeState> EnumerateEdges(WorldSnapshot snapshot)
        {
            var result = new List<HexEdgeState>();
            if (snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                if (snapshot.chunks[i].modifiedEdges != null)
                {
                    result.AddRange(snapshot.chunks[i].modifiedEdges);
                }
            }

            return result;
        }

        private static List<ResourceNodeState> EnumerateResourceNodes(WorldSnapshot snapshot)
        {
            var result = new List<ResourceNodeState>();
            if (snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                if (snapshot.chunks[i].resourceNodes != null)
                {
                    result.AddRange(snapshot.chunks[i].resourceNodes);
                }
            }

            return result;
        }

        private static List<LandmarkState> EnumerateLandmarks(WorldSnapshot snapshot)
        {
            var result = new List<LandmarkState>();
            if (snapshot?.chunks == null)
            {
                return result;
            }

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                if (snapshot.chunks[i].landmarks != null)
                {
                    result.AddRange(snapshot.chunks[i].landmarks);
                }
            }

            return result;
        }

        private static void DiffByKey<T>(IList<T> before, IList<T> after, Func<T, string> keySelector, Action<StateChangeKind, T, T> onChanged)
        {
            var beforeMap = new Dictionary<string, T>();
            var afterMap = new Dictionary<string, T>();

            if (before != null)
            {
                for (int i = 0; i < before.Count; i++)
                {
                    beforeMap[keySelector(before[i])] = before[i];
                }
            }

            if (after != null)
            {
                for (int i = 0; i < after.Count; i++)
                {
                    afterMap[keySelector(after[i])] = after[i];
                }
            }

            foreach (KeyValuePair<string, T> entry in beforeMap)
            {
                if (!afterMap.TryGetValue(entry.Key, out T afterValue))
                {
                    onChanged(StateChangeKind.Removed, entry.Value, default);
                    continue;
                }

                string beforeJson = ToJson(entry.Value);
                string afterJson = ToJson(afterValue);
                if (!string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
                {
                    onChanged(StateChangeKind.Updated, entry.Value, afterValue);
                }
            }

            foreach (KeyValuePair<string, T> entry in afterMap)
            {
                if (!beforeMap.ContainsKey(entry.Key))
                {
                    onChanged(StateChangeKind.Added, default, entry.Value);
                }
            }
        }

        private static List<ResourceStack> CloneList(List<ResourceStack> source)
        {
            if (source == null)
            {
                return new List<ResourceStack>();
            }

            var result = new List<ResourceStack>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i]);
            }

            return result;
        }

        private static string ToJson<T>(T value)
        {
            if (EqualityComparer<T>.Default.Equals(value, default))
            {
                return string.Empty;
            }

            return JsonUtility.ToJson(value);
        }
    }
}

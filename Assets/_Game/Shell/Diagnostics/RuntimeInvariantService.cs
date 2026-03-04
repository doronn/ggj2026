using System;
using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.World;

namespace Ggj2026.Game.Shell.Diagnostics
{
    public sealed class RuntimeInvariantService : IRuntimeInvariantChecker
    {
        private readonly ISessionTelemetrySink _telemetrySink;

        public RuntimeInvariantService(ISessionTelemetrySink telemetrySink)
        {
            _telemetrySink = telemetrySink;
        }

        public void Evaluate(WorldSnapshot snapshot, string actionId = null)
        {
            if (snapshot == null)
            {
                return;
            }

            EmitCheck(snapshot, actionId, "world.snapshot.exists", true, InvariantSeverity.Info, "Snapshot exists.");

            if (snapshot.itemStacks != null)
            {
                for (int i = 0; i < snapshot.itemStacks.Count; i++)
                {
                    var stack = snapshot.itemStacks[i];
                    EmitCheck(
                        snapshot,
                        actionId,
                        $"stack.occupied.{stack.itemStackId}",
                        stack.occupiedCells != null && stack.occupiedCells.Count > 0,
                        InvariantSeverity.Error,
                        "Item stack must occupy at least one cell.",
                        stack.itemStackId.ToString());

                    EmitCheck(
                        snapshot,
                        actionId,
                        $"stack.amount.{stack.itemStackId}",
                        stack.amount >= 0,
                        InvariantSeverity.Error,
                        "Item stack amount must not be negative.",
                        stack.itemStackId.ToString());

                    bool storagePolicyValid = stack.storagePolicy switch
                    {
                        StoragePolicy.OutdoorFloor => stack.bulkClass == BulkClass.LooseSmall,
                        StoragePolicy.ShelteredFloor => stack.bulkClass == BulkClass.LooseSmall || stack.bulkClass == BulkClass.PileMedium,
                        _ => true
                    };
                    EmitCheck(
                        snapshot,
                        actionId,
                        $"stack.policy.{stack.itemStackId}",
                        storagePolicyValid,
                        InvariantSeverity.Warning,
                        "Storage policy does not match bulk class.",
                        stack.itemStackId.ToString());
                }
            }

            if (snapshot.units != null)
            {
                for (int i = 0; i < snapshot.units.Count; i++)
                {
                    var unit = snapshot.units[i];
                    bool constructionValid = unit.assignedConstructionId == ConstructionFeatureId.None || ContainsConstruction(snapshot, unit.assignedConstructionId);
                    EmitCheck(snapshot, actionId, $"unit.construction.{unit.entityUid}", constructionValid, InvariantSeverity.Warning, "Unit references a missing construction feature.", unit.entityUid.ToString());

                    bool nodeValid = unit.assignedResourceNodeId == ResourceNodeId.None || ContainsResourceNode(snapshot, unit.assignedResourceNodeId);
                    EmitCheck(snapshot, actionId, $"unit.node.{unit.entityUid}", nodeValid, InvariantSeverity.Warning, "Unit references a missing resource node.", unit.entityUid.ToString());
                }
            }

            if (snapshot.constructionFeatures != null)
            {
                for (int i = 0; i < snapshot.constructionFeatures.Count; i++)
                {
                    var construction = snapshot.constructionFeatures[i];
                    EmitCheck(
                        snapshot,
                        actionId,
                        $"construction.completed.{construction.constructionFeatureId}",
                        !construction.isCompleted,
                        InvariantSeverity.Warning,
                        "Completed construction should not remain in the active list.",
                        construction.constructionFeatureId.ToString());
                }
            }

            if (snapshot.cachedSpaces != null)
            {
                for (int i = 0; i < snapshot.cachedSpaces.Count; i++)
                {
                    var space = snapshot.cachedSpaces[i];
                    bool validState = space.activationState != SpaceActivationState.ShelteredActive || space.isEnclosed;
                    EmitCheck(snapshot, actionId, $"space.enclosed.{space.regionId}", validState, InvariantSeverity.Warning, "Sheltered active spaces must be enclosed.", space.regionId.ToString());
                }
            }
        }

        private void EmitCheck(WorldSnapshot snapshot, string actionId, string checkId, bool passed, InvariantSeverity severity, string message, string relatedObjectId = null)
        {
            _telemetrySink?.RecordInvariant(new InvariantCheckRecord
            {
                sessionId = SessionTelemetryService.Active != null ? SessionTelemetryService.Active.SessionId : string.Empty,
                actionId = actionId,
                checkId = checkId,
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                severity = severity,
                passed = passed,
                message = message,
                relatedObjectId = relatedObjectId
            });
        }

        private static bool ContainsConstruction(WorldSnapshot snapshot, ConstructionFeatureId id)
        {
            if (snapshot.constructionFeatures == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.constructionFeatures.Count; i++)
            {
                if (snapshot.constructionFeatures[i].constructionFeatureId == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsResourceNode(WorldSnapshot snapshot, ResourceNodeId id)
        {
            if (snapshot.chunks == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.chunks.Count; i++)
            {
                List<ResourceNodeState> nodes = snapshot.chunks[i].resourceNodes;
                if (nodes == null)
                {
                    continue;
                }

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (nodes[j].resourceNodeId == id)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

using System.Collections.Generic;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Interfaces;
using Ggj2026.Game.Shared.Logistics;
using Ggj2026.Game.Shared.Telemetry;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.Diagnostics;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class RuntimeInvariantServiceTests
    {
        [Test]
        public void Evaluate_EmitsWarningForInvalidOutdoorMediumPile()
        {
            var sink = new RecordingTelemetrySink();
            var service = new RuntimeInvariantService(sink);
            var snapshot = new WorldSnapshot
            {
                itemStacks =
                {
                    new ItemStackEntity
                    {
                        amount = 3,
                        bulkClass = BulkClass.PileMedium,
                        storagePolicy = StoragePolicy.OutdoorFloor,
                        occupiedCells = { new HexCoord(0, 0) }
                    }
                }
            };

            service.Evaluate(snapshot);

            Assert.That(sink.Invariants.Count, Is.GreaterThan(0));
            Assert.That(sink.Invariants.Exists(record => record.checkId.Contains("stack.policy") && !record.passed), Is.True);
        }

        private sealed class RecordingTelemetrySink : ISessionTelemetrySink
        {
            public List<InvariantCheckRecord> Invariants { get; } = new List<InvariantCheckRecord>();

            public void RecordAction(SessionActionRecord record)
            {
            }

            public void RecordInvariant(InvariantCheckRecord record)
            {
                Invariants.Add(record);
            }
        }
    }
}

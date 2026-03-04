using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Ids;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;

namespace Ggj2026.Game.Tests.EditMode
{
    public class WorldGeneratorTests
    {
        [Test]
        public void Generate_IsDeterministicForSameSeed()
        {
            var generator = new DeterministicHexWorldGenerator();
            var profile = new WorldGenerationProfile
            {
                profileId = "test",
                chunkSize = 24,
                homeWorldSize = 384,
                aiFactionCount = 1
            };

            WorldId worldId = new WorldId(11);
            FactionId factionId = new FactionId(13);

            WorldSnapshot first = generator.Generate(profile, new WorldSeed(12345), worldId, factionId);
            WorldSnapshot second = generator.Generate(profile, new WorldSeed(12345), worldId, factionId);

            Assert.That(first.chunks.Count, Is.EqualTo(second.chunks.Count));
            Assert.That(first.colonies[0].center, Is.EqualTo(second.colonies[0].center));
            Assert.That(first.units.Count, Is.EqualTo(second.units.Count));
            Assert.That(first.itemStacks.Count, Is.EqualTo(second.itemStacks.Count));
            Assert.That(first.chunks[0].resourceRichness, Is.EqualTo(second.chunks[0].resourceRichness));

            ResourceNodeState firstNode = first.chunks.Find(chunk => chunk.resourceNodes.Count > 0).resourceNodes[0];
            ResourceNodeState secondNode = second.chunks.Find(chunk => chunk.resourceNodes.Count > 0).resourceNodes[0];

            Assert.That(firstNode.cell, Is.EqualTo(secondNode.cell));
            Assert.That(firstNode.resourceType, Is.EqualTo(secondNode.resourceType));
            Assert.That(firstNode.remainingYield, Is.EqualTo(secondNode.remainingYield));

            LandmarkState firstLandmark = first.chunks.Find(chunk => chunk.landmarks.Count > 0).landmarks[0];
            LandmarkState secondLandmark = second.chunks.Find(chunk => chunk.landmarks.Count > 0).landmarks[0];

            Assert.That(firstLandmark.landmarkKind, Is.EqualTo(LandmarkKind.TownCenterCore));
            Assert.That(firstLandmark.occupiedCells.Count, Is.EqualTo(secondLandmark.occupiedCells.Count));
            Assert.That(firstLandmark.originCell, Is.EqualTo(secondLandmark.originCell));
        }
    }
}

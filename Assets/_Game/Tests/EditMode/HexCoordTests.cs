using Ggj2026.Game.Shared.Hex;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class HexCoordTests
    {
        [Test]
        public void Distance_IsSymmetric()
        {
            var a = new HexCoord(0, 0);
            var b = new HexCoord(5, -2);

            Assert.That(a.DistanceTo(b), Is.EqualTo(b.DistanceTo(a)));
            Assert.That(a.DistanceTo(b), Is.EqualTo(5));
        }

        [Test]
        public void WorldConversion_RoundTripsNearCenter()
        {
            var original = new HexCoord(3, -1);
            Vector3 world = original.ToWorld(1f);
            HexCoord converted = HexCoord.FromWorld(world, 1f);

            Assert.That(converted, Is.EqualTo(original));
        }
    }
}

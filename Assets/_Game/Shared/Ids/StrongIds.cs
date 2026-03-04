using System;
using System.Globalization;

namespace Ggj2026.Game.Shared.Ids
{
    internal static class StrongIdGenerator
    {
        public static ulong NewValue()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            return BitConverter.ToUInt64(bytes, 0);
        }
    }

    [Serializable]
    public struct WorldId : IEquatable<WorldId>, IComparable<WorldId>
    {
        public ulong Value;

        public WorldId(ulong value) => Value = value;

        public static WorldId NewId() => new WorldId(StrongIdGenerator.NewValue());
        public static WorldId None => new WorldId(0);
        public bool Equals(WorldId other) => Value == other.Value;
        public int CompareTo(WorldId other) => Value.CompareTo(other.Value);
        public override bool Equals(object obj) => obj is WorldId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(WorldId left, WorldId right) => left.Equals(right);
        public static bool operator !=(WorldId left, WorldId right) => !left.Equals(right);
    }

    [Serializable]
    public struct FactionId : IEquatable<FactionId>
    {
        public ulong Value;

        public FactionId(ulong value) => Value = value;

        public static FactionId NewId() => new FactionId(StrongIdGenerator.NewValue());
        public static FactionId None => new FactionId(0);
        public bool Equals(FactionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FactionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(FactionId left, FactionId right) => left.Equals(right);
        public static bool operator !=(FactionId left, FactionId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ColonyId : IEquatable<ColonyId>
    {
        public ulong Value;

        public ColonyId(ulong value) => Value = value;

        public static ColonyId NewId() => new ColonyId(StrongIdGenerator.NewValue());
        public static ColonyId None => new ColonyId(0);
        public bool Equals(ColonyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ColonyId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(ColonyId left, ColonyId right) => left.Equals(right);
        public static bool operator !=(ColonyId left, ColonyId right) => !left.Equals(right);
    }

    [Serializable]
    public struct SquadId : IEquatable<SquadId>
    {
        public ulong Value;

        public SquadId(ulong value) => Value = value;

        public static SquadId NewId() => new SquadId(StrongIdGenerator.NewValue());
        public static SquadId None => new SquadId(0);
        public bool Equals(SquadId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SquadId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(SquadId left, SquadId right) => left.Equals(right);
        public static bool operator !=(SquadId left, SquadId right) => !left.Equals(right);
    }

    [Serializable]
    public struct EntityUid : IEquatable<EntityUid>
    {
        public ulong Value;

        public EntityUid(ulong value) => Value = value;

        public static EntityUid NewId() => new EntityUid(StrongIdGenerator.NewValue());
        public static EntityUid None => new EntityUid(0);
        public bool Equals(EntityUid other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityUid other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(EntityUid left, EntityUid right) => left.Equals(right);
        public static bool operator !=(EntityUid left, EntityUid right) => !left.Equals(right);
    }

    [Serializable]
    public struct ItemStackId : IEquatable<ItemStackId>
    {
        public ulong Value;

        public ItemStackId(ulong value) => Value = value;

        public static ItemStackId NewId() => new ItemStackId(StrongIdGenerator.NewValue());
        public static ItemStackId None => new ItemStackId(0);
        public bool Equals(ItemStackId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ItemStackId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(ItemStackId left, ItemStackId right) => left.Equals(right);
        public static bool operator !=(ItemStackId left, ItemStackId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ResourceNodeId : IEquatable<ResourceNodeId>
    {
        public ulong Value;

        public ResourceNodeId(ulong value) => Value = value;

        public static ResourceNodeId NewId() => new ResourceNodeId(StrongIdGenerator.NewValue());
        public static ResourceNodeId None => new ResourceNodeId(0);
        public bool Equals(ResourceNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ResourceNodeId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(ResourceNodeId left, ResourceNodeId right) => left.Equals(right);
        public static bool operator !=(ResourceNodeId left, ResourceNodeId right) => !left.Equals(right);
    }

    [Serializable]
    public struct ConstructionFeatureId : IEquatable<ConstructionFeatureId>
    {
        public ulong Value;

        public ConstructionFeatureId(ulong value) => Value = value;

        public static ConstructionFeatureId NewId() => new ConstructionFeatureId(StrongIdGenerator.NewValue());
        public static ConstructionFeatureId None => new ConstructionFeatureId(0);
        public bool Equals(ConstructionFeatureId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ConstructionFeatureId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(ConstructionFeatureId left, ConstructionFeatureId right) => left.Equals(right);
        public static bool operator !=(ConstructionFeatureId left, ConstructionFeatureId right) => !left.Equals(right);
    }

    [Serializable]
    public struct SpaceRegionId : IEquatable<SpaceRegionId>
    {
        public ulong Value;

        public SpaceRegionId(ulong value) => Value = value;

        public static SpaceRegionId NewId() => new SpaceRegionId(StrongIdGenerator.NewValue());
        public static SpaceRegionId None => new SpaceRegionId(0);
        public bool Equals(SpaceRegionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SpaceRegionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(SpaceRegionId left, SpaceRegionId right) => left.Equals(right);
        public static bool operator !=(SpaceRegionId left, SpaceRegionId right) => !left.Equals(right);
    }

    [Serializable]
    public struct LandmarkId : IEquatable<LandmarkId>
    {
        public ulong Value;

        public LandmarkId(ulong value) => Value = value;

        public static LandmarkId NewId() => new LandmarkId(StrongIdGenerator.NewValue());
        public static LandmarkId None => new LandmarkId(0);
        public bool Equals(LandmarkId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is LandmarkId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
        public static bool operator ==(LandmarkId left, LandmarkId right) => left.Equals(right);
        public static bool operator !=(LandmarkId left, LandmarkId right) => !left.Equals(right);
    }
}

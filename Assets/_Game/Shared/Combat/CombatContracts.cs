using System;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Ids;

namespace Ggj2026.Game.Shared.Combat
{
    public enum ThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    public enum SquadOrderKind
    {
        Move,
        Hold,
        AttackMove,
        DefendArea,
        Raid,
        Retreat,
        Garrison,
        Perimeter
    }

    [Serializable]
    public class SquadTemplate
    {
        public SquadId squadId = SquadId.None;
        public string displayName = "Squad";
        public int desiredSize = 6;
        public int assignedUnits;
        public SquadOrder currentOrder = new SquadOrder
        {
            orderKind = SquadOrderKind.Garrison
        };
    }

    [Serializable]
    public struct CombatProfile
    {
        public int damage;
        public int range;
        public float cooldownSeconds;
        public float accuracy;
    }

    [Serializable]
    public struct WeaponProfile
    {
        public string weaponId;
        public int damage;
        public int penetration;
        public int range;
    }

    [Serializable]
    public struct ArmorProfile
    {
        public string armorId;
        public int mitigation;
        public int durability;
    }

    [Serializable]
    public class SquadOrder
    {
        public SquadId squadId = SquadId.None;
        public SquadOrderKind orderKind;
        public HexCoord targetHex;
        public float issuedAtTime;
    }
}

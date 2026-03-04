using Unity.Entities;

namespace Ggj2026.Game.Simulation
{
    public struct WorldSimulationBootstrap : IComponentData
    {
        public int ChunkSize;
        public int HomeWorldSize;
        public int Initialized;
    }

    public struct FactionTag : IComponentData
    {
        public int FactionIndex;
    }

    public struct ColonyCore : IComponentData
    {
        public int Population;
        public int HousingCapacity;
        public int WorkforceCap;
    }

    public struct StructureWorkplace : IComponentData
    {
        public int RequiredWorkers;
        public int AssignedWorkers;
        public byte IsProductive;
    }

    public struct JobReservationState : IComponentData
    {
        public int ReservedBy;
        public long ExpiresAtTick;
        public byte IsActive;
    }

    public struct SupplyState : IComponentData
    {
        public float CoverageRadius;
        public float AttritionModifier;
    }
}

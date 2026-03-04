using Unity.Burst;
using Unity.Entities;

namespace Ggj2026.Game.Simulation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSimulationBootstrap>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<WorldSimulationBootstrap> bootstrap = SystemAPI.GetSingletonRW<WorldSimulationBootstrap>();
            if (bootstrap.ValueRO.Initialized != 0)
            {
                return;
            }

            Entity colony = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(colony, new FactionTag { FactionIndex = 0 });
            state.EntityManager.AddComponentData(colony, new ColonyCore
            {
                Population = 8,
                HousingCapacity = 12,
                WorkforceCap = 6
            });
            state.EntityManager.AddComponentData(colony, new StructureWorkplace
            {
                RequiredWorkers = 2,
                AssignedWorkers = 1,
                IsProductive = 0
            });
            state.EntityManager.AddComponentData(colony, new JobReservationState
            {
                ReservedBy = -1,
                ExpiresAtTick = 0,
                IsActive = 0
            });
            state.EntityManager.AddComponentData(colony, new SupplyState
            {
                CoverageRadius = 8f,
                AttritionModifier = 1f
            });

            bootstrap.ValueRW.Initialized = 1;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkplaceProductivitySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (RefRW<StructureWorkplace> workplace in SystemAPI.Query<RefRW<StructureWorkplace>>())
            {
                workplace.ValueRW.IsProductive = (byte)(workplace.ValueRO.AssignedWorkers >= workplace.ValueRO.RequiredWorkers ? 1 : 0);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ReservationExpirySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            long tick = state.WorldUnmanaged.Time.ElapsedTime > 0d
                ? (long)(state.WorldUnmanaged.Time.ElapsedTime * 10d)
                : 0;

            foreach (RefRW<JobReservationState> reservation in SystemAPI.Query<RefRW<JobReservationState>>())
            {
                if (reservation.ValueRO.IsActive == 0)
                {
                    continue;
                }

                if (tick >= reservation.ValueRO.ExpiresAtTick)
                {
                    reservation.ValueRW.IsActive = 0;
                    reservation.ValueRW.ReservedBy = -1;
                }
            }
        }
    }
}

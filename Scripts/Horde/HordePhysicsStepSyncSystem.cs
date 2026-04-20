using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct HordePhysicsStepSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<PhysicsStep>(out RefRW<PhysicsStep> physicsStep))
        {
            Entity entity = state.EntityManager.CreateEntity();
            PhysicsStep step = PhysicsStep.Default;
            step.SynchronizeCollisionWorld = 1;
            state.EntityManager.AddComponentData(entity, step);
            return;
        }

        if (physicsStep.ValueRO.SynchronizeCollisionWorld != 0)
            return;

        PhysicsStep updated = physicsStep.ValueRO;
        updated.SynchronizeCollisionWorld = 1;
        physicsStep.ValueRW = updated;
    }
}

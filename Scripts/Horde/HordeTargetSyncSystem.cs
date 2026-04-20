using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

public struct HordeTargetPoint : IComponentData
{
    public float3 Position;
    public byte IsValid;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(HordeSpawnSystem))]
[UpdateBefore(typeof(HordeEnemyMoveSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct HordeTargetSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        Entity entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new HordeTargetPoint());
    }

    public void OnUpdate(ref SystemState state)
    {
        RefRW<HordeTargetPoint> targetPoint = SystemAPI.GetSingletonRW<HordeTargetPoint>();

        foreach (RefRO<LocalTransform> localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerCar>())
        {
            targetPoint.ValueRW.Position = localTransform.ValueRO.Position;
            targetPoint.ValueRW.IsValid = 1;
            return;
        }

        Transform target = HordeTargetTracker.Target;

        if (target == null)
        {
            targetPoint.ValueRW.IsValid = 0;
            return;
        }

        Vector3 targetPosition = target.position;
        targetPoint.ValueRW.Position = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
        targetPoint.ValueRW.IsValid = 1;
    }
}

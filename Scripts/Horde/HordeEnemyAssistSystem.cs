using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

public struct HordeDead : IComponentData
{
    public float Time;
    public float ShrinkDelay;
    public float ShrinkDuration;
    public float StartScale;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct HordeEnemyAssistSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HordeEnemy>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (localTransform, velocity, enemy) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<HordeEnemy>>().WithNone<HordeDead>())
        {
            if (enemy.ValueRO.UprightSharpness > 0f)
            {
                float3 flattenedForward = math.rotate(localTransform.ValueRO.Rotation, new float3(0f, 0f, 1f));
                flattenedForward.y = 0f;

                if (math.lengthsq(flattenedForward) < 0.0001f)
                {
                    float3 planarVelocity = new float3(velocity.ValueRO.Linear.x, 0f, velocity.ValueRO.Linear.z);
                    if (math.lengthsq(planarVelocity) > 0.0001f)
                        flattenedForward = planarVelocity;
                    else
                        flattenedForward = new float3(0f, 0f, 1f);
                }

                flattenedForward = math.normalize(flattenedForward);
                quaternion targetRotation = quaternion.LookRotationSafe(flattenedForward, math.up());
                float blend = math.saturate(enemy.ValueRO.UprightSharpness * deltaTime);
                localTransform.ValueRW.Rotation = math.slerp(localTransform.ValueRO.Rotation, targetRotation, blend);
            }

            if (enemy.ValueRO.TiltDamping > 0f)
            {
                float damping = math.saturate(enemy.ValueRO.TiltDamping * deltaTime);
                float3 angular = velocity.ValueRO.Angular;
                angular.x = math.lerp(angular.x, 0f, damping);
                angular.z = math.lerp(angular.z, 0f, damping);
                velocity.ValueRW.Angular = angular;
            }
        }

        foreach (var (dead, localTransform, entity) in SystemAPI.Query<RefRW<HordeDead>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            dead.ValueRW.Time += deltaTime;

            if (dead.ValueRO.Time >= dead.ValueRO.ShrinkDelay)
            {
                if (dead.ValueRO.ShrinkDuration <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                float t = math.saturate((dead.ValueRO.Time - dead.ValueRO.ShrinkDelay) / dead.ValueRO.ShrinkDuration);
                localTransform.ValueRW.Scale = math.lerp(dead.ValueRO.StartScale, 0f, t);

                if (t >= 1f)
                    ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

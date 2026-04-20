using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[UpdateBefore(typeof(PlayerCarKillSystem))]
[UpdateBefore(typeof(HordePlayerDamageSystem))]
public partial struct HordeEnemySafetySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HordeEnemy>();
        state.RequireForUpdate<HordeEnemyState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (localTransform, velocity, enemy, enemyState) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<HordeEnemy>, RefRW<HordeEnemyState>>().WithNone<HordeDead>())
        {
            if (enemyState.ValueRO.IsInitialized == 0)
            {
                enemyState.ValueRW.LastStablePosition = localTransform.ValueRO.Position;
                enemyState.ValueRW.LastStableRotation = localTransform.ValueRO.Rotation;
                enemyState.ValueRW.IsInitialized = 1;
            }

            float3 rawPosition = localTransform.ValueRO.Position;
            quaternion rawRotation = localTransform.ValueRO.Rotation;
            float3 rawLinear = velocity.ValueRO.Linear;
            float3 rawAngular = velocity.ValueRO.Angular;
            float3 lastStablePosition = enemyState.ValueRO.LastStablePosition;
            float maxPlanarSpeed = math.max(35f, enemyState.ValueRO.MoveSpeed * 6f);
            float maxVerticalSpeed = 35f;
            float maxAngularSpeed = 30f;

            bool invalidState = !IsFinite(rawPosition) || !IsFinite(rawRotation) || !IsFinite(rawLinear) || !IsFinite(rawAngular);
            invalidState |= math.abs(rawPosition.y) > 250f;
            invalidState |= math.lengthsq(new float2(rawPosition.x - lastStablePosition.x, rawPosition.z - lastStablePosition.z)) > 2500f;

            if (invalidState)
            {
                localTransform.ValueRW.Position = enemyState.ValueRO.LastStablePosition;
                localTransform.ValueRW.Rotation = enemyState.ValueRO.LastStableRotation;
                velocity.ValueRW.Linear = float3.zero;
                velocity.ValueRW.Angular = float3.zero;
                enemyState.ValueRW.DamageTimer = 0f;
                enemyState.ValueRW.IsPlayerInsideDamageRange = 0;
                enemyState.ValueRW.HasAppliedEnterDamage = 0;
                continue;
            }

            float3 linear = rawLinear;
            float3 planar = new float3(linear.x, 0f, linear.z);
            float planarSpeed = math.length(planar);

            if (planarSpeed > maxPlanarSpeed && planarSpeed > 0f)
                planar = planar / planarSpeed * maxPlanarSpeed;

            linear = new float3(planar.x, math.clamp(linear.y, -maxVerticalSpeed, maxVerticalSpeed), planar.z);

            float3 angular = rawAngular;
            float angularSpeed = math.length(angular);
            if (angularSpeed > maxAngularSpeed && angularSpeed > 0f)
                angular = angular / angularSpeed * maxAngularSpeed;

            velocity.ValueRW.Linear = linear;
            velocity.ValueRW.Angular = angular;
            enemyState.ValueRW.LastStablePosition = localTransform.ValueRO.Position;
            enemyState.ValueRW.LastStableRotation = localTransform.ValueRO.Rotation;
        }
    }

    private static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    private static bool IsFinite(quaternion value)
    {
        return math.all(math.isfinite(value.value));
    }
}

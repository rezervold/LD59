using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct HordeEnemyMoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HordeEnemy>();
        state.RequireForUpdate<HordeEnemyState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasTargetPoint = SystemAPI.TryGetSingleton(out HordeTargetPoint targetPoint);
        bool hasValidTarget = hasTargetPoint && targetPoint.IsValid != 0;
        float3 targetPosition = hasValidTarget ? targetPoint.Position : float3.zero;

        foreach (var (localTransform, velocity, enemy, enemyState) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<HordeEnemy>, RefRW<HordeEnemyState>>().WithNone<HordeDead>())
        {
            float3 currentLinear = velocity.ValueRO.Linear;
            float3 horizontalVelocity = new float3(currentLinear.x, 0f, currentLinear.z);
            float3 moveDirection = float3.zero;
            bool hasDirection = false;

            if (enemyState.ValueRO.IsWanderer != 0)
            {
                float changeTimer = enemyState.ValueRO.WanderDirectionChangeTimer - deltaTime;
                float3 wanderDirection = enemyState.ValueRO.WanderDirection;

                if (changeTimer <= 0f)
                {
                    uint seed = enemyState.ValueRO.RandomSeed == 0 ? 1u : enemyState.ValueRO.RandomSeed;
                    Random random = new Random(seed);
                    float angle = random.NextFloat(0f, math.PI * 2f);
                    float timer = random.NextFloat(enemy.ValueRO.WanderDirectionChangeMin, enemy.ValueRO.WanderDirectionChangeMax);
                    wanderDirection = new float3(math.cos(angle), 0f, math.sin(angle));
                    enemyState.ValueRW.WanderDirection = wanderDirection;
                    enemyState.ValueRW.WanderDirectionChangeTimer = timer;

                    uint nextSeed = random.NextUInt();
                    enemyState.ValueRW.RandomSeed = nextSeed == 0 ? 1u : nextSeed;
                }
                else
                {
                    enemyState.ValueRW.WanderDirectionChangeTimer = changeTimer;
                }

                moveDirection = wanderDirection;
                hasDirection = math.lengthsq(moveDirection) > 0.0001f;
            }
            else if (hasValidTarget)
            {
                float3 toTarget = targetPosition - localTransform.ValueRO.Position;
                toTarget.y = 0f;
                hasDirection = math.lengthsq(toTarget) > 0.0001f;
                if (hasDirection)
                    moveDirection = math.normalize(toTarget);
            }

            if (hasDirection)
            {
                horizontalVelocity += math.normalize(moveDirection) * enemy.ValueRO.MoveForce * deltaTime;

                float speed = math.length(horizontalVelocity);
                if (speed > enemyState.ValueRO.MoveSpeed && speed > 0f)
                    horizontalVelocity = horizontalVelocity / speed * enemyState.ValueRO.MoveSpeed;
            }

            velocity.ValueRW.Linear = new float3(horizontalVelocity.x, currentLinear.y, horizontalVelocity.z);
        }
    }
}

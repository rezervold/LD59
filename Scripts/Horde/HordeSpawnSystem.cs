using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(HordeEnemyMoveSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct HordeSpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HordeSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (RefRW<HordeSpawner> spawner in SystemAPI.Query<RefRW<HordeSpawner>>())
        {
            ref HordeSpawner spawnerData = ref spawner.ValueRW;

            if (spawnerData.IsActive == 0)
                continue;

            if (spawnerData.SpawnedCount >= spawnerData.TotalCount)
                continue;

            if (elapsedTime < spawnerData.NextSpawnTime)
                continue;

            int remaining = spawnerData.TotalCount - spawnerData.SpawnedCount;
            int spawnCount = math.min(spawnerData.BatchSize, remaining);
            uint seed = spawnerData.RandomSeed == 0 ? 1u : spawnerData.RandomSeed;
            Random random = new Random(seed);
            HordeEnemy prefabEnemy = state.EntityManager.GetComponentData<HordeEnemy>(spawnerData.EnemyPrefab);
            HordeEnemyState prefabEnemyState = state.EntityManager.GetComponentData<HordeEnemyState>(spawnerData.EnemyPrefab);

            for (int i = 0; i < spawnCount; i++)
            {
                Entity enemy = ecb.Instantiate(spawnerData.EnemyPrefab);
                float3 spawnOffset = new float3(
                    random.NextFloat(-spawnerData.SpawnExtents.x, spawnerData.SpawnExtents.x),
                    random.NextFloat(-spawnerData.SpawnExtents.y, spawnerData.SpawnExtents.y),
                    random.NextFloat(-spawnerData.SpawnExtents.z, spawnerData.SpawnExtents.z));

                ecb.SetComponent(enemy, LocalTransform.FromPositionRotation(
                    spawnerData.SpawnPosition + spawnOffset,
                    quaternion.identity));

                float minSpeed = math.min(prefabEnemy.MinSpeed, prefabEnemy.MaxSpeed);
                float maxSpeed = math.max(prefabEnemy.MinSpeed, prefabEnemy.MaxSpeed);
                float moveSpeed = random.NextFloat(minSpeed, maxSpeed);
                bool isWanderer = random.NextFloat() <= prefabEnemy.WanderChance;

                if (isWanderer)
                    moveSpeed *= prefabEnemy.WanderSpeedMult;

                float wanderAngle = random.NextFloat(0f, math.PI * 2f);
                uint enemySeed = random.NextUInt();
                if (enemySeed == 0)
                    enemySeed = 1;

                HordeEnemyState enemyState = prefabEnemyState;
                float3 spawnPosition = spawnerData.SpawnPosition + spawnOffset;
                enemyState.MoveSpeed = moveSpeed;
                enemyState.DamageTimer = 0f;
                enemyState.WanderDirectionChangeTimer = random.NextFloat(prefabEnemy.WanderDirectionChangeMin, prefabEnemy.WanderDirectionChangeMax);
                enemyState.WanderDirection = new float3(math.cos(wanderAngle), 0f, math.sin(wanderAngle));
                enemyState.LastStablePosition = spawnPosition;
                enemyState.LastStableRotation = quaternion.identity;
                enemyState.RandomSeed = enemySeed;
                enemyState.IsWanderer = (byte)(isWanderer ? 1 : 0);
                enemyState.IsPlayerInsideDamageRange = 0;
                enemyState.HasAppliedEnterDamage = 0;
                enemyState.IsInitialized = 1;
                ecb.SetComponent(enemy, enemyState);
            }

            uint nextSeed = random.NextUInt();
            spawnerData.RandomSeed = nextSeed == 0 ? 1u : nextSeed;
            spawnerData.SpawnedCount += spawnCount;
            spawnerData.NextSpawnTime = elapsedTime + spawnerData.SpawnInterval;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

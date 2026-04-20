using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerCarPostPhysicsSystem))]
[UpdateBefore(typeof(HordeEnemyAssistSystem))]
public partial struct PlayerCarKillSystem : ISystem
{
    private ComponentLookup<HordeEnemy> enemyLookup;
    private ComponentLookup<HordeDead> deadLookup;
    private ComponentLookup<HordeEnemyState> enemyStateLookup;
    private ComponentLookup<PhysicsVelocity> velocityLookup;
    private ComponentLookup<LocalTransform> transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        enemyLookup = state.GetComponentLookup<HordeEnemy>(true);
        deadLookup = state.GetComponentLookup<HordeDead>(true);
        enemyStateLookup = state.GetComponentLookup<HordeEnemyState>(false);
        velocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        enemyLookup.Update(ref state);
        deadLookup.Update(ref state);
        enemyStateLookup.Update(ref state);
        velocityLookup.Update(ref state);
        transformLookup.Update(ref state);

        PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        HashSet<Entity> deathsThisStep = new HashSet<Entity>();
        bool hasWaveState = SystemAPI.HasSingleton<WaveSpawnerState>();
        RefRW<WaveSpawnerState> waveState = default;

        if (hasWaveState)
            waveState = SystemAPI.GetSingletonRW<WaveSpawnerState>();

        foreach (var (localTransform, carVelocity, carState, carCollider, car, carEntity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PlayerCarState>, RefRO<PhysicsCollider>, RefRO<PlayerCar>>().WithEntityAccess())
        {
            if (carState.ValueRO.IsDead != 0)
                continue;

            float3 carPlanarVelocity = carVelocity.ValueRO.Linear;
            carPlanarVelocity.y = 0f;

            if (carState.ValueRO.CurrentSpeed < car.ValueRO.KillSpeedThreshold || !carCollider.ValueRO.IsValid)
                continue;

            hits.Clear();
            physicsWorld.CalculateDistance(
                new ColliderDistanceInput(
                    carCollider.ValueRO.Value,
                    car.ValueRO.KillContactDistance,
                    new RigidTransform(localTransform.ValueRO.Rotation, localTransform.ValueRO.Position)),
                ref hits);

            for (int i = 0; i < hits.Length; i++)
            {
                Entity enemyEntity = hits[i].Entity;

                if (enemyEntity == carEntity || !enemyLookup.HasComponent(enemyEntity) || deadLookup.HasComponent(enemyEntity) || !velocityLookup.HasComponent(enemyEntity))
                    continue;

                if (!deathsThisStep.Add(enemyEntity))
                    continue;

                HordeEnemy enemy = enemyLookup[enemyEntity];
                float3 enemyPosition = transformLookup.HasComponent(enemyEntity) ? transformLookup[enemyEntity].Position : hits[i].Position;
                float3 impulseDirection = carPlanarVelocity;

                if (math.lengthsq(impulseDirection) < 0.0001f)
                {
                    impulseDirection = enemyPosition - localTransform.ValueRO.Position;
                    impulseDirection.y = 0f;
                }

                if (math.lengthsq(impulseDirection) < 0.0001f)
                    continue;

                impulseDirection = math.normalize(impulseDirection);

                PhysicsVelocity velocity = velocityLookup[enemyEntity];
                float impulseMultiplier = 1f;
                float3 randomExtraForce = float3.zero;

                if (enemyStateLookup.HasComponent(enemyEntity))
                {
                    HordeEnemyState enemyState = enemyStateLookup[enemyEntity];
                    uint seed = enemyState.RandomSeed == 0 ? 1u : enemyState.RandomSeed;
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
                    impulseMultiplier = random.NextFloat(0.7f, 1.2f);
                    randomExtraForce = new float3(
                        random.NextFloat(-10f, 10f),
                        random.NextFloat(-10f, 10f),
                        random.NextFloat(-10f, 10f));
                    enemyState.DamageTimer = 0f;
                    enemyState.IsPlayerInsideDamageRange = 0;
                    enemyState.HasAppliedEnterDamage = 0;

                    uint nextSeed = random.NextUInt();
                    enemyState.RandomSeed = nextSeed == 0 ? 1u : nextSeed;
                    enemyStateLookup[enemyEntity] = enemyState;
                }

                velocity.Linear += impulseDirection * (car.ValueRO.KillImpulse * impulseMultiplier) + new float3(0f, car.ValueRO.KillUpImpulse * impulseMultiplier, 0f) + randomExtraForce;
                velocity.Angular += new float3(
                    impulseDirection.z * car.ValueRO.KillSpinImpulse * impulseMultiplier,
                    0f,
                    -impulseDirection.x * car.ValueRO.KillSpinImpulse * impulseMultiplier);
                velocityLookup[enemyEntity] = velocity;

                if (HordeDeathPresenter.Instance != null)
                    HordeDeathPresenter.Instance.Play(hits[i].Position);

                float startScale = transformLookup.HasComponent(enemyEntity) ? transformLookup[enemyEntity].Scale : 1f;
                ecb.AddComponent(enemyEntity, new HordeDead
                {
                    ShrinkDelay = enemy.ShrinkDelay,
                    ShrinkDuration = enemy.ShrinkDuration,
                    StartScale = startScale
                });

                if (hasWaveState && waveState.ValueRO.IsLevelCompleted == 0)
                    waveState.ValueRW.KilledEnemyCount += 1;

                if (SoftManager.Instance != null)
                    SoftManager.Instance.Add(1);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        hits.Dispose();
    }
}

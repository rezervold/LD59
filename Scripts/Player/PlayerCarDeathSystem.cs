using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(HordePlayerDamageSystem))]
[UpdateBefore(typeof(HordeEnemyAssistSystem))]
public partial struct PlayerCarDeathSystem : ISystem
{
    private ComponentLookup<PhysicsGravityFactor> gravityFactorLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
        gravityFactorLookup = state.GetComponentLookup<PhysicsGravityFactor>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<WaveSpawnerState>())
        {
            WaveSpawnerState waveState = SystemAPI.GetSingleton<WaveSpawnerState>();

            if (waveState.IsLevelCompleted != 0)
                return;
        }

        gravityFactorLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (localTransform, velocity, car, carState, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<PlayerCar>, RefRW<PlayerCarState>>().WithEntityAccess())
        {
            if (carState.ValueRO.IsDead == 0 && carState.ValueRO.CurrentHealth <= 0f)
            {
                uint seed = carState.ValueRO.RandomSeed == 0 ? 1u : carState.ValueRO.RandomSeed;
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
                float angle = random.NextFloat(0f, math.PI * 2f);
                float horizontalForce = random.NextFloat(car.ValueRO.ExplosionHorizontalForceMin, car.ValueRO.ExplosionHorizontalForceMax);
                float upForce = random.NextFloat(car.ValueRO.ExplosionUpForceMin, car.ValueRO.ExplosionUpForceMax);
                float angularForce = random.NextFloat(car.ValueRO.ExplosionAngularForceMin, car.ValueRO.ExplosionAngularForceMax);
                float3 horizontal = new float3(math.cos(angle), 0f, math.sin(angle)) * horizontalForce;
                float3 angular = new float3(
                    random.NextFloat(-angularForce, angularForce),
                    random.NextFloat(-angularForce, angularForce),
                    random.NextFloat(-angularForce, angularForce));

                PhysicsVelocity currentVelocity = velocity.ValueRO;
                currentVelocity.Linear += horizontal + new float3(0f, upForce, 0f);
                currentVelocity.Angular += angular;
                velocity.ValueRW = currentVelocity;

                carState.ValueRW.IsDead = 1;
                carState.ValueRW.DeathTimer = 0f;
                carState.ValueRW.DamageCooldownTimer = 0f;
                carState.ValueRW.ThrottleInput = 0f;
                carState.ValueRW.SteerInput = 0f;

                if (gravityFactorLookup.HasComponent(entity))
                {
                    PhysicsGravityFactor gravityFactor = gravityFactorLookup[entity];
                    gravityFactor.Value = 1f;
                    gravityFactorLookup[entity] = gravityFactor;
                }

                uint nextSeed = random.NextUInt();
                carState.ValueRW.RandomSeed = nextSeed == 0 ? 1u : nextSeed;

                if (PlayerCarVfxPresenter.Instance != null)
                {
                    Vector3 position = new Vector3(localTransform.ValueRO.Position.x, localTransform.ValueRO.Position.y, localTransform.ValueRO.Position.z);
                    PlayerCarVfxPresenter.Instance.PlayExplosion(position);
                }
            }

            if (carState.ValueRO.IsDead == 0)
                continue;

            carState.ValueRW.DeathTimer += deltaTime;

            if (carState.ValueRO.DeathTimer < car.ValueRO.DeathRestartDelay)
                continue;

            if (SceneLoader.Instance != null)
            {
                if (!SceneLoader.Instance.IsLoading)
                    SceneLoader.Instance.ReloadCurrentScene();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            return;
        }
    }
}

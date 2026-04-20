using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerCarKillSystem))]
[UpdateBefore(typeof(HordeEnemyAssistSystem))]
public partial struct HordePlayerDamageSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
        state.RequireForUpdate<HordeEnemy>();
        state.RequireForUpdate<HordeEnemyState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (localTransform, car, carState) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerCar>, RefRW<PlayerCarState>>())
        {
            if (carState.ValueRO.IsDead != 0)
                return;

            float3 playerPosition = localTransform.ValueRO.Position;
            if (!IsFinite(playerPosition))
                return;

            float damageCooldownTimer = math.max(0f, carState.ValueRO.DamageCooldownTimer - deltaTime);
            float totalDamage = 0f;
            int invalidEnemies = 0;

            foreach (var (enemyTransform, enemy, enemyState) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<HordeEnemy>, RefRW<HordeEnemyState>>().WithNone<HordeDead>())
            {
                HordeEnemyState enemyStateData = enemyState.ValueRO;
                float3 enemyPosition = enemyTransform.ValueRO.Position;

                if (!IsFinite(enemyPosition))
                {
                    ResetDamageState(ref enemyStateData);
                    enemyState.ValueRW = enemyStateData;
                    invalidEnemies++;
                    continue;
                }

                if (enemy.ValueRO.DamageAmount <= 0f || enemy.ValueRO.DamageRange <= 0f)
                {
                    ResetDamageState(ref enemyStateData);
                    enemyState.ValueRW = enemyStateData;
                    continue;
                }

                float3 toPlayer = playerPosition - enemyPosition;
                toPlayer.y = 0f;
                float damageRangeSq = enemy.ValueRO.DamageRange * enemy.ValueRO.DamageRange;

                if (math.lengthsq(toPlayer) > damageRangeSq)
                {
                    ResetDamageState(ref enemyStateData);
                    enemyState.ValueRW = enemyStateData;
                    continue;
                }

                if (enemyStateData.IsPlayerInsideDamageRange == 0)
                {
                    enemyStateData.IsPlayerInsideDamageRange = 1;
                    enemyStateData.HasAppliedEnterDamage = 0;
                    enemyStateData.DamageTimer = 0f;
                }

                float interval = enemyStateData.HasAppliedEnterDamage == 0
                    ? math.max(0.05f, enemy.ValueRO.PlayerEnteredRangeDamageInterval)
                    : math.max(0.05f, enemy.ValueRO.DamageInterval);
                float timer = enemyStateData.DamageTimer + deltaTime;

                if (damageCooldownTimer > 0f)
                {
                    if (timer >= interval)
                        timer = interval;

                    enemyStateData.DamageTimer = timer;
                    enemyState.ValueRW = enemyStateData;
                    continue;
                }

                while (timer >= interval)
                {
                    timer -= interval;
                    totalDamage += enemy.ValueRO.DamageAmount;
                    enemyStateData.HasAppliedEnterDamage = 1;
                    interval = math.max(0.05f, enemy.ValueRO.DamageInterval);
                }

                enemyStateData.DamageTimer = timer;
                enemyState.ValueRW = enemyStateData;
            }

            carState.ValueRW.DamageCooldownTimer = damageCooldownTimer;

            if (totalDamage <= 0f)
                return;

            carState.ValueRW.CurrentHealth = math.max(0f, carState.ValueRO.CurrentHealth - totalDamage);
            carState.ValueRW.DamageCooldownTimer = car.ValueRO.DamageCooldown;

#if UNITY_EDITOR
            if (invalidEnemies > 0)
                Debug.LogWarning($"Damage system skipped {invalidEnemies} invalid enemies this tick.");
#endif

            if (PlayerHealthPresenter.Instance != null)
                PlayerHealthPresenter.Instance.ApplyDamage(carState.ValueRW.CurrentHealth, car.ValueRO.MaxHealth);

            return;
        }
    }

    private static void ResetDamageState(ref HordeEnemyState enemyState)
    {
        enemyState.DamageTimer = 0f;
        enemyState.IsPlayerInsideDamageRange = 0;
        enemyState.HasAppliedEnterDamage = 0;
    }

    private static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}

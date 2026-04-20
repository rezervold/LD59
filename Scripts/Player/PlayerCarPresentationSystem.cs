using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerCarPostPhysicsSystem))]
[UpdateBefore(typeof(PlayerCarKillSystem))]
public partial struct PlayerCarPresentationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (localTransform, car, carState) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerCar>, RefRO<PlayerCarState>>())
        {
            if (PlayerHealthPresenter.Instance != null)
                PlayerHealthPresenter.Instance.SetHealth(carState.ValueRO.CurrentHealth, car.ValueRO.MaxHealth);

            if (PlayerCarVfxPresenter.Instance != null)
            {
                float healthNormalized = car.ValueRO.MaxHealth <= 0f ? 0f : carState.ValueRO.CurrentHealth / car.ValueRO.MaxHealth;
                bool smokeActive = carState.ValueRO.IsDead == 0 && healthNormalized > 0f && healthNormalized <= car.ValueRO.SmokeHealthThreshold;
                Vector3 position = new Vector3(localTransform.ValueRO.Position.x, localTransform.ValueRO.Position.y, localTransform.ValueRO.Position.z);
                PlayerCarVfxPresenter.Instance.SetState(position, smokeActive);
            }

            return;
        }
    }
}

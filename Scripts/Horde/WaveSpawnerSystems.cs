using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(HordeSpawnSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct WaveSpawnerSetupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveSpawnerState>();
        state.RequireForUpdate<WaveSpawnerLevel>();
        state.RequireForUpdate<WaveSpawnerEntry>();
    }

    public void OnUpdate(ref SystemState state)
    {
        RefRW<WaveSpawnerState> waveState = SystemAPI.GetSingletonRW<WaveSpawnerState>();

        if (waveState.ValueRO.IsInitialized != 0)
            return;

        Entity waveEntity = SystemAPI.GetSingletonEntity<WaveSpawnerState>();
        DynamicBuffer<WaveSpawnerLevel> levels = state.EntityManager.GetBuffer<WaveSpawnerLevel>(waveEntity);
        DynamicBuffer<WaveSpawnerEntry> entries = state.EntityManager.GetBuffer<WaveSpawnerEntry>(waveEntity);

        if (levels.Length == 0)
            return;

        int levelIndex = GameManager.Instance != null ? GameManager.Instance.GetWrappedLevelIndex(levels.Length) : 0;

        WaveSpawnerLevel level = levels[levelIndex];
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        for (int i = 0; i < level.EntryCount; i++)
        {
            WaveSpawnerEntry entry = entries[level.StartIndex + i];

            if (!state.EntityManager.HasComponent<HordeSpawner>(entry.SpawnerEntity))
                continue;

            HordeSpawner spawner = state.EntityManager.GetComponentData<HordeSpawner>(entry.SpawnerEntity);
            spawner.IsActive = 1;
            spawner.SpawnedCount = 0;
            spawner.NextSpawnTime = elapsedTime + spawner.StartDelay + math.max(0f, entry.Delay);
            state.EntityManager.SetComponentData(entry.SpawnerEntity, spawner);
        }

        int requiredKillCount = 0;

        if (level.TotalEnemyCount > 0)
            requiredKillCount = math.clamp((int)math.ceil(level.TotalEnemyCount * waveState.ValueRO.CompletionThreshold), 1, level.TotalEnemyCount);

        waveState.ValueRW.ActiveLevelIndex = levelIndex;
        waveState.ValueRW.TotalEnemyCount = level.TotalEnemyCount;
        waveState.ValueRW.KilledEnemyCount = 0;
        waveState.ValueRW.RequiredKillCount = requiredKillCount;
        waveState.ValueRW.IsInitialized = 1;
        waveState.ValueRW.IsLevelCompleted = 0;
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerCarKillSystem))]
[UpdateBefore(typeof(PlayerCarDeathSystem))]
public partial struct WaveSpawnerCompletionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveSpawnerState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        RefRW<WaveSpawnerState> waveState = SystemAPI.GetSingletonRW<WaveSpawnerState>();

        if (waveState.ValueRO.IsInitialized == 0 || waveState.ValueRO.IsLevelCompleted != 0)
            return;

        if (waveState.ValueRO.RequiredKillCount <= 0)
            return;

        if (waveState.ValueRO.KilledEnemyCount < waveState.ValueRO.RequiredKillCount)
            return;

        waveState.ValueRW.IsLevelCompleted = 1;

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteCurrentLevel();
    }
}

using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct WaveSpawnerState : IComponentData
{
    public int ActiveLevelIndex;
    public int TotalEnemyCount;
    public int KilledEnemyCount;
    public int RequiredKillCount;
    public float CompletionThreshold;
    public byte IsInitialized;
    public byte IsLevelCompleted;
}

public struct WaveSpawnerLevel : IBufferElementData
{
    public int StartIndex;
    public int EntryCount;
    public int TotalEnemyCount;
}

public struct WaveSpawnerEntry : IBufferElementData
{
    public Entity SpawnerEntity;
    public float Delay;
}

[Serializable]
public class WaveSpawnerEntryAuthoring
{
    public HordeSpawnerAuthoring spawner;
    public float delay;
}

[Serializable]
public class WaveSpawnerLevelAuthoring
{
    public WaveSpawnerEntryAuthoring[] spawners = Array.Empty<WaveSpawnerEntryAuthoring>();
}

[DisallowMultipleComponent]
public class WaveSpawnerAuthoring : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float completionThreshold = 0.93f;
    [SerializeField] private WaveSpawnerLevelAuthoring[] levels = Array.Empty<WaveSpawnerLevelAuthoring>();

    public float CompletionThreshold => completionThreshold;
    public WaveSpawnerLevelAuthoring[] Levels => levels;
}

public class WaveSpawnerAuthoringBaker : Baker<WaveSpawnerAuthoring>
{
    public override void Bake(WaveSpawnerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new WaveSpawnerState
        {
            ActiveLevelIndex = -1,
            CompletionThreshold = math.saturate(authoring.CompletionThreshold)
        });

        DynamicBuffer<WaveSpawnerLevel> levelBuffer = AddBuffer<WaveSpawnerLevel>(entity);
        DynamicBuffer<WaveSpawnerEntry> entryBuffer = AddBuffer<WaveSpawnerEntry>(entity);
        WaveSpawnerLevelAuthoring[] levels = authoring.Levels ?? Array.Empty<WaveSpawnerLevelAuthoring>();
        int startIndex = 0;

        for (int i = 0; i < levels.Length; i++)
        {
            WaveSpawnerLevelAuthoring level = levels[i];
            WaveSpawnerEntryAuthoring[] spawners = level != null && level.spawners != null ? level.spawners : Array.Empty<WaveSpawnerEntryAuthoring>();
            int entryCount = 0;
            int totalEnemyCount = 0;

            for (int j = 0; j < spawners.Length; j++)
            {
                WaveSpawnerEntryAuthoring entry = spawners[j];

                if (entry == null || entry.spawner == null)
                    continue;

                Entity spawnerEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                Vector3 position = entry.spawner.transform.position;
                uint seed = math.hash(new int4(
                    (int)math.round(position.x * 100f) ^ (i + 1),
                    (int)math.round(position.y * 100f) ^ ((j + 1) << 8),
                    (int)math.round(position.z * 100f) ^ ((i + 1) << 16),
                    entry.spawner.TotalCount ^ (entry.spawner.BatchSize << 16) ^ ((j + 1) << 24)));

                if (seed == 0)
                    seed = 1;

                AddComponent(spawnerEntity, new HordeSpawner
                {
                    EnemyPrefab = GetEntity(entry.spawner.EnemyPrefab, TransformUsageFlags.Dynamic),
                    SpawnPosition = new float3(position.x, position.y, position.z),
                    SpawnExtents = new float3(entry.spawner.SpawnExtents.x, entry.spawner.SpawnExtents.y, entry.spawner.SpawnExtents.z),
                    SpawnInterval = math.max(0f, entry.spawner.SpawnInterval),
                    StartDelay = math.max(0f, entry.spawner.InitialDelay),
                    NextSpawnTime = double.MaxValue,
                    TotalCount = math.max(0, entry.spawner.TotalCount),
                    BatchSize = math.max(1, entry.spawner.BatchSize),
                    RandomSeed = seed,
                    IsActive = 0
                });

                entryBuffer.Add(new WaveSpawnerEntry
                {
                    SpawnerEntity = spawnerEntity,
                    Delay = math.max(0f, entry.delay)
                });
                entryCount += 1;
                totalEnemyCount += math.max(0, entry.spawner.TotalCount);
            }

            levelBuffer.Add(new WaveSpawnerLevel
            {
                StartIndex = startIndex,
                EntryCount = entryCount,
                TotalEnemyCount = totalEnemyCount
            });

            startIndex += entryCount;
        }
    }
}

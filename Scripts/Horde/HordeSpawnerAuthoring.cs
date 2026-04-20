using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct HordeSpawner : IComponentData
{
    public Entity EnemyPrefab;
    public float3 SpawnPosition;
    public float3 SpawnExtents;
    public float SpawnInterval;
    public float StartDelay;
    public double NextSpawnTime;
    public int TotalCount;
    public int SpawnedCount;
    public int BatchSize;
    public uint RandomSeed;
    public byte IsActive;
}

[DisallowMultipleComponent]
public class HordeSpawnerAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int totalCount = 40;
    [SerializeField] private int batchSize = 4;
    [SerializeField] private float spawnInterval = 0.1f;
    [SerializeField] private Vector3 spawnExtents = new Vector3(5f, 0.25f, 5f);
    [SerializeField] private float initialDelay;
    [SerializeField, HideInInspector] private float spawnRadius = 5f;
    [SerializeField, HideInInspector] private float spawnHeight = 0.25f;
    [SerializeField, HideInInspector] private bool spawnExtentsInitialized;

    public GameObject EnemyPrefab => enemyPrefab;
    public int TotalCount => totalCount;
    public int BatchSize => batchSize;
    public float SpawnInterval => spawnInterval;
    public Vector3 SpawnExtents
    {
        get
        {
            if (!spawnExtentsInitialized)
                return new Vector3(Mathf.Max(0f, spawnRadius), Mathf.Max(0f, spawnHeight), Mathf.Max(0f, spawnRadius));

            return spawnExtents;
        }
    }
    public float InitialDelay => initialDelay;

    private void OnValidate()
    {
        if (!spawnExtentsInitialized)
        {
            spawnExtents = new Vector3(Mathf.Max(0f, spawnRadius), spawnHeight, Mathf.Max(0f, spawnRadius));
            spawnExtentsInitialized = true;
        }

        spawnExtents.x = Mathf.Max(0f, spawnExtents.x);
        spawnExtents.y = Mathf.Max(0f, spawnExtents.y);
        spawnExtents.z = Mathf.Max(0f, spawnExtents.z);
    }
}

public class HordeSpawnerAuthoringBaker : Baker<HordeSpawnerAuthoring>
{
    public override void Bake(HordeSpawnerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        Vector3 position = authoring.transform.position;
        uint seed = math.hash(new int4(
            (int)math.round(position.x * 100f),
            (int)math.round(position.y * 100f),
            (int)math.round(position.z * 100f),
            authoring.TotalCount ^ (authoring.BatchSize << 16)));

        if (seed == 0)
            seed = 1;

        AddComponent(entity, new HordeSpawner
        {
            EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
            SpawnPosition = new float3(position.x, position.y, position.z),
            SpawnExtents = new float3(authoring.SpawnExtents.x, authoring.SpawnExtents.y, authoring.SpawnExtents.z),
            SpawnInterval = math.max(0f, authoring.SpawnInterval),
            StartDelay = math.max(0f, authoring.InitialDelay),
            NextSpawnTime = double.MaxValue,
            TotalCount = math.max(0, authoring.TotalCount),
            BatchSize = math.max(1, authoring.BatchSize),
            RandomSeed = seed,
            IsActive = 0
        });
    }
}

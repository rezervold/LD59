using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public static class HordeCombatBridge
{
    private static World cachedWorld;
    private static bool queriesInitialized;
    private static EntityQuery physicsWorldQuery;
    private static EntityQuery playerQuery;
    private static EntityQuery waveStateQuery;

    public static bool IsPlayerAlive()
    {
        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (playerQuery.IsEmptyIgnoreFilter)
            return false;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        PlayerCarState playerState = entityManager.GetComponentData<PlayerCarState>(playerEntity);
        return playerState.IsDead == 0 && playerState.CurrentHealth > 0f;
    }

    public static bool TryGetPlayerPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (playerQuery.IsEmptyIgnoreFilter)
            return false;

        Entity playerEntity = playerQuery.GetSingletonEntity();

        if (!entityManager.HasComponent<LocalTransform>(playerEntity))
            return false;

        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        position = new Vector3(localTransform.Position.x, localTransform.Position.y, localTransform.Position.z);
        rotation = new Quaternion(localTransform.Rotation.value.x, localTransform.Rotation.value.y, localTransform.Rotation.value.z, localTransform.Rotation.value.w);
        return true;
    }

    public static bool TryRaycast(Vector3 origin, Vector3 direction, float maxDistance, out Entity hitEntity, out Vector3 hitPoint)
    {
        hitEntity = Entity.Null;
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        hitPoint = origin + normalizedDirection * Mathf.Max(0f, maxDistance);

        if (!TryGetPhysicsWorld(out PhysicsWorldSingleton physicsWorldSingleton))
            return false;

        RaycastInput raycastInput = new RaycastInput
        {
            Start = new float3(origin.x, origin.y, origin.z),
            End = new float3(hitPoint.x, hitPoint.y, hitPoint.z),
            Filter = CollisionFilter.Default
        };

        if (!physicsWorldSingleton.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
            return false;

        hitEntity = hit.Entity;
        hitPoint = new Vector3(hit.Position.x, hit.Position.y, hit.Position.z);
        return true;
    }

    public static bool TryOverlapEnemyAlongSegment(Vector3 startPoint, Vector3 endPoint, float radius, out Entity hitEntity, out Vector3 hitPoint)
    {
        hitEntity = Entity.Null;
        hitPoint = endPoint;

        if (!TryGetPhysicsWorld(out PhysicsWorldSingleton physicsWorldSingleton))
            return false;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        bool hasHit = physicsWorldSingleton.OverlapCapsule(
            new float3(startPoint.x, startPoint.y, startPoint.z),
            new float3(endPoint.x, endPoint.y, endPoint.z),
            Mathf.Max(0.01f, radius),
            ref hits,
            CollisionFilter.Default);

        if (!hasHit)
        {
            hits.Dispose();
            return false;
        }

        float closestDistanceSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Entity enemyEntity = hits[i].Entity;

            if (!entityManager.Exists(enemyEntity) ||
                !entityManager.HasComponent<HordeEnemy>(enemyEntity) ||
                entityManager.HasComponent<HordeDead>(enemyEntity))
                continue;

            float distanceSq = math.lengthsq(hits[i].Position - new float3(startPoint.x, startPoint.y, startPoint.z));

            if (distanceSq >= closestDistanceSq)
                continue;

            closestDistanceSq = distanceSq;
            hitEntity = enemyEntity;
            hitPoint = new Vector3(hits[i].Position.x, hits[i].Position.y, hits[i].Position.z);
        }

        hits.Dispose();
        return hitEntity != Entity.Null;
    }

    public static bool TryKillEnemy(Entity enemyEntity, Vector3 hitPosition, Vector3 impulseDirection, float impulse, float upImpulse, float spinImpulse, float randomForce)
    {
        if (enemyEntity == Entity.Null || !TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (!entityManager.Exists(enemyEntity))
            return false;

        if (!entityManager.HasComponent<HordeEnemy>(enemyEntity) ||
            !entityManager.HasComponent<PhysicsVelocity>(enemyEntity) ||
            !entityManager.HasComponent<LocalTransform>(enemyEntity) ||
            entityManager.HasComponent<HordeDead>(enemyEntity))
            return false;

        HordeEnemy enemy = entityManager.GetComponentData<HordeEnemy>(enemyEntity);
        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        PhysicsVelocity velocity = entityManager.GetComponentData<PhysicsVelocity>(enemyEntity);
        float3 direction = new float3(impulseDirection.x, 0f, impulseDirection.z);

        if (math.lengthsq(direction) < 0.0001f)
        {
            float3 fallbackDirection = localTransform.Position - new float3(hitPosition.x, hitPosition.y, hitPosition.z);
            fallbackDirection.y = 0f;
            direction = fallbackDirection;
        }

        if (math.lengthsq(direction) < 0.0001f)
            direction = new float3(0f, 0f, 1f);
        else
            direction = math.normalize(direction);

        float3 randomExtraForce = float3.zero;

        if (math.abs(randomForce) > 0.0001f)
        {
            uint seed = (uint)(Time.frameCount + enemyEntity.Index + 1);

            if (entityManager.HasComponent<HordeEnemyState>(enemyEntity))
            {
                HordeEnemyState enemyState = entityManager.GetComponentData<HordeEnemyState>(enemyEntity);
                enemyState.DamageTimer = 0f;
                enemyState.IsPlayerInsideDamageRange = 0;
                enemyState.HasAppliedEnterDamage = 0;
                seed = enemyState.RandomSeed == 0 ? seed : enemyState.RandomSeed;
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
                randomExtraForce = new float3(
                    random.NextFloat(-randomForce, randomForce),
                    random.NextFloat(-randomForce, randomForce),
                    random.NextFloat(-randomForce, randomForce));
                uint nextSeed = random.NextUInt();
                enemyState.RandomSeed = nextSeed == 0 ? 1u : nextSeed;
                entityManager.SetComponentData(enemyEntity, enemyState);
            }
            else
            {
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
                randomExtraForce = new float3(
                    random.NextFloat(-randomForce, randomForce),
                    random.NextFloat(-randomForce, randomForce),
                    random.NextFloat(-randomForce, randomForce));
            }
        }
        else if (entityManager.HasComponent<HordeEnemyState>(enemyEntity))
        {
            HordeEnemyState enemyState = entityManager.GetComponentData<HordeEnemyState>(enemyEntity);
            enemyState.DamageTimer = 0f;
            enemyState.IsPlayerInsideDamageRange = 0;
            enemyState.HasAppliedEnterDamage = 0;
            entityManager.SetComponentData(enemyEntity, enemyState);
        }

        velocity.Linear += direction * impulse + new float3(0f, upImpulse, 0f) + randomExtraForce;
        velocity.Angular += new float3(direction.z * spinImpulse, 0f, -direction.x * spinImpulse);
        entityManager.SetComponentData(enemyEntity, velocity);

        if (HordeDeathPresenter.Instance != null)
            HordeDeathPresenter.Instance.Play(hitPosition);

        entityManager.AddComponentData(enemyEntity, new HordeDead
        {
            ShrinkDelay = enemy.ShrinkDelay,
            ShrinkDuration = enemy.ShrinkDuration,
            StartScale = localTransform.Scale
        });

        if (!waveStateQuery.IsEmptyIgnoreFilter)
        {
            Entity waveEntity = waveStateQuery.GetSingletonEntity();
            WaveSpawnerState waveState = entityManager.GetComponentData<WaveSpawnerState>(waveEntity);

            if (waveState.IsLevelCompleted == 0)
            {
                waveState.KilledEnemyCount += 1;
                entityManager.SetComponentData(waveEntity, waveState);
            }
        }

        if (SoftManager.Instance != null)
            SoftManager.Instance.Add(1);

        return true;
    }

    private static bool TryGetPhysicsWorld(out PhysicsWorldSingleton physicsWorldSingleton)
    {
        physicsWorldSingleton = default;

        if (!TryGetEntityManager(out EntityManager entityManager))
            return false;

        if (physicsWorldQuery.IsEmptyIgnoreFilter)
            return false;

        physicsWorldSingleton = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();
        return true;
    }

    private static bool TryGetEntityManager(out EntityManager entityManager)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            entityManager = default;
            return false;
        }

        EnsureQueries(world);
        entityManager = world.EntityManager;
        return true;
    }

    private static void EnsureQueries(World world)
    {
        if (queriesInitialized && cachedWorld == world)
            return;

        cachedWorld = world;
        physicsWorldQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
        playerQuery = world.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerCar>(),
            ComponentType.ReadOnly<PlayerCarState>());
        waveStateQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WaveSpawnerState>());
        queriesInitialized = true;
    }
}

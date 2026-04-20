using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct HordeEnemy : IComponentData
{
    public float MoveForce;
    public float MinSpeed;
    public float MaxSpeed;
    public float WanderChance;
    public float WanderSpeedMult;
    public float WanderDirectionChangeMin;
    public float WanderDirectionChangeMax;
    public float DamageRange;
    public float PlayerEnteredRangeDamageInterval;
    public float DamageInterval;
    public float DamageAmount;
    public float UprightSharpness;
    public float TiltDamping;
    public float ShrinkDelay;
    public float ShrinkDuration;
}

public struct HordeEnemyState : IComponentData
{
    public float MoveSpeed;
    public float DamageTimer;
    public float WanderDirectionChangeTimer;
    public float3 WanderDirection;
    public float3 LastStablePosition;
    public quaternion LastStableRotation;
    public uint RandomSeed;
    public byte IsWanderer;
    public byte IsPlayerInsideDamageRange;
    public byte HasAppliedEnterDamage;
    public byte IsInitialized;
}

[DisallowMultipleComponent]
public class HordeEnemyAuthoring : MonoBehaviour
{
    [SerializeField] private float moveForce = 30f;
    [SerializeField] private float minSpeed = 4f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float wanderChance = 0.05f;
    [SerializeField] private float wanderSpeedMult = 0.7f;
    [SerializeField] private float wanderDirectionChangeMin = 1f;
    [SerializeField] private float wanderDirectionChangeMax = 5f;
    [SerializeField] private float damageRange = 1.6f;
    [SerializeField] private float playerEnteredRangeDamageInterval = 0.45f;
    [SerializeField] private float damageInterval = 1f;
    [SerializeField] private float damageAmount = 5f;
    [SerializeField] private float uprightSharpness = 10f;
    [SerializeField] private float tiltDamping = 10f;
    [SerializeField] private float shrinkDelay = 2f;
    [SerializeField] private float shrinkDuration = 0.35f;
    [SerializeField] private Color deathTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    public float MoveForce => moveForce;
    public float MinSpeed => minSpeed;
    public float MaxSpeed => maxSpeed;
    public float WanderChance => wanderChance;
    public float WanderSpeedMult => wanderSpeedMult;
    public float WanderDirectionChangeMin => wanderDirectionChangeMin;
    public float WanderDirectionChangeMax => wanderDirectionChangeMax;
    public float DamageRange => damageRange;
    public float PlayerEnteredRangeDamageInterval => playerEnteredRangeDamageInterval;
    public float DamageInterval => damageInterval;
    public float DamageAmount => damageAmount;
    public float UprightSharpness => uprightSharpness;
    public float TiltDamping => tiltDamping;
    public float ShrinkDelay => shrinkDelay;
    public float ShrinkDuration => shrinkDuration;
    public Color DeathTint => deathTint;
}

public class HordeEnemyAuthoringBaker : Baker<HordeEnemyAuthoring>
{
    public override void Bake(HordeEnemyAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        float minSpeed = math.max(0f, math.min(authoring.MinSpeed, authoring.MaxSpeed));
        float maxSpeed = math.max(minSpeed, math.max(authoring.MinSpeed, authoring.MaxSpeed));
        float wanderDirectionChangeMin = math.max(0.1f, authoring.WanderDirectionChangeMin);
        float wanderDirectionChangeMax = math.max(wanderDirectionChangeMin, authoring.WanderDirectionChangeMax);

        AddComponent(entity, new HordeEnemy
        {
            MoveForce = authoring.MoveForce,
            MinSpeed = minSpeed,
            MaxSpeed = maxSpeed,
            WanderChance = math.saturate(authoring.WanderChance),
            WanderSpeedMult = math.max(0f, authoring.WanderSpeedMult),
            WanderDirectionChangeMin = wanderDirectionChangeMin,
            WanderDirectionChangeMax = wanderDirectionChangeMax,
            DamageRange = math.max(0f, authoring.DamageRange),
            PlayerEnteredRangeDamageInterval = math.max(0.05f, authoring.PlayerEnteredRangeDamageInterval),
            DamageInterval = math.max(0.05f, authoring.DamageInterval),
            DamageAmount = math.max(0f, authoring.DamageAmount),
            UprightSharpness = authoring.UprightSharpness,
            TiltDamping = authoring.TiltDamping,
            ShrinkDelay = authoring.ShrinkDelay,
            ShrinkDuration = authoring.ShrinkDuration
        });
        AddComponent(entity, new HordeEnemyState
        {
            MoveSpeed = maxSpeed,
            WanderDirection = new float3(0f, 0f, 1f),
            RandomSeed = 1
        });

        BakeDeathVisuals(authoring, entity);
    }

    private void BakeDeathVisuals(HordeEnemyAuthoring authoring, Entity enemyEntity)
    {
        MeshRenderer[] renderers = authoring.GetComponentsInChildren<MeshRenderer>(true);
        HashSet<Entity> bakedVisualEntities = new HashSet<Entity>();
        float4 deathColor = ToFloat4(authoring.DeathTint.linear);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            Entity renderEntity = GetEntity(renderer.gameObject, TransformUsageFlags.Dynamic);

            if (!bakedVisualEntities.Add(renderEntity))
                continue;

            byte colorPropertyMask = GetDeathColorPropertyMask(renderer.sharedMaterials);
            if (colorPropertyMask == 0)
                colorPropertyMask = HordeEnemyDeathVisual.BaseColorProperty | HordeEnemyDeathVisual.ColorProperty;

            Entity deathVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, $"DeathVisual {i}");
            AddComponent(deathVisualEntity, new HordeEnemyDeathVisual
            {
                EnemyEntity = enemyEntity,
                RenderEntity = renderEntity,
                DeadColor = deathColor,
                ColorPropertyMask = colorPropertyMask
            });
        }
    }

    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    private static byte GetDeathColorPropertyMask(Material[] materials)
    {
        byte mask = 0;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
                mask |= HordeEnemyDeathVisual.BaseColorProperty;

            if (material.HasProperty("_Color"))
                mask |= HordeEnemyDeathVisual.ColorProperty;
        }

        return mask;
    }
}

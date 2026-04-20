using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct PlayerCarWheelVisualReference
{
    [SerializeField] private Transform pivot;
    [SerializeField] private Transform model;
    [SerializeField] private bool isFront;

    public Transform Pivot => pivot;
    public Transform Model => model;
    public bool IsFront => isFront;
}

public struct PlayerCar : IComponentData
{
    public float ForwardAcceleration;
    public float ReverseAcceleration;
    public float BrakeAcceleration;
    public float CoastingDrag;
    public float MaxForwardSpeed;
    public float MaxReverseSpeed;
    public float TurnSpeed;
    public float MinTurnSpeed;
    public float LateralDamping;
    public float Grip;
    public float FrontGrip;
    public float RearGrip;
    public float TractionGrip;
    public float SlipGripLoss;
    public float WheelBase;
    public float MaxSteerAngle;
    public float YawAcceleration;
    public float YawDamping;
    public float MaxYawSpeed;
    public float SlipYawInfluence;
    public float MaxHealth;
    public float DamageCooldown;
    public float SmokeHealthThreshold;
    public float DeathRestartDelay;
    public float ExplosionUpForceMin;
    public float ExplosionUpForceMax;
    public float ExplosionHorizontalForceMin;
    public float ExplosionHorizontalForceMax;
    public float ExplosionAngularForceMin;
    public float ExplosionAngularForceMax;
    public float KillSpeedThreshold;
    public float KillContactDistance;
    public float KillBoxForwardOffset;
    public float3 KillBoxHalfExtents;
    public float KillImpulse;
    public float KillUpImpulse;
    public float KillSpinImpulse;
}

public struct PlayerCarState : IComponentData
{
    public float CurrentSpeed;
    public float ForwardSpeed;
    public float SideSpeed;
    public float YawSpeed;
    public float SlipAmount;
    public float ThrottleInput;
    public float SteerInput;
    public float CurrentHealth;
    public float DamageCooldownTimer;
    public float DeathTimer;
    public float LockedHeight;
    public float3 LastStablePosition;
    public quaternion LastStableRotation;
    public uint RandomSeed;
    public byte IsDead;
    public byte IsInitialized;
}

[DisallowMultipleComponent]
public class PlayerCarAuthoring : MonoBehaviour
{
    [SerializeField] private float forwardAcceleration = 32f;
    [SerializeField] private float reverseAcceleration = 18f;
    [SerializeField] private float brakeAcceleration = 42f;
    [SerializeField] private float coastingDrag = 14f;
    [SerializeField] private float maxForwardSpeed = 18f;
    [SerializeField] private float maxReverseSpeed = 7f;
    [SerializeField] private float turnSpeed = 2.75f;
    [SerializeField] private float minTurnSpeed = 1.25f;
    [SerializeField] private float lateralDamping = 12f;
    [SerializeField] private float grip = 1f;
    [SerializeField] private float frontGrip = 1.1f;
    [SerializeField] private float rearGrip = 0.82f;
    [SerializeField] private float tractionGrip = 1f;
    [SerializeField] private float slipGripLoss = 1.45f;
    [SerializeField] private float wheelBase = 2.45f;
    [SerializeField] private float maxSteerAngle = 32f;
    [SerializeField] private float yawAcceleration = 11f;
    [SerializeField] private float yawDamping = 8f;
    [SerializeField] private float maxYawSpeed = 5.5f;
    [SerializeField] private float slipYawInfluence = 1.4f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damageCooldown = 0.5f;
    [SerializeField] private float smokeHealthThreshold = 0.3f;
    [SerializeField] private float deathRestartDelay = 3f;
    [SerializeField] private float explosionUpForceMin = 10f;
    [SerializeField] private float explosionUpForceMax = 16f;
    [SerializeField] private float explosionHorizontalForceMin = 2f;
    [SerializeField] private float explosionHorizontalForceMax = 8f;
    [SerializeField] private float explosionAngularForceMin = 3f;
    [SerializeField] private float explosionAngularForceMax = 9f;
    [SerializeField] private float killSpeedThreshold = 8f;
    [SerializeField] private float killContactDistance = 0.35f;
    [SerializeField] private float killBoxForwardOffset = 1.3f;
    [SerializeField] private Vector3 killBoxHalfExtents = new Vector3(1.1f, 0.7f, 1.8f);
    [SerializeField] private float killImpulse = 13f;
    [SerializeField] private float killUpImpulse = 2.5f;
    [SerializeField] private float killSpinImpulse = 9f;
    [SerializeField] private float visualWheelRadius = 0.72f;
    [SerializeField] private float visualWheelSpinMultiplier = 1f;
    [SerializeField] private Vector3 visualWheelSpinAxis = Vector3.right;
    [SerializeField] private float visualFrontWheelMaxSteerAngle = 38f;
    [SerializeField] private float visualWheelSteerSharpness = 12f;
    [SerializeField] private PlayerCarWheelVisualReference[] wheelVisuals = Array.Empty<PlayerCarWheelVisualReference>();

    public float ForwardAcceleration => forwardAcceleration;
    public float ReverseAcceleration => reverseAcceleration;
    public float BrakeAcceleration => brakeAcceleration;
    public float CoastingDrag => coastingDrag;
    public float MaxForwardSpeed => maxForwardSpeed;
    public float MaxReverseSpeed => maxReverseSpeed;
    public float TurnSpeed => turnSpeed;
    public float MinTurnSpeed => minTurnSpeed;
    public float LateralDamping => lateralDamping;
    public float Grip => grip;
    public float FrontGrip => frontGrip;
    public float RearGrip => rearGrip;
    public float TractionGrip => tractionGrip;
    public float SlipGripLoss => slipGripLoss;
    public float WheelBase => wheelBase;
    public float MaxSteerAngle => maxSteerAngle;
    public float YawAcceleration => yawAcceleration;
    public float YawDamping => yawDamping;
    public float MaxYawSpeed => maxYawSpeed;
    public float SlipYawInfluence => slipYawInfluence;
    public float MaxHealth => maxHealth;
    public float DamageCooldown => damageCooldown;
    public float SmokeHealthThreshold => smokeHealthThreshold;
    public float DeathRestartDelay => deathRestartDelay;
    public float ExplosionUpForceMin => explosionUpForceMin;
    public float ExplosionUpForceMax => explosionUpForceMax;
    public float ExplosionHorizontalForceMin => explosionHorizontalForceMin;
    public float ExplosionHorizontalForceMax => explosionHorizontalForceMax;
    public float ExplosionAngularForceMin => explosionAngularForceMin;
    public float ExplosionAngularForceMax => explosionAngularForceMax;
    public float KillSpeedThreshold => killSpeedThreshold;
    public float KillContactDistance => killContactDistance;
    public float KillBoxForwardOffset => killBoxForwardOffset;
    public Vector3 KillBoxHalfExtents => killBoxHalfExtents;
    public float KillImpulse => killImpulse;
    public float KillUpImpulse => killUpImpulse;
    public float KillSpinImpulse => killSpinImpulse;
    public float VisualWheelRadius => visualWheelRadius;
    public float VisualWheelSpinMultiplier => visualWheelSpinMultiplier;
    public Vector3 VisualWheelSpinAxis => visualWheelSpinAxis;
    public float VisualFrontWheelMaxSteerAngle => visualFrontWheelMaxSteerAngle;
    public float VisualWheelSteerSharpness => visualWheelSteerSharpness;
    public PlayerCarWheelVisualReference[] WheelVisuals => wheelVisuals;
}

public class PlayerCarAuthoringBaker : Baker<PlayerCarAuthoring>
{
    public override void Bake(PlayerCarAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        Vector3 position = authoring.transform.position;
        uint seed = math.hash(new int4(
            (int)math.round(position.x * 100f),
            (int)math.round(position.y * 100f),
            (int)math.round(position.z * 100f),
            (int)math.round(authoring.MaxHealth * 100f)));
        if (seed == 0)
            seed = 1;

        AddComponent(entity, new PlayerCar
        {
            ForwardAcceleration = authoring.ForwardAcceleration,
            ReverseAcceleration = authoring.ReverseAcceleration,
            BrakeAcceleration = authoring.BrakeAcceleration,
            CoastingDrag = authoring.CoastingDrag,
            MaxForwardSpeed = authoring.MaxForwardSpeed,
            MaxReverseSpeed = authoring.MaxReverseSpeed,
            TurnSpeed = authoring.TurnSpeed,
            MinTurnSpeed = authoring.MinTurnSpeed,
            LateralDamping = authoring.LateralDamping,
            Grip = authoring.Grip,
            FrontGrip = authoring.FrontGrip,
            RearGrip = authoring.RearGrip,
            TractionGrip = authoring.TractionGrip,
            SlipGripLoss = authoring.SlipGripLoss,
            WheelBase = authoring.WheelBase,
            MaxSteerAngle = authoring.MaxSteerAngle,
            YawAcceleration = authoring.YawAcceleration,
            YawDamping = authoring.YawDamping,
            MaxYawSpeed = authoring.MaxYawSpeed,
            SlipYawInfluence = authoring.SlipYawInfluence,
            MaxHealth = math.max(1f, authoring.MaxHealth),
            DamageCooldown = math.max(0f, authoring.DamageCooldown),
            SmokeHealthThreshold = math.saturate(authoring.SmokeHealthThreshold),
            DeathRestartDelay = math.max(0f, authoring.DeathRestartDelay),
            ExplosionUpForceMin = math.min(authoring.ExplosionUpForceMin, authoring.ExplosionUpForceMax),
            ExplosionUpForceMax = math.max(authoring.ExplosionUpForceMin, authoring.ExplosionUpForceMax),
            ExplosionHorizontalForceMin = math.min(authoring.ExplosionHorizontalForceMin, authoring.ExplosionHorizontalForceMax),
            ExplosionHorizontalForceMax = math.max(authoring.ExplosionHorizontalForceMin, authoring.ExplosionHorizontalForceMax),
            ExplosionAngularForceMin = math.min(authoring.ExplosionAngularForceMin, authoring.ExplosionAngularForceMax),
            ExplosionAngularForceMax = math.max(authoring.ExplosionAngularForceMin, authoring.ExplosionAngularForceMax),
            KillSpeedThreshold = authoring.KillSpeedThreshold,
            KillContactDistance = authoring.KillContactDistance,
            KillBoxForwardOffset = authoring.KillBoxForwardOffset,
            KillBoxHalfExtents = new float3(authoring.KillBoxHalfExtents.x, authoring.KillBoxHalfExtents.y, authoring.KillBoxHalfExtents.z),
            KillImpulse = authoring.KillImpulse,
            KillUpImpulse = authoring.KillUpImpulse,
            KillSpinImpulse = authoring.KillSpinImpulse
        });
        AddComponent(entity, new PlayerCarState
        {
            CurrentHealth = math.max(1f, authoring.MaxHealth),
            RandomSeed = seed
        });

        BakeWheelVisuals(authoring, entity);
    }

    private void BakeWheelVisuals(PlayerCarAuthoring authoring, Entity playerEntity)
    {
        float3 spinAxis = math.normalizesafe(new float3(
            authoring.VisualWheelSpinAxis.x,
            authoring.VisualWheelSpinAxis.y,
            authoring.VisualWheelSpinAxis.z), new float3(1f, 0f, 0f));

        PlayerCarWheelVisualReference[] wheelVisuals = authoring.WheelVisuals;

        for (int i = 0; i < wheelVisuals.Length; i++)
        {
            Transform wheelPivot = wheelVisuals[i].Pivot;
            Transform wheelModel = wheelVisuals[i].Model;

            if (wheelPivot == null || wheelModel == null)
                continue;

            Entity wheelPivotEntity = GetEntity(wheelPivot.gameObject, TransformUsageFlags.Dynamic);
            Entity wheelModelEntity = GetEntity(wheelModel.gameObject, TransformUsageFlags.Dynamic);
            Entity wheelVisualEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, $"WheelVisual {i}");
            AddComponent(wheelVisualEntity, new PlayerCarWheelVisual
            {
                PlayerEntity = playerEntity,
                PivotEntity = wheelPivotEntity,
                ModelEntity = wheelModelEntity,
                BasePivotRotation = wheelPivot.localRotation,
                BaseModelRotation = wheelModel.localRotation,
                SpinAxis = spinAxis,
                WheelRadius = math.max(0.01f, authoring.VisualWheelRadius),
                SpinMultiplier = authoring.VisualWheelSpinMultiplier,
                MaxSteerAngle = math.max(0f, authoring.VisualFrontWheelMaxSteerAngle),
                SteerSharpness = math.max(0f, authoring.VisualWheelSteerSharpness),
                IsFront = (byte)(wheelVisuals[i].IsFront ? 1 : 0)
            });
        }
    }
}

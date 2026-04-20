using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

public struct PlayerCarWheelVisual : IComponentData
{
    public Entity PlayerEntity;
    public Entity PivotEntity;
    public Entity ModelEntity;
    public quaternion BasePivotRotation;
    public quaternion BaseModelRotation;
    public float3 SpinAxis;
    public float WheelRadius;
    public float SpinMultiplier;
    public float CurrentSpinAngle;
    public float CurrentSteerAngle;
    public float MaxSteerAngle;
    public float SteerSharpness;
    public byte IsFront;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerCarPostPhysicsSystem))]
[UpdateBefore(typeof(PlayerCarPresentationSystem))]
public partial struct PlayerCarWheelVisualSystem : ISystem
{
    private ComponentLookup<PlayerCarState> carStateLookup;
    private ComponentLookup<LocalTransform> localTransformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCarWheelVisual>();
        carStateLookup = state.GetComponentLookup<PlayerCarState>(true);
        localTransformLookup = state.GetComponentLookup<LocalTransform>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        carStateLookup.Update(ref state);
        localTransformLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var wheelVisual in SystemAPI.Query<RefRW<PlayerCarWheelVisual>>())
        {
            PlayerCarWheelVisual wheel = wheelVisual.ValueRO;

            if (!carStateLookup.HasComponent(wheel.PlayerEntity) ||
                !localTransformLookup.HasComponent(wheel.PivotEntity) ||
                !localTransformLookup.HasComponent(wheel.ModelEntity))
                continue;

            PlayerCarState carState = carStateLookup[wheel.PlayerEntity];
            float targetSteerAngle = wheel.IsFront != 0 ? carState.SteerInput * wheel.MaxSteerAngle : 0f;

            if (wheel.SteerSharpness > 0f)
            {
                float steerBlend = math.saturate(deltaTime * wheel.SteerSharpness);
                wheel.CurrentSteerAngle = math.lerp(wheel.CurrentSteerAngle, targetSteerAngle, steerBlend);
            }
            else
            {
                wheel.CurrentSteerAngle = targetSteerAngle;
            }

            float spinDelta = math.degrees((carState.ForwardSpeed / math.max(0.01f, wheel.WheelRadius)) * wheel.SpinMultiplier * deltaTime);
            wheel.CurrentSpinAngle += spinDelta;

            if (math.abs(wheel.CurrentSpinAngle) >= 360f)
                wheel.CurrentSpinAngle = math.fmod(wheel.CurrentSpinAngle, 360f);

            quaternion steerRotation = quaternion.RotateY(math.radians(wheel.CurrentSteerAngle));
            LocalTransform wheelPivotTransform = localTransformLookup[wheel.PivotEntity];
            wheelPivotTransform.Rotation = math.normalize(math.mul(wheel.BasePivotRotation, steerRotation));
            localTransformLookup[wheel.PivotEntity] = wheelPivotTransform;

            LocalTransform wheelModelTransform = localTransformLookup[wheel.ModelEntity];
            quaternion spinRotation = quaternion.AxisAngle(wheel.SpinAxis, math.radians(wheel.CurrentSpinAngle));
            wheelModelTransform.Rotation = math.normalize(math.mul(wheel.BaseModelRotation, spinRotation));
            localTransformLookup[wheel.ModelEntity] = wheelModelTransform;

            wheelVisual.ValueRW = wheel;
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[UpdateBefore(typeof(HordeEnemyAssistSystem))]
public partial struct PlayerCarPostPhysicsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (localTransform, velocity, carState, car) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerCarState>, RefRO<PlayerCar>>())
        {
            if (carState.ValueRO.IsInitialized == 0)
            {
                carState.ValueRW.LockedHeight = localTransform.ValueRO.Position.y;
                carState.ValueRW.LastStablePosition = localTransform.ValueRO.Position;
                carState.ValueRW.LastStableRotation = localTransform.ValueRO.Rotation;
                carState.ValueRW.IsInitialized = 1;
            }

            float3 rawPosition = localTransform.ValueRO.Position;
            quaternion rawRotation = localTransform.ValueRO.Rotation;
            float3 rawLinear = velocity.ValueRO.Linear;
            float3 rawAngular = velocity.ValueRO.Angular;
            float3 lastStablePosition = carState.ValueRO.LastStablePosition;

            bool invalidState = !IsFinite(rawPosition) || !IsFinite(rawRotation) || !IsFinite(rawLinear) || !IsFinite(rawAngular);
            invalidState |= math.lengthsq(new float2(rawPosition.x - lastStablePosition.x, rawPosition.z - lastStablePosition.z)) > 2500f;
            invalidState |= math.lengthsq(new float2(rawLinear.x, rawLinear.z)) > 6400f;

            if (invalidState)
            {
                localTransform.ValueRW.Position = carState.ValueRO.LastStablePosition;
                localTransform.ValueRW.Rotation = carState.ValueRO.LastStableRotation;
                velocity.ValueRW.Linear = float3.zero;
                velocity.ValueRW.Angular = float3.zero;
                carState.ValueRW.CurrentSpeed = 0f;
                carState.ValueRW.ForwardSpeed = 0f;
                carState.ValueRW.SideSpeed = 0f;
                carState.ValueRW.YawSpeed = 0f;
                carState.ValueRW.SlipAmount = 0f;
                carState.ValueRW.ThrottleInput = 0f;
                carState.ValueRW.SteerInput = 0f;
                continue;
            }

            if (carState.ValueRO.IsDead != 0)
            {
                carState.ValueRW.CurrentSpeed = math.length(new float2(rawLinear.x, rawLinear.z));
                carState.ValueRW.ForwardSpeed = rawLinear.z;
                carState.ValueRW.SideSpeed = rawLinear.x;
                carState.ValueRW.YawSpeed = rawAngular.y;
                carState.ValueRW.SlipAmount = math.length(new float2(rawLinear.x, rawLinear.z));
                continue;
            }

            float3 position = localTransform.ValueRO.Position;
            position.y = carState.ValueRO.LockedHeight;

            float3 flattenedForward = math.rotate(localTransform.ValueRO.Rotation, new float3(0f, 0f, 1f));
            flattenedForward.y = 0f;

            if (math.lengthsq(flattenedForward) < 0.0001f)
            {
                float3 planarVelocity = new float3(velocity.ValueRO.Linear.x, 0f, velocity.ValueRO.Linear.z);
                if (math.lengthsq(planarVelocity) > 0.0001f)
                    flattenedForward = planarVelocity;
                else
                    flattenedForward = new float3(0f, 0f, 1f);
            }

            flattenedForward = math.normalize(flattenedForward);
            quaternion stableRotation = quaternion.LookRotationSafe(flattenedForward, math.up());

            localTransform.ValueRW.Position = position;
            localTransform.ValueRW.Rotation = stableRotation;

            float3 linear = velocity.ValueRO.Linear;
            linear.y = 0f;

            float3 angular = velocity.ValueRO.Angular;
            angular.x = 0f;
            angular.z = 0f;
            float maxYawSpeed = math.max(0.5f, car.ValueRO.MaxYawSpeed * 1.5f);
            angular.y = math.clamp(angular.y, -maxYawSpeed, maxYawSpeed);

            velocity.ValueRW.Linear = linear;
            velocity.ValueRW.Angular = angular;

            float3 forward = math.rotate(stableRotation, new float3(0f, 0f, 1f));
            float3 right = math.normalize(math.cross(math.up(), forward));
            float forwardSpeed = math.dot(linear, forward);
            float sideSpeed = math.dot(linear, right);

            carState.ValueRW.CurrentSpeed = math.length(new float2(forwardSpeed, sideSpeed));
            carState.ValueRW.ForwardSpeed = forwardSpeed;
            carState.ValueRW.SideSpeed = sideSpeed;
            carState.ValueRW.YawSpeed = angular.y;
            carState.ValueRW.SlipAmount = math.abs(sideSpeed);
            carState.ValueRW.LastStablePosition = position;
            carState.ValueRW.LastStableRotation = stableRotation;
        }
    }

    private static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    private static bool IsFinite(quaternion value)
    {
        return math.all(math.isfinite(value.value));
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(HordeTargetSyncSystem))]
[UpdateBefore(typeof(HordeEnemyMoveSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct PlayerCarControlSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCar>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float throttle = 0f;
        if (Input.GetKey(KeyCode.W))
            throttle += 1f;
        if (Input.GetKey(KeyCode.S))
            throttle -= 1f;

        float steer = 0f;
        if (Input.GetKey(KeyCode.A))
            steer -= 1f;
        if (Input.GetKey(KeyCode.D))
            steer += 1f;

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (localTransform, velocity, carState, car) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerCarState>, RefRO<PlayerCar>>())
        {
            if (carState.ValueRO.IsDead != 0)
            {
                carState.ValueRW.ThrottleInput = 0f;
                carState.ValueRW.SteerInput = 0f;
                continue;
            }

            float3 flattenedForward = GetFlattenedForward(localTransform.ValueRO.Rotation);
            float3 right = math.normalize(math.cross(math.up(), flattenedForward));
            float3 currentLinear = velocity.ValueRO.Linear;
            float3 horizontalVelocity = new float3(currentLinear.x, 0f, currentLinear.z);
            float forwardSpeed = math.dot(horizontalVelocity, flattenedForward);
            float sideSpeed = math.dot(horizontalVelocity, right);
            float planarSpeed = math.length(new float2(forwardSpeed, sideSpeed));
            float grip = math.max(0.05f, car.ValueRO.Grip);
            float frontGrip = math.max(0.05f, grip * car.ValueRO.FrontGrip);
            float rearGrip = math.max(0.05f, grip * car.ValueRO.RearGrip);
            float tractionGrip = math.max(0.05f, grip * car.ValueRO.TractionGrip);
            float speedAbs = math.abs(forwardSpeed);
            float sideAbs = math.abs(sideSpeed);
            float slipRatio = sideAbs / (speedAbs + 1f);
            float traction = math.saturate(tractionGrip / (1f + slipRatio * math.max(0f, car.ValueRO.SlipGripLoss)));
            bool braking = (throttle > 0f && forwardSpeed < -0.2f) || (throttle < 0f && forwardSpeed > 0.2f);

            if (throttle > 0f)
            {
                float acceleration = (forwardSpeed < -0.2f ? car.ValueRO.BrakeAcceleration : car.ValueRO.ForwardAcceleration) * traction;
                forwardSpeed += acceleration * deltaTime;
            }
            else if (throttle < 0f)
            {
                float acceleration = (forwardSpeed > 0.2f ? car.ValueRO.BrakeAcceleration : car.ValueRO.ReverseAcceleration) * traction;
                forwardSpeed -= acceleration * deltaTime;
            }
            else
            {
                forwardSpeed = MoveTowards(forwardSpeed, 0f, car.ValueRO.CoastingDrag * deltaTime);
            }

            float rollingDrag = car.ValueRO.CoastingDrag * math.lerp(0.08f, 0.22f, math.saturate(planarSpeed / math.max(1f, car.ValueRO.MaxForwardSpeed))) * deltaTime;
            forwardSpeed = MoveTowards(forwardSpeed, 0f, rollingDrag);
            forwardSpeed = math.clamp(forwardSpeed, -car.ValueRO.MaxReverseSpeed, car.ValueRO.MaxForwardSpeed);
            speedAbs = math.abs(forwardSpeed);

            float dynamicRearGrip = rearGrip;
            if (braking)
                dynamicRearGrip *= math.lerp(1f, 0.72f, math.abs(steer));

            float powerSlide = math.abs(throttle) * math.saturate(speedAbs / math.max(1f, car.ValueRO.MaxForwardSpeed * 0.5f)) * (1f - traction);
            dynamicRearGrip *= math.lerp(1f, 0.78f, powerSlide);

            float lateralGrip = car.ValueRO.LateralDamping * math.lerp(dynamicRearGrip, frontGrip, math.saturate(speedAbs / math.max(1f, car.ValueRO.MaxForwardSpeed)));
            sideSpeed = MoveTowards(sideSpeed, 0f, lateralGrip * deltaTime);
            sideAbs = math.abs(sideSpeed);

            float yawSpeed = velocity.ValueRO.Angular.y;
            float yawTarget = 0f;
            planarSpeed = math.length(new float2(forwardSpeed, sideSpeed));

            if (planarSpeed >= car.ValueRO.MinTurnSpeed)
            {
                float steerAuthority = math.saturate(planarSpeed / math.max(car.ValueRO.MinTurnSpeed * 3.5f, 0.01f));
                float steerAngle = math.radians(car.ValueRO.MaxSteerAngle) * steer * steerAuthority;
                float wheelBase = math.max(0.5f, car.ValueRO.WheelBase);
                float driveSign = forwardSpeed < -0.15f ? -1f : 1f;
                if (math.abs(forwardSpeed) < 0.15f)
                {
                    if (math.abs(throttle) > 0.01f)
                        driveSign = math.sign(throttle);
                    else if (math.abs(carState.ValueRO.ForwardSpeed) > 0.01f)
                        driveSign = math.sign(carState.ValueRO.ForwardSpeed);
                }

                float driftBlend = math.saturate(sideAbs / math.max(planarSpeed, 0.01f));
                float steerSpeed = math.lerp(math.abs(forwardSpeed), planarSpeed, driftBlend * 0.85f) * driveSign;
                yawTarget = (steerSpeed / wheelBase) * math.tan(steerAngle);

                float rearLoose = math.max(0f, 1.2f - dynamicRearGrip);
                yawTarget += steer * sideAbs * car.ValueRO.SlipYawInfluence * rearLoose;
                yawTarget += sideSpeed * car.ValueRO.SlipYawInfluence * rearLoose * math.saturate(planarSpeed / math.max(1f, car.ValueRO.MaxForwardSpeed * 0.35f));
                float yawResponse = car.ValueRO.YawAcceleration * math.max(0.1f, car.ValueRO.TurnSpeed) * math.lerp(0.85f, 1.2f, math.saturate(frontGrip)) * deltaTime;
                yawSpeed = MoveTowards(yawSpeed, yawTarget, yawResponse);
            }

            if (math.abs(steer) < 0.001f || planarSpeed < car.ValueRO.MinTurnSpeed)
                yawSpeed = MoveTowards(yawSpeed, 0f, car.ValueRO.YawDamping * deltaTime);
            else
                yawSpeed = MoveTowards(yawSpeed, yawTarget, car.ValueRO.YawDamping * 0.35f * deltaTime);

            float3 planarVelocity = flattenedForward * forwardSpeed + right * sideSpeed;
            float planarSpeedLimit = GetPlanarSpeedLimit(forwardSpeed, throttle, carState.ValueRO.ForwardSpeed, car.ValueRO);
            float planarVelocityLength = math.length(planarVelocity);
            if (planarVelocityLength > planarSpeedLimit)
                planarVelocity *= planarSpeedLimit / planarVelocityLength;

            forwardSpeed = math.dot(planarVelocity, flattenedForward);
            sideSpeed = math.dot(planarVelocity, right);
            sideAbs = math.abs(sideSpeed);
            velocity.ValueRW.Linear = planarVelocity + new float3(0f, currentLinear.y, 0f);
            velocity.ValueRW.Angular = new float3(0f, math.clamp(yawSpeed, -car.ValueRO.MaxYawSpeed, car.ValueRO.MaxYawSpeed), 0f);
            carState.ValueRW.CurrentSpeed = math.length(new float2(forwardSpeed, sideSpeed));
            carState.ValueRW.ForwardSpeed = forwardSpeed;
            carState.ValueRW.SideSpeed = sideSpeed;
            carState.ValueRW.YawSpeed = yawSpeed;
            carState.ValueRW.SlipAmount = sideAbs;
            carState.ValueRW.ThrottleInput = throttle;
            carState.ValueRW.SteerInput = steer;
        }
    }

    private static float3 GetFlattenedForward(quaternion rotation)
    {
        float3 forward = math.rotate(rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;

        if (math.lengthsq(forward) < 0.0001f)
            return new float3(0f, 0f, 1f);

        return math.normalize(forward);
    }

    private static float GetPlanarSpeedLimit(float forwardSpeed, float throttle, float previousForwardSpeed, PlayerCar car)
    {
        float signedSpeed = forwardSpeed;

        if (math.abs(signedSpeed) < 0.1f)
        {
            if (math.abs(throttle) > 0.01f)
                signedSpeed = throttle;
            else
                signedSpeed = previousForwardSpeed;
        }

        return math.max(0.5f, signedSpeed < 0f ? car.MaxReverseSpeed : car.MaxForwardSpeed);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (math.abs(target - current) <= maxDelta)
            return target;

        return current + math.sign(target - current) * maxDelta;
    }
}

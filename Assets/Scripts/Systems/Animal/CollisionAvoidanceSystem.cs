using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Burst;
using Unity.Rendering;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;

/// <summary>
/// System handling collision avoidance. 
/// </summary>
public class CollisionAvoidanceSystem : SystemBase
{
    private BuildPhysicsWorld buildPhysicsWorld;
    private StepPhysicsWorld stepPhysicsWorld;

    [BurstCompile]
    struct NotificationJob : ITriggerEventsJob
    {
        public ComponentDataFromEntity<AnimalTag> animalTag;
        public ComponentDataFromEntity<AnimalMovementData> mvmtData;
        public ComponentDataFromEntity<Translation> translation;

        public void Execute(TriggerEvent triggerEvent)
        {
            if (animalTag.HasComponent(triggerEvent.Entities.EntityA) && animalTag.HasComponent(triggerEvent.Entities.EntityB))
            {
                AnimalMovementData mvmtDataA = mvmtData[triggerEvent.Entities.EntityA];
                AnimalMovementData mvmtDataB = mvmtData[triggerEvent.Entities.EntityB];
                float3 positionA = translation[triggerEvent.Entities.EntityA].Value;

                float speedA = mvmtDataA.movementSpeed;
                float speedB = mvmtDataB.movementSpeed;
                float maxSpeed = math.max(speedA, speedB);

                float dotProduct = math.dot(math.normalize(mvmtDataA.targetDirection - positionA), math.normalize(mvmtDataB.targetDirection - positionA));

                float angle = 0.262f;
                angle += 1f - math.abs(dotProduct);
                angle += (1f - (maxSpeed * 0.1f));
                angle = math.clamp(angle, 0f, StaticValues.AVOIDANCE_MIN_ANGLE);
                quaternion newRotation = quaternion.RotateY(angle);
                float3 newDirection = math.rotate(newRotation, mvmtDataA.direction);
                newDirection = math.normalizesafe(newDirection);
                // Set EntityA's target direction
                mvmtDataA.targetDirection = newDirection;
                mvmtData[triggerEvent.Entities.EntityA] = mvmtDataA;
            }
        }
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysicsWorld = World.GetOrCreateSystem<StepPhysicsWorld>();
        Enabled = true;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        NotificationJob notificationJob = new NotificationJob
        {
            animalTag = GetComponentDataFromEntity<AnimalTag>(),
            mvmtData = GetComponentDataFromEntity<AnimalMovementData>(),
            translation = GetComponentDataFromEntity<Translation>(),
        };

        Dependency = notificationJob.Schedule(stepPhysicsWorld.Simulation, ref buildPhysicsWorld.PhysicsWorld, Dependency);
        Dependency.Complete();
    }
}
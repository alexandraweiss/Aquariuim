using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Burst;
using Unity.Rendering;
using Unity.Mathematics;


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

        public void Execute(TriggerEvent triggerEvent)
        {
            if (animalTag.HasComponent(triggerEvent.Entities.EntityA))
            {
                AnimalMovementData mvmtDataA = mvmtData[triggerEvent.Entities.EntityA];
                AnimalMovementData mvmtDataB = mvmtData[triggerEvent.Entities.EntityB];

                quaternion deltar = quaternion.RotateY(0.175f);
                float3 d = math.rotate(deltar, mvmtDataB.direction);
                mvmtDataA.targetDirection = d;
                mvmtData[triggerEvent.Entities.EntityA] = mvmtDataA;
            }
        }
    }

    protected override void OnCreate()
    {
        buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysicsWorld = World.GetOrCreateSystem<StepPhysicsWorld>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        NotificationJob notificationJob = new NotificationJob
        {
            animalTag = GetComponentDataFromEntity<AnimalTag>(),
            mvmtData = GetComponentDataFromEntity<AnimalMovementData>(),
        };

        Dependency = JobHandle.CombineDependencies(Dependency, World.GetExistingSystem<EndFramePhysicsSystem>().FinalJobHandle);
        Dependency = notificationJob.Schedule(stepPhysicsWorld.Simulation, ref buildPhysicsWorld.PhysicsWorld, 
            JobHandle.CombineDependencies(Dependency, buildPhysicsWorld.FinalJobHandle));
        Dependency.Complete();
    }
}

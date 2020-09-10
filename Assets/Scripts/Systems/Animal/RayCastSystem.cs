using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;

[BurstCompile]
public class RayCastSystem : SystemBase
{
    protected BuildPhysicsWorld physicsWorld;

    protected override void OnCreate()
    {
        base.OnCreate();
        Enabled = false;
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
    }

    protected override void OnUpdate()
    {
        CollisionFilter collisionFilter = new CollisionFilter() {
            BelongsTo = ~0u,
            CollidesWith = ~0u, // all 1s, so all layers, collide with everything
            GroupIndex = 0
        };
        EntityManager entityManager = World.EntityManager;
        CollisionWorld collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        Dependency = Entities.WithAll<AnimalTag>().ForEach( (ref AnimalMovementData mvmtData, in Translation translation) => {
            float3 start = new float3(translation.Value.x, translation.Value.y, translation.Value.z + 0.2f);
            
            #region front
            RaycastInput inputFront = new RaycastInput()
            {
                Start = start,
                End = new float3(translation.Value.x, translation.Value.y, translation.Value.z + (2.5f * mvmtData.movementSpeed)),
                Filter = collisionFilter
            };
            RaycastHit hitFront = new RaycastHit();
            bool hasHitFront = collisionWorld.CastRay(inputFront, out hitFront);
            #endregion
            #region front right
            RaycastInput inputFR = new RaycastInput()
            {
                Start = start,
                End = new float3(translation.Value.x + (2.5f * mvmtData.movementSpeed), translation.Value.y, translation.Value.z + (2.5f * mvmtData.movementSpeed)),
                Filter = collisionFilter
            };
            RaycastHit hitFR = new RaycastHit();
            bool hasHitFR = collisionWorld.CastRay(inputFR, out hitFR);
            #endregion
            #region front left
            RaycastInput inputFL = new RaycastInput()
            {
                Start = start,
                End = new float3(translation.Value.x - (2.5f * mvmtData.movementSpeed), translation.Value.y, translation.Value.z + (2.5f * mvmtData.movementSpeed)),
                Filter = collisionFilter
            };
            RaycastHit hitFL = new RaycastHit();
            bool hasHitFL = collisionWorld.CastRay(inputFR, out hitFL);
            #endregion

            if (hasHitFront)
            {
                float3 normal = hitFront.SurfaceNormal;
                //AnimalMovementData mvmtDataHitEntity = entityManager.GetComponentData<AnimalMovementData>(hitEntity);
                //float maxSpeed = math.max(mvmtData.movementSpeed, mvmtDataHitEntity.movementSpeed);

                float dotProduct = math.dot(math.normalize(mvmtData.targetDirection - translation.Value), math.normalize(hitFront.SurfaceNormal)); 

                float angle = 0.262f;
                angle += 1f - math.abs(dotProduct);
                //angle += (1f - (maxSpeed * 0.1f));
                angle = math.clamp(angle, 0f, StaticValues.AVOIDANCE_MIN_ANGLE);
                quaternion newRotation = quaternion.RotateY(angle);
                float3 newDirection = math.rotate(newRotation, mvmtData.direction);
                newDirection = math.normalizesafe(newDirection);
                // Set EntityA's target direction
                mvmtData.targetDirection = newDirection;
            } 
            else if (hasHitFR)
            {
                float3 normal = hitFR.SurfaceNormal;
                //AnimalMovementData mvmtDataHitEntity = entityManager.GetComponentData<AnimalMovementData>(hitEntity);
                //float maxSpeed = math.max(mvmtData.movementSpeed, mvmtDataHitEntity.movementSpeed);

                float dotProduct = math.dot(math.normalize(mvmtData.targetDirection - translation.Value), math.normalize(hitFR.SurfaceNormal));

                float angle = 0.262f;
                angle += 1f - math.abs(dotProduct);
                //angle += (1f - (maxSpeed * 0.1f));
                angle = math.clamp(angle, 0f, StaticValues.AVOIDANCE_MIN_ANGLE);
                quaternion newRotation = quaternion.RotateY(angle);
                float3 newDirection = math.rotate(newRotation, mvmtData.direction);
                newDirection = math.normalizesafe(newDirection);
                // Set EntityA's target direction
                mvmtData.targetDirection = newDirection;
            }
            else if (hasHitFL)
            {
                float3 normal = hitFR.SurfaceNormal;
                //AnimalMovementData mvmtDataHitEntity = entityManager.GetComponentData<AnimalMovementData>(hitEntity);
                //float maxSpeed = math.max(mvmtData.movementSpeed, mvmtDataHitEntity.movementSpeed);

                float dotProduct = math.dot(math.normalize(mvmtData.targetDirection - translation.Value), math.normalize(hitFR.SurfaceNormal));

                float angle = 0.262f;
                angle += 1f - math.abs(dotProduct);
                //angle += (1f - (maxSpeed * 0.1f));
                angle = math.clamp(angle, 0f, StaticValues.AVOIDANCE_MIN_ANGLE);
                quaternion newRotation = quaternion.RotateY(angle);
                float3 newDirection = math.rotate(newRotation, mvmtData.direction);
                newDirection = math.normalizesafe(newDirection);
                // Set EntityA's target direction
                mvmtData.targetDirection = newDirection;
            }

        }).Schedule(Dependency);

        Dependency.Complete();
    }
}

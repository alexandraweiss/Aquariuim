using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;


public class DistanceQuerySystem : SystemBase
{
    BuildPhysicsWorld buildPhysicsWorld;

    protected override void OnCreate()
    {
        base.OnCreate();
        buildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();

        Enabled = false;
    }


    [BurstCompile]
    protected override unsafe void OnUpdate()
    {
        CollisionWorld collisionWorld = buildPhysicsWorld.PhysicsWorld.CollisionWorld;

        CollisionFilter collisionFilter = new CollisionFilter()
        {
            BelongsTo = 1u << 0,
            CollidesWith = 0u | (1u << 0) | (1u << 1),
            GroupIndex = 0
        };

        Dependency = Entities.WithAll<AnimalTag>().ForEach((ref Translation translation, ref AnimalMovementData mvmtData, in PhysicsCollider collider, in Rotation rotation) => {
            Unity.Collections.NativeList<DistanceHit> hits = new Unity.Collections.NativeList<DistanceHit>(Unity.Collections.Allocator.Temp);
            float maxDist = 1f;

            float3 fwd = translation.Value + (mvmtData.targetDirection * 0.25f);

            // Tested in Unity 2020.1.0b10
            //ColliderDistanceInput cdi = new ColliderDistanceInput()
            //{
            //    Collider = collider.ColliderPtr,
            //    MaxDistance = maxDist,
            //    Transform = new RigidTransform(rotation.Value, translation.Value)
            //};

            PointDistanceInput pdi = new PointDistanceInput()
            {
                Filter = collisionFilter,
                MaxDistance = maxDist,
                Position = fwd
            };

            if (collisionWorld.CalculateDistance(pdi, ref hits))
            {
                
                DistanceHit closest = GetClosestHit(hits, maxDist);
                if (closest.Distance > 0.00001)
                {
                    float distance = closest.Distance;

                    float distanceFrac = 1 - (distance / maxDist);
                    distanceFrac = math.clamp(distanceFrac, 0.25f, 1f);

                    byte colliderTags = collisionWorld.Bodies[closest.RigidBodyIndex].CustomTags;
                    float dotProduct = 0f;
                    float angle = 0f;
                    bool avoid = false;

                    if (colliderTags == 1) // Collision with another animal
                    {
                        quaternion r = collisionWorld.Bodies[closest.RigidBodyIndex].WorldFromBody.rot;
                        float3 othersFwd = math.rotate(r, new float3(0f, 0f, 1f));

                        dotProduct = (float)(math.dot(mvmtData.targetDirection, math.normalize(othersFwd)));

                        if (dotProduct < -0.7f )
                        {
                            avoid = true;
                            angle = 5.846853f * distanceFrac; //- 25f degrees
                        }
                        else if (dotProduct >= -0.001f && dotProduct < 0.975f)
                        {
                            avoid = true;
                            angle = (0.436332f - math.acos(dotProduct)) * distanceFrac; // 25 degrees

                        }
                    }
                    else if (colliderTags == 2) // Collision with terrain
                    {

                        dotProduct = math.dot(mvmtData.targetDirection, math.normalize(closest.SurfaceNormal)); 
                        if (dotProduct < -0.1f)
                        {
                            avoid = true;
                            angle = - (1.570796f - math.acos(dotProduct)) * distanceFrac; //90 degrees -> parallel to surface normal
                        }
                    }

                    if (avoid)
                    {
                        quaternion newRotation = quaternion.RotateY(angle);
                        float3 newDirection = math.rotate(newRotation, mvmtData.direction);
                        newDirection = math.normalizesafe(newDirection);
                        mvmtData.targetDirection = newDirection;
                    }
                }
                
            }
            hits.Dispose();


        }).Schedule(Dependency);

        Dependency.Complete();
    }

    [BurstCompile]
    private static DistanceHit GetClosestHit(Unity.Collections.NativeList<DistanceHit> hits, float maxDist)
    {
        float currentDistance = 0f;
        float minDistance = maxDist;
        int resultIndex = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            currentDistance = hits[i].Distance;
            if (currentDistance > 0.00001f && currentDistance < minDistance)
            {
                minDistance = currentDistance;
                resultIndex = i;
            }
        }
        return hits[resultIndex];
    }
}

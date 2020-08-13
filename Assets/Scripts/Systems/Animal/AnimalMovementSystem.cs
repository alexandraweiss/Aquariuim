using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics.Systems;
using UnityEngine;

/// <summary>
/// System which controls the animals movement. 
/// The movement consists of a direction, a speed, and an undulating movement in the z-axis. 
/// </summary>
public class AnimalMovementSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        Enabled = true;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);
        Dependency = JobHandle.CombineDependencies(Dependency, World.GetExistingSystem<EndFramePhysicsSystem>().FinalJobHandle);

        Dependency = Entities.WithAll<AnimalTag>().ForEach((ref Translation translation, in AnimalMovementData movementData) => 
        {
            // Apply offset on the z-axis to a copy of the direction.
            float3 t_dir = movementData.direction;
            float oscillated_z = math.cos(et) * movementData.amplitude;
            t_dir.z += oscillated_z;

            float3 forward = t_dir * movementData.movementSpeed * dt;

            translation.Value += forward;
        }).Schedule(Dependency);

        Dependency.Complete();
    }
}
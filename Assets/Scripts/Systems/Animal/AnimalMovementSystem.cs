using System;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics.Systems;

/// <summary>
/// System which controls the animals forward movement. 
/// The movement consists of a direction, a speed, and an undulating movement in the z-axis. 
/// </summary>
public class AnimalMovementSystem : SystemBase
{
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

            // Clamp the value for translation in a min/max range
            float x = translation.Value.x + forward.x;
            float y = translation.Value.y + forward.y;
            float z = translation.Value.z + forward.z;
            x = math.clamp(x, StaticValues.MIN_X, StaticValues.MAX_X);
            y = math.clamp(y, StaticValues.MIN_Y, StaticValues.MAX_Y);
            z = math.clamp(z, StaticValues.MIN_Z, StaticValues.MAX_Z);

            translation.Value = new float3(x, y, z);

        }).Schedule(Dependency);

        Dependency.Complete();
    }
}
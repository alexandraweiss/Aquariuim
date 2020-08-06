using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System;
using Unity.Burst;
using Unity.Collections;


/// <summary>
/// System which controls the animals movement. 
/// The movement consists of a direction, a speed, and an undulating movement in the z-axis. 
/// </summary>
[AlwaysSynchronizeSystem]
public class AnimalMovementSystem : SystemBase
{

    [BurstCompile]
    protected override void OnUpdate()
    {
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);

        Entities.ForEach((ref Translation translation, in AnimalMovementData movementData) => 
        {
            // Apply offset on the z-axis to a copy of the direction.
            float3 t_dir = movementData.direction;
            float oscillated_z = math.cos(et) * movementData.amplitude;
            t_dir.z += oscillated_z;

            float3 forward = t_dir * movementData.movementSpeed * dt;

            translation.Value += forward;
        }).ScheduleParallel();
    }
}


/// <summary>
/// System which lets the animal move randomly in a random interval of a couple of seconds. 
/// </summary>
[AlwaysSynchronizeSystem]
public class AnimalRandomDirectionSystem : SystemBase
{
    [BurstCompile]
    protected override void OnUpdate() 
    {
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);

        NativeArray<Unity.Mathematics.Random> randArray = World.GetExistingSystem<RandomSystem>().Randoms;

        Entities.ForEach( (int nativeThreadIndex, ref Rotation rotation, ref AnimalMovementData movementData) => 
        {
            // If the interval is ending, set a new random direction and random update interval.
            if (et % movementData.updateInterval <= 0.009f)
            {
                Unity.Mathematics.Random randomInstance = randArray[nativeThreadIndex];

                float x = randomInstance.NextFloat(movementData.direction.x - 0.5f, movementData.direction.x + 0.5f); 
                float y = randomInstance.NextFloat(movementData.direction.x - 0.1f, movementData.direction.x + 0.1f); 
                float z = randomInstance.NextFloat(movementData.direction.x - 0.5f, movementData.direction.x + 0.5f); 
                movementData.directionOffset = new float3(x, y, z);

                float length = math.length(movementData.direction - movementData.directionOffset);

                movementData.updateInterval = randomInstance.NextInt(3, 10);

                randArray[nativeThreadIndex] = randomInstance;
            }

            // Apply the delta movement during the update
            movementData.direction += (movementData.directionOffset * dt);
            movementData.direction = math.normalize(movementData.direction);
            movementData.direction.y = math.clamp(movementData.direction.y, 0f, 1000f);

            rotation.Value = quaternion.LookRotationSafe(movementData.direction, new float3(0f, 1f, 0f));


        }).ScheduleParallel();
    }
}
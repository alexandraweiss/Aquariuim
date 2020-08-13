using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics.Systems;


/// <summary>
/// System which lets the animal move randomly in a random interval of a couple of seconds. 
/// </summary>
public class AnimalRandomDirectionSystem : SystemBase
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

        NativeArray<Unity.Mathematics.Random> randArray = World.GetExistingSystem<RandomSystem>().Randoms;
        Dependency = JobHandle.CombineDependencies(Dependency, World.GetExistingSystem<EndFramePhysicsSystem>().FinalJobHandle);

        Dependency = Entities.WithAll<AnimalTag>().ForEach((int nativeThreadIndex, ref Rotation rotation, ref AnimalMovementData movementData) =>
        {
            
            // If the interval is ending, set a new random direction and random update interval.
            if (et % movementData.updateInterval <= 0.009f)
            {
                Unity.Mathematics.Random randomInstance = randArray[nativeThreadIndex];

                float x = randomInstance.NextFloat(movementData.direction.x - 0.2f, movementData.direction.x + 0.2f);
                float y = randomInstance.NextFloat(movementData.direction.x - 0.1f, movementData.direction.x + 0.1f);
                float z = randomInstance.NextFloat(movementData.direction.x - 0.2f, movementData.direction.x + 0.2f);
                movementData.targetDirection = new float3(x, y, z);

                float length = math.length(movementData.direction - movementData.targetDirection);

                movementData.updateInterval = randomInstance.NextInt(3, 10);

                randArray[nativeThreadIndex] = randomInstance;
            }
            
            
            // Apply the delta movement during the update
            movementData.direction += (movementData.targetDirection * dt);
            movementData.direction = math.normalize(movementData.direction);
            movementData.direction.y = math.clamp(movementData.direction.y, 0f, 1000f);

            rotation.Value = quaternion.LookRotationSafe(movementData.direction, new float3(0f, 1f, 0f));

        }).Schedule(Dependency);

        Dependency.Complete();
    }
}
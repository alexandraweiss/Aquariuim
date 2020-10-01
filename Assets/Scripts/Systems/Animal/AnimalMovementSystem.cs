using System;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Jobs.LowLevel.Unsafe;

/// <summary>
/// System which controls the animals forward movement. 
/// The movement consists of a direction, a speed, and an undulating movement in the z-axis. 
/// </summary>
public class AnimalMovementSystem : SystemBase
{
    EntityQuery entityQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        entityQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<AnimalMovementData>() },
        });
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);
        Dependency = JobHandle.CombineDependencies(Dependency, World.GetExistingSystem<EndFramePhysicsSystem>().FinalJobHandle);
        NativeArray<Unity.Mathematics.Random> randArray = World.GetExistingSystem<RandomSystem>().Randoms;

        int count = entityQuery.CalculateEntityCount();

        NativeArray<AnimalMovementData> animalMvmtData = new NativeArray<AnimalMovementData>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<Rotation> animalRotations = new NativeArray<Rotation>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<Translation> animalTranslations = new NativeArray<Translation>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        
        JobHandle fillList = Entities
            .WithName("fillListJob")
            .WithAll<AnimalTag>().ForEach( (int entityInQueryIndex, in AnimalMovementData mvmtData, in Rotation rotation, in Translation translation) => 
            {
                animalMvmtData[entityInQueryIndex] = mvmtData;
                animalRotations[entityInQueryIndex] = rotation;
                animalTranslations[entityInQueryIndex] = translation;
            }).ScheduleParallel(Dependency);


        JobHandle rndJob = Entities
            .WithName("rndJob")
            .WithAll<AnimalTag>()
            .ForEach( (int entityInQueryIndex) =>
            {
                AnimalMovementData movementData = animalMvmtData[entityInQueryIndex];
                // If the interval is ending, set a new random direction and random update interval.
                if (et % movementData.updateInterval <= 0.009f)
                {
                    int rndIdx = math.min(entityInQueryIndex, JobsUtility.MaxJobThreadCount-1);
                    
                    Unity.Mathematics.Random randomInstance = randArray[rndIdx];
                    float x = randomInstance.NextFloat(movementData.direction.x - 0.2f, movementData.direction.x + 0.2f);
                    float y = randomInstance.NextFloat(movementData.direction.y - 0.1f, movementData.direction.y + 0.1f);
                    float z = randomInstance.NextFloat(movementData.direction.z - 0.2f, movementData.direction.z + 0.2f);
                    y = math.clamp(y, -0.3f, 0.3f);

                    movementData.targetDirection = math.normalize(new float3(x,y,z));

                    movementData.updateInterval = randomInstance.NextInt(3, 5);

                    animalMvmtData[entityInQueryIndex] = movementData;
                    randArray[rndIdx] = randomInstance;
                }

            }).ScheduleParallel(fillList);

        JobHandle dirJob = Entities
            .WithName("dirJob")
            .WithAll<AnimalTag>().ForEach( (int entityInQueryIndex) =>
            {
                AnimalMovementData movementData = animalMvmtData[entityInQueryIndex];
                Rotation rot = animalRotations[entityInQueryIndex];
                // Apply and store the new direction based on the animal's target direction.
                movementData.direction += (movementData.targetDirection * dt);
                movementData.direction.y = math.clamp(movementData.direction.y, -0.4f, 0.4f);
                movementData.direction = math.normalize(movementData.direction);
                rot.Value = quaternion.LookRotationSafe(movementData.direction, math.up());

                animalMvmtData[entityInQueryIndex] = movementData;
                animalRotations[entityInQueryIndex] = rot;

            }).ScheduleParallel(rndJob);

        
        JobHandle fwdMvmt = Entities
            .WithName("fwdMvmtJob")
            .WithReadOnly(animalMvmtData)
            .WithReadOnly(animalRotations)
            .WithAll<AnimalTag>().ForEach( (int entityInQueryIndex) => 
            {
                AnimalMovementData movementData = animalMvmtData[entityInQueryIndex];
                Rotation rot = animalRotations[entityInQueryIndex];
                Translation translation = animalTranslations[entityInQueryIndex];

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

                animalTranslations[entityInQueryIndex] = translation;

            }).ScheduleParallel(dirJob);

        JobHandle writeBack = Entities.
            WithName("writeBackJob").
            WithReadOnly(animalMvmtData).
            WithReadOnly(animalRotations).
            WithReadOnly(animalTranslations).
            WithAll<AnimalTag>().ForEach( (int entityInQueryIndex, ref AnimalMovementData animalMovementData, ref Rotation rotation, ref Translation translation) => {
                animalMovementData = animalMvmtData[entityInQueryIndex];
                rotation = animalRotations[entityInQueryIndex];
                translation = animalTranslations[entityInQueryIndex];
            }).ScheduleParallel(fwdMvmt);
        
        Dependency = JobHandle.CombineDependencies(Dependency, fillList);
        Dependency = JobHandle.CombineDependencies(Dependency, rndJob);
        Dependency = JobHandle.CombineDependencies(Dependency, dirJob);
        Dependency = JobHandle.CombineDependencies(Dependency, fwdMvmt);
        Dependency = JobHandle.CombineDependencies(Dependency, writeBack);

        entityQuery.AddDependency(Dependency);
        Dependency.Complete();

    }
}
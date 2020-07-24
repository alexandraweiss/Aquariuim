using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System;
using Unity.Burst;

[AlwaysSynchronizeSystem]
[BurstCompile]
public class AnimalMovementSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);

        Entities.ForEach((ref Translation position, in AnimalMovementData movementData) =>
        {
            float3 t_dir = movementData.direction;
            float oscillated_z = math.cos(et) * movementData.amplitude;
            t_dir.z += oscillated_z;

            float3 forward = t_dir * movementData.movementSpeed * dt;

            position.Value += forward;

        }).Run();

        return default;
    }
}
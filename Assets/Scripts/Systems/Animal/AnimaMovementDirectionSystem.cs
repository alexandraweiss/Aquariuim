﻿using System;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Physics.Systems;


/// <summary>
/// System that controls an animal's current movement direction by setting its rotation. 
/// The targetDirection value is set at specific events (e.g. to avoid collisions) and is only used for lerping in this system. 
/// </summary>
public class AnimaMovementDirectionSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        Enabled = true;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        float dt = Convert.ToSingle(Time.DeltaTime);
        Dependency = JobHandle.CombineDependencies(Dependency, World.GetExistingSystem<EndFramePhysicsSystem>().FinalJobHandle);

        Dependency = Entities.WithAll<AnimalTag>().ForEach((ref Rotation rotation, ref AnimalMovementData movementData) =>
        {
            // Apply and store the new direction based on the animal's target direction.
            movementData.direction += (movementData.targetDirection * dt);
            movementData.direction = math.normalize(movementData.direction);
            rotation.Value = quaternion.LookRotationSafe(movementData.direction, new float3(0f, 1f, 0f));

        }).Schedule(Dependency);

        Dependency.Complete();
    }
}
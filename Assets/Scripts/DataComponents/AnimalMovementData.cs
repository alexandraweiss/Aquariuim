using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct AnimalMovementData : IComponentData
{
    public float3 direction;
    public float3 targetDirection;

    public float movementSpeed;
    public float amplitude;

    public int updateInterval;
}
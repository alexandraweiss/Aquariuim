using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct AnimalBehaviourData : IComponentData
{
    public enum BehaviourType
    {
        forage = 0,
        hunt = 1
    }

    public BehaviourType behaviour;
}

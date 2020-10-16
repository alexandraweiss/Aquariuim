using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidBehaviourData : IComponentData
{
    public float separation;
    public float alignmewnt;
    public float cohesion;
}

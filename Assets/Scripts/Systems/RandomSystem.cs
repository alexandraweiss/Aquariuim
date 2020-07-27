using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;


public class RandomSystem : SystemBase
{
    public NativeArray<Random> Randoms { get; private set; }
    
    protected override void OnUpdate() { }

    protected override void OnCreate()
    {
        Random[] rand = new Random[JobsUtility.MaxJobThreadCount];
        System.Random seed = new System.Random();

        for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
        {
            rand[i] = new Random((uint)seed.Next());
        }

        Randoms = new NativeArray<Random>(rand, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        Randoms.Dispose();
    }
}
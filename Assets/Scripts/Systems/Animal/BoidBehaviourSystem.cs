using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System;

[UpdateBefore(typeof(AnimalMovementSystem))]
public class BoidBehaviourSystem : SystemBase
{
    BuildPhysicsWorld buildPhysicsWorld;
    EntityQuery query;

    

    protected override void OnCreate()
    {
        base.OnCreate();
        buildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();

        query = GetEntityQuery(new EntityQueryDesc
        {
            All = new [] { ComponentType.ReadOnly<ObstacleTag>(), ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<Rotation>() }
        });

        Enabled = true;
    }

    [BurstCompile]
    protected struct GetCenter:IJob
    {
        [ReadOnly] public NativeMultiHashMap<int, int> bucketEntityMap; // bucketIndex -> entityInQueryIndex
        [ReadOnly] public NativeArray<float3> positions;
        public NativeHashMap<int, float3> centerPositions; // bucketIndex -> position

        public void Execute()
        {
            NativeMultiHashMapIterator<int> iterator;
            NativeArray<int> keys = bucketEntityMap.GetKeyArray(Allocator.Temp);
            int entityIndex = 0;
            int bucketIndex = 0;
            int counter = 0;
            float3 sumPos = float3.zero;
            
            for (int i = 0; i < keys.Length; i++)
            {
                bucketIndex = keys[i];
                if (bucketEntityMap.TryGetFirstValue(bucketIndex, out entityIndex, out iterator))
                {
                    do
                    {
                        float3 pos = positions[entityIndex];
                        sumPos += pos;
                        counter++;
                    }
                    while (bucketEntityMap.TryGetNextValue(out entityIndex, ref iterator));

                    centerPositions[bucketIndex] = sumPos / counter;  
                    //Debug.Log(string.Format("assign position for bucket: {0}    pos {1}  ", bucketIndex, centerPositions[bucketIndex]));
                    counter = 0;
                    sumPos = float3.zero;
                    //Debug.DrawLine(float3.zero, centerPositions[bucketIndex], Color.red);
                }

            }
        }
    }
    
    [BurstCompile]
    protected override unsafe void OnUpdate()
    {
        int bucketWidth = (int)StaticValues.BUCKET_SIZE; 
        int bucketsPerAxis = (int)(StaticValues.SIZE / StaticValues.BUCKET_SIZE);
        int bucketCount = (int)math.pow(bucketsPerAxis, 3);
        int bucketsPAHalf = bucketsPerAxis / 2;
        int count = query.CalculateEntityCount();
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);

        NativeArray<float3> translations = new NativeArray<float3>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<quaternion> rotations = new NativeArray<quaternion>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> cohesionValues = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeMultiHashMap<int, int> bucketEntityMap = new NativeMultiHashMap<int, int>(count, Allocator.TempJob); // bucketIndex -> entityInQueryIndex
        NativeHashMap<int, float3> bucketCenterPositions = new NativeHashMap<int, float3>(count, Allocator.TempJob); // bucketIndex -> bucket center pos
        NativeHashMap<int, int> entityBucketIndexMap = new NativeHashMap<int, int>(count, Allocator.TempJob); // entityInQueryIndex -> bucketIndex
        NativeArray<AnimalMovementData> mvmtData = new NativeArray<AnimalMovementData>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); 

        NativeMultiHashMap<int, int> .ParallelWriter parallelBucketMap = bucketEntityMap.AsParallelWriter();
        NativeHashMap<int, int>.ParallelWriter parallelEntityMap = entityBucketIndexMap.AsParallelWriter();
        
        JobHandle fillLists = Entities
            .WithName("fillListsJob")
            .WithAll<ObstacleTag>()
            .ForEach( (int entityInQueryIndex, in Translation translation, in Rotation rotation, in AnimalMovementData movData, in BoidBehaviourData boidData) => 
            {
                translations[entityInQueryIndex] = translation.Value;
                rotations[entityInQueryIndex] = rotation.Value;
                cohesionValues[entityInQueryIndex] = boidData.cohesion;
                mvmtData[entityInQueryIndex] = movData;

                int3 roundedPosition = new int3((int) translation.Value.x, (int) translation.Value.y, (int) translation.Value.z);
                int3 bucket3D = (roundedPosition / bucketWidth);
                int3 offsetBucket3D = bucket3D + new int3(bucketsPAHalf, bucketsPAHalf, bucketsPAHalf);
                int bucketIndex = (offsetBucket3D.x - 1) * bucketsPerAxis + offsetBucket3D.z + (bucketsPerAxis * bucketsPerAxis * offsetBucket3D.y);

                //Debug.Log(string.Format("fill bucket: idx {0}  pos {1}   pos {2}", bucketIndex, roundedPosition, bucket3D));
                parallelBucketMap.Add(bucketIndex, entityInQueryIndex);
                bool result = parallelEntityMap.TryAdd(entityInQueryIndex, bucketIndex);
            }).ScheduleParallel(Dependency);

        var getCenterJob = new GetCenter
        {
            bucketEntityMap = bucketEntityMap,
            positions = translations,
            centerPositions = bucketCenterPositions,
        };
        JobHandle centerJobHandle = getCenterJob.Schedule(fillLists);

        var steerCenterJob = Entities
            .WithName("steerCenterJob")
            .WithReadOnly(bucketCenterPositions)
            .WithReadOnly(entityBucketIndexMap)
            .WithAll<AnimalTag>()
            .ForEach( (int entityInQueryIndex) => {

                if (entityBucketIndexMap.ContainsKey(entityInQueryIndex))
                {
                    int bucketIndex = entityBucketIndexMap[entityInQueryIndex];
                    //Debug.Log(string.Format("bucket: {0}", bucketIndex));
                    if (bucketCenterPositions.ContainsKey(bucketIndex))
                    {
                        float3 bucketCenter = bucketCenterPositions[bucketIndex];
                        float3 direction = bucketCenter - translations[entityInQueryIndex];
                        AnimalMovementData mData = mvmtData[entityInQueryIndex];
                        mData.direction = math.lerp(mData.direction, math.normalizesafe(direction), cohesionValues[entityInQueryIndex]);

                        mvmtData[entityInQueryIndex] = mData;
                        //Debug.Log(string.Format("{0}  {1}   {2}  found", bucketIndex, bucketCenter, translations[entityInQueryIndex]));
                        //Debug.DrawLine(translations[entityInQueryIndex], bucketCenter, Color.green);
                    }
                }
            }).ScheduleParallel(centerJobHandle);

        JobHandle writeBack = Entities.
            WithName("writeBackJob").
            WithReadOnly(mvmtData).
            WithAll<AnimalTag>()
            .ForEach((int entityInQueryIndex, ref AnimalMovementData animalMovementData) => {
                //if (et % animalMovementData.updateInterval < 0.001)
                //{
                    AnimalMovementData mData = mvmtData[entityInQueryIndex];
                    //mData.updateInterval = (int) (entityInQueryIndex * dt * 100f);
                    animalMovementData = mData;
                //}
            }).ScheduleParallel(steerCenterJob);


        Dependency = JobHandle.CombineDependencies(Dependency, fillLists);
        Dependency = JobHandle.CombineDependencies(Dependency, centerJobHandle);
        Dependency = JobHandle.CombineDependencies(Dependency, steerCenterJob);
        Dependency = JobHandle.CombineDependencies(Dependency, writeBack);

        Dependency.Complete();

        translations.Dispose();
        rotations.Dispose();
        cohesionValues.Dispose();
        bucketEntityMap.Dispose();
        bucketCenterPositions.Dispose();
        entityBucketIndexMap.Dispose();
        mvmtData.Dispose();
    }
}
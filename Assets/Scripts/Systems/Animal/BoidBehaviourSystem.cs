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
    EntityQuery obstacleQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        buildPhysicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();

        obstacleQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<ObstacleTag>(), ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<Rotation>() }
        });

        Enabled = true;
    }

    [BurstCompile]
    protected struct BucketCenterAlignment : IJob
    {
        [ReadOnly] public NativeMultiHashMap<int, int> bucketEntityMap; // bucketIndex -> entityInQueryIndex
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<quaternion> rotations;
        public NativeHashMap<int, float3> centerPosition; // bucketIndex -> position
        public NativeHashMap<int, float3> averageHeading; // bucketIndex -> average heading of boids

        public void Execute()
        {
            NativeMultiHashMapIterator<int> iterator;
            NativeArray<int> keys = bucketEntityMap.GetKeyArray(Allocator.Temp);
            int entityIndex = 0;
            int bucketIndex = 0;
            int counter = 0;
            float3 sumPos = float3.zero;
            float3 sumHeading = float3.zero;

            for (int i = 0; i < keys.Length; i++)
            {
                bucketIndex = keys[i];
                if (bucketEntityMap.TryGetFirstValue(bucketIndex, out entityIndex, out iterator))
                {
                    do
                    {
                        float3 pos = positions[entityIndex];
                        sumPos += pos;
                        sumHeading += math.forward(rotations[entityIndex]);
                        counter++;
                    }
                    while (bucketEntityMap.TryGetNextValue(out entityIndex, ref iterator));

                    centerPosition[bucketIndex] = sumPos / counter;
                    averageHeading[bucketIndex] = sumHeading / counter;
                    //Debug.DrawLine(sumPos / counter, sumPos / counter + sumHeading / counter, Color.white);
                    //Debug.Log(string.Format("assign position for bucket: {0}    pos {1}  ", bucketIndex, centerPositions[bucketIndex]));
                    counter = 0;
                    sumPos = float3.zero;
                    sumHeading = float3.zero;
                }

            }
        }
    }

    [BurstCompile]
    protected struct CheckNeighbourhood : IJob
    {
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<quaternion> rotations;
        [ReadOnly] public NativeArray<float> separationValues;
        [ReadOnly] public NativeMultiHashMap<int, int> bucketEntityMap; // bucketIndex -> entityInQueryIndex
        [ReadOnly] public NativeHashMap<int, float3> bucketCenterPositions; // bucketIndex -> bucket center position

        public NativeHashMap<int, float3>.ParallelWriter entityTargetDirection;

        public void Execute()
        {
            NativeMultiHashMapIterator<int> iterator;
            NativeArray<int> keys = bucketEntityMap.GetKeyArray(Allocator.Temp);
            int entityIndex = 0;
            int bucketIndex = 0;
            float nearestDistance = StaticValues.NEIGHBOUR_RADIUS;
            float distance = -1;
            int nearestEntity = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                bucketIndex = keys[i];

                if (bucketEntityMap.TryGetFirstValue(bucketIndex, out entityIndex, out iterator))
                {
                    do
                    {
                        float3 ownPos = positions[entityIndex];
                        nearestEntity = entityIndex;
                        NativeMultiHashMap<int, int>.Enumerator e = bucketEntityMap.GetValuesForKey(bucketIndex);
                        while (e.MoveNext())
                        {
                            int nextEntIdx = e.Current;
                            if (nextEntIdx != entityIndex)
                            {
                                float3 compPos = positions[nextEntIdx];
                                distance = math.distancesq(ownPos, compPos);
                                if (distance < StaticValues.NEIGHBOUR_RADIUS)
                                {
                                    nearestEntity = math.select(nearestEntity, nextEntIdx, distance < nearestDistance);
                                    nearestDistance = math.select(nearestDistance, distance, distance < nearestDistance);
                                }
                            }
                        }
                        float3 dirToNearest = positions[entityIndex] - positions[nearestEntity];
                        float3 ownFwd = math.forward(rotations[entityIndex]);

                        float3 newValue = math.lerp(ownFwd, dirToNearest, separationValues[entityIndex]);
                        entityTargetDirection.TryAdd(entityIndex, newValue);

                        nearestDistance = StaticValues.NEIGHBOUR_RADIUS;
                    }
                    while (bucketEntityMap.TryGetNextValue(out entityIndex, ref iterator));
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
        int count = obstacleQuery.CalculateEntityCount();
        float et = Convert.ToSingle(Time.ElapsedTime);
        float dt = Convert.ToSingle(Time.DeltaTime);

        NativeArray<float3> translations = new NativeArray<float3>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<quaternion> rotations = new NativeArray<quaternion>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> cohesionValues = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> alignmentValues = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationValues = new NativeArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeMultiHashMap<int, int> bucketEntityMap = new NativeMultiHashMap<int, int>(count, Allocator.TempJob); // bucketIndex -> entityInQueryIndex
        NativeHashMap<int, float3> bucketCenterPositions = new NativeHashMap<int, float3>(count, Allocator.TempJob); // bucketIndex -> bucket center pos
        NativeHashMap<int, float3> bucketAvergeHeading = new NativeHashMap<int, float3>(count, Allocator.TempJob); // bucketIndex -> average heading in the bucket
        NativeHashMap<int, int> entityBucketIndexMap = new NativeHashMap<int, int>(count, Allocator.TempJob); // entityInQueryIndex -> bucketIndex
        NativeArray<AnimalMovementData> mvmtData = new NativeArray<AnimalMovementData>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeHashMap<int, float3> entityToCenterTargetDirection = new NativeHashMap<int, float3>(count, Allocator.TempJob); // entityInQueryIndex -> bucketCenterDirection
        NativeHashMap<int, float3> entitySeparationTargetDirection = new NativeHashMap<int, float3>(count, Allocator.TempJob); // entityInQueryIndex -> separationDirection

        NativeMultiHashMap<int, int>.ParallelWriter parallelBucketMap = bucketEntityMap.AsParallelWriter();
        NativeHashMap<int, int>.ParallelWriter parallelEntityMap = entityBucketIndexMap.AsParallelWriter();

        JobHandle fillLists = Entities
            .WithName("fillListsJob")
            .WithAll<ObstacleTag>()
            .ForEach((int entityInQueryIndex, in Translation translation, in Rotation rotation, in AnimalMovementData movData, in BoidBehaviourData boidData) =>
            {
                translations[entityInQueryIndex] = translation.Value;
                rotations[entityInQueryIndex] = rotation.Value;
                cohesionValues[entityInQueryIndex] = boidData.cohesion;
                alignmentValues[entityInQueryIndex] = boidData.alignmewnt;
                separationValues[entityInQueryIndex] = boidData.separation;
                mvmtData[entityInQueryIndex] = movData;

                int3 roundedPosition = new int3((int)translation.Value.x, (int)translation.Value.y, (int)translation.Value.z);
                int3 bucket3D = (roundedPosition / bucketWidth);
                int3 offsetBucket3D = bucket3D + new int3(bucketsPAHalf, bucketsPAHalf, bucketsPAHalf);
                int bucketIndex = (offsetBucket3D.x - 1) * bucketsPerAxis + offsetBucket3D.z + (bucketsPerAxis * bucketsPerAxis * offsetBucket3D.y);

                //Debug.Log(string.Format("fill bucket: idx {0}  pos {1}   pos {2}", bucketIndex, roundedPosition, bucket3D));
                parallelBucketMap.Add(bucketIndex, entityInQueryIndex);
                bool result = parallelEntityMap.TryAdd(entityInQueryIndex, bucketIndex);
            }).ScheduleParallel(Dependency);

        var getCenterJob = new BucketCenterAlignment
        {
            bucketEntityMap = bucketEntityMap,
            positions = translations,
            rotations = rotations,
            centerPosition = bucketCenterPositions,
            averageHeading = bucketAvergeHeading,
        };
        JobHandle centerJobHandle = getCenterJob.Schedule(fillLists);

        // Cohesion
        NativeHashMap<int, float3>.ParallelWriter parallelEntityToCenterDir = entityToCenterTargetDirection.AsParallelWriter();
        var steerCenterJob = Entities
            .WithName("steerCenterJob")
            .WithReadOnly(bucketCenterPositions)
            .WithReadOnly(entityBucketIndexMap)
            .WithReadOnly(mvmtData)
            .WithAll<AnimalTag>()
            .ForEach((int entityInQueryIndex) => {

                if (entityBucketIndexMap.ContainsKey(entityInQueryIndex))
                {
                    int bucketIndex = entityBucketIndexMap[entityInQueryIndex];
                    //Debug.Log(string.Format("bucket: {0}", bucketIndex));
                    if (bucketCenterPositions.ContainsKey(bucketIndex))
                    {
                        float3 bucketCenter = bucketCenterPositions[bucketIndex];
                        float3 direction = bucketCenter - translations[entityInQueryIndex];
                        AnimalMovementData mData = mvmtData[entityInQueryIndex];
                        parallelEntityToCenterDir.TryAdd(entityInQueryIndex, math.normalizesafe(direction));

                        //Debug.Log(string.Format("{0}  {1}   {2}  found", bucketIndex, bucketCenter, translations[entityInQueryIndex]));
                    }
                }
            }).ScheduleParallel(centerJobHandle);


        // Separation
        NativeHashMap<int, float3>.ParallelWriter parallelEntitySeparationDir = entitySeparationTargetDirection.AsParallelWriter();
        var keepDistanceJob = new CheckNeighbourhood
        {
            positions = translations,
            rotations = rotations,
            separationValues = separationValues,
            bucketEntityMap = bucketEntityMap,
            entityTargetDirection = parallelEntitySeparationDir,
            bucketCenterPositions = bucketCenterPositions,
        };
        JobHandle keepDistanceJobHandle = keepDistanceJob.Schedule(steerCenterJob);


        // Unify target directions
        JobHandle unifyDirections = Entities
           .WithName("unifyDirectionsJob")
           .WithReadOnly(entityBucketIndexMap)
           .WithReadOnly(entitySeparationTargetDirection)
           .WithReadOnly(entityToCenterTargetDirection)
           .WithReadOnly(bucketAvergeHeading)
           .WithReadOnly(bucketCenterPositions)
           .WithAll<AnimalTag>()
           .ForEach((int entityInQueryIndex, ref AnimalMovementData animalMovementData) => {
               AnimalMovementData mData = mvmtData[entityInQueryIndex];

               float3 separationDir = entitySeparationTargetDirection[entityInQueryIndex];
               separationDir *= separationValues[entityInQueryIndex];

               float3 centerDirection = entityToCenterTargetDirection[entityInQueryIndex];
               centerDirection *= cohesionValues[entityInQueryIndex];

               float3 bucketCenter = float3.zero;
               float3 alignmentDirection = float3.zero;
               if (entityBucketIndexMap.ContainsKey(entityInQueryIndex))
               {
                   int bucketIndex = entityBucketIndexMap[entityInQueryIndex];
                   bucketCenter = bucketCenterPositions[bucketIndex];
                   if (bucketAvergeHeading.ContainsKey(bucketIndex))
                   {
                       alignmentDirection = bucketAvergeHeading[bucketIndex];
                       alignmentDirection *= alignmentValues[entityInQueryIndex];
                   }
               }

               float d = math.distancesq(bucketCenter, translations[entityInQueryIndex]);
               float lerpFactor = (d - StaticValues.FLOCK_RADIUS) / d;
               float3 newTargetDir = math.lerp(math.forward(rotations[entityInQueryIndex]), math.normalizesafe(centerDirection + separationDir + alignmentDirection), lerpFactor);
               mData.direction = newTargetDir;
               animalMovementData = mData;

           }).ScheduleParallel(keepDistanceJobHandle);


        JobHandle writeBack = Entities
            .WithName("writeBackJob")
            .WithReadOnly(mvmtData)
            .WithAll<AnimalTag>()
            .ForEach((int entityInQueryIndex, ref AnimalMovementData animalMovementData, ref Rotation rotation) => {
                AnimalMovementData mData = mvmtData[entityInQueryIndex];
                rotation.Value = quaternion.LookRotationSafe(animalMovementData.direction, math.up());
            }).ScheduleParallel(unifyDirections);


        Dependency = JobHandle.CombineDependencies(Dependency, fillLists);
        Dependency = JobHandle.CombineDependencies(Dependency, centerJobHandle);
        Dependency = JobHandle.CombineDependencies(Dependency, steerCenterJob);
        Dependency = JobHandle.CombineDependencies(Dependency, keepDistanceJobHandle);
        Dependency = JobHandle.CombineDependencies(Dependency, unifyDirections);
        Dependency = JobHandle.CombineDependencies(Dependency, writeBack);

        Dependency.Complete();

        translations.Dispose();
        rotations.Dispose();
        cohesionValues.Dispose();
        alignmentValues.Dispose();
        separationValues.Dispose();
        bucketEntityMap.Dispose();
        bucketCenterPositions.Dispose();
        bucketAvergeHeading.Dispose();
        entityBucketIndexMap.Dispose();
        mvmtData.Dispose();
        entityToCenterTargetDirection.Dispose();
        entitySeparationTargetDirection.Dispose();
    }
}
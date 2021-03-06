﻿using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Profiling;

/// <summary>
/// Class that  spawns animals and manages their behaviour. 
/// </summary>
public class AnimalManager : MonoBehaviour
{
    [Range(1, 100000)]
    public uint amount = 1;
    [Range(5, 100)]
    public float spread_scale = 5;

    public bool spawnCircular;
    public bool specificAngle;
    [Range(0, 90)]
    public float spawnAngle;

    public GameObject rawPrefab;

    protected EntityManager entityManager;
    protected Entity animalPrefab;
    protected BlobAssetStore blobAsset;


    private void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        GameObjectConversionSettings settings;
        blobAsset = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAsset);

        if (settings != null)
        {
            animalPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(rawPrefab, settings);
            SpawnRandomAnimals(amount);
        }
    }

    private void OnAnimalPrefabLoaded (AsyncOperationHandle<GameObject> operationHandle)
    {

        GameObjectConversionSettings settings;
        blobAsset = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAsset);

        if (settings != null)
        {
            SpawnRandomAnimals(amount);
        }
    }


    private void OnDestroy()
    {
        if (blobAsset != null)
        {
            blobAsset.Dispose();
        }
    }


    protected void SpawnRandomAnimals(uint amount)
    {
        if (animalPrefab != null)
        {
            UnityEngine.Random.InitState(System.Convert.ToInt32(Time.deltaTime * 10000f));
            
            float circularAngle = 0f;
            for (int i = 0; i < amount; i++)
            {
                Entity animal = entityManager.Instantiate(animalPrefab);

                float3 pos = float3.zero;
                float3 dir = float3.zero;

                if(spawnCircular)
                {
                    pos = getCircularSpawnPosition(circularAngle, spread_scale);
                    circularAngle += (2 * math.PI) / amount;
                    dir = float3.zero - pos;
                }
                else if (specificAngle)
                {
                    pos = new float3(i * 5f, 0f, 0f);
                    dir = spawnAtAngle(i, pos);
                }
                else
                {
                    dir = new float3(UnityEngine.Random.Range(-0.25f, 0.25f),
                                            UnityEngine.Random.Range(-0.05f, 0.05f),
                                            UnityEngine.Random.Range(-0.25f, 0.25f));

                    pos = new float3(UnityEngine.Random.Range(-3f * spread_scale, 3f * spread_scale),
                                            UnityEngine.Random.Range(0.2f * spread_scale, 5.8f * spread_scale),
                                            UnityEngine.Random.Range(-3f * spread_scale, 3f * spread_scale));
                }
               
                dir = math.normalize(dir);


                Translation t = new Translation { Value = pos };
                entityManager.AddComponentData(animal, t);

                Rotation r = new Rotation { Value = quaternion.LookRotationSafe(dir, math.up()) };
                entityManager.AddComponentData(animal, r);

                float animalSpeed = 0.5f; // 0.15f;

                AnimalMovementData mvmtData = new AnimalMovementData
                {
                    direction = dir,
                    movementSpeed = animalSpeed,
                    amplitude = 0.1f,
                    updateInterval = UnityEngine.Random.Range(2, 10),
                };
                entityManager.AddComponentData(animal, mvmtData);
            }

            entityManager.DestroyEntity(animalPrefab);

        }
    }

    float3 getCircularSpawnPosition(float circAngle, float scale)
    {
        return new float3(math.cos(circAngle) * scale, 0, math.sin(circAngle) * scale);
    }

    float3 spawnAtAngle(int index, float3 position)
    {
        float angle = index % 2 == 0 ? spawnAngle : 360f - spawnAngle;
        quaternion q = quaternion.RotateY(math.radians(angle));
        return math.forward(q);
    }
}

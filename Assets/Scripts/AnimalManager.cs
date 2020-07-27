﻿using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Class that  spawns animals and manages their behaviour. 
/// </summary>
public class AnimalManager : MonoBehaviour
{
    public uint amount = 1;

    protected EntityManager entityManager;
    protected Entity animalPrefab;
    protected BlobAssetStore blobAsset;
    protected readonly float3 up = new float3(0f, 1f, 0f);


    private void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        GameObject animalPrefabObject = Resources.Load<GameObject>("Prefabs/Animal");
        GameObjectConversionSettings settings;
        blobAsset = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAsset);

        if (settings != null && animalPrefabObject != null)
        {
            animalPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(animalPrefabObject, settings);
        }
    }


    private void Start()
    {
        SpawnRandomAnimals(amount);
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
            float scale = amount / 100f;
            scale = math.clamp(scale, 10f, 10000f);
            UnityEngine.Random.InitState(System.Convert.ToInt32(Time.deltaTime * 10000f));
            
            for (int i = 0; i < amount; i++)
            {
                Entity animal = entityManager.Instantiate(animalPrefab);
                
                float3 dir = new float3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-3f, 3f));
                dir = math.normalize(dir);

                float3 pos = new float3(UnityEngine.Random.Range(-3f * scale, 3f * scale),
                                        UnityEngine.Random.Range(0.2f * scale, 5.8f * scale),
                                        UnityEngine.Random.Range(-3f * scale, 3f * scale));

                Translation t = new Translation();
                t.Value = pos;
                entityManager.AddComponentData(animal, t);

                Rotation r = new Rotation();
                r.Value = quaternion.LookRotationSafe(dir, up);
                entityManager.AddComponentData(animal, r);

                AnimalMovementData mvmtData = new AnimalMovementData();
                mvmtData.direction = dir; 
                mvmtData.movementSpeed = 1f;
                mvmtData.amplitude = 0.25f;
                mvmtData.updateInterval = UnityEngine.Random.Range(2, 10);
                entityManager.AddComponentData(animal, mvmtData);
            }

            entityManager.DestroyEntity(animalPrefab);
        }
    }
}

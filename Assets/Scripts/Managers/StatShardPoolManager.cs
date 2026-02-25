using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A centralized Object Pool for managing Stat Shard (Chest) drops from enemies.
/// </summary>
public class StatShardPoolManager : MonoBehaviour
{
    public static StatShardPoolManager Instance { get; private set; }

    private Dictionary<GameObject, ObjectPool<StatShardInteractable>> pools = new Dictionary<GameObject, ObjectPool<StatShardInteractable>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Spawns a Stat Shard from its ObjectPool.
    /// </summary>
    public StatShardInteractable SpawnShard(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<StatShardInteractable>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(prefab);
                    StatShardInteractable shard = obj.GetComponent<StatShardInteractable>();
                    shard.SourcePrefab = prefab; // Cache prefab so it knows which pool to return to
                    return shard;
                },
                actionOnGet: (shard) => 
                {
                    shard.transform.SetParent(null); // Keep in world space
                    shard.gameObject.SetActive(true);
                    shard.ResetShard(); // Triggers "Appear" animation
                },
                actionOnRelease: (shard) => 
                {
                    shard.gameObject.SetActive(false);
                    shard.transform.SetParent(transform); // Hide in manager
                },
                actionOnDestroy: (shard) => Destroy(shard.gameObject),
                collectionCheck: false,
                defaultCapacity: 20,
                maxSize: 100
            );
            
            pools[prefab] = pool;
        }

        StatShardInteractable spawnedShard = pool.Get();
        spawnedShard.transform.SetPositionAndRotation(position, rotation);
        
        return spawnedShard;
    }

    /// <summary>
    /// Returns a Stat Shard to its original ObjectPool.
    /// </summary>
    public void ReturnToPool(StatShardInteractable shard)
    {
        if (shard == null || shard.SourcePrefab == null)
        {
            if (shard != null) Destroy(shard.gameObject); // Fallback
            return;
        }

        if (pools.TryGetValue(shard.SourcePrefab, out var pool))
        {
            pool.Release(shard);
        }
        else
        {
            Destroy(shard.gameObject);
        }
    }
}

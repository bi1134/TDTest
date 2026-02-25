using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Centralized manager for spawning and pooling generic VFX prefabs (like Elemental Strikes).
/// Uses Unity's ObjectPool to prevent FPS drops from rapid Instantiate() calls.
/// </summary>
public class VFXPoolManager : MonoBehaviour
{
    public static VFXPoolManager Instance { get; private set; }

    // Dictionary tracking unique ObjectPools per prefab
    private Dictionary<GameObject, ObjectPool<PooledVFX>> pools = new Dictionary<GameObject, ObjectPool<PooledVFX>>();

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
    /// Spawns a VFX prefab from its pool, initializing it at the target position.
    /// It will automatically return to the pool after play duration.
    /// </summary>
    public void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;

        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<PooledVFX>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(prefab);
                    
                    // Attach auto-returner component if missing
                    PooledVFX vfx = obj.GetComponent<PooledVFX>();
                    if (vfx == null) vfx = obj.AddComponent<PooledVFX>();
                    
                    vfx.Initialize(this, prefab);
                    return vfx;
                },
                actionOnGet: (vfx) => 
                {
                    vfx.transform.SetParent(null);
                    vfx.gameObject.SetActive(true);
                    vfx.Play();
                },
                actionOnRelease: (vfx) => 
                {
                    vfx.gameObject.SetActive(false);
                    vfx.transform.SetParent(transform);
                },
                actionOnDestroy: (vfx) => Destroy(vfx.gameObject),
                collectionCheck: false,
                defaultCapacity: 20,
                maxSize: 500
            );
            
            pools[prefab] = pool;
        }

        PooledVFX spawnedVFX = pool.Get();
        spawnedVFX.transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// Returns a VFX instance to its original pool. Called internally by PooledVFX.
    /// </summary>
    public void ReturnToPool(PooledVFX vfx)
    {
        if (vfx == null || vfx.SourcePrefab == null)
        {
            if (vfx != null) Destroy(vfx.gameObject);
            return;
        }

        if (pools.TryGetValue(vfx.SourcePrefab, out var pool))
        {
            pool.Release(vfx);
        }
        else
        {
            Destroy(vfx.gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A centralized manager for dynamically pooling Turrets across the game.
/// Uses Unity's native high-performance ObjectPool<T>.
/// </summary>
public class TurretPoolManager : MonoBehaviour
{
    public static TurretPoolManager Instance { get; private set; }

    // Dictionary caching ObjectPools by their Turret Prefab reference
    private Dictionary<GameObject, ObjectPool<GameObject>> pools = new Dictionary<GameObject, ObjectPool<GameObject>>();

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
    /// Spawns a Turret from its specific pool.
    /// </summary>
    /// <param name="prefab">The Turret prefab to spawn</param>
    /// <param name="position">Spawn position</param>
    /// <param name="rotation">Spawn rotation</param>
    /// <param name="parent">Parent Transform (e.g., the Node's offset)</param>
    /// <returns>The spawned Turret GameObject</returns>
    public GameObject SpawnTurret(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(prefab);
                    
                    // Attach a tracker to know which pool this belongs to
                    var tracker = obj.AddComponent<TurretPoolTracker>();
                    tracker.SourcePrefab = prefab;
                    
                    return obj;
                },
                actionOnGet: (turretObj) => 
                {
                    turretObj.SetActive(true);
                },
                actionOnRelease: (turretObj) => 
                {
                    turretObj.SetActive(false);
                    turretObj.transform.SetParent(transform); // Parent to manager to hide from scene root
                },
                actionOnDestroy: (turretObj) => Destroy(turretObj),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 100 // Player rarely builds > 100 of the exact same turret
            );
            
            pools[prefab] = pool;
        }

        GameObject spawnedTurret = pool.Get();
        spawnedTurret.transform.SetParent(parent);
        spawnedTurret.transform.position = position;
        spawnedTurret.transform.rotation = rotation;
        
        return spawnedTurret;
    }

    /// <summary>
    /// Returns a Turret to its ObjectPool.
    /// Called when the player sells a Turret.
    /// </summary>
    public void ReturnToPool(GameObject turretObj)
    {
        if (turretObj == null) return;

        var tracker = turretObj.GetComponent<TurretPoolTracker>();
        if (tracker == null || tracker.SourcePrefab == null)
        {
            Destroy(turretObj); // Not pooled
            return;
        }

        if (pools.TryGetValue(tracker.SourcePrefab, out var pool))
        {
            pool.Release(turretObj);
        }
        else
        {
            Destroy(turretObj);
        }
    }
}

/// <summary>
/// Simple helper component to remember which prefab a Turret was instantiated from.
/// </summary>
public class TurretPoolTracker : MonoBehaviour
{
    [HideInInspector] public GameObject SourcePrefab;
}

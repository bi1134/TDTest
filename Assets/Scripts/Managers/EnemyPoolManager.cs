using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A centralized manager for spawning and pooling Enemies across the game.
/// Uses Unity's native high-performance ObjectPool<T>.
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    // Dictionary caching ObjectPools by their Enemy Prefab reference
    private Dictionary<GameObject, ObjectPool<Enemy>> pools = new Dictionary<GameObject, ObjectPool<Enemy>>();

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
    /// Spawns an Enemy from its specific pool, initializing it at the target position.
    /// </summary>
    /// <param name="prefab">The Enemy prefab to spawn</param>
    /// <param name="position">Spawn position</param>
    /// <param name="rotation">Spawn rotation</param>
    /// <returns>The spawned Enemy component</returns>
    public Enemy SpawnEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<Enemy>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(prefab, transform);
                    Enemy enemy = obj.GetComponent<Enemy>();
                    enemy.SourcePrefab = prefab; // Cache prefab so it knows which pool to return to
                    return enemy;
                },
                actionOnGet: (enemy) => 
                {
                    enemy.gameObject.SetActive(true);
                    enemy.ResetEnemy(); // Reset HP, Shields, Debuffs, Animator, Colliders
                },
                actionOnRelease: (enemy) => 
                {
                    enemy.gameObject.SetActive(false);
                },
                actionOnDestroy: (enemy) => Destroy(enemy.gameObject),
                collectionCheck: false,
                defaultCapacity: 20,
                maxSize: 1000
            );
            
            pools[prefab] = pool;
        }

        Enemy spawnedEnemy = pool.Get();
        spawnedEnemy.transform.position = position;
        spawnedEnemy.transform.rotation = rotation;
        
        return spawnedEnemy;
    }

    /// <summary>
    /// Returns an Enemy to its original ObjectPool.
    /// Called by the Enemy script 2 seconds after death.
    /// </summary>
    /// <param name="enemy">The enemy to return</param>
    public void ReturnToPool(Enemy enemy)
    {
        if (enemy == null || enemy.SourcePrefab == null)
        {
            if (enemy != null) Destroy(enemy.gameObject); // Fallback: no pool found, just destroy
            return;
        }

        if (pools.TryGetValue(enemy.SourcePrefab, out var pool))
        {
            pool.Release(enemy);
        }
        else
        {
            // Fallback: If pool inexplicably doesn't exist, destroy
            Destroy(enemy.gameObject);
        }
    }
}

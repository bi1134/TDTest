using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A centralized manager for spawning and pooling Bullets across the game.
/// Uses Unity's native high-performance ObjectPool<T>.
/// </summary>
public class BulletPoolManager : MonoBehaviour
{
    public static BulletPoolManager Instance { get; private set; }

    // Dictionary caching ObjectPools by their Bullet Prefab reference
    private Dictionary<GameObject, ObjectPool<BulletProjectile>> pools = new Dictionary<GameObject, ObjectPool<BulletProjectile>>();

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
    /// Spawns a Bullet from its specific pool, initializing it at the target position.
    /// </summary>
    /// <param name="prefab">The Bullet prefab to spawn</param>
    /// <param name="position">Spawn position</param>
    /// <param name="rotation">Spawn rotation</param>
    /// <returns>The spawned Bullet component</returns>
    public BulletProjectile SpawnBullet(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<BulletProjectile>(
                createFunc: () => 
                {
                    GameObject obj = Instantiate(prefab);
                    BulletProjectile bullet = obj.GetComponent<BulletProjectile>();
                    bullet.SourcePrefab = prefab; // Cache prefab so it knows which pool to return to
                    return bullet;
                },
                actionOnGet: (bullet) => 
                {
                    bullet.transform.SetParent(null); // Keep active bullets cleanly in world space
                    bullet.gameObject.SetActive(true);
                    bullet.ResetBullet(); // Reset velocities, active states
                },
                actionOnRelease: (bullet) => 
                {
                    bullet.gameObject.SetActive(false);
                    bullet.transform.SetParent(transform); // Hide in the manager when inactive
                },
                actionOnDestroy: (bullet) => Destroy(bullet.gameObject),
                collectionCheck: false,
                defaultCapacity: 50,
                maxSize: 5000 // Bullets can be numerous!
            );
            
            pools[prefab] = pool;
        }

        BulletProjectile spawnedBullet = pool.Get();
        spawnedBullet.transform.SetPositionAndRotation(position, rotation);
        
        // Clear Trail Renderer AFTER teleporting it to avoid drawing huge lines across the map
        TrailRenderer tr = spawnedBullet.GetComponent<TrailRenderer>();
        if (tr != null) tr.Clear();
        
        return spawnedBullet;
    }

    /// <summary>
    /// Returns a Bullet to its original ObjectPool.
    /// Called by the BulletProjectile script when it hits something or times out.
    /// </summary>
    /// <param name="bullet">The bullet to return</param>
    public void ReturnToPool(BulletProjectile bullet)
    {
        if (bullet == null || bullet.SourcePrefab == null)
        {
            if (bullet != null) Destroy(bullet.gameObject); // Fallback
            return;
        }

        if (pools.TryGetValue(bullet.SourcePrefab, out var pool))
        {
            pool.Release(bullet);
        }
        else
        {
            // Fallback: If pool inexplicably doesn't exist, destroy
            Destroy(bullet.gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// A centralized manager for spawning and pooling generic Visual Effects across the game.
/// Uses Unity's native high-performance ObjectPool<T>.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [SerializeField] private VFXRefSO vfxReferences;

    // Dictionary holding an ObjectPool for every PREFAB we want to use
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
    /// Play an effect from the generic VFXRefSO library.
    /// </summary>
    /// <param name="type">The category of effect to play</param>
    /// <param name="position">World position</param>
    /// <param name="normal">Surface normal (e.g. wall or ground normal to spawn dust away from it)</param>
    public void PlayEffect(VFXType type, Vector3 position, Vector3 normal = default)
    {
        if (vfxReferences == null) return;

        GameObject[] prefabArray = GetPrefabArray(type);
        if (prefabArray == null || prefabArray.Length == 0) return;

        // Pick a random variant
        GameObject selectedPrefab = prefabArray[Random.Range(0, prefabArray.Length)];
        if (selectedPrefab == null) return;

        SpawnFromPool(selectedPrefab, position, normal);
    }

    /// <summary>
    /// Spawns a specific prefab (useful if bullets have their own unique explosion prefabs not in the generic SO)
    /// </summary>
    public void PlaySpecificEffect(GameObject prefab, Vector3 position, Vector3 normal = default)
    {
        if (prefab == null) return;
        SpawnFromPool(prefab, position, normal);
    }

    private void SpawnFromPool(GameObject prefab, Vector3 position, Vector3 normal)
    {
        // 1. Get or Create the Pool for this specific Prefab
        if (!pools.TryGetValue(prefab, out var pool))
        {
            // First time seeing this prefab -> Make a new pool for it
            pool = new ObjectPool<GameObject>(
                createFunc: () => 
                {
                    // Action when pool needs a brand new object
                    GameObject obj = Instantiate(prefab, transform); // keep under VFXManager hierarchy
                    // Attach AutoReturnToPool script
                    var returner = obj.AddComponent<PooledVFXReturner>();
                    returner.Setup(pool);
                    return obj;
                },
                actionOnGet: (obj) => 
                {
                    // Action when pulling from pool
                    obj.SetActive(true);
                },
                actionOnRelease: (obj) => 
                {
                    // Action when returning to pool
                    obj.SetActive(false);
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false, // Turn off for max performance in prod
                defaultCapacity: 20,
                maxSize: 200 // Prevent memory leaks if hundreds spawn at once
            );
            
            pools[prefab] = pool;
        }

        // 2. Grab an instance from the pool
        GameObject vfxInstance = pool.Get();

        // 3. Set Position & Rotation
        vfxInstance.transform.position = position;
        
        if (normal != default && normal.sqrMagnitude > 0.01f)
        {
            // Point the effect AWAY from the normal (e.g. blood splatters "out" from the flesh)
            vfxInstance.transform.rotation = Quaternion.LookRotation(normal);
        }
        else
        {
            // Default random rotation for variety (good for explosions/poofs)
            vfxInstance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
        
        // Note: The PooledVFXReturner script on the object will automatically return it 
        // to this pool when its ParticleSystem finishes playing!
    }

    private GameObject[] GetPrefabArray(VFXType type)
    {
        return type switch
        {
            VFXType.Blood           => vfxReferences.bloodSplatters,
            VFXType.ShieldSpark     => vfxReferences.shieldSparks,
            VFXType.ShieldBreak     => vfxReferences.shieldBreaks,
            VFXType.GroundDust      => vfxReferences.groundDust,
            VFXType.GenericExplosion=> vfxReferences.genericExplosions,
            VFXType.DefaultHit      => vfxReferences.defaultHits,
            VFXType.DeathPoof       => vfxReferences.deathPoofs,
            VFXType.ItemDisappear   => vfxReferences.itemDisappear,
            VFXType.ElectricStrike  => vfxReferences.electricStrike,
            VFXType.IceStrike       => vfxReferences.iceStrike,
            VFXType.ChunkExpand     => vfxReferences.chunkExpand,
            _ => null
        };
    }
}

/// <summary>
/// Helper script that sits on the pooled GameObjects.
/// It waits for particle systems to finish, then automatically returns itself to the VFXManager's pool.
/// </summary>
public class PooledVFXReturner : MonoBehaviour
{
    private ObjectPool<GameObject> parentPool;
    private ParticleSystem mainPS;

    public void Setup(ObjectPool<GameObject> pool)
    {
        parentPool = pool;
        mainPS = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (mainPS != null)
        {
            // Start listening for when particle system stops
            var invokeObj = gameObject; // Capture closure
            float duration = mainPS.main.duration;
            
            // Wait slightly longer than duration to ensure particles are dead. 
            // Better yet, use ParticleSystem's own checking if possible, or just a simple timer:
            Invoke(nameof(ReturnToPool), duration + 0.5f);
            
            // For more robust systems, you can check !mainPS.IsAlive(true), 
            // but Invoke runs on the C++ side and is ultra fast for TD games.
        }
        else
        {
            // If it's just a mesh/sprite with no particles, return it after a generic 2 seconds
            Invoke(nameof(ReturnToPool), 2f);
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void ReturnToPool()
    {
        if (parentPool != null && gameObject.activeSelf)
        {
            parentPool.Release(gameObject);
        }
    }
}

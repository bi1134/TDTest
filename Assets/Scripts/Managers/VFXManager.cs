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
    public void PlayEffect(VFXType type, Vector3 position, Vector3 normal = default)
    {
        if (vfxReferences == null) return;

        GameObject[] prefabArray = GetPrefabArray(type);
        if (prefabArray == null || prefabArray.Length == 0) return;

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

    /// <summary>
    /// Spawn a persistent (looping) effect that does NOT auto-return to pool.
    /// Caller must call ReleasePersistentEffect() to return it.
    /// Optionally parents to a transform so it follows the object.
    /// </summary>
    public GameObject SpawnPersistentEffect(VFXType type, Vector3 position, Transform parent = null)
    {
        if (vfxReferences == null) return null;

        GameObject[] prefabArray = GetPrefabArray(type);
        if (prefabArray == null || prefabArray.Length == 0) return null;

        GameObject prefab = prefabArray[Random.Range(0, prefabArray.Length)];
        if (prefab == null) return null;

        var pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;

        if (parent != null)
            instance.transform.SetParent(parent, true);

        // Cancel auto-return so it stays alive until manually released
        var returner = instance.GetComponent<PooledVFXReturner>();
        if (returner != null) returner.CancelAutoReturn();

        return instance;
    }

    /// <summary>
    /// Manually return a persistent effect to the pool.
    /// </summary>
    public void ReleasePersistentEffect(GameObject instance)
    {
        if (instance == null) return;

        // Unparent before releasing back to manager hierarchy
        instance.transform.SetParent(transform, false);

        var returner = instance.GetComponent<PooledVFXReturner>();
        if (returner != null)
            returner.ManualRelease();
        else
            instance.SetActive(false);
    }

    // ─── Internal ───────────────────────────────────────────────────────────

    private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out var pool))
            return pool;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject obj = Instantiate(prefab, transform);
                var returner = obj.AddComponent<PooledVFXReturner>();
                returner.Setup(pool);
                return obj;
            },
            actionOnGet: (obj) =>
            {
                obj.SetActive(true);
            },
            actionOnRelease: (obj) =>
            {
                obj.SetActive(false);
            },
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: false,
            defaultCapacity: 20,
            maxSize: 200
        );

        pools[prefab] = pool;
        return pool;
    }

    private void SpawnFromPool(GameObject prefab, Vector3 position, Vector3 normal)
    {
        var pool = GetOrCreatePool(prefab);
        GameObject vfxInstance = pool.Get();

        vfxInstance.transform.position = position;

        if (normal != default && normal.sqrMagnitude > 0.01f)
            vfxInstance.transform.rotation = Quaternion.LookRotation(normal);
        else
            vfxInstance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }

    private GameObject[] GetPrefabArray(VFXType type)
    {
        return type switch
        {
            VFXType.Blood           => vfxReferences.bloodSplatters,
            VFXType.ShieldSpark     => vfxReferences.shieldSparks,
            VFXType.ShieldBreak     => vfxReferences.shieldBreaks,
            VFXType.GroundDust      => vfxReferences.groundDust,
            VFXType.GenericExplosion => vfxReferences.genericExplosions,
            VFXType.DefaultHit      => vfxReferences.defaultHits,
            VFXType.DeathPoof       => vfxReferences.deathPoofs,
            VFXType.ItemDisappear   => vfxReferences.itemDisappear,
            VFXType.ElectricStrike  => vfxReferences.electricStrike,
            VFXType.IceStrike       => vfxReferences.iceStrike,
            VFXType.ChunkExpand     => vfxReferences.chunkExpand,
            VFXType.UpgradeReady    => vfxReferences.upgradeReady,
            _ => null
        };
    }
}

/// <summary>
/// Helper script that sits on the pooled GameObjects.
/// It waits for particle systems to finish, then automatically returns itself to the VFXManager's pool.
/// For persistent/looping effects, CancelAutoReturn() prevents the auto-release.
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
            float duration = mainPS.main.duration;
            Invoke(nameof(ReturnToPool), duration + 0.5f);
        }
        else
        {
            Invoke(nameof(ReturnToPool), 2f);
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    /// <summary>Cancel the auto-return timer. Used for persistent/looping effects.</summary>
    public void CancelAutoReturn()
    {
        CancelInvoke();
    }

    /// <summary>Manually return this effect to its pool.</summary>
    public void ManualRelease()
    {
        CancelInvoke();
        if (parentPool != null && gameObject.activeSelf)
            parentPool.Release(gameObject);
    }

    private void ReturnToPool()
    {
        if (parentPool != null && gameObject.activeSelf)
        {
            parentPool.Release(gameObject);
        }
    }
}

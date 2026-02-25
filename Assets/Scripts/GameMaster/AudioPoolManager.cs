using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Audio;

/// <summary>
/// A centralized Object Pool for managing 3D AudioSources to prevent Instantiate/Destroy garbage collection overhead.
/// </summary>
public class AudioPoolManager : MonoBehaviour
{
    public static AudioPoolManager Instance { get; private set; }

    [SerializeField] private int defaultCapacity = 20;
    [SerializeField] private int maxSize = 100;

    private ObjectPool<AudioSource> audioPool;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializePool();
    }

    private void InitializePool()
    {
        audioPool = new ObjectPool<AudioSource>(
            createFunc: () =>
            {
                GameObject obj = new GameObject("Pooled_3D_AudioSource");
                obj.transform.SetParent(transform);
                AudioSource source = obj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                return source;
            },
            actionOnGet: (source) =>
            {
                source.gameObject.SetActive(true);
            },
            actionOnRelease: (source) =>
            {
                source.Stop();
                source.clip = null;
                source.gameObject.SetActive(false);
                source.transform.SetParent(transform); // Return to under manager
            },
            actionOnDestroy: (source) =>
            {
                if (source != null) Destroy(source.gameObject);
            },
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    /// <summary>
    /// Grabs an AudioSource from the pool, copies the 3D spatial properties from the reference source, plays the clip, and automatically returns it when finished.
    /// </summary>
    public void PlayPooledSound(AudioClip clip, Vector3 position, float volume, float pitch, AudioSource reference3DSource)
    {
        if (clip == null || reference3DSource == null) return;

        AudioSource source = audioPool.Get();
        
        // Position it at the event origin
        source.transform.position = position;

        // Copy 3D spatial settings from the master 3D reference source
        source.spatialBlend = reference3DSource.spatialBlend;
        source.rolloffMode = reference3DSource.rolloffMode;
        source.minDistance = reference3DSource.minDistance;
        source.maxDistance = reference3DSource.maxDistance;
        source.outputAudioMixerGroup = reference3DSource.outputAudioMixerGroup;

        // Apply specific clip attributes
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;

        source.Play();

        // Start routine to return to pool after clip finishes playing
        float playDuration = clip.length / pitch;
        StartCoroutine(ReturnToPoolRoutine(source, playDuration));
    }

    private IEnumerator ReturnToPoolRoutine(AudioSource source, float delay)
    {
        yield return Helpers.GetWaitForSecond(delay + 0.1f); // Tiny buffer to avoid cutting off verbatim ends
        if (source != null && source.gameObject.activeInHierarchy)
        {
            audioPool.Release(source);
        }
    }
}

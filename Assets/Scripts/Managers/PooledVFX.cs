using System.Collections;
using UnityEngine;

/// <summary>
/// Helper component attached by VFXPoolManager to pooled effects.
/// Auto-returns the GameObject to the pool when particles finish or after a fallback timer.
/// </summary>
public class PooledVFX : MonoBehaviour
{
    private VFXPoolManager poolManager;
    public GameObject SourcePrefab { get; private set; }
    
    // Cache particle systems to play/stop them properly and read their exact duration
    private ParticleSystem[] particleSystems;
    private float maxDuration = 1.0f;

    public void Initialize(VFXPoolManager manager, GameObject prefab)
    {
        poolManager = manager;
        SourcePrefab = prefab;

        particleSystems = GetComponentsInChildren<ParticleSystem>();
        
        // Find the longest playing particle system to use as the natural return timer
        if (particleSystems.Length > 0)
        {
            maxDuration = 0f;
            foreach (var ps in particleSystems)
            {
                float dur = ps.main.duration;
                if (dur > maxDuration) maxDuration = dur;
            }
        }
        else
        {
            // Fallback for non-particle VFX (like purely animated meshes)
            maxDuration = 1.5f; 
        }
    }

    public void Play()
    {
        if (particleSystems != null)
        {
            foreach (var ps in particleSystems)
            {
                if (ps != null) ps.Play(true);
            }
        }
        
        // Start the auto-return timer (+ buffer to let trails fade)
        StartCoroutine(AutoReturnRoutine(maxDuration + 0.1f));
    }

    private IEnumerator AutoReturnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        poolManager.ReturnToPool(this);
    }
}

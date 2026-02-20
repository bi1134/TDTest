using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct ImpactPayload
{
    public float aoeRadius;
    public float aoeForce;
    public LayerMask aoeMask;
    public float falloffExponent;
    public bool explodeOnTimeout;
}

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private GameObject tracer;
    
    [Header("Bullet Properties (can be overridden at Initialize)")]
    [SerializeField] private BulletPropertiesSO defaultSettings;
    
    // Runtime settings (can be passed at Initialize or use defaultSettings)
    private BulletPropertiesSO settings;

    // Masks
    [Header("Layer Masks")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask groundMask;

    private int bounceRemaining;
    [SerializeField] private bool isActive; // Serialized for debugging, private set
    public bool IsActive => isActive; // Public read-only property
    private Rigidbody rb;
    private GameObject shooterGameObject;
    private float baseDamage;
    private ImpactPayload impactPayload;
    private bool hasPayload;           
    private bool HasAOE => hasPayload && impactPayload.aoeRadius > 0f;
    private bool isArcProjectile = false;
    private float initialVy;

    /// <summary>
    /// Public accessor for bullet settings (used by external systems)
    /// </summary>
    public BulletPropertiesSO Settings => settings ?? defaultSettings;

    private void OnEnable()
    {
        rb = GetComponent<Rigidbody>();
        isActive = true;
        
        // Use runtime settings or fall back to default
        var activeSettings = settings ?? defaultSettings;
        if (activeSettings == null)
        {
            Debug.LogWarning("BulletProjectile: No settings assigned!");
            return;
        }
        
        bounceRemaining = activeSettings.maxBounces;
        SetupVisuals(activeSettings);
    }

    private void SetupVisuals(BulletPropertiesSO activeSettings)
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            Color color = GetBulletColor(activeSettings);
            mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(mpb);
        }

        if (tracer != null && tracer.TryGetComponent(out TrailRenderer trail))
        {
            tracer.SetActive(true);
            trail.Clear();
            trail.transform.position = transform.position;

            var mpb = new MaterialPropertyBlock();
            trail.GetPropertyBlock(mpb);
            (Color trailBase, Color trailEmission) = GetTrailColors(activeSettings);
            mpb.SetColor("_BaseColor", trailBase);
            mpb.SetColor("_EmissionColor", trailEmission);
            trail.SetPropertyBlock(mpb);
        }
    }

    private Color GetBulletColor(BulletPropertiesSO s)
    {
        if (s == null) return Color.white;
        return s.bulletType switch
        {
            BulletType.Normal => new Color(191f / 255f, 131f / 255f, 0f, 1f),
            BulletType.Explosive => new Color(6f / 255f, 51f / 255f, 3f / 255f, 1f),
            BulletType.Fire => new Color(1f, 0.3f, 0f, 1f),
            BulletType.Ice => new Color(0.5f, 0.8f, 1f, 1f),
            BulletType.Electric => new Color(1f, 1f, 0.2f, 1f),
            BulletType.Utility => new Color(0.8f, 0.3f, 0.8f, 1f),
            BulletType.Buff => new Color(0.3f, 1f, 0.3f, 1f),
            _ => Color.white
        };
    }

    private (Color baseColor, Color emission) GetTrailColors(BulletPropertiesSO s)
    {
        if (s == null) return (Color.red, Color.yellow);
        return s.bulletType switch
        {
            BulletType.Normal => (new Color(1f, 0.7f, 0f), new Color(191f / 255f, 102f / 255f, 0f) * 3.4f),
            BulletType.Explosive => (new Color(2f / 255f, 18f / 255f, 191f / 255f), new Color(81f / 255f, 186f / 255f, 5f / 255f) * 2.9f),
            BulletType.Fire => (new Color(1f, 0.4f, 0.1f), new Color(1f, 0.2f, 0f) * 3f),
            BulletType.Ice => (new Color(0.5f, 0.8f, 1f), new Color(0.3f, 0.6f, 1f) * 2.5f),
            BulletType.Electric => (new Color(1f, 1f, 0.3f), new Color(1f, 1f, 0f) * 3f),
            BulletType.Utility => (new Color(0.8f, 0.3f, 0.8f), new Color(0.6f, 0.1f, 0.6f) * 2.5f),
            BulletType.Buff => (new Color(0.3f, 1f, 0.3f), new Color(0.1f, 0.8f, 0.1f) * 2.5f),
            _ => (Color.red, Color.yellow)
        };
    }

    #region Initialize Methods

    /// <summary>
    /// Full Initialize with BulletPropertiesSO - the preferred method
    /// </summary>
    public void Initialize(
        Vector3 direction,
        float bulletSpeed,
        float upwardForce,
        float damage,
        BulletPropertiesSO bulletSO,
        ImpactPayload? payload = null,
        bool useGravity = false,
        Vector3? velocityOverride = null
    )
    {
        // Set runtime settings from passed SO
        settings = bulletSO ?? defaultSettings;
        
        baseDamage = damage;
        hasPayload = payload.HasValue;
        impactPayload = payload ?? default;
        rb.useGravity = useGravity;
        isArcProjectile = useGravity; // Arc projectiles use gravity

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (velocityOverride.HasValue)
            rb.linearVelocity = velocityOverride.Value;
        else
        {
            rb.AddForce(direction.normalized * bulletSpeed, ForceMode.Impulse);
            rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);
        }
        
        initialVy = rb.linearVelocity.y;
        
        // Phase I: Collision Filtering
        // Non-Arc projectiles should ignore the ground to prevent clipping/accidental hits
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (!isArcProjectile)
            {
                // Exclude ground layer from collision
                col.excludeLayers = groundMask; 
            }
            else
            {
                // Arc projectiles must hit ground -> ensure it's not excluded (default is 'Nothing')
                col.excludeLayers = 0;
            }
        }

        // Update visuals after settings are set
        SetupVisuals(settings);
        
        var activeSettings = settings ?? defaultSettings;
        if (activeSettings != null)
        {
            StartCoroutine(DestroySelf(activeSettings.maxLifeTime));
        }
        else
        {
            StartCoroutine(DestroySelf(3f)); // Fallback
        }
    }

    /// <summary>
    /// Legacy Initialize without SO (uses prefab's default settings)
    /// </summary>
    public void Initialize(
        Vector3 direction,
        float bulletSpeed,
        float upwardForce,
        float damage,
        ImpactPayload? payload = null,
        bool useGravity = false,
        Vector3? velocityOverride = null
    )
    {
        Initialize(direction, bulletSpeed, upwardForce, damage, null, payload, useGravity, velocityOverride);
    }

    // Convenience overloads
    public void Initialize(Vector3 dir, float speed, float up, float dmg)
        => Initialize(dir, speed, up, dmg, null, null, false, null);

    public void Initialize(Vector3 dir, float speed, float up, float dmg, BulletPropertiesSO bulletSO)
        => Initialize(dir, speed, up, dmg, bulletSO, null, false, null);

    public void Initialize(Vector3 dir, float speed, float up, float dmg, ImpactPayload payload)
        => Initialize(dir, speed, up, dmg, null, payload, false, null);

    public void Initialize(Vector3 dir, float speed, float up, float dmg, bool useGravity)
        => Initialize(dir, speed, up, dmg, null, null, useGravity, null);

    #endregion

    private void FixedUpdate()
    {
        var activeSettings = settings ?? defaultSettings;
        if (activeSettings != null)
        {
            if (activeSettings.bulletDrop != 0f)
            {
                rb.AddForce(Vector3.down * activeSettings.bulletDrop, ForceMode.Acceleration);
            }

            // Apply Gravity Scale
            if (rb.useGravity && activeSettings.gravityScale != 1f)
            {
                rb.AddForce(Physics.gravity * (activeSettings.gravityScale - 1f), ForceMode.Acceleration);
            }
        }
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        isActive = false;
    }

    IEnumerator DestroySelf(float delay)
    {
        yield return Helpers.GetWaitForSecond(delay);

        if (isActive)
        {
            if (impactPayload.explodeOnTimeout && HasAOE)
                Explode(transform.position);
            else
                Deactivate();
        }
    }

    private float spawnTime;
    
    // ... Initialize ...
    // Note: I need to set spawnTime in Initialize.
    // But I'm only editing this method block.
    // I'll rely on OnEnable setting it, or I need to Find 'OnEnable'.
    // Let's assume I can add 'spawnTime = Time.time;' in OnEnable if I edit it.
    // Or I can add it here if I check 'if (spawnTime == 0)'.

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        var activeSettings = settings ?? defaultSettings;
        bool isExplosiveBullet = activeSettings != null && activeSettings.bulletType == BulletType.Explosive;
        
        // Should explode: either payload has AOE OR bullet type is Explosive
        bool shouldExplode = HasAOE || isExplosiveBullet;

        // Check mask for Ground
        bool isGroundHit = (groundMask.value & (1 << collision.gameObject.layer)) > 0;
        
        // Arc Logic:
        // 1. If Arc, and hitting Wall (not Ground, not Enemy), ignore collision if early in flight.
        // Assuming Wall is Default layer? Or anything not Ground/Enemy.
        
        if (isArcProjectile)
        {
            // If hitting Enemy -> Hit.
            // If hitting Ground -> Hit (Spawn Zone).
            // If hitting Wall -> Check time.
            
            bool isEnemy = (enemyMask.value & (1 << collision.gameObject.layer)) > 0;
            
            if (!isEnemy && !isGroundHit)
            {
                // Must be Wall or Obstacle
                // Check flight progress
                // Estimate total flight time? hard.
                // Use simple time check. 0.5s? or %?
                // User said "first 25%".
                // We don't know total time.
                // Heuristic: If we are going UP? 
                // Arc goes up then down.
                // If rb.velocity.y > 0, we are in first half.
                // So if moving UP, ignore walls?
                
                if (rb.linearVelocity.y > initialVy * 0.5f)
                {
                    Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
                    return;
                }
            }
        }
        if ((enemyMask.value & (1 << collision.gameObject.layer)) > 0)
        {
            if (shouldExplode)
            {
                // Explosive bullet or AOE payload: do explosion
                if (isExplosiveBullet && !HasAOE)
                {
                    // Explosive bullet without payload - use DoArcExplosion for consistent behavior
                    DoArcExplosion(hitPoint, activeSettings);
                }
                else
                {
                    Explode(hitPoint);
                }
            }
            else
            {
                // Non-explosive: apply direct damage
                if (collision.gameObject.TryGetComponent(out Enemy enemy))
                {
                    Vector3 attackDir = rb.linearVelocity.normalized;
                    BulletEffectApplicator.ApplyEffect(enemy, activeSettings, baseDamage, attackDir);
                }
                
                // Arc hitting enemy: check for zone effect (elemental only)
                if (isArcProjectile)
                {
                    SpawnGroundZone(hitPoint);
                    return;
                }
                
                Deactivate();
            }
            return;
        }

        // Check if we hit ground (for arc projectiles specifically)
        // Check mask for Ground
        // isGroundHit already calculated above
        
        // Arc projectiles spawn ground zone on ground impact immediately
        if (isArcProjectile && isGroundHit)
        {
            SpawnGroundZone(hitPoint);
            return;
        }

        if (bounceRemaining > 0)
        {
            HandleBounce(hitNormal);
        }
        else
        {
            // Arc projectiles spawn ground zone on any final impact
            if (isArcProjectile)
            {
                SpawnGroundZone(hitPoint);
            }
            else if (shouldExplode)
            {
                if (isExplosiveBullet && !HasAOE)
                {
                    DoArcExplosion(hitPoint, activeSettings);
                }
                else
                {
                    Explode(hitPoint);
                }
            }
            else
            {
                Deactivate();
            }
        }
    }

    private void HandleBounce(Vector3 hitNormal)
    {
        if (bounceRemaining <= 0)
        {
            if (HasAOE) 
                Explode(transform.position);
            else 
                Deactivate();
            return;
        }

        bounceRemaining--;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        Vector3 direction = velocity.normalized;
        float travelDistance = speed * Time.fixedDeltaTime;
        float radius = GetBulletRadius();

        if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, travelDistance, ~0))
        {
            transform.position = hit.point + hit.normal * 0.01f;
            Vector3 reflected = Vector3.Reflect(direction, hit.normal);
            rb.linearVelocity = reflected * speed;
        }
        else
        {
            transform.position += velocity * Time.fixedDeltaTime;
        }
    }

    private void Explode(Vector3 center)
    {
        var activeSettings = settings ?? defaultSettings;
        
        // Use BulletPropertiesSO explosive radius if bullet type is Explosive
        // Otherwise fall back to impactPayload (from TurretPropertiesSO)
        float radius;
        if (activeSettings != null && activeSettings.bulletType == BulletType.Explosive)
        {
            radius = Mathf.Max(0f, activeSettings.explosiveRadius);
        }
        else
        {
            radius = Mathf.Max(0f, impactPayload.aoeRadius);
        }

        if (radius <= 0f)
        {
            Deactivate();
            return;
        }

        Collider[] hits = Physics.OverlapSphere(center, radius, impactPayload.aoeMask);

        List<Enemy> enemiesInAOE = new List<Enemy>();
        foreach (var c in hits)
        {
            if (c.TryGetComponent(out Enemy enemy) && enemy.IsAlive)
            {
                enemiesInAOE.Add(enemy);
            }

            if (impactPayload.aoeForce > 0f && c.attachedRigidbody != null)
            {
                c.attachedRigidbody.AddExplosionForce(impactPayload.aoeForce, center, radius, 0.1f, ForceMode.Impulse);
            }
        }

        BulletEffectApplicator.ApplyAOEEffect(enemiesInAOE, activeSettings, baseDamage, center);
        
        Deactivate();
    }

    /// <summary>
    /// Handle arc projectile impact - behavior depends on bullet type:
    /// - Elemental (Fire/Ice/Electric) with prefab: spawn ground zone
    /// - Explosive: one-time explosion damage
    /// - Normal/Other: just apply contact damage and deactivate
    /// </summary>
    private void SpawnGroundZone(Vector3 hitPoint)
    {
        var activeSettings = settings ?? defaultSettings;
        if (activeSettings == null)
        {
            Deactivate();
            return;
        }

        switch (activeSettings.bulletType)
        {
            case BulletType.Explosive:
                // Explosive: one-time explosion damage
                DoArcExplosion(hitPoint, activeSettings);
                break;
                
            case BulletType.Fire:
            case BulletType.Ice:
            case BulletType.Electric:
                // Elemental: spawn zone if prefab exists, otherwise just explosion
                if (activeSettings.arcZonePrefab != null)
                {
                    SpawnElementalZone(hitPoint, activeSettings);
                }
                else
                {
                    // No prefab = just do explosion damage
                    DoArcExplosion(hitPoint, activeSettings);
                }
                break;
                
            default:
                // Normal/Buff/Utility: no zone, just deactivate
                Deactivate();
                break;
        }
    }

    /// <summary>
    /// Do a one-time explosion at hit point
    /// </summary>
    private void DoArcExplosion(Vector3 hitPoint, BulletPropertiesSO bulletSO)
    {
        float radius = bulletSO.explosiveRadius;
        if (radius <= 0f) radius = bulletSO.arcZoneRadius; // Fallback to arc zone radius
        
        Collider[] hits = Physics.OverlapSphere(hitPoint, radius, bulletSO.explosionMask);
        
        List<Enemy> enemies = new List<Enemy>();
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Enemy enemy) && enemy.IsAlive)
            {
                enemies.Add(enemy);
            }
        }

        if (enemies.Count > 0)
        {
            BulletEffectApplicator.ApplyAOEEffect(enemies, bulletSO, baseDamage, hitPoint);
        }

        Deactivate();
    }

    /// <summary>
    /// Spawn a ground zone for elemental arc projectiles
    /// </summary>
    private void SpawnElementalZone(Vector3 hitPoint, BulletPropertiesSO bulletSO)
    {
        GameObject zoneObj = Instantiate(bulletSO.arcZonePrefab, hitPoint, Quaternion.identity);

        var zone = zoneObj.GetComponent<GroundZone>();
        if (zone == null)
        {
            zone = zoneObj.AddComponent<GroundZone>();
        }
        zone.Initialize(bulletSO, bulletSO.explosionMask);

        Deactivate();
    }

    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        StopAllCoroutines();
        if (tracer != null) tracer.SetActive(false);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Destroy(gameObject);
    }

    private float GetBulletRadius()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            return sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            return capsule.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        }

        return 0.05f;
    }

    public void SetShooter(GameObject shooter)
    {
        shooterGameObject = shooter;
    }
}

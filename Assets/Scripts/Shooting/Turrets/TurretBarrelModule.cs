using UnityEngine;
using System.Collections.Generic;

public class TurretBarrelModule : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioSource audioSource;

    [Header("Bullet Configuration (Authoritative)")]
    [Tooltip("The bullet prefab to use for projectile-based fire modes")]
    [SerializeField] private BulletProjectile bulletPrefab;
    [Tooltip("The bullet properties SO - authoritative source for all bullet effects")]
    [SerializeField] private BulletPropertiesSO currentBulletSO;

    [Header("Spread Shape Controls")]
    [Range(0f, 90f)]
    [SerializeField] private float axisRotationDeg = 0f;
    [SerializeField] private bool evenAxisDistribution = true;
    [SerializeField] private bool deterministicPelletSpacing = true;

    [Header("Per-shot Jitter (optional)")]
    [SerializeField] private float jitterSpreadDeg = 0f;
    [SerializeField] private float jitterLineExtentX = 0f;
    [SerializeField] private float jitterLineExtentY = 0f;
    [SerializeField] private float jitterAxisRotationDeg = 0f;

    // Beam effect timer (interval comes from TurretPropertiesSO)
    
    // Beam state
    private float beamEffectTimer = 0f;

    public BulletPropertiesSO CurrentBulletSO => currentBulletSO;

    #region Public Setup Methods

    /// <summary>
    /// Set the bullet type (prefab + SO) - called when ammo is installed
    /// </summary>
    public void SetBulletType(BulletProjectile prefab, BulletPropertiesSO bulletSO)
    {
        bulletPrefab = prefab;
        currentBulletSO = bulletSO;
    }

    /// <summary>
    /// Legacy method for backwards compatibility
    /// </summary>
    public void SetBulletType(BulletProjectile prefab)
    {
        bulletPrefab = prefab;
        if (prefab != null)
        {
            currentBulletSO = prefab.Settings;
        }
    }

    #endregion

    #region Projectile Fire Methods

    public void FireBullet(Vector3 targetPos, TurretPropertiesSO weaponStats, int pelletsOverride = -1)
    {
        if (bulletPrefab == null) return;

        Vector3 baseDir = (targetPos - firePoint.position).normalized;

        float effSpread = Mathf.Max(0f, weaponStats.spread + Random.Range(-jitterSpreadDeg, jitterSpreadDeg));
        float effSpreadX = weaponStats.spreadX + Random.Range(-jitterLineExtentX, jitterLineExtentX);
        float effSpreadY = weaponStats.spreadY + Random.Range(-jitterLineExtentY, jitterLineExtentY);
        float effAxisRot = axisRotationDeg + Random.Range(-jitterAxisRotationDeg, jitterAxisRotationDeg);

        int pelletCount = pelletsOverride > 0 ? pelletsOverride : Mathf.Max(1, weaponStats.bulletsPerTap);

        var directions = ShootHelpers.GeneratePelletDirections(
            baseDir, pelletCount, effSpread, effSpreadX, effSpreadY, effAxisRot,
            evenAxisDistribution, deterministicPelletSpacing
        );

        foreach (var dir in directions)
        {
            var bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));
            if (bulletObj != null)
            {
                bulletObj.SetShooter(this.gameObject);
                bulletObj.Initialize(dir, weaponStats.bulletSpeed, weaponStats.upwardForce, weaponStats.damage, currentBulletSO);
            }
        }

        PlayShootSound();
    }

    public void FireAOE(Vector3 targetPos, TurretPropertiesSO s)
    {
        if (bulletPrefab == null) return;

        Vector3 dir = (targetPos - firePoint.position).normalized;

        // Get explosion properties from BulletPropertiesSO (now the authoritative source)
        var bulletSO = currentBulletSO;
        var payload = new ImpactPayload
        {
            aoeRadius = bulletSO != null ? bulletSO.explosiveRadius : 3f,
            aoeForce = bulletSO != null ? bulletSO.explosionForce : 500f,
            aoeMask = bulletSO != null ? bulletSO.explosionMask : default,
            falloffExponent = bulletSO != null ? Mathf.Max(0.01f, bulletSO.explosionFalloffExponent) : 1f,
            explodeOnTimeout = bulletSO != null ? bulletSO.explodeOnTimeout : false
        };

        var proj = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));
        proj.SetShooter(gameObject);
        proj.Initialize(dir, s.bulletSpeed, s.upwardForce, s.damage, currentBulletSO, payload);

        PlayShootSound();
    }

    public void FireArc(Vector3 targetPos, TurretPropertiesSO s, float minAngleDeg = 25f)
    {
        if (bulletPrefab == null) return;

        var proj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        proj.SetShooter(gameObject);

        Vector3 origin = firePoint.position;
        float xz = new Vector2(targetPos.x - origin.x, targetPos.z - origin.z).magnitude;

        bool picked = false;
        Vector3 v0 = Vector3.zero;

        bool Accept(Vector3 cand)
        {
            if (ShootHelpers.LaunchAngleDeg(cand) < minAngleDeg) return false;
            float vh = new Vector2(cand.x, cand.z).magnitude;
            float t = vh > 0f ? xz / vh : 0f;
            return ShootHelpers.PathIsClear(origin, cand, Physics.gravity, 0.05f, t, 20, s.groundMask, out _);
        }

        if (ShootHelpers.TrySolveBallisticArc(origin, targetPos, s.bulletSpeed, Physics.gravity.y, false, out var lowV) && Accept(lowV))
        {
            v0 = lowV; picked = true;
        }

        if (!picked && ShootHelpers.TrySolveBallisticArc(origin, targetPos, s.bulletSpeed, Physics.gravity.y, true, out var highV) && Accept(highV))
        {
            v0 = highV; picked = true;
        }

        if (!picked)
        {
            float baseTime = Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(2f, 20f, xz));
            var minAngleV = ShootHelpers.SolveBallisticByMinAngle(origin, targetPos, minAngleDeg, baseTime, Physics.gravity);
            Vector3 cand = minAngleV;
            bool ok = false;
            float bumpT = 0f;
            for (int i = 0; i < 3; i++)
            {
                cand = ShootHelpers.SolveBallisticByTime(origin, targetPos, baseTime + bumpT, Physics.gravity);
                if (ShootHelpers.LaunchAngleDeg(cand) >= minAngleDeg && Accept(cand)) { ok = true; break; }
                bumpT += 0.2f;
            }
            v0 = ok ? cand : minAngleV;
            picked = true;
        }

        if (!picked)
        {
            Vector3 dir = (targetPos - origin).normalized;
            proj.Initialize(dir, s.bulletSpeed, s.upwardForce, s.damage, currentBulletSO, null, true);
            return;
        }

        proj.Initialize(default, 0f, 0f, s.damage, currentBulletSO, null, true, v0);
    }

    #endregion

    #region Beam Fire (Simple - Trusts Turret.cs for targeting)

    /// <summary>
    /// Fire beam - simple hitscan that applies damage and effects.
    /// Called every frame by TurretBaseModule when in Beam mode.
    /// Trusts that Turret.cs has already validated the target is in range.
    /// </summary>
    public void FireBeam(Enemy target, TurretPropertiesSO weaponStats, float deltaTime)
    {
        if (target == null || !target.IsAlive || weaponStats == null) return;

        Vector3 origin = firePoint.position;
        Vector3 targetPos = target.transform.position;
        Vector3 dir = (targetPos - origin).normalized;

        // Apply continuous DPS (damage per frame)
        float dps = weaponStats.damage;
        float frameDamage = dps * deltaTime;
        
        // GDD: Buff and Utility never deal damage
        bool shouldDealDamage = currentBulletSO == null || 
            (currentBulletSO.bulletType != BulletType.Buff && 
             currentBulletSO.bulletType != BulletType.Utility);
        
        if (shouldDealDamage)
        {
            target.TakeDamage(frameDamage);
        }

        beamEffectTimer -= deltaTime;
        if (beamEffectTimer <= 0f)
        {
            beamEffectTimer = weaponStats.beamEffectInterval;
            ApplyBeamEffect(target);
        }

        // Visual beam
        Color beamColor = GetBeamColor();
        Debug.DrawLine(origin, targetPos, beamColor);
    }

    /// <summary>
    /// Legacy FireBeam for compatibility - single shot hitscan
    /// </summary>
    public void FireBeam(Vector3 targetPos, TurretPropertiesSO weaponStats, BulletPropertiesSO bulletProperties = null)
    {
        Vector3 origin = firePoint.position;
        Vector3 dir = (targetPos - origin).normalized;
        
        if (Physics.Raycast(origin, dir, out RaycastHit hit, 100f))
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                var bulletSO = bulletProperties ?? currentBulletSO;
                if (bulletSO != null)
                {
                    BulletEffectApplicator.ApplyEffect(enemy, bulletSO, weaponStats.damage, dir);
                }
                else
                {
                    enemy.TakeDamage(weaponStats.damage);
                }
            }
            
            Color beamColor = GetBeamColor();
            Debug.DrawLine(origin, hit.point, beamColor, 0.1f);
        }
        else
        {
            Color beamColor = GetBeamColor();
            Debug.DrawRay(origin, dir * 100f, beamColor, 0.1f);
        }
        
        PlayShootSound();
    }

    /// <summary>
    /// Apply beam effects on interval (stun, slow, DOT, chain, etc.)
    /// </summary>
    private void ApplyBeamEffect(Enemy target)
    {
        if (target == null || currentBulletSO == null) return;

        switch (currentBulletSO.bulletType)
        {
            case BulletType.Fire:
                target.ApplyFireDOT(
                    currentBulletSO.fireDOTDamagePerSecond, 
                    currentBulletSO.fireDOTDuration
                );
                break;

            case BulletType.Ice:
                target.ApplySlow(
                    currentBulletSO.iceSlowPercent, 
                    currentBulletSO.iceSlowDuration
                );
                break;

            case BulletType.Electric:
                // Apply stun + chain using BulletEffectApplicator
                // Use reduced damage for beam chain balance
                float chainDamage = 5f; // Base chain damage for beam
                Vector3 dir = (target.transform.position - firePoint.position).normalized;
                BulletEffectApplicator.ApplyEffect(target, currentBulletSO, chainDamage, dir);
                break;

            case BulletType.Utility:
                ApplyBeamUtilityEffect(target);
                break;
        }
    }

    private void ApplyBeamUtilityEffect(Enemy target)
    {
        if (currentBulletSO == null || target == null) return;

        switch (currentBulletSO.utilityDebuffType)
        {
            case UtilityDebuffType.Slow:
                target.ApplySlow(
                    currentBulletSO.utilitySlowPercent, 
                    currentBulletSO.utilitySlowDuration
                );
                break;

            case UtilityDebuffType.Vulnerability:
                target.ApplyVulnerability(
                    currentBulletSO.vulnerabilityPercent, 
                    currentBulletSO.vulnerabilityDuration
                );
                break;

            case UtilityDebuffType.ShieldShred:
                // Apply reduced shred per tick
                target.ApplyShieldShred(currentBulletSO.shieldShredPercent * 0.1f);
                break;
        }
    }

    private Color GetBeamColor()
    {
        if (currentBulletSO == null) return Color.cyan;
        
        return currentBulletSO.bulletType switch
        {
            BulletType.Fire => Color.red,
            BulletType.Ice => Color.blue,
            BulletType.Electric => Color.yellow,
            BulletType.Explosive => new Color(1f, 0.5f, 0f),
            BulletType.Utility => Color.magenta,
            BulletType.Buff => Color.green,
            _ => Color.cyan
        };
    }

    #endregion

    private void PlayShootSound()
    {
        if (audioSource != null && fireClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(fireClip);
        }
    }
}

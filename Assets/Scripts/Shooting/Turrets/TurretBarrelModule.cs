using UnityEngine;
using System.Collections; // Needed for IEnumerator
using System.Collections.Generic;

public class TurretBarrelModule : MonoBehaviour
{
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private ParticleSystem[] muzzleFlashes;

    private int currentBarrelIndex = 0;

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

    public void FireBullet(Vector3 targetPos, TurretPropertiesSO weaponStats, int pelletsOverride = -1, float damageOverride = -1f)
    {
        if (bulletPrefab == null) return;
        
        // Single shot -> use current barrel
        // Multiple pellets -> divide among barrels? For now, we spawn all from the current barrel
        // but let's just use the current barrel for the base direction calculation.
        Transform activeFirePoint = GetCurrentFirePoint();
        Vector3 baseDir = (targetPos - activeFirePoint.position).normalized;

        float effSpread = Mathf.Max(0f, weaponStats.spread + Random.Range(-jitterSpreadDeg, jitterSpreadDeg));
        float effSpreadX = weaponStats.spreadX + Random.Range(-jitterLineExtentX, jitterLineExtentX);
        float effSpreadY = weaponStats.spreadY + Random.Range(-jitterLineExtentY, jitterLineExtentY);
        float effAxisRot = axisRotationDeg + Random.Range(-jitterAxisRotationDeg, jitterAxisRotationDeg);

        int pelletCount = pelletsOverride > 0 ? pelletsOverride : Mathf.Max(1, weaponStats.bulletsPerTap);

        var directions = ShootHelpers.GeneratePelletDirections(
            baseDir, pelletCount, effSpread, effSpreadX, effSpreadY, effAxisRot,
            evenAxisDistribution, deterministicPelletSpacing
        );

        // Stagger Logic: If multi-pellet, stagger them over the burst interval
        if (pelletCount > 1)
        {
            StartCoroutine(FireStaggered(directions, weaponStats, damageOverride));
        }
        else
        {
            // Instant Fire for single pellet
            int barrelToUse = currentBarrelIndex;
            PlayMuzzleFlash(barrelToUse);
            SpawnProjectile(directions[0], weaponStats, damageOverride, GetFirePoint(barrelToUse));
            AdvanceBarrel();
        }
    }

    private IEnumerator FireStaggered(List<Vector3> directions, TurretPropertiesSO weaponStats, float damageOverride = -1f)
    {
        // Calculate random delays for each pellet within the interval
        // User requested "0 to burst interval amount".
        float window = Mathf.Max(0.01f, weaponStats.burstInterval); 
        
        List<float> delays = new List<float>();
        for (int i = 0; i < directions.Count; i++)
        {
            delays.Add(Random.Range(0f, window));
        }
        delays.Sort(); // Fire in chronological order

        float currentTime = 0f;
        int firedCount = 0;

        for (int i = 0; i < directions.Count; i++)
        {
            float targetTime = delays[i];
            float rawWait = targetTime - currentTime;
            
            // Quantize to 0.01s to avoid flooding Helpers dictionary with random floats
            float waitTime = (float)System.Math.Round(rawWait, 2);
            
            if (waitTime > 0f)
            {
                // Use the cached WaitForSeconds from Helpers
                yield return Helpers.GetWaitForSecond(waitTime);
            }
            
            int barrelToUse = currentBarrelIndex;
            PlayMuzzleFlash(barrelToUse);

            currentTime = targetTime; // Advance simulated time to target (even if we rounded wait)
            SpawnProjectile(directions[i], weaponStats, damageOverride, GetFirePoint(barrelToUse));
            AdvanceBarrel();
            firedCount++;
        }
    }

    private void SpawnProjectile(Vector3 dir, TurretPropertiesSO weaponStats, float damageOverride = -1f, Transform originPoint = null)
    {
        if (originPoint == null) originPoint = transform;
        BulletProjectile bulletObj = null;

        if (BulletPoolManager.Instance != null && bulletPrefab != null)
        {
            bulletObj = BulletPoolManager.Instance.SpawnBullet(bulletPrefab.gameObject, originPoint.position, Quaternion.LookRotation(dir));
        }
        else
        {
            var go = Instantiate(bulletPrefab, originPoint.position, Quaternion.LookRotation(dir));
            bulletObj = go.GetComponent<BulletProjectile>();
        }
        if (bulletObj != null)
        {
            bulletObj.SetShooter(this.gameObject);
            
            // Use Override if valid, else base stats
            float dmg = (damageOverride > 0) ? damageOverride : weaponStats.damage;
            
            bulletObj.Initialize(dir, weaponStats.bulletSpeed, weaponStats.upwardForce, dmg, currentBulletSO);
        }
    }

    public void FireAOE(Vector3 targetPos, TurretPropertiesSO s)
    {
        if (bulletPrefab == null) return;
        
        int barrelToUse = currentBarrelIndex;
        PlayMuzzleFlash(barrelToUse);
        Transform fireOrigin = GetFirePoint(barrelToUse);

        Vector3 dir = (targetPos - fireOrigin.position).normalized;

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

        BulletProjectile proj = null;
        if (BulletPoolManager.Instance != null && bulletPrefab != null)
        {
            proj = BulletPoolManager.Instance.SpawnBullet(bulletPrefab.gameObject, fireOrigin.position, Quaternion.LookRotation(dir));
        }
        else
        {
            var go = Instantiate(bulletPrefab, fireOrigin.position, Quaternion.LookRotation(dir));
            proj = go.GetComponent<BulletProjectile>();
        }
        
        if (proj != null)
        {
            proj.SetShooter(gameObject);
            proj.Initialize(dir, s.bulletSpeed, s.upwardForce, s.damage, currentBulletSO, payload);
        }
        
        AdvanceBarrel();
    }

    public void FireArc(Vector3 targetPos, TurretPropertiesSO s, float minAngleDeg = 25f)
    {
        if (bulletPrefab == null) return;
        
        int barrelToUse = currentBarrelIndex;
        PlayMuzzleFlash(barrelToUse);
        Transform fireOrigin = GetFirePoint(barrelToUse);

        BulletProjectile proj = null;
        if (BulletPoolManager.Instance != null && bulletPrefab != null)
        {
            proj = BulletPoolManager.Instance.SpawnBullet(bulletPrefab.gameObject, fireOrigin.position, Quaternion.identity);
        }
        else
        {
            var go = Instantiate(bulletPrefab, fireOrigin.position, Quaternion.identity);
            proj = go.GetComponent<BulletProjectile>();
        }
        
        if (proj != null) proj.SetShooter(gameObject);

        Vector3 origin = fireOrigin.position;
        // Vector3 direction = (targetPos - origin).normalized; // Not used for velocity calculation now due to override
        
        // Fixed Height Arc Logic (requested by User)
        // Vy = upwardForce
        // Solve T: 0.5*g*T^2 - Vy*T + dy = 0
        
        float Vy = s.upwardForce;
        float gravity = Physics.gravity.magnitude;
        float dy = targetPos.y - origin.y;
        
        float a = 0.5f * gravity;
        float b = -Vy;
        float c = dy;
        
        float det = b*b - 4f*a*c;
        float T = 0f;
        
        if (det < 0)
        {
            // Can't reach height, use max possible time (apex) or just force flat shot?
            // Fallback: Use standard heuristic
            T = Vector3.Distance(origin, targetPos) / 20f;
        }
        else
        {
            // Use longer time (downward slope hit)
            T = (-b + Mathf.Sqrt(det)) / (2f * a);
        }
        
        if (T <= 0.01f) T = 0.1f;
        
        // Horizontal Velocity
        Vector3 d = targetPos - origin;
        d.y = 0; // Horizontal vector
        float dist = d.magnitude;
        
        float Vx = dist / T;
        
        Vector3 horizontalDir = d.normalized;
        Vector3 v0 = horizontalDir * Vx + Vector3.up * Vy;
        
        // Use v0 override
        proj.Initialize(default, 0f, 0f, s.damage, currentBulletSO, null, true, v0);
        
        AdvanceBarrel();
    }

    #endregion

    #region Beam Fire (Simple - Trusts Turret.cs for targeting)

    /// <summary>
    /// Fire beam - simple hitscan that applies damage and effects.
    /// Called every frame by TurretBaseModule when in Beam mode.
    /// Trusts that Turret.cs has already validated the target is in range.
    /// </summary>
    public void FireBeam(Enemy target, TurretPropertiesSO weaponStats, float deltaTime, float damageOverride = -1f)
    {
        if (target == null || !target.IsAlive || weaponStats == null) return;
        
        // Only play muzzle flash periodically for continuous beams to avoid spam
        int barrelToUse = currentBarrelIndex;
        if (Time.time - beamEffectTimer > 0.1f)
        {
            PlayMuzzleFlash(barrelToUse);
            beamEffectTimer = Time.time;
        }

        Transform fireOrigin = GetFirePoint(barrelToUse);
        Vector3 origin = fireOrigin.position;
        Vector3 targetPos = target.transform.position;
        Vector3 dir = (targetPos - origin).normalized;

        // Apply continuous DPS (damage per frame)
        // Use Override if valid, else base stats
        float dps = (damageOverride > 0) ? damageOverride : weaponStats.damage;
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
        Transform activeFirePoint = GetCurrentFirePoint();
        Vector3 origin = activeFirePoint.position;
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
                Transform activeFirePoint = GetCurrentFirePoint();
                Vector3 dir = (target.transform.position - activeFirePoint.position).normalized;
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

    #region Visuals & FX

    private Transform GetCurrentFirePoint()
    {
        if (firePoints == null || firePoints.Length == 0) return transform;
        if (currentBarrelIndex >= firePoints.Length) currentBarrelIndex = 0;
        
        Transform fp = firePoints[currentBarrelIndex];
        return fp != null ? fp : transform;
    }

    private Transform GetFirePoint(int index)
    {
        if (firePoints == null || firePoints.Length == 0) return transform;
        if (index >= firePoints.Length) index = 0;
        
        Transform fp = firePoints[index];
        return fp != null ? fp : transform;
    }

    private void AdvanceBarrel()
    {
        if (firePoints == null || firePoints.Length <= 1) return;
        currentBarrelIndex = (currentBarrelIndex + 1) % firePoints.Length;
    }

    private void PlayMuzzleFlash(int index)
    {
        if (muzzleFlashes == null || muzzleFlashes.Length == 0) return;
        if (index >= muzzleFlashes.Length) index = 0;

        ParticleSystem flash = muzzleFlashes[index];
        if (flash != null)
        {
            // Force stop and clear so it can play rapidly even if the previous effect hasn't ended
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flash.Play(true);
        }
    }

    #endregion
}

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

    [Header("Elemental VFX")]
    [Tooltip("Looping cone particle (flamethrower/snowstorm) per barrel. Scale is lerped to 1 when firing.")]
    [SerializeField] private ParticleSystem[] coneParticles;

    [Header("Elemental Muzzle Flash VFX")]
    [Tooltip("One-shot burst played alongside the muzzle flash when the turret uses Fire bullets")]
    [SerializeField] private ParticleSystem[] fireFlashParticles;
    [Tooltip("One-shot burst played alongside the muzzle flash when the turret uses Ice bullets")]
    [SerializeField] private ParticleSystem[] iceFlashParticles;
    [Tooltip("One-shot burst played alongside the muzzle flash when the turret uses Electric bullets")]
    [SerializeField] private ParticleSystem[] electricFlashParticles;

    // VFX state
    private float beamLastFireTime = -99f;
    private const float CONE_VFX_FADE_DELAY = 0.15f;

    // --- Cone Particle Defaults (Part 1: VFX Scaling) ---
    private struct ConeParticleData
    {
        public ParticleSystem ps;
        public float baseSpeedMin;
        public float baseSpeedMax;
        public int baseMaxParticles;
    }

    private List<ConeParticleData> coneParticleDefaults = new List<ConeParticleData>();
    private float coneBaseRange = 4f;
    private bool coneDefaultsInitialized = false;

    public BulletPropertiesSO CurrentBulletSO => currentBulletSO;

    // Which flash array is currently active (null = use default muzzle)
    private ParticleSystem[] activeFlashParticles;

    private void Awake()
    {
        // Disable everything on startup
        SetFlashArrayActive(fireFlashParticles, false);
        SetFlashArrayActive(iceFlashParticles, false);
        SetFlashArrayActive(electricFlashParticles, false);
        activeFlashParticles = null;

        // If bullet type is already serialized on the prefab, activate the right flash now
        if (currentBulletSO != null)
        {
            ActivateFlashForBulletType(currentBulletSO);
        }
    }

    private void OnEnable()
    {
        // Re-activate flashes when turret is pulled from pool (Awake doesn't re-run)
        if (currentBulletSO != null)
        {
            ActivateFlashForBulletType(currentBulletSO);
        }
    }

    /// <summary>
    /// Permanently enable the matching elemental flash and disable all others + default muzzle.
    /// Called when a bullet type is installed.
    /// </summary>
    private void ActivateFlashForBulletType(BulletPropertiesSO bulletSO)
    {
        // Disable all first
        SetFlashArrayActive(fireFlashParticles, false);
        SetFlashArrayActive(iceFlashParticles, false);
        SetFlashArrayActive(electricFlashParticles, false);
        activeFlashParticles = null;

        if (bulletSO == null) return;

        // Pick the matching array
        ParticleSystem[] match = bulletSO.bulletType switch
        {
            BulletType.Fire     => fireFlashParticles,
            BulletType.Ice      => iceFlashParticles,
            BulletType.Electric => electricFlashParticles,
            _                   => null
        };

        if (match != null && match.Length > 0)
        {
            // Permanently enable the matching flash GameObjects (but don't play yet)
            SetFlashArrayActive(match, true);
            // Stop them so they don't auto-play on enable
            foreach (var ps in match)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    // Disable Play On Awake to prevent auto-play on SetActive
                    var main = ps.main;
                    main.playOnAwake = false;
                }
            }
            activeFlashParticles = match;

            // Hide default muzzle since elemental replaces it
            SetFlashArrayActive(muzzleFlashes, false);
        }
        else
        {
            // No elemental match — re-enable default muzzle
            if (muzzleFlashes != null && muzzleFlashes.Length > 0)
            {
                SetFlashArrayActive(muzzleFlashes, true);
                foreach (var ps in muzzleFlashes)
                {
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }

    private void SetFlashArrayActive(ParticleSystem[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var ps in arr)
        {
            if (ps != null) ps.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// Capture base startSpeed and maxParticles for each cone particle system and its children.
    /// Call once after the turret is placed / initialized.
    /// </summary>
    public void InitConeParticleDefaults(float baseRange)
    {
        coneBaseRange = Mathf.Max(1f, baseRange);
        coneParticleDefaults.Clear();

        if (coneParticles == null) { coneDefaultsInitialized = true; return; }

        foreach (var ps in coneParticles)
        {
            if (ps == null) continue;
            CaptureParticleDefaults(ps);
            // Also capture children
            foreach (Transform child in ps.transform)
            {
                var childPS = child.GetComponent<ParticleSystem>();
                if (childPS != null) CaptureParticleDefaults(childPS);
            }
        }
        coneDefaultsInitialized = true;
    }

    private void CaptureParticleDefaults(ParticleSystem ps)
    {
        var main = ps.main;
        coneParticleDefaults.Add(new ConeParticleData
        {
            ps = ps,
            baseSpeedMin = main.startSpeed.constantMin,
            baseSpeedMax = main.startSpeed.constantMax,
            baseMaxParticles = main.maxParticles
        });
    }

    /// <summary>
    /// Update cone particle speed and max particles when turret range changes.
    /// </summary>
    public void SetConeRange(float currentRange)
    {
        if (!coneDefaultsInitialized || coneParticleDefaults.Count == 0) return;

        float rangeDelta = currentRange - coneBaseRange;
        float rangeRatio = currentRange / coneBaseRange;

        foreach (var data in coneParticleDefaults)
        {
            if (data.ps == null) continue;
            var main = data.ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                data.baseSpeedMin + rangeDelta,
                data.baseSpeedMax + rangeDelta
            );
            main.maxParticles = Mathf.Max(1, (int)(data.baseMaxParticles * rangeRatio));
        }
    }

    /// <summary>
    /// Play the pre-activated elemental flash particles.
    /// Returns true if elemental flash exists and was played, false otherwise.
    /// </summary>
    private bool PlayElementalFlash()
    {
        if (activeFlashParticles == null || activeFlashParticles.Length == 0) return false;

        foreach (var ps in activeFlashParticles)
        {
            if (ps == null) continue;
            if (!ps.gameObject.activeInHierarchy) continue;
            // Robust restart: Clear → Simulate(0, restart) → Play
            ps.Clear(true);
            ps.Simulate(0f, true, true);
            ps.Play(true);
        }
        return true;
    }

    #region Public Setup Methods

    /// <summary>
    /// Set the bullet type (prefab + SO) - called when ammo is installed
    /// </summary>
    public void SetBulletType(BulletProjectile prefab, BulletPropertiesSO bulletSO)
    {
        bulletPrefab = prefab;
        currentBulletSO = bulletSO;
        ActivateFlashForBulletType(bulletSO);
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
        ActivateFlashForBulletType(currentBulletSO);
    }

    /// <summary>
    /// Returns the primary fire point (muzzle) for visual previews like trajectory lines.
    /// Falls back to this transform if no fire points are assigned.
    /// </summary>
    public Transform GetPrimaryFirePoint()
    {
        if (firePoints != null && firePoints.Length > 0 && firePoints[0] != null)
            return firePoints[0];
        return transform;
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
            SoundEvents.TriggerTurretShoot(this, GetFirePoint(barrelToUse).position, weaponStats.weaponName, weaponStats.fireMode);
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
            SoundEvents.TriggerTurretShoot(this, GetFirePoint(barrelToUse).position, weaponStats.weaponName, weaponStats.fireMode);

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
        SoundEvents.TriggerTurretShoot(this, GetFirePoint(barrelToUse).position, s.weaponName, s.fireMode);
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
        SoundEvents.TriggerTurretShoot(this, GetFirePoint(barrelToUse).position, s.weaponName, s.fireMode);
        Transform fireOrigin = GetFirePoint(barrelToUse);

        Vector3 origin = fireOrigin.position;

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
            T = Vector3.Distance(origin, targetPos) / 20f;
        }
        else
        {
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

        // Spawn with rotation facing initial velocity direction
        Quaternion arcRotation = v0.sqrMagnitude > 0.01f ? Quaternion.LookRotation(v0) : Quaternion.identity;

        BulletProjectile proj = null;
        if (BulletPoolManager.Instance != null && bulletPrefab != null)
        {
            proj = BulletPoolManager.Instance.SpawnBullet(bulletPrefab.gameObject, fireOrigin.position, arcRotation);
        }
        else
        {
            var go = Instantiate(bulletPrefab, fireOrigin.position, arcRotation);
            proj = go.GetComponent<BulletProjectile>();
        }

        if (proj != null) proj.SetShooter(gameObject);

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
            SoundEvents.TriggerTurretShoot(this, GetFirePoint(barrelToUse).position, weaponStats.weaponName, weaponStats.fireMode);
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
            target.TakeDamage(frameDamage, false, default, gameObject);
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
                    BulletEffectApplicator.ApplyEffect(enemy, bulletSO, weaponStats.damage, dir, gameObject);
                }
                else
                {
                    enemy.TakeDamage(weaponStats.damage, false, default, gameObject);
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
                    currentBulletSO.fireDOTDuration,
                    gameObject
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
                BulletEffectApplicator.ApplyEffect(target, currentBulletSO, chainDamage, dir, gameObject);
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

    #region Elemental Beam / Pulse VFX Methods

    /// <summary>
    /// Fire a continuous cone spray in front of the barrel (Fire element).
    /// Damage = turretDamage * 0.5 per second. VFX scale based on turret range (1 unit = range 7).
    /// </summary>
    public void FireConeArea(TurretPropertiesSO weaponStats, BulletPropertiesSO bulletSO, float deltaTime, LayerMask enemyMask, float turretRange)
    {
        if (weaponStats == null || bulletSO == null) return;

        Transform fp = GetCurrentFirePoint();
        Vector3 origin = fp.position;
        Vector3 forward = fp.forward;

        // Use turret effective range as cone range
        float range = turretRange > 0 ? turretRange : (weaponStats.coneRange > 0 ? weaponStats.coneRange : 8f);

        // Scale VFX particle speed/count based on turret range
        SetConeVFXActive(true, range);
        beamLastFireTime = Time.time;

        // Fire damage = 50% of base DPS
        float fireDamage = weaponStats.damage * 0.5f * deltaTime;

        // Overlap sphere then filter by cone angle
        Collider[] hits = Physics.OverlapSphere(origin, range, enemyMask);
        foreach (var col in hits)
        {
            if (!col.TryGetComponent(out Enemy enemy)) continue;
            if (!enemy.IsAlive) continue;

            Vector3 toEnemy = (col.transform.position - origin).normalized;
            float angle = Vector3.Angle(forward, toEnemy);
            if (angle > weaponStats.coneAngle) continue;

            enemy.TakeDamage(fireDamage, false, default, gameObject);
            // Apply fire DOT (no extra hit damage, just the effect)
            BulletEffectApplicator.ApplyEffect(enemy, bulletSO, 0f, toEnemy, gameObject);
        }

        // Throttled sound
        SoundEvents.TriggerElementalStrike(this, BulletType.Fire, origin);
    }

    /// <summary>
    /// Spawn an elemental strike (Ice or Electric) at the enemy's feet.
    /// Damage formulas: Ice = turretDamage * 0.8, Electric = turretDamage * 0.7 + lightningBonus.
    /// No chain for either. No direct base turret damage - zone handles it.
    /// </summary>
    public void SpawnElementalStrike(BulletType element, Vector3 worldPos, BulletPropertiesSO bulletSO, LayerMask enemyMask, float turretBaseDamage = 0f, bool isBeamTurret = true)
    {
        // Spawn at feet (lower by 0.5)
        Vector3 spawnPos = worldPos - Vector3.up * 0.5f;

        switch (element)
        {
            case BulletType.Electric:
                VFXManager.Instance?.PlayEffect(VFXType.ElectricStrike, spawnPos);

                float electricBonus = isBeamTurret
                    ? (bulletSO?.lightningStrikeDamage ?? 0f)
                    : (bulletSO?.lightningStaticDamage ?? 0f);
                float electricDamage = turretBaseDamage * 0.7f + electricBonus;

                float eRadius = bulletSO?.electricStrikeRadius ?? 2f;
                Collider[] eCols = Physics.OverlapSphere(worldPos, eRadius, enemyMask);
                foreach (var col in eCols)
                {
                    if (col.TryGetComponent(out Enemy eHit) && eHit.IsAlive)
                    {
                        eHit.TakeDamage(electricDamage, false, default, gameObject);
                        eHit.ApplyStun(bulletSO?.electricStunDuration ?? 0.5f);
                    }
                }
                SoundEvents.TriggerElementalStrike(this, BulletType.Electric, worldPos);
                break;

            case BulletType.Ice:
                VFXManager.Instance?.PlayEffect(VFXType.IceStrike, spawnPos);

                float iceDamage = turretBaseDamage * 0.8f + (bulletSO?.iceStrikeDamage ?? 0f);
                float iRadius = bulletSO?.iceZoneRadius ?? 2.5f;
                Collider[] iCols = Physics.OverlapSphere(worldPos, iRadius, enemyMask);
                foreach (var col in iCols)
                {
                    if (col.TryGetComponent(out Enemy iHit) && iHit.IsAlive)
                    {
                        iHit.TakeDamage(iceDamage, false, default, gameObject);
                        iHit.ApplySlow(bulletSO?.iceSlowPercent ?? 0.3f, bulletSO?.iceSlowDuration ?? 2f);
                    }
                }
                SoundEvents.TriggerElementalStrike(this, BulletType.Ice, worldPos);
                break;
        }
    }

    /// <summary>
    /// Enable/disable cone particles. Lerps localScale 0↔1 AND adjusts startSpeed/maxParticles
    /// based on range for smooth on/off with proper particle behavior.
    /// </summary>
    public void SetConeVFXActive(bool firing, float turretRange = 0f)
    {
        if (coneParticles == null || coneParticles.Length == 0) return;

        float lerpSpeed = Time.deltaTime * 8f;

        if (firing)
        {
            // Update particle properties based on range
            if (turretRange > 0f && coneDefaultsInitialized)
            {
                SetConeRange(turretRange);
            }

            foreach (var ps in coneParticles)
            {
                if (ps == null) continue;
                if (!ps.gameObject.activeSelf)
                {
                    ps.transform.localScale = Vector3.zero;
                    ps.gameObject.SetActive(true);
                    ps.Play(true);
                }
                // Lerp scale toward 1
                ps.transform.localScale = Vector3.Lerp(ps.transform.localScale, Vector3.one, lerpSpeed);
            }
        }
        else
        {
            bool allFaded = true;

            // Lerp scale toward 0
            foreach (var ps in coneParticles)
            {
                if (ps == null || !ps.gameObject.activeSelf) continue;
                ps.transform.localScale = Vector3.Lerp(ps.transform.localScale, Vector3.zero, lerpSpeed);
                if (ps.transform.localScale.x > 0.01f)
                    allFaded = false;
            }

            // Also lerp startSpeed/maxParticles toward 0 for particle fade
            if (coneDefaultsInitialized && coneParticleDefaults.Count > 0)
            {
                foreach (var data in coneParticleDefaults)
                {
                    if (data.ps == null || !data.ps.gameObject.activeSelf) continue;
                    var main = data.ps.main;
                    float curMax = main.startSpeed.constantMax;
                    float curMin = main.startSpeed.constantMin;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(
                        Mathf.Lerp(curMin, 0f, lerpSpeed),
                        Mathf.Lerp(curMax, 0f, lerpSpeed)
                    );
                    main.maxParticles = Mathf.Max(0, (int)Mathf.Lerp(main.maxParticles, 0f, lerpSpeed));
                }
            }

            if (allFaded)
            {
                foreach (var ps in coneParticles)
                {
                    if (ps != null && ps.gameObject.activeSelf)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        ps.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    /// <summary>Returns time since last cone fire — used by BaseModule to fade VFX after beam stops.</summary>
    public float TimeSinceLastBeamFire => Time.time - beamLastFireTime;

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
        // Try elemental flash first — skip default muzzle if elemental played
        if (PlayElementalFlash()) return;

        // No elemental match — play default muzzle flash as fallback
        if (muzzleFlashes == null || muzzleFlashes.Length == 0) return;
        if (index >= muzzleFlashes.Length) index = 0;

        ParticleSystem flash = muzzleFlashes[index];
        if (flash != null)
        {
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flash.Play(true);
        }
    }

    /// <summary>
    /// Public entry point for playing muzzle/elemental flash.
    /// Used by TurretBaseModule for beam/pulse modes that bypass FireBullet().
    /// </summary>
    public void PlayFlash()
    {
        PlayMuzzleFlash(currentBarrelIndex);
    }

    #endregion
}

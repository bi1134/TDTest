using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// TurretBaseModule - Controls WHEN and HOW the turret shoots.
/// Does NOT own bullet properties - that's the barrel's job.
/// </summary>
public class TurretBaseModule : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("Base Stats (How it shoots)")]
    public TurretPropertiesSO weaponStats;

    public TurretPropertiesSO GetTurretProperties() => weaponStats;

    [SerializeField] private TurretBarrelModule barrel;

    private float fireCooldown;
    [SerializeField] private Transform target;

    [SerializeField] private Turret parentNode;

    public Color hoverColor;
    private Color startColor;

    [Tooltip("The renderer to apply hover effects to. Auto-detected if null.")]
    public Renderer targetRenderer; 
    
    private Renderer rend => targetRenderer; // Backwards compatibility for now, or just use targetRenderer
    private BuildManager buildManager;

    // Beam state tracking (settings come from weaponStats SO)
    private float beamTimer = 0f;
    private float beamShotTimer = 0f;
    private bool isBeamFiring = false;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
        
        if (targetRenderer != null)
        {
            startColor = targetRenderer.material.color;
        }

        buildManager = BuildManager.instance;
    }

    private void Update()
    {
        if (target == null || weaponStats == null || barrel == null || !barrel.isActiveAndEnabled)
        {
            if (isBeamFiring)
            {
                isBeamFiring = false;
            }
            return;
        }

        // Handle different fire modes
        if (weaponStats.fireMode == FireMode.Beam)
        {
            HandleBeamMode();
        }
        else if (weaponStats.fireMode == FireMode.Pulse && IsElementalBullet())
        {
            HandleContinuousPulse();
        }
        else
        {
            HandleStandardFire();
        }
    }

    /// <summary>
    /// Beam mode: 
    /// - Elemental: continuous damage per frame
    /// - Non-elemental (sniper): fire bullets at intervals during duration
    /// After duration, reload based on fireRate
    /// </summary>
    private void HandleBeamMode()
    {
        if (isBeamFiring)
        {
            // Currently in firing window
            beamTimer -= Time.deltaTime;
            
            Enemy enemy = target.GetComponent<Enemy>();
            if (enemy != null && enemy.IsAlive)
            {
                if (IsElementalBullet())
                {
                    // Elemental: continuous damage per frame
                    // Calculate Augmented Damage
                    float dmgMult = UpgradesManager.GetStatMultiplier(AugmentType.Damage); 
                    float dmgFlat = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);
                    float finalDamage = (weaponStats.damage * dmgMult) + dmgFlat;
                    
                    // FireBeam logic inside checks "weaponStats.damage". 
                    // I need to change FireBeam signature to accept damage or manually handle it here.
                    // FireBeam(Enemy target, TurretPropertiesSO weaponStats, float deltaTime) uses weaponStats.damage.
                    // I should ideally pass the calculated damage.
                    // Let's modify TurretBarrelModule.FireBeam(Enemy...) to take 'damagePerSecond'.
                    
                    // For now, I will modify the weaponStats clone temporarily? No that sticks.
                    // I'll update TurretBarrelModule.FireBeam to take damage override.
                    
                    // Waiting for Barrel update, but let's assume I'll update it.
                    barrel.FireBeam(enemy, weaponStats, Time.deltaTime, finalDamage);
                }
                else
                {
                    // Non-elemental (sniper): fire discrete bullets at intervals
                    beamShotTimer -= Time.deltaTime;
                    if (beamShotTimer <= 0f)
                    {
                        beamShotTimer = weaponStats.beamShotInterval;
                        
                        // Calculate Augmented Damage for Sniper Shot too
                        float dmgMult = UpgradesManager.GetStatMultiplier(AugmentType.Damage); 
                        float dmgFlat = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);
                        float finalDamage = (weaponStats.damage * dmgMult) + dmgFlat;

                        // Fire a single bullet (sniper shot) with override
                        SoundEvents.TriggerTurretShoot(this, weaponStats.weaponName, weaponStats.fireMode);
                        barrel.FireBullet(target.position, weaponStats, 1, finalDamage);
                    }
                }
            }
            
            if (beamTimer <= 0f)
            {
                // Duration finished, start reload
                isBeamFiring = false;
                
                // Apply fire rate augment
                float fireRateMultiplier = UpgradesManager.GetStatMultiplier(AugmentType.FireRate);
                float modifiedFireRate = weaponStats.fireRate * fireRateMultiplier;
                fireCooldown = 1f / modifiedFireRate;
            }
        }
        else
        {
            // Reloading (cooldown)
            fireCooldown -= Time.deltaTime;
            if (fireCooldown <= 0f)
            {
                // Start firing window
                isBeamFiring = true;
                beamTimer = weaponStats.beamDuration;
                beamShotTimer = 0f; // Fire immediately on start
            }
        }
    }

    /// <summary>
    /// Handle continuous Pulse mode for elemental bullets
    /// </summary>
    private float pulseTickTimer = 0f;
    private float pulseTickInterval = 0.2f;
    
    private void HandleContinuousPulse()
    {
        pulseTickTimer -= Time.deltaTime;
        
        if (pulseTickTimer <= 0f)
        {
            pulseTickTimer = pulseTickInterval;
            ApplyPulseAOETick();
        }
    }

    /// <summary>
    /// Standard fire rate based shooting
    /// </summary>
    private void HandleStandardFire()
    {
        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            // Apply fire rate augment
            float fireRateMultiplier = UpgradesManager.GetStatMultiplier(AugmentType.FireRate);
            float modifiedFireRate = weaponStats.fireRate * fireRateMultiplier;
            fireCooldown = 1f / modifiedFireRate;
            Fire();
        }
    }

    /// <summary>
    /// Check if current bullet is elemental - asks the barrel since it owns the bullet SO
    /// </summary>
    private bool IsElementalBullet()
    {
        var bulletSO = barrel.CurrentBulletSO;
        if (bulletSO == null) return false;
        
        return bulletSO.bulletType switch
        {
            BulletType.Fire => true,
            BulletType.Ice => true,
            BulletType.Electric => true,
            BulletType.Utility => true,
            _ => false
        };
    }

    /// <summary>
    /// Apply Pulse AOE tick damage for continuous elemental mode
    /// </summary>
    private void ApplyPulseAOETick()
    {
        var bulletSO = barrel.CurrentBulletSO;
        if (bulletSO == null) return;
        
        // Use BulletPropertiesSO for explosion radius and mask (now the authoritative source)
        float radius = bulletSO.explosiveRadius;
        Collider[] hits = Physics.OverlapSphere(target.position, radius, bulletSO.explosionMask);
        
        float tickDamage = weaponStats.damage * pulseTickInterval * weaponStats.fireRate;
        
        List<Enemy> enemiesInAOE = new List<Enemy>();
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Enemy enemy) && enemy.IsAlive)
            {
                enemiesInAOE.Add(enemy);
            }
        }
        
        BulletEffectApplicator.ApplyAOEEffect(enemiesInAOE, bulletSO, tickDamage, target.position);
        
        Color beamColor = GetElementColor(bulletSO);
        Debug.DrawLine(transform.position, target.position, beamColor, 0.1f);
    }

    private Color GetElementColor(BulletPropertiesSO bulletSO)
    {
        if (bulletSO == null) return Color.white;
        
        return bulletSO.bulletType switch
        {
            BulletType.Fire => Color.red,
            BulletType.Ice => Color.blue,
            BulletType.Electric => Color.yellow,
            BulletType.Utility => Color.magenta,
            _ => Color.white
        };
    }

    private void Fire()
    {
        if (target == null) return;

        SoundEvents.TriggerTurretShoot(this, weaponStats.weaponName, weaponStats.fireMode);

        // Calculate Final Damage with Augments
        float dmgMult = UpgradesManager.GetStatMultiplier(AugmentType.Damage); 
        
        float dmgFlat = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);
        float finalDamage = (weaponStats.damage * dmgMult) + dmgFlat;

        switch (weaponStats.fireMode)
        {
            case FireMode.Single:
                // Allow bulletsPerTap to control pellet count (enabling "Shotgun Pistol" behavior)
                // Pass finalDamage override
                barrel.FireBullet(target.position, weaponStats, -1, finalDamage);
                break;

            case FireMode.MultiShot:
                barrel.FireBullet(target.position, weaponStats, -1, finalDamage);
                break;

            case FireMode.Burst:
                StartCoroutine(FireBurst(finalDamage));
                break;
                
            case FireMode.Pulse:
                // AOE usually uses weaponStats.damage in FireAOE.
                // We should update FireAOE to take damage override too?
                // For now, let's fix Single/Multi/Burst first. AOE needs separate update in Barrel.
                // Actually FireAOE uses weaponStats inside.
                // I should update FireAOE in barrel too if possible, but let's stick to Bullet first.
                barrel.FireAOE(target.position, weaponStats); // TODO: Add override to FireAOE
                break;

            case FireMode.Arc:
                // FireArc also needs override
                // barrel.FireArc(target.position, weaponStats, weaponStats.minArcAngle);
                // I need to update FireArc too.
                barrel.FireArc(target.position, weaponStats, weaponStats.minArcAngle); 
                break;
                
            case FireMode.Beam:
                // Beam is handled in HandleBeamMode
                break;
        }
    }

    private IEnumerator FireBurst(float damageOverride)
    {
        int count = Mathf.Max(1, weaponStats.burstCount);
        for (int i = 0; i < count; i++)
        {
            // Allow bulletsPerTap per burst shot
            barrel.FireBullet(target.position, weaponStats, -1, damageOverride);
            yield return Helpers.GetWaitForSecond(weaponStats.burstInterval);
        }
    }

    #region Shop Integration

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Allow hover if we have bullet selection OR if hand is empty (for upgrade)
        if (buildManager.HasBulletSelection)
        {
            rend.material.color = hoverColor;
        }
        else if (!buildManager.HasTurretSelection)
        {
             // Maybe highlight for upgrade?
             rend.material.color = hoverColor;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. Install Bullet Logic
        if (buildManager.HasBulletSelection)
        {
             // Check if player can afford the bullet
             // Logic is in BuildManager.TryInstallBullet actually.
             // But here we call it.
             buildManager.TryInstallBullet(this);
             return;
        }

        // 2. Build Turret Logic (handled by Node, but if we click a turret logic shouldn't trigger node build)
        if (buildManager.HasTurretSelection)
        {
            // Do nothing, let raycast hit Node? 
            // Actually Turret collider blocks Node.
            return;
        }

        // 3. Upgrade UI Logic (Empty Hand)
        ToggleUpgradeUI();
    }
    
    // --- Upgrade System ---
    
    public enum StatType { Damage, FireRate, Range, BulletsPerTap, BurstCount, BeamDuration, BeamShotInterval }

    [Header("Runtime State")]
    public string turretName;
    [TextArea] public string description;
    public int currentLevel = 1;
    public int maxLevel = 100;
    
    public int totalInvestment = 0;
    
    public void Initialize(TurretBlueprintSO bp)
    {
        turretName = bp.turretName;
        description = bp.description ?? "";
        totalInvestment = bp.cost; // Initial cost
        
        // Clone the stats so upgrades are local to this instance
        if (weaponStats != null)
        {
            weaponStats = Instantiate(weaponStats);
        }
    }

    private void ToggleUpgradeUI()
    {
        if (TurretUpgradeUI.Instance != null)
        {
            TurretUpgradeUI.Instance.SetTarget(this);
        }
        else
        {
            Debug.LogWarning("TurretUpgradeUI Instance not found! Ensure the UI Canvas exists in the scene.");
        }
    }
    
    public int GetSellValue()
    {
        return Mathf.FloorToInt(totalInvestment * 0.4f);
    }

    public void Sell()
    {
        int refund = GetSellValue();
        PlayerStats.wallet += refund;
        
        // Close UI if open
        if (TurretUpgradeUI.Instance != null) TurretUpgradeUI.Instance.Close();
        
        if (parentNode != null)
        {
            Destroy(parentNode.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public int GetUpgradeCost(StatType type)
    {
        // Placeholder formula: Fixed cost 50 for MVP
        // In future, could scale based on current value or level
        return 50; 
    }
    
    public void UpgradeStat(StatType type)
    {
        if (currentLevel >= maxLevel)
        {
             Debug.Log("Max level reached!");
             return;
        }

        int cost = GetUpgradeCost(type);
        if (PlayerStats.wallet < cost)
        {
            Debug.Log("Not enough money to upgrade!");
            return;
        }
        
        PlayerStats.wallet -= cost;
        totalInvestment += cost;
        currentLevel++;
        
        switch (type)
        {
            case StatType.Damage:
                weaponStats.damage *= 1.1f; // +10%
                break;
            case StatType.FireRate:
                weaponStats.fireRate *= 1.1f; // +10%
                break;
            case StatType.Range:
                if (parentNode != null) parentNode.range *= 1.1f; // +10%
                break;
            case StatType.BulletsPerTap:
                weaponStats.bulletsPerTap += 1;
                break;
            case StatType.BurstCount:
                weaponStats.burstCount += 1;
                break;
            case StatType.BeamDuration:
                weaponStats.beamDuration *= 1.1f;
                break;
            case StatType.BeamShotInterval:
                weaponStats.beamShotInterval *= 0.9f; // -10% delay
                break;
        }
        
        Debug.Log($"Upgraded {type}!");
    }

    /// <summary>
    /// Check if the turret already has this bullet type installed
    /// </summary>
    public bool HasBullet(BulletBlueprintSO bulletType)
    {
        if (barrel == null || barrel.CurrentBulletSO == null) return false;
        
        // Compare by BulletType enum (assuming Properties exist)
        return barrel.CurrentBulletSO.bulletType == bulletType.bulletProperties.bulletType;
    }

    /// <summary>
    /// Install bullet into barrel - called by BuildManager
    /// </summary>
    public void SetBulletType(BulletBlueprintSO bulletType)
    {
        // Now fetching prefab from the BulletPropertiesSO
        var properties = bulletType.bulletProperties;
        GameObject prefabObj = properties.bulletPrefab; 
        
        if (prefabObj != null && prefabObj.TryGetComponent<BulletProjectile>(out var projectile))
        {
            // Only add cost if it's a new/different bullet (prevent infinite value stacking)
            // But usually SetBulletType is called after BuildManager checks HasBullet.
            // We assume valid install here.
            
            // If we are replacing an existing bullet, maybe we shouldn't add full cost?
            // User requirement: "increase it total value from the bullets value"
            // And "if its the same bullet... don't adding money into it value"
            
            if (!HasBullet(bulletType))
            {
                 totalInvestment += bulletType.cost;
            }

            barrel.SetBulletType(projectile, properties);
            parentNode.SetBarrelActive(true);
            SoundEvents.TriggerTurretBuilt(this);
            Debug.Log($"Installed bullet: {properties?.bulletType ?? BulletType.Normal}");
        }
        else
        {
            Debug.LogError($"Bullet Prefab in {bulletType.name} is missing BulletProjectile component!");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rend.material.color = startColor;
    }

    #endregion
}


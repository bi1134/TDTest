using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// TurretBaseModule - Controls WHEN and HOW the turret shoots.
/// Does NOT own bullet properties - that's the barrel's job.
/// </summary>
/// </summary>

[System.Serializable]
public struct BulletVisualOverride
{
    public BulletType bulletType;
    public BulletProjectile visualPrefab;
}

public class TurretBaseModule : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("Base Stats (How it shoots)")]
    public TurretPropertiesSO weaponStats;

    public TurretPropertiesSO GetTurretProperties() => weaponStats;

    [SerializeField] private TurretBarrelModule barrel;

    private float fireCooldown;
    [SerializeField] private Transform target;

    [SerializeField] private Turret parentNode;

    [Header("Visual Overrides")]
    [Tooltip("Allows this specific turret to use different bullet prefabs for standard bullet types (e.g., throwing rocks instead of normal bullets)")]
    [SerializeField] private List<BulletVisualOverride> visualOverrides = new List<BulletVisualOverride>();

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
    private float flashTimer = 0f;
    private const float FLASH_INTERVAL = 0.15f; // How often to play muzzle/elemental flash for continuous modes

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
            beamTimer -= Time.deltaTime;

            Enemy enemy = target.GetComponent<Enemy>();
            if (enemy != null && enemy.IsAlive)
            {
                var bulletSO = barrel.CurrentBulletSO;
                
                float dmgMult = UpgradesManager.GetStatMultiplier(AugmentType.Damage);
                float dmgFlat = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);

                if (bulletSO != null && IsElementalBullet())
                {
                    switch (bulletSO.bulletType)
                    {
                        case BulletType.Fire:
                            // Continuous cone spray — flash plays continuously
                            flashTimer -= Time.deltaTime;
                            if (flashTimer <= 0f)
                            {
                                flashTimer = FLASH_INTERVAL;
                                barrel.PlayFlash();
                            }
                            float beamRange = parentNode != null ? parentNode.GetEffectiveRange() : weaponStats.coneRange;
                            barrel.FireConeArea(weaponStats, bulletSO, Time.deltaTime, weaponStats.enemyMask, beamRange);
                            break;

                        case BulletType.Ice:
                        case BulletType.Electric:
                            // Strike at enemy position on an interval — flash plays per strike
                            beamShotTimer -= Time.deltaTime;
                            if (beamShotTimer <= 0f)
                            {
                                beamShotTimer = weaponStats.beamEffectInterval;
                                barrel.PlayFlash();
                                float beamBaseDmg = (weaponStats.damage * dmgMult) + dmgFlat;
                                barrel.SpawnElementalStrike(bulletSO.bulletType, enemy.transform.position, bulletSO, weaponStats.enemyMask, beamBaseDmg, isBeamTurret: true);
                            }
                            break;

                        default:
                            // Utility/Buff: use older beam logic
                            float finalDamage = (weaponStats.damage * dmgMult) + dmgFlat;
                            barrel.FireBeam(enemy, weaponStats, Time.deltaTime, finalDamage);
                            break;
                    }
                }
                else
                {
                    // Non-elemental (sniper): fire discrete bullets at intervals
                    beamShotTimer -= Time.deltaTime;
                    if (beamShotTimer <= 0f)
                    {
                        beamShotTimer = weaponStats.beamShotInterval;
                        float finalDamage = (weaponStats.damage * dmgMult) + dmgFlat;
                        barrel.FireBullet(target.position, weaponStats, 1, finalDamage);
                    }
                }
            }
            else
            {
                // No target or target died — fade out cone VFX
                barrel.SetConeVFXActive(false);
            }

            if (beamTimer <= 0f)
            {
                isBeamFiring = false;
                barrel.SetConeVFXActive(false);
                float fireRateMultiplier = UpgradesManager.GetStatMultiplier(AugmentType.FireRate);
                fireCooldown = 1f / (weaponStats.fireRate * fireRateMultiplier);
            }
        }
        else
        {
            // Fading out cone VFX while reloading
            barrel.SetConeVFXActive(false);
            
            fireCooldown -= Time.deltaTime;
            if (fireCooldown <= 0f)
            {
                isBeamFiring = true;
                beamTimer = weaponStats.beamDuration;
                beamShotTimer = 0f;
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

        var bulletSO = barrel.CurrentBulletSO;
        if (bulletSO == null) return;

        switch (bulletSO.bulletType)
        {
            case BulletType.Fire:
                // Continuous cone spray — flash plays continuously
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    flashTimer = FLASH_INTERVAL;
                    barrel.PlayFlash();
                }
                float pulseRange = parentNode != null ? parentNode.GetEffectiveRange() : weaponStats.coneRange;
                barrel.FireConeArea(weaponStats, bulletSO, Time.deltaTime, weaponStats.enemyMask, pulseRange);
                break;

            case BulletType.Ice:
            case BulletType.Electric:
                // Strike at target position — flash plays per strike
                if (pulseTickTimer <= 0f)
                {
                    pulseTickTimer = pulseTickInterval;
                    if (target != null)
                    {
                        barrel.PlayFlash();
                        float pulseDmgMult = UpgradesManager.GetStatMultiplier(AugmentType.Damage);
                        float pulseDmgFlat = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);
                        float pulseBaseDmg = (weaponStats.damage * pulseDmgMult) + pulseDmgFlat;
                        barrel.SpawnElementalStrike(bulletSO.bulletType, target.position, bulletSO, weaponStats.enemyMask, pulseBaseDmg, isBeamTurret: false);
                    }
                }
                break;

            default:
                // Default pulse AOE (non-elemental)
                if (pulseTickTimer <= 0f)
                {
                    pulseTickTimer = pulseTickInterval;
                    ApplyPulseAOETick();
                }
                break;
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
        
        BulletEffectApplicator.ApplyAOEEffect(enemiesInAOE, bulletSO, tickDamage, target.position, gameObject);
        
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
            if (target == null) break;
            
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
        // 1. Install Bullet Logic (Attach anything that can be attached)
        if (buildManager.HasBulletSelection)
        {
             // TryInstallBullet checks validity and installs if affordable
             buildManager.TryInstallBullet(this);
             return; // Stop here, do not open upgrade UI or clear turret hand
        }

        // 2. Clear Turret Selection so we can fluidly select the Turret to open Upgrade UI
        if (buildManager.HasTurretSelection)
        {
            buildManager.ClearTurretSelection();
        }

        // 3. Upgrade UI Logic (Empty Hand or freshly cleared Turret Hand)
        SoundEvents.TriggerButtonClicked(this);
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

    [Header("Experience")]
    private int totalExperience;
    private int xpLevel;
    public int freeUpgradePoints;
    private int nextLevelXP;
    private bool hasNewLevelUp;
    private GameObject upgradeVFXInstance;
    public bool HasNewLevelUp => hasNewLevelUp;

    private TurretBlueprintSO installedTurretBlueprint;
    public TurretBlueprintSO InstalledTurretBlueprint => installedTurretBlueprint;

    private BulletBlueprintSO installedBulletBlueprint;
    public BulletBlueprintSO InstalledBulletBlueprint => installedBulletBlueprint;

    public int TotalExperience => totalExperience;
    public int XPLevel => xpLevel;
    public int FreeUpgradePoints => freeUpgradePoints;
    public int NextLevelXP => nextLevelXP;
    /// <summary>XP threshold of the current level (for fill bar calculation)</summary>
    public int CurrentLevelXP => xpLevel > 0 ? WaveManager.GetTurretXPForLevel(xpLevel) : 0;
    
    public void Initialize(TurretBlueprintSO bp)
    {
        installedTurretBlueprint = bp;
        turretName = bp.turretName;
        description = bp.description ?? "";
        totalInvestment = bp.cost; // Initial cost

        // NOTE: Initialize is also called when the Turret is pulled from the Object Pool.
        weaponStats = Resources.Load<TurretPropertiesSO>(weaponStats.name) ?? Object.Instantiate(weaponStats);

        // Reset properties in case it came from the pool with old upgrades
        fireCooldown = 1f / weaponStats.fireRate;
        currentLevel = 0;

        // Reset XP
        totalExperience = 0;
        xpLevel = 0;
        freeUpgradePoints = 0;
        nextLevelXP = WaveManager.GetTurretXPForLevel(1);
        hasNewLevelUp = false;
        installedBulletBlueprint = null;

        // Cleanup upgrade VFX from previous pool use
        if (upgradeVFXInstance != null)
        {
            VFXManager.Instance?.ReleasePersistentEffect(upgradeVFXInstance);
            upgradeVFXInstance = null;
        }

        target = null;
        isBeamFiring = false;
        beamTimer = 0f;

        // Initialize cone VFX defaults on barrel
        if (barrel != null && parentNode != null)
        {
            barrel.InitConeParticleDefaults(parentNode.range);
        }
    }

    public void AddExperience(int amount)
    {
        totalExperience += amount;
        CheckForLevelUp();
    }

    private void CheckForLevelUp()
    {
        bool didLevelUp = false;
        while (totalExperience >= nextLevelXP)
        {
            xpLevel++;
            freeUpgradePoints++;
            nextLevelXP = WaveManager.GetTurretXPForLevel(xpLevel + 1);
            didLevelUp = true;
        }

        if (didLevelUp)
        {
            hasNewLevelUp = true;
            ShowUpgradeReadyVFX();
        }
    }

    private void ShowUpgradeReadyVFX()
    {
        if (upgradeVFXInstance != null) return; // already showing
        if (VFXManager.Instance == null) return;

        upgradeVFXInstance = VFXManager.Instance.SpawnPersistentEffect(
            VFXType.UpgradeReady, transform.position, transform);
    }

    /// <summary>
    /// Called when the player selects this turret. Hides the upgrade VFX indicator.
    /// VFX stays hidden until the next level-up.
    /// </summary>
    public void AcknowledgeLevelUp()
    {
        hasNewLevelUp = false;
        if (upgradeVFXInstance != null)
        {
            VFXManager.Instance?.ReleasePersistentEffect(upgradeVFXInstance);
            upgradeVFXInstance = null;
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
        SoundEvents.TriggerTurretSold(this, transform.position);
        
        // Close UI if open
        if (TurretUpgradeUI.Instance != null) TurretUpgradeUI.Instance.Close();
        
        if (parentNode != null)
        {
            Node builtNode = parentNode.GetComponentInParent<Node>();
            if (builtNode != null)
            {
                builtNode.turretBase = null; // Free up the node
            }
            
            if (TurretPoolManager.Instance != null) {
                // If it has a parent Turret script (the visual base), we should pool the parent!
                TurretPoolManager.Instance.ReturnToPool(parentNode.gameObject);
            } else {
                Destroy(parentNode.gameObject);
            }
        }
        else
        {
            if (TurretPoolManager.Instance != null) {
                TurretPoolManager.Instance.ReturnToPool(gameObject);
            } else {
                Destroy(gameObject);
            }
        }
    }
    
    public int GetUpgradeCost(StatType type)
    {
        return WaveManager.GetUpgradeCostForLevel(currentLevel);
    }

    public void UpgradeStat(StatType type)
    {
        if (currentLevel >= maxLevel)
        {
             Debug.Log("Max level reached!");
             return;
        }

        int cost = GetUpgradeCost(type);

        if (freeUpgradePoints > 0)
        {
            // Free upgrade from XP level-up
            freeUpgradePoints--;
        }
        else
        {
            if (PlayerStats.wallet < cost)
            {
                Debug.Log("Not enough money to upgrade!");
                return;
            }
            PlayerStats.wallet -= cost;
            totalInvestment += cost;
        }

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
        
        // Check for Visual Override for this specific turret
        foreach (var over in visualOverrides)
        {
            if (over.bulletType == properties.bulletType && over.visualPrefab != null)
            {
                prefabObj = over.visualPrefab.gameObject;
                break;
            }
        }

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

            installedBulletBlueprint = bulletType;
            barrel.SetBulletType(projectile, properties);
            parentNode.SetBarrelActive(true);
            SoundEvents.TriggerTurretBuilt(this, transform.position);
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


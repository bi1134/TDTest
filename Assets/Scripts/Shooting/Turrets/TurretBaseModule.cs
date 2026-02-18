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
                    barrel.FireBeam(enemy, weaponStats, Time.deltaTime);
                }
                else
                {
                    // Non-elemental (sniper): fire discrete bullets at intervals
                    beamShotTimer -= Time.deltaTime;
                    if (beamShotTimer <= 0f)
                    {
                        beamShotTimer = weaponStats.beamShotInterval;
                        // Fire a single bullet (sniper shot)
                        barrel.FireBullet(target.position, weaponStats, pelletsOverride: 1);
                    }
                }
            }
            
            if (beamTimer <= 0f)
            {
                // Duration finished, start reload
                isBeamFiring = false;
                
                // Apply fire rate augment
                float fireRateMultiplier = AugmentManager.GetStatMultiplier(AugmentType.FireRate);
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
            float fireRateMultiplier = AugmentManager.GetStatMultiplier(AugmentType.FireRate);
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

        switch (weaponStats.fireMode)
        {
            case FireMode.Single:
                barrel.FireBullet(target.position, weaponStats, pelletsOverride: 1);
                break;

            case FireMode.MultiShot:
                barrel.FireBullet(target.position, weaponStats);
                break;

            case FireMode.Burst:
                StartCoroutine(FireBurst());
                break;
                
            case FireMode.Pulse:
                barrel.FireAOE(target.position, weaponStats);
                break;

            case FireMode.Arc:
                barrel.FireArc(target.position, weaponStats, weaponStats.minArcAngle);
                break;
                
            case FireMode.Beam:
                // Beam is handled in HandleBeamMode
                break;
        }
    }

    private IEnumerator FireBurst()
    {
        int count = Mathf.Max(1, weaponStats.burstCount);
        for (int i = 0; i < count; i++)
        {
            barrel.FireBullet(target.position, weaponStats, pelletsOverride: 1);
            yield return new WaitForSeconds(weaponStats.burstInterval);
        }
    }

    #region Shop Integration

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Allow hover if we have bullet selection, regardless of whether barrel is active
        if (!buildManager.HasBulletSelection) return; 
        
        rend.material.color = hoverColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!buildManager.HasBulletSelection)
        {
            Debug.Log("Cannot install ammo: projectile is null");
            return;
        }

        buildManager.TryInstallBullet(this);
    }

    /// <summary>
    /// Install bullet into barrel - called by BuildManager
    /// </summary>
    public void SetBulletType(BulletBlueprintSO bulletType)
    {
        // Allow replacement if barrel is already active
        // if (!barrel.isActiveAndEnabled || !barrel.gameObject.activeSelf) 
        {
            // Now fetching prefab from the BulletPropertiesSO
            var properties = bulletType.bulletProperties;
            GameObject prefabObj = properties.bulletPrefab; 
            
            if (prefabObj != null && prefabObj.TryGetComponent<BulletProjectile>(out var projectile))
            {
                barrel.SetBulletType(projectile, properties);
                parentNode.SetBarrelActive(true);
                Debug.Log($"Installed bullet: {properties?.bulletType ?? BulletType.Normal}");
            }
            else
            {
                Debug.LogError($"Bullet Prefab in {bulletType.name} is missing BulletProjectile component!");
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rend.material.color = startColor;
    }

    #endregion
}

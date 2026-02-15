using UnityEngine;

[CreateAssetMenu(fileName = "BulletProperties", menuName = "Scriptable Objects/BulletProperties")]
public class BulletPropertiesSO : ScriptableObject
{
    [Header("Bullet Type")]
    public BulletType bulletType;
    
    [Header("Utility Debuff (only when bulletType == Utility)")]
    public UtilityDebuffType utilityDebuffType = UtilityDebuffType.None;
    
    [Header("Basic Properties")]
    public int maxBounces = 2;
    public float maxLifeTime = 3f;
    public float bulletDrop;
    [Tooltip("Multiplier for gravity. 1 = normal, >1 = falls faster.")]
    public float gravityScale = 1f;
    public string bulletPoolTag = "Bullet";
    
    [Header("Visuals")]
    public GameObject bulletPrefab;

    [Header("=== ELEMENTAL STATS ===")]
    
    [Header("Ice Properties")]
    [Tooltip("Slow percentage (0-1), e.g., 0.3 = 30% slow")]
    [Range(0f, 1f)]
    public float iceSlowPercent = 0.3f;
    [Tooltip("Slow duration in seconds")]
    public float iceSlowDuration = 2f;

    [Header("Fire Properties")]
    [Tooltip("Damage per second for Fire DOT")]
    public float fireDOTDamagePerSecond = 5f;
    [Tooltip("Fire DOT duration in seconds")]
    public float fireDOTDuration = 3f;

    [Header("Electric Properties")]
    [Tooltip("Stun duration in seconds")]
    public float electricStunDuration = 0.5f;
    [Tooltip("Number of enemies to chain to")]
    public int electricChainCount = 2;
    [Tooltip("Chain range in units")]
    public float electricChainRange = 5f;
    [Tooltip("Damage multiplier per chain (e.g., 0.7 = 70% of previous)")]
    [Range(0f, 1f)]
    public float electricChainDamageMultiplier = 0.7f;

    [Header("Explosive Properties")]
    [Tooltip("AoE radius for explosions")]
    public float explosiveRadius = 3f;
    [Tooltip("Force applied to rigidbodies in explosion")]
    public float explosionForce = 500f;
    [Tooltip("Layer mask for detecting enemies in explosion")]
    public LayerMask explosionMask;
    [Tooltip("Damage falloff exponent (1 = linear, 2 = quadratic)")]
    public float explosionFalloffExponent = 1f;
    [Tooltip("Damage multiplier at edge of explosion (0 = no damage, 1 = full)")]
    [Range(0f, 1f)]
    public float explosiveFalloff = 0.5f;
    [Tooltip("If true, projectile explodes on lifetime timeout")]
    public bool explodeOnTimeout = false;

    [Header("Utility Debuff Stats")]
    [Tooltip("Slow percentage for Utility Slow (weaker than Ice)")]
    [Range(0f, 1f)]
    public float utilitySlowPercent = 0.15f;
    [Tooltip("Utility slow duration")]
    public float utilitySlowDuration = 1.5f;
    
    [Tooltip("Vulnerability percentage (extra damage taken)")]
    [Range(0f, 1f)]
    public float vulnerabilityPercent = 0.25f;
    [Tooltip("Vulnerability duration")]
    public float vulnerabilityDuration = 3f;
    
    [Tooltip("Shield Shred percentage of max shield to remove")]
    [Range(0f, 1f)]
    public float shieldShredPercent = 0.35f;

    [Header("Buff Stats (affects friendly turrets)")]
    [Tooltip("Damage buff percentage")]
    [Range(0f, 1f)]
    public float buffDamagePercent = 0.2f;
    [Tooltip("Fire rate buff percentage")]
    [Range(0f, 1f)]
    public float buffFireRatePercent = 0.15f;
    [Tooltip("Buff duration")]
    public float buffDuration = 5f;

    [Header("Arc Ground Zone Properties")]
    [Tooltip("Radius of the ground zone effect")]
    public float arcZoneRadius = 3f;
    [Tooltip("How long the ground zone lasts")]
    public float arcZoneDuration = 4f;
    [Tooltip("How often the zone damages enemies inside")]
    public float arcZoneTickInterval = 0.5f;
    [Tooltip("Damage per tick of the ground zone")]
    public float arcZoneDamagePerTick = 5f;
    [Tooltip("Prefab to spawn for the ground zone visual")]
    public GameObject arcZonePrefab;
}

public enum BulletType
{
    Normal,     // Flat damage only
    Explosive,  // AoE damage
    Electric,   // Chain/stun
    Fire,       // Damage over time
    Ice,        // Slow + damage
    Buff,       // GDD: Never deals damage, never applies elemental effects
    Utility     // GDD: Utility only, no damage (uses UtilityDebuffType)
}

/// <summary>
/// GDD Utility Debuff Types:
/// - Slow: weaker than Ice
/// - Vulnerability: increases damage to normal health only (no Shield HP effect)
/// - ShieldShred: reduces Shield HP by percentage of max
/// </summary>
public enum UtilityDebuffType
{
    None,
    Slow,
    Vulnerability,
    ShieldShred
}
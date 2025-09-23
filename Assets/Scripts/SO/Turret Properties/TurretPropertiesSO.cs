using UnityEngine;

public enum FireMode { Single, MultiShot, Burst, Pulse, Arc, Beam, Fountain}
public enum WeaponName { Pistol, Rifle, Shotgun, Sniper, Sword, BowAndArrow, Staff }

[CreateAssetMenu(fileName = "TurretProperities", menuName = "Scriptable Objects/TurretProperities")]
public class TurretPropertiesSO : ScriptableObject
{
    public WeaponName weaponName;

    //check the mask for avoid
    public LayerMask groundMask;

    [Header("Gun Stats")]
    public float damage;
    public float fireRate = 1;

    [Tooltip("How many pellets spawn in one trigger pull (used in MultiShot/shotgun).")]
    public int bulletsPerTap = 1;

    [Header("Projectile Stats (Only for Projectile Weapons)")]
    public float bulletSpeed;
    public float upwardForce;

    // spreadDeg: cone angle in degrees. >0 = cone/ellipse, 0 = line/cross mode (uses spreadX/Y)
    public float spread;   // degrees
    public float spreadX;  // multiplier in cone mode, or half-extent (deg) in line mode
    public float spreadY;  // multiplier in cone mode, or half-extent (deg) in line mode

    [Header("Fire Mode")]
    public FireMode fireMode = FireMode.Single;
    [Tooltip("Used only when fireMode = Burst")]
    public int burstCount = 3;
    [Tooltip("Seconds between shots in a burst")]
    public float burstInterval = 0.05f;

    [Header("AOE stuff")]
    [Tooltip("Use only when firemode = pulse")]
    public float explosionRadius = 0f;
    public float explosionForce = 0f;
    public LayerMask explosionMask;
    [Tooltip("1 = linear falloff, 2 = quadratic, etc.")]
    public float explosionFalloffExponent = 1f;
    [Tooltip("If true, projectile explodes on lifetime timeout.")]
    public bool explodeOnTimeout = false;
}

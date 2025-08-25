using UnityEngine;

public enum FireMode { Single, MultiShot, Burst }
public enum WeaponName { Pistol, Rifle, Shotgun, Sniper, Sword, BowAndArrow, Staff }

[CreateAssetMenu(fileName = "WeaponProperities", menuName = "Scriptable Objects/WeaponProperities")]
public class WeaponPropertiesSO : ScriptableObject
{
    public WeaponName weaponName;

    [Header("Gun Stats")]
    public float damage;
    public float fireRate = 1;

    [Tooltip("How many pellets spawn in one trigger pull (used in MultiShot/shotgun).")]
    public int bulletsPerTap = 1;

    [Header("Projectile Stats (Only for Projectile Weapons)")]
    public float bulletSpeed;
    public float bulletLifetime;

    // spreadDeg: cone angle in degrees. >0 = cone/ellipse, 0 = line/cross mode (uses spreadX/Y)
    public float spread;   // degrees
    public float spreadX;  // multiplier in cone mode, or half-extent (deg) in line mode
    public float spreadY;  // multiplier in cone mode, or half-extent (deg) in line mode
    public float upwardForce;

    [Header("Fire Mode")]
    public FireMode fireMode = FireMode.Single;
    [Tooltip("Used only when fireMode = Burst")]
    public int burstCount = 3;
    [Tooltip("Seconds between shots in a burst")]
    public float burstInterval = 0.05f;
}

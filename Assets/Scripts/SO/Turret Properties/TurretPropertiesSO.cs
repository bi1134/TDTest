using UnityEngine;

public enum FireMode { Single, MultiShot, Burst, Pulse, Arc, Beam }
public enum WeaponName { Cannon, Crossbow, MachineGun, Magic }

[CreateAssetMenu(fileName = "TurretProperities", menuName = "Scriptable Objects/TurretProperities")]
public class TurretPropertiesSO : ScriptableObject
{
    public WeaponName weaponName;

    [Header("Masks")]
    [Tooltip("Ground layer for Arc projectiles to detect landing")]
    public LayerMask groundMask;
    [Tooltip("Enemy layer for AOE detection")]
    public LayerMask enemyMask;

    [Header("Gun Stats")]
    public float damage;
    public float fireRate = 1;

    [Tooltip("How many pellets spawn in one trigger pull (used in MultiShot/shotgun).")]
    public int bulletsPerTap = 1;

    [Header("Projectile Stats (Only for Projectile Weapons)")]
    public float bulletSpeed;
    public float upwardForce;
    [Tooltip("Minimum launch angle for Arc projectiles (lower = lower arc, higher = lob)")]
    public float minArcAngle = 25f;

    [Tooltip("Accuracy error in degrees (0 = perfect accuracy)")]
    public float accuracyError = 0f;

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

    [Header("Beam Mode Settings")]
    [Tooltip("How long the beam fires before reloading")]
    public float beamDuration = 3f;
    [Tooltip("Time between shots for non-elemental bullets in beam mode (sniper)")]
    public float beamShotInterval = 1.75f;
    [Tooltip("Time between effect applications (stun, slow, chain) during beam")]
    public float beamEffectInterval = 0.5f;

    [Header("Elemental Cone (Fire) Settings")]
    [Tooltip("Half-angle of the Fire cone in degrees. 45 = 90 degree wide fan.")]
    public float coneAngle = 45f;
    [Tooltip("Range of the Fire cone in world units. 0 = use parent Turret's range.")]
    public float coneRange = 8f;
}

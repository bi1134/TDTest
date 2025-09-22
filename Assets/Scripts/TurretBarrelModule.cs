using UnityEngine;
using System.Collections.Generic;

public class TurretBarrelModule : MonoBehaviour
{
    [SerializeField] private BulletPropertiesSO bulletSettings;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BulletProjectile bulletPrefab;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioSource audioSource;

    [Header("Spread Shape Controls")]
    [Range(0f, 90f)]
    [SerializeField] private float axisRotationDeg = 0f;
    [SerializeField] private bool evenAxisDistribution = true;
    [SerializeField] private bool deterministicPelletSpacing = true;

    [Header("Per-shot Jitter (optional)")]
    [Tooltip("Extra random degrees added to 'spread' per shot (cone mode).")]
    [SerializeField] private float jitterSpreadDeg = 0f;
    [Tooltip("Extra random degrees added to line extents per shot (line/cross mode).")]
    [SerializeField] private float jitterLineExtentX = 0f;
    [SerializeField] private float jitterLineExtentY = 0f;
    [Tooltip("Random rotation (deg) applied to axes per shot (useful for gatling wobble).")]
    [SerializeField] private float jitterAxisRotationDeg = 0f;

    public void FireBullet(Vector3 targetPos, WeaponPropertiesSO weaponStats, int pelletsOverride = -1)
    {
        Vector3 baseDir = (targetPos - firePoint.position).normalized;

        // Effective params with jitter
        float effSpread = Mathf.Max(0f, weaponStats.spread + Random.Range(-jitterSpreadDeg, jitterSpreadDeg));
        float effSpreadX = weaponStats.spreadX + Random.Range(-jitterLineExtentX, jitterLineExtentX);
        float effSpreadY = weaponStats.spreadY + Random.Range(-jitterLineExtentY, jitterLineExtentY);
        float effAxisRot = axisRotationDeg + Random.Range(-jitterAxisRotationDeg, jitterAxisRotationDeg);

        int pelletCount = pelletsOverride > 0 ? pelletsOverride : Mathf.Max(1, weaponStats.bulletsPerTap);

        // Directions for exactly pelletCount pellets
        List<Vector3> directions = SpreadHelpers.GeneratePelletDirections(
            baseDir,
            pelletCount,
            effSpread,
            effSpreadX,
            effSpreadY,
            effAxisRot,
            evenAxisDistribution,
            deterministicPelletSpacing
        );

        foreach (var dir in directions)
        {
            var bulletObj = Instantiate(
                bulletPrefab,
                firePoint.position,
                Quaternion.LookRotation(dir)
            );

            if (bulletObj != null)
            {
                bulletObj.SetShooter(this.gameObject);
                bulletObj.Initialize(
                    dir,
                    weaponStats.bulletSpeed,
                    weaponStats.upwardForce
                );
            }
        }

        //muzzleFlash?.Play();
        PlayShootSound();
    }

    private void PlayShootSound()
    {
        if (audioSource != null && fireClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(fireClip);
        }
    }

    public void SetBulletType(BulletProjectile bulletType)
    {
        bulletPrefab = bulletType;
    }
}

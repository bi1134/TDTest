using UnityEngine;

public class TurretBarrelModule : MonoBehaviour
{
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

    public void SetBulletType(BulletProjectile bulletType) => bulletPrefab = bulletType;

    public void FireBullet(Vector3 targetPos, TurretPropertiesSO weaponStats, int pelletsOverride = -1)
    {
        Vector3 baseDir = (targetPos - firePoint.position).normalized;

        // Effective params with jitter
        float effSpread = Mathf.Max(0f, weaponStats.spread + Random.Range(-jitterSpreadDeg, jitterSpreadDeg));
        float effSpreadX = weaponStats.spreadX + Random.Range(-jitterLineExtentX, jitterLineExtentX);
        float effSpreadY = weaponStats.spreadY + Random.Range(-jitterLineExtentY, jitterLineExtentY);
        float effAxisRot = axisRotationDeg + Random.Range(-jitterAxisRotationDeg, jitterAxisRotationDeg);

        int pelletCount = pelletsOverride > 0 ? pelletsOverride : Mathf.Max(1, weaponStats.bulletsPerTap);

        // Directions for exactly pelletCount pellets
        var directions = ShootHelpers.GeneratePelletDirections(
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
                    weaponStats.upwardForce,
                    weaponStats.damage
                );
            }
        }

        //muzzleFlash?.Play();
        PlayShootSound();
    }

    public void FireAOE(Vector3 targetPos, TurretPropertiesSO s)
    {
        if (bulletPrefab == null) return;

        Vector3 dir = (targetPos - firePoint.position).normalized;

        var payload = new ImpactPayload
        {
            aoeRadius = s.explosionRadius,
            aoeForce = s.explosionForce,
            aoeMask = s.explosionMask,
            falloffExponent = Mathf.Max(0.01f, s.explosionFalloffExponent),
            explodeOnTimeout = s.explodeOnTimeout
        };

        var proj = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));
        proj.SetShooter(gameObject);

        // if lob, you can compute a ballistic velocity; else just use forward shot + upwardForce
        proj.Initialize(dir, s.bulletSpeed, s.upwardForce, s.damage, payload);

        PlayShootSound();
    }

    public void FireArc(Vector3 targetPos, TurretPropertiesSO s, float minAngleDeg = 25f)
    {
        if (bulletPrefab == null) return;

        var proj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        proj.SetShooter(gameObject);

        Vector3 origin = firePoint.position;
        float xz = new Vector2(targetPos.x - origin.x, targetPos.z - origin.z).magnitude;

        bool picked = false;
        Vector3 v0 = Vector3.zero;

        // local helper: angle + clearance
        bool Accept(Vector3 cand)
        {
            if (ShootHelpers.LaunchAngleDeg(cand) < minAngleDeg) return false;

            float vh = new Vector2(cand.x, cand.z).magnitude;
            float t = vh > 0f ? xz / vh : 0f;

            return ShootHelpers.PathIsClear(origin, cand, Physics.gravity, 0.05f, t, 20, s.groundMask, out _);
        }

        // 1) Try LOW arc (fixed speed)
        if (ShootHelpers.TrySolveBallisticArc(origin, targetPos, s.bulletSpeed, Physics.gravity.y, false, out var lowV) && Accept(lowV))
        {
            v0 = lowV; picked = true;
        }

        // 2) Try HIGH arc (fixed speed)
        if (!picked && ShootHelpers.TrySolveBallisticArc(origin, targetPos, s.bulletSpeed, Physics.gravity.y, true, out var highV) && Accept(highV))
        {
            v0 = highV; picked = true;
        }

        // 3) Enforce minimum angle using time-of-flight
        if (!picked)
        {
            float baseTime = Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(2f, 20f, xz)); // tune
            var minAngleV = ShootHelpers.SolveBallisticByMinAngle(origin, targetPos, minAngleDeg, baseTime, Physics.gravity);

            // Optionally bump time a bit to raise the arc until it clears (and still meets min angle)
            Vector3 cand = minAngleV;
            bool ok = false;
            float bumpT = 0f;

            for (int i = 0; i < 3; i++)
            {
                cand = ShootHelpers.SolveBallisticByTime(origin, targetPos, baseTime + bumpT, Physics.gravity);
                if (ShootHelpers.LaunchAngleDeg(cand) >= minAngleDeg && Accept(cand)) { ok = true; break; }
                bumpT += 0.2f;
            }

            v0 = ok ? cand : minAngleV;
            picked = true; // we’ll accept even if it might clip; the next fallback still handles that.
        }

        // 4) If somehow still not acceptable, fallback to forward+gravity (shouldn’t happen often)
        if (!picked)
        {
            Vector3 dir = (targetPos - origin).normalized;
            proj.Initialize(dir, s.bulletSpeed, s.upwardForce, s.damage, null, true);
            return;
        }

        // Final: ballistic with gravity + direct velocity
        proj.Initialize(default, 0f, 0f, s.damage, null, true, v0);
    }

    private void PlayShootSound()
    {
        if (audioSource != null && fireClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(fireClip);
        }
    }

}

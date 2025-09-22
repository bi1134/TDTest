using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] public GameObject tracer;
    [SerializeField] private BulletPropertiesSO settings;

    private int bounceRemaining;
    public bool isActive;
    private Rigidbody rb;
    private GameObject shooterGameObject;
    private float baseDamage;

    private void OnEnable()
    {
        rb = GetComponent<Rigidbody>();
        isActive = true;
        bounceRemaining = settings.maxBounces;

        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);

            Color color = Color.white;
            switch (settings.bulletType)
            {
                case BulletType.Normal:
                    color = new Color(191f / 255f, 131f / 255f, 0f, 1f); // yellow-ish
                    break;
                case BulletType.Explosive:
                    color = new Color(6f / 255f, 51f / 255f, 3f / 255f, 1f); // dark green
                    break;
            }

            mpb.SetColor("_Color", color); // Particle Unlit uses _Color
            renderer.SetPropertyBlock(mpb);
        }

        if (tracer != null && tracer.TryGetComponent(out TrailRenderer trail))
        {
            tracer.SetActive(true);
            trail.Clear();
            trail.transform.position = transform.position;

            var mpb = new MaterialPropertyBlock();
            trail.GetPropertyBlock(mpb);

            Color trailBase = Color.red;
            Color trailEmission = Color.yellow;

            switch (settings.bulletType)
            {
                case BulletType.Normal:
                    trailBase = new Color(1f, 0.7f, 0f); // yellow
                    trailEmission = new Color(191f / 255f, 102f / 255f, 0f) * 3.416924f;
                    break;
                case BulletType.Explosive:
                    trailBase = new Color(2f / 255f, 18f / 255f, 191f / 255f);
                    trailEmission = new Color(81f / 255f, 186f / 255f, 5f / 255f) * 2.923552f;
                    break;
            }

            mpb.SetColor("_BaseColor", trailBase);         // URP Lit base color
            mpb.SetColor("_EmissionColor", trailEmission); // HDR Emission
            trail.SetPropertyBlock(mpb);
        }
    }

    public void Initialize(Vector3 direction, float bulletSpeed, float upwardForce)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * bulletSpeed, ForceMode.Impulse);
        rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);

        StartCoroutine(DestroySelf(settings.maxLifeTime));
    }

    private void FixedUpdate()
    {
        // Optional gravity influence (bullet drop)
        if (settings.bulletDrop != 0f)
        {
            rb.AddForce(Vector3.down * settings.bulletDrop, ForceMode.Acceleration);
        }
    }
    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        isActive = false;
    }

    IEnumerator DestroySelf(float delay)
    {
        yield return Helpers.GetWaitForSecond(delay);

        // Force deactivate if bullet still exists
        if (isActive)
            TryExplodeOrDeactivate();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;
        if (collision.gameObject.CompareTag("Enemy"))
        {
            contact = collision.contacts[0];
            hitPoint = contact.point;
            TryExplodeOrDeactivate(hitPoint);
        }
        /*
        GameObject fx = ObjectPooler.SpawnFromPool("BulletHit", hitPoint, Quaternion.identity);
        if (fx.TryGetComponent(out PooledEffect pooledFx))
        {
            pooledFx.SetPoolTag("BulletHit");
        }
        */
        /*
        if (collision.collider.TryGetComponent(out HitBox target))
        {
            if (target.healthSystem == shooterHealth)
                return;

            float damageMultiplier = 1f;

            if (target.gameObject.layer == shooterGameObject.layer)
            {
                damageMultiplier = 0.3f; // 30% damage to teammates
            }

            target.TakeDamage(baseDamage, rb.linearVelocity.normalized, damageMultiplier);

            if (settings.bulletType == BulletType.Explosive)
            {
                Explode(contact.point);
                return;
            }
        }
        */

        if (bounceRemaining > 0)
        {
            //might gonna change the bounce to automaticly bounce to the next enemy instead of bounce freely from physics
            HandleBounce(hitNormal);
        }
        else
        {
            TryExplodeOrDeactivate(hitPoint);
        }
    }

    private void HandleBounce(Vector3 hitNormal)
    {
        if (bounceRemaining <= 0)
        {
            TryExplodeOrDeactivate(transform.position);
            return;
        }

        bounceRemaining--;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        Vector3 direction = velocity.normalized;
        float travelDistance = speed * Time.fixedDeltaTime;
        float radius = GetBulletRadius();

        if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, travelDistance, ~0))
        {
            // Move just before the hit point
            transform.position = hit.point + hit.normal * 0.01f;

            // Reflect off the surface
            Vector3 reflected = Vector3.Reflect(direction, hit.normal);
            rb.linearVelocity = reflected * speed;
        }
        else
        {
            // Continue moving forward manually
            transform.position += velocity * Time.fixedDeltaTime;
        }
    }

    private void Explode(Vector3 point)
    {
        /*
        Collider[] enemies = Physics.OverlapSphere(point, settings.explosionRadius, settings.explosionMask);

        foreach (Collider enemy in enemies)
        {
            if (enemy.TryGetComponent(out HitBox target))
            {
                if (target.healthSystem == shooterHealth)
                    continue;

                Vector3 direction = (enemy.transform.position - point).normalized;
                target.TakeDamage(baseDamage, direction);
            }

            if (enemy.TryGetComponent<Rigidbody>(out var enemyRb))
            {
                enemyRb.AddExplosionForce(settings.explosionForce, point, settings.explosionRadius, 0.1f, ForceMode.Impulse);
            }
        }

        GameObject fx = ObjectPooler.SpawnFromPool("Explosion", point, Quaternion.identity);

        if (fx.TryGetComponent(out PooledEffect pooledFx))
        {
            pooledFx.SetPoolTag("Explosion");
        }
        */
        Deactivate();
    }

    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        StopAllCoroutines();
        if (tracer != null) tracer.SetActive(false);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Destroy(gameObject);//remove if using Object Pooling

        //ObjectPooler.ReturnToPool(settings.bulletPoolTag, gameObject);
    }

    private float GetBulletRadius()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            return sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            return capsule.radius * Mathf.Max(transform.localScale.x, transform.localScale.z); // x/z for horizontal capsules
        }

        // Default fallback
        return 0.05f;
    }

    public void SetShooter(GameObject shooter)
    {
        shooterGameObject = shooter;
        //shooterHealth = shooter.GetComponentInParent<HealthSystem>();
        baseDamage = 10f;
        /*
        if (shooter.TryGetComponent<WeaponProjectile>(out var weapon))
        {
            baseDamage = weapon.weaponProperties.damage;
        }
        else if (shooter.TryGetComponent<Enemy>(out var enemy))
        {
            baseDamage = enemy.enemyStats.baseStats.baseDamage;
        }
        */
        // skip self-collision only if bulletDrop is 0
        if (settings.bulletDrop == 0f)
        {
            Collider[] bulletColliders = GetComponentsInChildren<Collider>(true);

            // gather all colliders from entire hierarchy of the shooter, including inactive ones
            List<Collider> shooterColliders = new List<Collider>();

            // include all colliders from all child objects
            shooterColliders.AddRange(shooter.GetComponentsInChildren<Collider>(true));

            // include CharacterController collider if present
            CharacterController cc = shooter.GetComponent<CharacterController>();
            if (cc != null)
            {
                Collider controllerCol = cc.GetComponent<Collider>();
                if (controllerCol != null)
                    shooterColliders.Add(controllerCol);
            }

            foreach (var bulletCol in bulletColliders)
            {
                foreach (var shooterCol in shooterColliders)
                {
                    if (bulletCol != null && shooterCol != null)
                        Physics.IgnoreCollision(bulletCol, shooterCol, true);
                }
            }
        }
    }


    private void TryExplodeOrDeactivate(Vector3? explosionPoint = null)
    {
        if (settings.bulletType == BulletType.Explosive)
        {
            Explode(explosionPoint ?? transform.position);
        }
        else
        {
            Deactivate();
        }
    }
}

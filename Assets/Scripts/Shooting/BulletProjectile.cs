using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct ImpactPayload
{
    public float aoeRadius;
    public float aoeForce;
    public LayerMask aoeMask;
    public float falloffExponent;
    public bool explodeOnTimeout;
}

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] public GameObject tracer;
    [SerializeField] private BulletPropertiesSO settings;

    private int bounceRemaining;
    public bool isActive;
    private Rigidbody rb;
    private GameObject shooterGameObject;
    private float baseDamage;
    private ImpactPayload impactPayload;
    private bool hasPayload;           
    private bool HasAOE => hasPayload && impactPayload.aoeRadius > 0f;


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

    public void Initialize(
    Vector3 direction,
    float bulletSpeed,
    float upwardForce,
    float damage,
    ImpactPayload? payload = null,     // caller can pass null
    bool useGravity = false,
    Vector3? velocityOverride = null
    )
    {
        baseDamage = damage;
        hasPayload = payload.HasValue;            // set the flag
        impactPayload = payload ?? default;          // safe default (all zeros)
        rb.useGravity = useGravity;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (velocityOverride.HasValue)
            rb.linearVelocity = velocityOverride.Value;
        else
        {
            rb.AddForce(direction.normalized * bulletSpeed, ForceMode.Impulse);
            rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);
        }

        StartCoroutine(DestroySelf(settings.maxLifeTime));
    }

    public void Initialize(Vector3 dir, float speed, float up, float dmg)
     => Initialize(dir, speed, up, dmg, null, false, null);

    public void Initialize(Vector3 dir, float speed, float up, float dmg, ImpactPayload payload)
        => Initialize(dir, speed, up, dmg, payload, false, null);

    public void Initialize(Vector3 dir, float speed, float up, float dmg, bool useGravity)
        => Initialize(dir, speed, up, dmg, null, useGravity, null);

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
        {
            // Grenade-style behavior if requested by the turret
            if (impactPayload.explodeOnTimeout && HasAOE)
                Explode(transform.position);
            else
                Deactivate();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;
        if (collision.gameObject.CompareTag("Enemy"))
        {
            if (HasAOE)
            {
                Explode(hitPoint);
            }
            else
            {
                if (collision.gameObject.TryGetComponent(out Enemy enemy))
                {
                    enemy.TakeDamage(baseDamage);
                }
                Deactivate();
            }
            return;
        }

        if (bounceRemaining > 0)
        {
            //might gonna change the bounce to automaticly bounce to the next enemy instead of bounce freely from physics
            HandleBounce(hitNormal);
        }
        else
        {
            //if this shot is AOE capable, explode on impact or else just deactivate
            if (HasAOE)
                Explode(hitPoint);
            else
                Deactivate();
        }
    }

    private void HandleBounce(Vector3 hitNormal)
    {
        if (bounceRemaining <= 0)
        {
            if (HasAOE) 
                Explode(transform.position);
            else 
                Deactivate();
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

    private void Explode(Vector3 center)
    {
        float radius = Mathf.Max(0f, impactPayload.aoeRadius);

        // Safety check
        if (radius <= 0f)
        {
            Deactivate();
            return;
        }

        // Collect targets
        Collider[] hits = Physics.OverlapSphere(center, radius, impactPayload.aoeMask);

        float falloffExponent = Mathf.Max(0.01f, impactPayload.falloffExponent);

        foreach (var c in hits)
        {
            // Damage
            if (c.TryGetComponent(out Enemy enemy))
            {
                float distance = Vector3.Distance(c.transform.position, center);
                float t = Mathf.Clamp01(distance / radius);           // normalized distance (0 at center, 1 at edge)
                float falloff = 1f - Mathf.Pow(t, falloffExponent); //full damage at center fade to 0 at edge

                float damage = baseDamage * falloff; // baseDamage should come from the turret
                enemy.TakeDamage(damage);
            }

            // Physics push (optional)
            if (impactPayload.aoeForce > 0f && c.attachedRigidbody != null)
            {
                c.attachedRigidbody.AddExplosionForce(impactPayload.aoeForce, center, radius, 0.1f, ForceMode.Impulse);
            }
        }
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
        //tbh this func doesn't do anything right now because the bullet only interact with Enemy tagged and the enemy layer
    }

}

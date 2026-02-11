using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ground zone effect that damages enemies inside over time.
/// Used by Arc weapon projectiles on impact.
/// </summary>
public class GroundZone : MonoBehaviour
{
    [Header("Zone Settings")]
    private float radius = 3f;
    private float duration = 4f;
    private float tickInterval = 0.5f;
    private float damagePerTick = 5f;
    private BulletPropertiesSO bulletSO;
    private LayerMask enemyMask;

    private float lifeTimer;
    private float tickTimer;

    /// <summary>
    /// Initialize the ground zone with settings from BulletPropertiesSO
    /// </summary>
    public void Initialize(BulletPropertiesSO settings, LayerMask mask)
    {
        bulletSO = settings;
        enemyMask = mask;

        if (settings != null)
        {
            radius = settings.arcZoneRadius;
            duration = settings.arcZoneDuration;
            tickInterval = settings.arcZoneTickInterval;
            damagePerTick = settings.arcZoneDamagePerTick;
        }

        lifeTimer = duration;
        tickTimer = 0f; // Apply immediately on spawn

        // Scale visual to match radius
        transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            ApplyTickDamage();
        }

        // Fade out visual over time (optional)
        float alpha = Mathf.Clamp01(lifeTimer / duration);
        // You could apply alpha to material here if desired
    }

    private void ApplyTickDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyMask);
        
        List<Enemy> enemies = new List<Enemy>();
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Enemy enemy) && enemy.IsAlive)
            {
                enemies.Add(enemy);
            }
        }

        if (enemies.Count > 0 && bulletSO != null)
        {
            BulletEffectApplicator.ApplyAOEEffect(enemies, bulletSO, damagePerTick, transform.position);
        }

        // Debug visual
        Debug.DrawLine(transform.position, transform.position + Vector3.up * radius, GetZoneColor(), tickInterval);
    }

    private Color GetZoneColor()
    {
        if (bulletSO == null) return Color.white;
        
        return bulletSO.bulletType switch
        {
            BulletType.Fire => new Color(1f, 0.3f, 0f, 0.5f),
            BulletType.Ice => new Color(0.3f, 0.7f, 1f, 0.5f),
            BulletType.Electric => new Color(1f, 1f, 0f, 0.5f),
            BulletType.Utility => new Color(0.8f, 0.3f, 0.8f, 0.5f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
        };
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

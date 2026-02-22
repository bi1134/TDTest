using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Helper class to apply bullet effects to enemies based on BulletType.
/// GDD Rules:
/// - Buff bullets: no damage, no elemental effects
/// - Utility debuffs: no damage, utility only
/// - Elemental bullets: damage + effect
/// </summary>
public static class BulletEffectApplicator
{
    /// <summary>
    /// Apply bullet effects to an enemy based on bullet type.
    /// For single-target hits (projectile collision, beam)
    /// </summary>
    public static void ApplyEffect(Enemy enemy, BulletPropertiesSO bullet, float baseDamage, Vector3 attackDirection = default)
    {
        if (enemy == null || bullet == null) return;
        
        // Apply augment modifiers to damage
        float augmentMultiplier = UpgradesManager.GetStatMultiplier(AugmentType.Damage);
        float augmentFlatBonus = UpgradesManager.GetStatFlatBonus(AugmentType.Damage);
        float modifiedDamage = (baseDamage * augmentMultiplier) + augmentFlatBonus;
        
        // Apply barrier reduction if applicable
        float damage = modifiedDamage;
        if (attackDirection != default)
        {
            damage = enemy.ApplyBarrierReduction(damage, attackDirection);
        }

        switch (bullet.bulletType)
        {
            case BulletType.Normal:
                enemy.TakeDamage(damage, false, attackDirection != default ? enemy.transform.position - attackDirection : default);
                break;

            case BulletType.Explosive:
                // Explosive is handled separately with AoE
                enemy.TakeDamage(damage, false, attackDirection != default ? enemy.transform.position - attackDirection : default);
                break;

            case BulletType.Electric:
                // Damage + Stun + Chain (single target only)
                enemy.TakeDamage(damage, false, attackDirection != default ? enemy.transform.position - attackDirection : default);
                enemy.ApplyStun(bullet.electricStunDuration);
                // Chain from this single target
                ChainToNearbyEnemies(enemy, bullet, damage, new HashSet<Enemy> { enemy });
                break;

            case BulletType.Fire:
                // Damage + DOT
                enemy.TakeDamage(damage, false, attackDirection != default ? enemy.transform.position - attackDirection : default);
                enemy.ApplyFireDOT(bullet.fireDOTDamagePerSecond, bullet.fireDOTDuration);
                break;

            case BulletType.Ice:
                // Damage + Slow
                enemy.TakeDamage(damage, false, attackDirection != default ? enemy.transform.position - attackDirection : default);
                enemy.ApplySlow(bullet.iceSlowPercent, bullet.iceSlowDuration);
                break;

            case BulletType.Buff:
                // GDD: Buff bullets never deal damage, never apply elemental effects
                Debug.Log("Buff bullet hit enemy - no effect (buffs only affect turrets)");
                break;

            case BulletType.Utility:
                // GDD: Utility only, no damage
                ApplyUtilityDebuff(enemy, bullet);
                break;
        }
    }

    /// <summary>
    /// Apply bullet effects to multiple enemies in an AOE.
    /// Electric: damages all, then chains ONCE from the first enemy to enemies OUTSIDE the AOE
    /// Fire: damages all + applies DOT to all
    /// Ice: damages all + slows all
    /// </summary>
    public static void ApplyAOEEffect(List<Enemy> enemies, BulletPropertiesSO bullet, float baseDamage, Vector3 center)
    {
        if (enemies == null || enemies.Count == 0 || bullet == null) return;

        // Track all enemies hit by AOE (they should NOT be chained to)
        HashSet<Enemy> aoeHitEnemies = new HashSet<Enemy>(enemies);

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;

            // Calculate falloff
            float dist = Vector3.Distance(center, enemy.transform.position);
            float falloff = 1f; // No falloff for this simplified version
            float damage = baseDamage * falloff;
            
            Vector3 attackDir = (enemy.transform.position - center).normalized;
            damage = enemy.ApplyBarrierReduction(damage, attackDir);

            switch (bullet.bulletType)
            {
                case BulletType.Normal:
                    enemy.TakeDamage(damage, false, center);
                    break;

                case BulletType.Explosive:
                    enemy.TakeDamage(damage, false, center);
                    break;

                case BulletType.Electric:
                    // Damage + Stun to all in AOE
                    enemy.TakeDamage(damage, false, center);
                    enemy.ApplyStun(bullet.electricStunDuration);
                    // NO chaining within AOE - all are already hit
                    break;

                case BulletType.Fire:
                    enemy.TakeDamage(damage, false, center);
                    enemy.ApplyFireDOT(bullet.fireDOTDamagePerSecond, bullet.fireDOTDuration);
                    break;

                case BulletType.Ice:
                    enemy.TakeDamage(damage, false, center);
                    enemy.ApplySlow(bullet.iceSlowPercent, bullet.iceSlowDuration);
                    break;

                case BulletType.Utility:
                    ApplyUtilityDebuff(enemy, bullet);
                    break;

                case BulletType.Buff:
                    // No effect on enemies
                    break;
            }
        }

        // Electric: Chain ONCE from the first enemy to enemies OUTSIDE the AOE
        if (bullet.bulletType == BulletType.Electric && enemies.Count > 0 && bullet.electricChainCount > 0)
        {
            Enemy chainSource = enemies[0];
            if (chainSource != null && chainSource.IsAlive)
            {
                ChainToNearbyEnemies(chainSource, bullet, baseDamage, aoeHitEnemies);
            }
        }
    }

    /// <summary>
    /// Apply utility debuff based on UtilityDebuffType
    /// </summary>
    private static void ApplyUtilityDebuff(Enemy enemy, BulletPropertiesSO bullet)
    {
        switch (bullet.utilityDebuffType)
        {
            case UtilityDebuffType.Slow:
                enemy.ApplySlow(bullet.utilitySlowPercent, bullet.utilitySlowDuration);
                break;

            case UtilityDebuffType.Vulnerability:
                enemy.ApplyVulnerability(bullet.vulnerabilityPercent, bullet.vulnerabilityDuration);
                break;

            case UtilityDebuffType.ShieldShred:
                enemy.ApplyShieldShred(bullet.shieldShredPercent);
                break;

            case UtilityDebuffType.None:
            default:
                Debug.LogWarning("Utility bullet with no debuff type set!");
                break;
        }
    }

    /// <summary>
    /// Electric chain effect - chains to nearby enemies NOT in the exclude set
    /// </summary>
    private static void ChainToNearbyEnemies(Enemy source, BulletPropertiesSO bullet, float initialDamage, HashSet<Enemy> alreadyHit)
    {
        if (bullet.electricChainCount <= 0) return;

        Enemy currentSource = source;
        float currentDamage = initialDamage * bullet.electricChainDamageMultiplier;

        for (int i = 0; i < bullet.electricChainCount; i++)
        {
            // Find nearest enemy within chain range that hasn't been hit
            Enemy nearest = FindNearestEnemy(currentSource.transform.position, bullet.electricChainRange, alreadyHit);
            
            if (nearest == null) break;

            // Apply chained damage and stun (originating from currentSource)
            nearest.TakeDamage(currentDamage, false, currentSource.transform.position);
            nearest.ApplyStun(bullet.electricStunDuration * 0.5f); // Reduced stun on chain
            
            // Visual debug
            Debug.DrawLine(currentSource.transform.position, nearest.transform.position, Color.yellow, 0.5f);

            // Add to already hit list so it won't be hit again
            alreadyHit.Add(nearest);
            currentSource = nearest;
            currentDamage *= bullet.electricChainDamageMultiplier;
        }
    }

    /// <summary>
    /// Find nearest enemy within range, excluding already hit enemies
    /// </summary>
    private static Enemy FindNearestEnemy(Vector3 position, float range, HashSet<Enemy> exclude)
    {
        Enemy nearest = null;
        float nearestDist = range;

        Enemy[] allEnemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        
        foreach (var enemy in allEnemies)
        {
            if (exclude.Contains(enemy)) continue;
            if (!enemy.IsAlive) continue;

            float dist = Vector3.Distance(position, enemy.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }
}


using System;
using UnityEngine;

public static class SoundEvents
{
    // --- UI Actions ---
    public static event EventHandler OnAnyButtonClicked;
    public static event EventHandler OnAnyButtonHovered;
    public static event EventHandler OnCardAppeared;
    public static event EventHandler OnCardClicked;
    public static event EventHandler OnCardHovered;
    public static event EventHandler OnCoinCollected;

    // --- Game Results ---
    public static event EventHandler OnGameWon;
    public static event EventHandler OnGameLost;

    // --- Enemy Actions ---
    // Payload for hit sound
    public class EnemyHitEventArgs : EventArgs
    {
        public bool hitShield;
    }
    public static event EventHandler OnEnemyWalk;
    public static event EventHandler OnEnemyGrunt;
    public static event EventHandler<EnemyHitEventArgs> OnEnemyHit;
    public static event EventHandler OnEnemyDeath;

    // --- Turret Actions ---
    public class TurretShootEventArgs : EventArgs
    {
        public WeaponName weaponName;
        public FireMode fireMode;
    }
    public static event EventHandler<TurretShootEventArgs> OnTurretShoot;
    public static event EventHandler OnTurretBuilt;
    public static event EventHandler OnTurretSold;

    // --- Bullet Actions ---
    public class BulletImpactEventArgs : EventArgs
    {
        public BulletType bulletType;
        public bool hitEnemy; 
    }
    public static event EventHandler<BulletImpactEventArgs> OnBulletImpact;
    public static event EventHandler OnAOEExplosion;

    // --- Trigger Methods (To make calling them cleaner) ---

    // UI
    public static void TriggerButtonClicked(object sender) => OnAnyButtonClicked?.Invoke(sender, EventArgs.Empty);
    public static void TriggerButtonHovered(object sender) => OnAnyButtonHovered?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardAppeared(object sender) => OnCardAppeared?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardClicked(object sender) => OnCardClicked?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardHovered(object sender) => OnCardHovered?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCoinCollected(object sender) => OnCoinCollected?.Invoke(sender, EventArgs.Empty);

    // Game
    public static void TriggerGameWon(object sender) => OnGameWon?.Invoke(sender, EventArgs.Empty);
    public static void TriggerGameLost(object sender) => OnGameLost?.Invoke(sender, EventArgs.Empty);

    // Enemy
    public static void TriggerEnemyWalk(object sender) => OnEnemyWalk?.Invoke(sender, EventArgs.Empty);
    public static void TriggerEnemyGrunt(object sender) => OnEnemyGrunt?.Invoke(sender, EventArgs.Empty);
    public static void TriggerEnemyHit(object sender, bool hitShield) 
        => OnEnemyHit?.Invoke(sender, new EnemyHitEventArgs { hitShield = hitShield });
    public static void TriggerEnemyDeath(object sender) => OnEnemyDeath?.Invoke(sender, EventArgs.Empty);

    // Turret
    public static void TriggerTurretShoot(object sender, WeaponName weapon, FireMode mode) 
        => OnTurretShoot?.Invoke(sender, new TurretShootEventArgs { weaponName = weapon, fireMode = mode });
    public static void TriggerTurretBuilt(object sender) => OnTurretBuilt?.Invoke(sender, EventArgs.Empty);
    public static void TriggerTurretSold(object sender) => OnTurretSold?.Invoke(sender, EventArgs.Empty);

    // Bullet
    public static void TriggerBulletImpact(object sender, BulletType type, bool hitEnemy) 
        => OnBulletImpact?.Invoke(sender, new BulletImpactEventArgs { bulletType = type, hitEnemy = hitEnemy });
    public static void TriggerAOEExplosion(object sender) => OnAOEExplosion?.Invoke(sender, EventArgs.Empty);
}
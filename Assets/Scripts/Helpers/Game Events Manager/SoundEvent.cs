using System;
using UnityEngine;

public static class SoundEvents
{
    // --- UI Actions ---
    public static event EventHandler OnAnyButtonClicked;
    public static event EventHandler OnAnyButtonHovered;
    public static event EventHandler OnCancelButtonClicked;
    public static event EventHandler OnCardAppeared;
    public static event EventHandler OnCardClicked;
    public static event EventHandler OnCardHovered;
    public static event EventHandler OnCoinCollected;

    // --- Game Results ---
    public static event EventHandler OnGameWon;
    public static event EventHandler OnGameLost;

    // --- World Event Args ---
    public class WorldPositionEventArgs : EventArgs
    {
        public Vector3 position;
    }

    public class EnemyAudioEventArgs : WorldPositionEventArgs
    {
        public AudioClip[] overrideClips;
    }

    // --- Enemy Actions ---
    // Payload for hit sound
    public class EnemyHitEventArgs : WorldPositionEventArgs
    {
        public bool hitShield;
    }
    public static event EventHandler<WorldPositionEventArgs> OnEnemyWalk;
    public static event EventHandler<WorldPositionEventArgs> OnEnemySprint;
    public static event EventHandler<EnemyAudioEventArgs> OnEnemyGrunt;
    public static event EventHandler<EnemyHitEventArgs> OnEnemyHit;
    public static event EventHandler<EnemyAudioEventArgs> OnEnemyDeath;
    public static event EventHandler<EnemyAudioEventArgs> OnEnemyPoof;

    // --- Turret Actions ---
    public class TurretShootEventArgs : WorldPositionEventArgs
    {
        public WeaponName weaponName;
        public FireMode fireMode;
    }
    public static event EventHandler<TurretShootEventArgs> OnTurretShoot;
    public static event EventHandler<WorldPositionEventArgs> OnTurretBuilt;
    public static event EventHandler<WorldPositionEventArgs> OnTurretSold;

    // --- Bullet Actions ---
    public class BulletImpactEventArgs : WorldPositionEventArgs
    {
        public BulletType bulletType;
        public bool hitEnemy; 
    }
    public static event EventHandler<BulletImpactEventArgs> OnBulletImpact;
    public static event EventHandler<WorldPositionEventArgs> OnAOEExplosion;

    // --- Elemental Strike Sounds ---
    public class ElementalStrikeEventArgs : WorldPositionEventArgs { public BulletType element; }
    public static event EventHandler<ElementalStrikeEventArgs> OnElementalStrike;

    // --- Shield Break ---
    public static event EventHandler<WorldPositionEventArgs> OnShieldBreak;

    // --- Barrier Break ---
    public static event EventHandler<WorldPositionEventArgs> OnBarrierBreak;

    // --- Chunk Expand ---
    public static event EventHandler<WorldPositionEventArgs> OnChunkExpand;

    // --- Cached Event Args (Zero Allocation) ---
    private static readonly WorldPositionEventArgs cachedWorldPosArgs = new WorldPositionEventArgs();
    private static readonly EnemyAudioEventArgs cachedEnemyAudioArgs = new EnemyAudioEventArgs();
    private static readonly EnemyHitEventArgs cachedEnemyHitArgs = new EnemyHitEventArgs();
    private static readonly TurretShootEventArgs cachedTurretShootArgs = new TurretShootEventArgs();
    private static readonly BulletImpactEventArgs cachedBulletImpactArgs = new BulletImpactEventArgs();
    private static readonly ElementalStrikeEventArgs cachedElementalArgs = new ElementalStrikeEventArgs();

    // --- Trigger Methods (To make calling them cleaner) ---

    // UI
    public static void TriggerButtonClicked(object sender) => OnAnyButtonClicked?.Invoke(sender, EventArgs.Empty);
    public static void TriggerButtonHovered(object sender) => OnAnyButtonHovered?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCancelButtonClicked(object sender) => OnCancelButtonClicked?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardAppeared(object sender) => OnCardAppeared?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardClicked(object sender) => OnCardClicked?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCardHovered(object sender) => OnCardHovered?.Invoke(sender, EventArgs.Empty);
    public static void TriggerCoinCollected(object sender) => OnCoinCollected?.Invoke(sender, EventArgs.Empty);

    // Game
    public static void TriggerGameLost(object sender) => OnGameLost?.Invoke(sender, EventArgs.Empty);

    // Enemy
    public static void TriggerEnemyWalk(object sender, Vector3 pos) 
    {
        cachedWorldPosArgs.position = pos;
        OnEnemyWalk?.Invoke(sender, cachedWorldPosArgs);
    }
    public static void TriggerEnemySprint(object sender, Vector3 pos) 
    {
        cachedWorldPosArgs.position = pos;
        OnEnemySprint?.Invoke(sender, cachedWorldPosArgs);
    }
    public static void TriggerEnemyGrunt(object sender, Vector3 pos, AudioClip[] overrides = null) 
    {
        cachedEnemyAudioArgs.position = pos;
        cachedEnemyAudioArgs.overrideClips = overrides;
        OnEnemyGrunt?.Invoke(sender, cachedEnemyAudioArgs);
    }
    public static void TriggerEnemyHit(object sender, Vector3 pos, bool shield) 
    {
        cachedEnemyHitArgs.position = pos;
        cachedEnemyHitArgs.hitShield = shield;
        OnEnemyHit?.Invoke(sender, cachedEnemyHitArgs);
    }
    public static void TriggerEnemyDeath(object sender, Vector3 pos, AudioClip[] overrides = null) 
    {
        cachedEnemyAudioArgs.position = pos;
        cachedEnemyAudioArgs.overrideClips = overrides;
        OnEnemyDeath?.Invoke(sender, cachedEnemyAudioArgs);
    }
    public static void TriggerEnemyPoof(object sender, Vector3 pos, AudioClip[] overrides = null) 
    {
        cachedEnemyAudioArgs.position = pos;
        cachedEnemyAudioArgs.overrideClips = overrides;
        OnEnemyPoof?.Invoke(sender, cachedEnemyAudioArgs);
    }

    // Turret
    public static void TriggerTurretShoot(object sender, Vector3 pos, WeaponName weapon, FireMode mode) 
    {
        cachedTurretShootArgs.position = pos;
        cachedTurretShootArgs.weaponName = weapon;
        cachedTurretShootArgs.fireMode = mode;
        OnTurretShoot?.Invoke(sender, cachedTurretShootArgs);
    }
    public static void TriggerTurretBuilt(object sender, Vector3 pos) 
    {
        cachedWorldPosArgs.position = pos;
        OnTurretBuilt?.Invoke(sender, cachedWorldPosArgs);
    }
    public static void TriggerTurretSold(object sender, Vector3 pos) 
    {
        cachedWorldPosArgs.position = pos;
        OnTurretSold?.Invoke(sender, cachedWorldPosArgs);
    }

    // Bullet
    public static void TriggerBulletImpact(object sender, Vector3 pos, BulletType type, bool enemy) 
    {
        cachedBulletImpactArgs.position = pos;
        cachedBulletImpactArgs.bulletType = type;
        cachedBulletImpactArgs.hitEnemy = enemy;
        OnBulletImpact?.Invoke(sender, cachedBulletImpactArgs);
    }
    public static void TriggerAOEExplosion(object sender, Vector3 pos) 
    {
        cachedWorldPosArgs.position = pos;
        OnAOEExplosion?.Invoke(sender, cachedWorldPosArgs);
    }

    // Elemental
    public static void TriggerElementalStrike(object sender, BulletType element, Vector3 pos)
    {
        cachedElementalArgs.position = pos;
        cachedElementalArgs.element = element;
        OnElementalStrike?.Invoke(sender, cachedElementalArgs);
    }

    // Shield Break
    public static void TriggerShieldBreak(object sender, Vector3 pos)
    {
        cachedWorldPosArgs.position = pos;
        OnShieldBreak?.Invoke(sender, cachedWorldPosArgs);
    }

    // Barrier Break
    public static void TriggerBarrierBreak(object sender, Vector3 pos)
    {
        cachedWorldPosArgs.position = pos;
        OnBarrierBreak?.Invoke(sender, cachedWorldPosArgs);
    }

    // Chunk Expand
    public static void TriggerChunkExpand(object sender, Vector3 pos)
    {
        cachedWorldPosArgs.position = pos;
        OnChunkExpand?.Invoke(sender, cachedWorldPosArgs);
    }
}
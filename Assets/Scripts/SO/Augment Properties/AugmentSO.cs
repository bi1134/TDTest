using UnityEngine;

/// <summary>
/// Augment ScriptableObject - Defines an augment/upgrade card.
/// Create instances via: Assets > Create > Tower Defense > Augment
/// </summary>
[CreateAssetMenu(fileName = "New Augment", menuName = "Tower Defense/Augment")]
public class AugmentSO : ScriptableObject
{
    [Header("Display")]
    public string augmentName = "New Augment";
    [TextArea(3, 5)]
    public string description = "Augment description";
    public Sprite icon;

    [Header("Unique Mechanic Setup")]
    public AugmentRarity rarity = AugmentRarity.Common;
    
    [Tooltip("Can this augment appear multiple times in a run?")]
    public bool isRepeatable = false;

    [Tooltip("For special augments with unique behavior (can be used by sub-classes if needed)")]
    public string specialEffect = "";

    [Header("Item Unlocks")]
    [Tooltip("If true, this Augment acts as a Blueprint Unlock rather than a pure stat boost.")]
    public bool unlocksItem = false;
    
    [Tooltip("The Turret Blueprint to unlock. Leave empty if unlocking a bullet.")]
    public TurretBlueprintSO unlockedTurret;
    
    [Tooltip("The Bullet Blueprint to unlock. Leave empty if unlocking a turret.")]
    public BulletBlueprintSO unlockedBullet;
    
    [Tooltip("If true, the unlocked item is immediately populated into the active Shop menu.")]
    public bool addToShop = true;

    /// <summary>
    /// Base method for handling when a turret with this augment hits an enemy.
    /// Sub-classes can override this to apply burns, slows, chain lightning, etc.
    /// </summary>
    public virtual void OnHit(Enemy target, float damage) 
    { 
        // Default does nothing.
    }
}

public enum AugmentType
{
    Damage,          // Increase turret damage
    FireRate,        // Increase fire rate
    Range,           // Increase turret range
    Money,           // Increase money earned
    ExplosiveRadius, // Increase explosion radius for explosive bullets
    Special,         // Custom behavior (use specialEffect field)
    UnlockItem       // Unlocks a new shop item (Turret or Bullet)
}

public enum AugmentRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

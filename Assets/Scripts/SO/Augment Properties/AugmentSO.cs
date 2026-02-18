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

    [Header("Augment Type")]
    public AugmentType type;
    public AugmentRarity rarity = AugmentRarity.Common;
    
    [Tooltip("Can this augment appear multiple times in a run? (e.g., stackable damage buffs)")]
    public bool isRepeatable = false;

    [Header("Effect Values")]
    [Tooltip("Percentage increase (e.g., 20 = +20%)")]
    public float percentageBonus = 0f;
    
    [Tooltip("Flat value increase (e.g., 5 = +5 damage)")]
    public float flatBonus = 0f;

    [Tooltip("For special augments with unique behavior")]
    public string specialEffect = "";
}

public enum AugmentType
{
    Damage,          // Increase turret damage
    FireRate,        // Increase fire rate
    Range,           // Increase turret range
    Money,           // Increase money earned
    ExplosiveRadius, // Increase explosion radius for explosive bullets
    Special          // Custom behavior (use specialEffect field)
}

public enum AugmentRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

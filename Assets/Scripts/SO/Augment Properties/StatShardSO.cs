using UnityEngine;

/// <summary>
/// Defines a Stat Shard that provides deterministic, randomized stat increases
/// based on its rarity bracket.
/// </summary>
[CreateAssetMenu(fileName = "New Stat Shard", menuName = "Tower Defense/Stat Shard")]
public class StatShardSO : ScriptableObject
{
    [Header("Display")]
    [Tooltip("Base name of the shard, e.g., 'Fire Rate Shard'")]
    public string shardName;
    public Sprite icon;

    [Header("Config")]
    public AugmentType statType;
    
    [Tooltip("If true, the rolled value is treated as a percentage (e.g., +21%). If false, it's a flat value.")]
    public bool isPercentage = true;

    /// <summary>
    /// Returns the min and max roll bounds for a given rarity.
    /// Values correspond to the percentages requested (e.g., 1 to 11 for Common).
    /// </summary>
    public (float min, float max) GetBounds(AugmentRarity rarity)
    {
        return rarity switch
        {
            AugmentRarity.Common => (1f, 11f),
            AugmentRarity.Uncommon => (6f, 16f),
            AugmentRarity.Rare => (11f, 22f),
            AugmentRarity.Epic => (21f, 42f),
            AugmentRarity.Legendary => (38f, 80f),
            _ => (1f, 11f)
        };
    }
}

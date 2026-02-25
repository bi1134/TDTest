using System.Collections.Generic;
using UnityEngine;

using TerrainGenerator;

/// <summary>
/// Augment Manager - Manages active augments and provides stat modifiers.
/// Tracks augments selected during the current run.
/// Supports deterministic generation via WFCWorldManager seed.
/// </summary>
public class UpgradesManager : MonoBehaviour
{
    public static UpgradesManager Instance { get; private set; }

    [Header("Pools")]
    [Tooltip("All available augments that can appear")]
    public List<AugmentSO> augmentPool = new List<AugmentSO>();
    
    [Tooltip("All available stat shards that can appear")]
    public List<StatShardSO> statShardPool = new List<StatShardSO>();

    [Header("Wave Trigger")]
    [Tooltip("Wave number to show augments (e.g., 7, 14, 21)")]
    public List<int> augmentWaves = new List<int> { 7, 14, 21 };

    [Header("Dependencies")]
    public WFCWorldManager worldManager;

    [Header("Debug")]
    public bool logAugments = true;

    // Static data - resets on new game
    private static List<AugmentSO> activeAugments = new List<AugmentSO>();
    private static List<ActiveStatShard> activeStatShards = new List<ActiveStatShard>();
    
    // Deterministic RNG
    private System.Random rng;

    [System.Serializable]
    public class ActiveStatShard
    {
        public StatShardSO shardDef;
        public AugmentRarity rarity;
        public float rolledValue;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
        InitializeRNG();
    }

    private void InitializeRNG()
    {
        int seed = System.Environment.TickCount;
        
        // Try to get seed from WorldManager for consistency
        if (worldManager == null)
        {
            worldManager = FindAnyObjectByType<WFCWorldManager>();
        }
        
        if (worldManager != null)
        {
            // Use world seed + constant to ensure it's deterministic but different stream
            // Or just use world seed.
            seed = worldManager.runSeed + 12345; 
            Debug.Log($"[UpgradesManager] Initialized with World Seed: {worldManager.runSeed}");
        }
        else
        {
             Debug.Log($"[UpgradesManager] Initialized with Random Seed: {seed}");
        }
        
        rng = new System.Random(seed);
    }

    private void OnDisable()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
    }

    private void HandleWaveCompleted(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        // Check if this wave should trigger augments
        if (augmentWaves.Contains(e.waveNumber))
        {
            if (logAugments)
            {
                Debug.Log($"[UpgradesManager] Wave {e.waveNumber} complete - triggering augment selection");
            }

            ShowAugmentSelection();
        }
    }

    private void ShowAugmentSelection()
    {
        // Get 3 random augments
        List<AugmentSO> options = GetRandomAugments(3);

        // Queue the logic instead of overriding current UI 
        if (SelectionQueueManager.Instance != null)
        {
            SelectionQueueManager.Instance.EnqueueAugmentSelection(options);
        }
        else
        {
            Debug.LogWarning("[UpgradesManager] SelectionQueueManager missing! Triggering Event directly.");
            GameEvents.TriggerAugmentSelectionStarted(this, options);
        }
    }

    /// <summary>
    /// Get random augments from the pool.
    /// Respects isRepeatable flag - non-repeatable augments won't appear in same selection.
    /// </summary>
    public List<AugmentSO> GetRandomAugments(int count)
    {
        List<AugmentSO> result = new List<AugmentSO>();
        
        if (augmentPool.Count == 0)
        {
            Debug.LogWarning("[UpgradesManager] Augment pool is empty!");
            return result;
        }

        // Create temporary pool
        List<AugmentSO> tempPool = new List<AugmentSO>(augmentPool);

        for (int i = 0; i < count && tempPool.Count > 0; i++)
        {
            // Use seeded RNG
            int randomIndex = rng.Next(0, tempPool.Count);
            AugmentSO selected = tempPool[randomIndex];
            result.Add(selected);

            // Remove from pool only if NOT repeatable
            if (!selected.isRepeatable)
            {
                tempPool.RemoveAt(randomIndex);
            }
        }

        return result;
    }

    /// <summary>
    /// Get a single random augment, excluding specific ones (for rerolls).
    /// </summary>
    public AugmentSO GetRandomAugment(List<AugmentSO> excludeList)
    {
        if (augmentPool.Count == 0) return null;

        // Create pool excluding the list
        List<AugmentSO> tempPool = new List<AugmentSO>(augmentPool);
        
        if (excludeList != null)
        {
            foreach (var excluded in excludeList)
            {
                // If augment is NOT repeatable, remove it from potential choices
                // If it IS repeatable, we might still want to exclude the *exact instance* currently shown?
                // For reroll, usually we don't want to see the same card we just rerolled.
                // So strictly remove everything in excludeList.
                tempPool.Remove(excluded);
            }
        }

        if (tempPool.Count == 0)
        {
            Debug.LogWarning("[UpgradesManager] No valid augments left after exclusion! returning random from full pool.");
            // Fallback: pick from full pool
            tempPool = new List<AugmentSO>(augmentPool);
        }

        int randomIndex = rng.Next(0, tempPool.Count);
        return tempPool[randomIndex];
    }

    /// <summary>
    /// Apply selected augment - called when player chooses a card.
    /// </summary>
    public void ApplyAugment(AugmentSO augment)
    {
        if (augment == null)
        {
            Debug.LogWarning("[UpgradesManager] Tried to apply null augment!");
            return;
        }

        activeAugments.Add(augment);

        // Permanent removal for unique items (like new turret blueprints)
        if (!augment.isRepeatable)
        {
            augmentPool.Remove(augment);
            Debug.Log($"[UpgradesManager] Removed non-repeatable augment from main pool: {augment.augmentName}");
        }

        if (logAugments)
        {
            Debug.Log($"[UpgradesManager] Applied augment: {augment.augmentName} (Total active: {activeAugments.Count})");
        }
        
        // Handle explicit item unlocks
        if (augment.unlocksItem && augment.addToShop)
        {
            if (augment.unlockedTurret != null)
            {
                GameEvents.TriggerTurretUnlocked(this, augment.unlockedTurret);
                Debug.Log($"[UpgradesManager] Unlocked new Turret: {augment.unlockedTurret.turretName}");
            }
            if (augment.unlockedBullet != null)
            {
                GameEvents.TriggerBulletUnlocked(this, augment.unlockedBullet);
                Debug.Log($"[UpgradesManager] Unlocked new Bullet: {augment.unlockedBullet.bulletName}");
            }
        }

        // Trigger event so systems can react
        GameEvents.TriggerAugmentSelected(this, augment);
    }

    /// <summary>
    /// Apply selected stat shard. Called by UI.
    /// </summary>
    public void ApplyStatShard(ActiveStatShard shard)
    {
        if (shard == null) return;

        activeStatShards.Add(shard);
        if (logAugments) Debug.Log($"[UpgradesManager] Applied Stat Shard: {shard.shardDef.shardName} (+{shard.rolledValue})");

        // Trigger event
        GameEvents.TriggerStatShardSelected(this, shard);
    }

    /// <summary>
    /// Get total multiplier for a specific stat type.
    /// Example: GetStatMultiplier(AugmentType.Damage) returns 1.4 if player has +40% damage.
    /// </summary>
    public static float GetStatMultiplier(AugmentType type)
    {
        float multiplier = 1f;

        foreach (var shard in activeStatShards)
        {
            if (shard.shardDef.statType == type && shard.shardDef.isPercentage)
            {
                multiplier += shard.rolledValue / 100f;
            }
        }

        return multiplier;
    }

    /// <summary>
    /// Get total flat bonus for a specific stat type.
    /// </summary>
    public static float GetStatFlatBonus(AugmentType type)
    {
        float bonus = 0f;

        foreach (var shard in activeStatShards)
        {
            if (shard.shardDef.statType == type && !shard.shardDef.isPercentage)
            {
                bonus += shard.rolledValue;
            }
        }

        return bonus;
    }

    private AugmentRarity GetRandomRarity()
    {
        float roll = (float)rng.NextDouble() * 100f;
        if (roll <= 1f) return AugmentRarity.Legendary; // 1%
        if (roll <= 5f) return AugmentRarity.Epic;      // 4%
        if (roll <= 20f) return AugmentRarity.Rare;     // 15%
        if (roll <= 50f) return AugmentRarity.Uncommon; // 30%
        return AugmentRarity.Common;                    // 50%
    }

    /// <summary>
    /// Generates a randomized Stat Shard instance based on its rarity bracket using the seeded RNG.
    /// </summary>
    public ActiveStatShard GenerateRandomShard(StatShardSO so)
    {
        AugmentRarity rolledRarity = GetRandomRarity();
        var bounds = so.GetBounds(rolledRarity);
        
        // Use System.Random to get a double between 0.0 and 1.0
        float roll = (float)rng.NextDouble();
        
        // Lerp between min and max
        float rawValue = Mathf.Lerp(bounds.min, bounds.max, roll);
        
        // Round to whole number
        float finalValue = Mathf.Round(rawValue);

        return new ActiveStatShard
        {
            shardDef = so,
            rarity = rolledRarity,
            rolledValue = finalValue
        };
    }

    /// <summary>
    /// Generates multiple choices for a single Stat Shard drop.
    /// </summary>
    public List<ActiveStatShard> GetRandomStatShardChoices(int count)
    {
        List<ActiveStatShard> result = new List<ActiveStatShard>();
        if (statShardPool.Count == 0) return result;

        List<StatShardSO> tempPool = new List<StatShardSO>(statShardPool);
        
        for (int i = 0; i < count; i++)
        {
            if (tempPool.Count == 0)
            {
                // If we run out of unique stats, refill the pool to allow repeats of the same stat
                tempPool = new List<StatShardSO>(statShardPool);
            }

            int randomIndex = rng.Next(0, tempPool.Count);
            StatShardSO selectedSO = tempPool[randomIndex];
            
            result.Add(GenerateRandomShard(selectedSO));
            tempPool.RemoveAt(randomIndex); // Prevent showing the same stat multiple times in one panel if possible
        }

        return result;
    }

    /// <summary>
    /// Check if player has any active augments.
    /// </summary>
    public static bool HasAugments() => activeAugments.Count > 0 || activeStatShards.Count > 0;

    /// <summary>
    /// Get all active augments (for UI display, debugging).
    /// </summary>
    public static List<AugmentSO> GetActiveAugments() => new List<AugmentSO>(activeAugments);

    public static List<ActiveStatShard> GetActiveStatShards() => new List<ActiveStatShard>(activeStatShards);

    /// <summary>
    /// Reset all static data - called by GameStateResetter on new game.
    /// </summary>
    public static void ResetStaticData()
    {
        activeAugments.Clear();
        activeStatShards.Clear();
        Debug.Log("[UpgradesManager] Static data reset - all augments and shards cleared");
    }
}


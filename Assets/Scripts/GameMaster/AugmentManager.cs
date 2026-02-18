using System.Collections.Generic;
using UnityEngine;

using TerrainGenerator;

/// <summary>
/// Augment Manager - Manages active augments and provides stat modifiers.
/// Tracks augments selected during the current run.
/// Supports deterministic generation via WFCWorldManager seed.
/// </summary>
public class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance { get; private set; }

    [Header("Augment Pool")]
    [Tooltip("All available augments that can appear")]
    public List<AugmentSO> augmentPool = new List<AugmentSO>();

    [Header("Wave Trigger")]
    [Tooltip("Wave number to show augments (e.g., 7, 14, 21)")]
    public List<int> augmentWaves = new List<int> { 7, 14, 21 };

    [Header("Dependencies")]
    public WFCWorldManager worldManager;

    [Header("Debug")]
    public bool logAugments = true;

    // Static data - resets on new game
    private static List<AugmentSO> activeAugments = new List<AugmentSO>();
    
    // Deterministic RNG
    private System.Random rng;

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
            Debug.Log($"[AugmentManager] Initialized with World Seed: {worldManager.runSeed}");
        }
        else
        {
             Debug.Log($"[AugmentManager] Initialized with Random Seed: {seed}");
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
                Debug.Log($"[AugmentManager] Wave {e.waveNumber} complete - triggering augment selection");
            }

            ShowAugmentSelection();
        }
    }

    private void ShowAugmentSelection()
    {
        // Get 3 random augments
        List<AugmentSO> options = GetRandomAugments(3);

        // Trigger event for UI to show cards
        GameEvents.TriggerAugmentSelectionStarted(this, options);
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
            Debug.LogWarning("[AugmentManager] Augment pool is empty!");
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
            Debug.LogWarning("[AugmentManager] No valid augments left after exclusion! returning random from full pool.");
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
            Debug.LogWarning("[AugmentManager] Tried to apply null augment!");
            return;
        }

        activeAugments.Add(augment);

        if (logAugments)
        {
            Debug.Log($"[AugmentManager] Applied augment: {augment.augmentName} (Total active: {activeAugments.Count})");
        }

        // Trigger event so systems can react
        GameEvents.TriggerAugmentSelected(this, augment);
    }

    /// <summary>
    /// Get total multiplier for a specific stat type.
    /// Example: GetStatMultiplier(AugmentType.Damage) returns 1.4 if player has +40% damage.
    /// </summary>
    public static float GetStatMultiplier(AugmentType type)
    {
        float multiplier = 1f;

        foreach (var augment in activeAugments)
        {
            if (augment.type == type)
            {
                multiplier += augment.percentageBonus / 100f;
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

        foreach (var augment in activeAugments)
        {
            if (augment.type == type)
            {
                bonus += augment.flatBonus;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Check if player has any active augments.
    /// </summary>
    public static bool HasAugments() => activeAugments.Count > 0;

    /// <summary>
    /// Get all active augments (for UI display, debugging).
    /// </summary>
    public static List<AugmentSO> GetActiveAugments() => new List<AugmentSO>(activeAugments);

    /// <summary>
    /// Reset all static data - called by GameStateResetter on new game.
    /// </summary>
    public static void ResetStaticData()
    {
        activeAugments.Clear();
        Debug.Log("[AugmentManager] Static data reset - all augments cleared");
    }
}

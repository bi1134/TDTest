using UnityEngine;

/// <summary>
/// Game State Resetter - Resets all static data when starting a new game.
/// Place this on a GameObject in the Game scene (NOT Main Menu).
/// Runs on Awake to ensure clean state before game starts.
/// </summary>
public class GameStateResetter : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logResets = true;

    private void Awake()
    {
        ResetAllStaticData();
    }

    /// <summary>
    /// Reset all static data across the game.
    /// Add new reset calls here as you add systems with static data (e.g., AugmentManager).
    /// </summary>
    private void ResetAllStaticData()
    {
        if (logResets)
        {
            Debug.Log("[GameStateResetter] Resetting all static game data...");
        }

        // Player data
        PlayerStats.ResetStaticData();

        // Augment system
        AugmentManager.ResetStaticData();

        // Future: Add more reset calls as needed
        // Example:
        // AugmentManager.ResetStaticData();
        // UpgradeManager.ResetStaticData();
        // MetaProgressionManager.ResetStaticData(); // If using persistent upgrades

        if (logResets)
        {
            Debug.Log("[GameStateResetter] All static data reset complete!");
        }
    }

    /// <summary>
    /// Public method to manually trigger reset (e.g., from Restart button).
    /// Can be called from UI or GameHandler.
    /// </summary>
    public void ManualReset()
    {
        Debug.Log("[GameStateResetter] Manual reset triggered");
        ResetAllStaticData();
    }
}

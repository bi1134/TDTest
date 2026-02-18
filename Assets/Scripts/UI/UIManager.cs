using UnityEngine;

/// <summary>
/// Centralized UI Manager - Controls visibility of all UI panels based on game state.
/// Attach this to the Canvas (parent of all UI elements).
/// 
/// IMPORTANT SETUP:
/// - gameplayUI can be active/inactive (contains Shop, PlayerStats)
/// - gameOverUI should reference the GameOverUI CONTROLLER (always active parent)
/// - pauseUI should reference the PauseUI CONTROLLER (always active parent)
/// The controllers will handle showing/hiding their own panels.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("UI shown during gameplay (shop, stats, etc.) - hidden only on Game Over")]
    public GameObject gameplayUI;
    
    [Tooltip("Reference to GameOverUI controller (always-active parent)")]
    public GameOverUI gameOverController;
    
    [Tooltip("Reference to PauseUI controller (always-active parent)")]
    public PauseUI pauseController;

    private void Start()
    {
        // Subscribe to game state events only
        GameEvents.OnGameStateChanged += HandleGameStateChanged;
        // Note: Pause UI is handled by PauseUI controller directly
    }

    private void OnDestroy()
    {
        GameEvents.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(object sender, GameEvents.GameStateChangedEventArgs e)
    {
        Debug.Log($"[UIManager] Updating UI for state: {e.newState}");

        // Simple logic: Show gameplay UI except on GameOver
        bool isGameOver = (e.newState == GameHandler.GameState.GameOver);
        
        SetActive(gameplayUI, !isGameOver);
        
        // Game Over UI is managed by its own controller
        if (isGameOver && gameOverController != null)
        {
            gameOverController.Show();
        }
        else if (gameOverController != null)
        {
            gameOverController.Hide();
        }
    }

    // Helper to safely SetActive
    private void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }
}

using UnityEngine;

/// <summary>
/// Centralized UI Manager - Controls visibility of all UI panels based on game state.
/// Attach this to the Canvas (parent of all UI elements).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("UI shown during gameplay (shop, stats, etc.) - hidden only on Game Over")]
    public GameObject gameplayUI;
    
    [Tooltip("UI shown when game is over")]
    public GameObject gameOverUI;

    private void Start()
    {
        // Subscribe to game state events
        GameEvents.OnGameStateChanged += HandleGameStateChanged;
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
        SetActive(gameOverUI, isGameOver);
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

using UnityEngine;

/// <summary>
/// Pause Manager - Handles game pause/unpause via Escape key.
/// Toggles Time.timeScale and triggers UI events.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public bool IsPaused { get; private set; } = false;

    private void Start()
    {
        // Subscribe to pause event
        GameEvents.OnPauseAction += HandlePauseAction;
        
        // Ensure game starts unpaused
        IsPaused = false;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        GameEvents.OnPauseAction -= HandlePauseAction;
    }

    private void HandlePauseAction(object sender, System.EventArgs e)
    {
        // Don't allow pausing during Game Over
        // You can add more conditions here if needed
        TogglePause();
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        Debug.Log("[PauseManager] Game Paused");
        
        // Trigger event for UI to show pause menu
        GameEvents.TriggerPauseStateChanged(this, true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        Debug.Log("[PauseManager] Game Resumed");
        
        // Trigger event for UI to hide pause menu
        GameEvents.TriggerPauseStateChanged(this, false);
    }
}

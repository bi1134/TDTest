using UnityEngine;

/// <summary>
/// Pause Manager - Handles game pause/unpause via Escape key.
/// Toggles Time.timeScale and triggers UI events.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public bool IsPaused { get; private set; } = false;
    public float currentTimeScale = 1f;

    private void Start()
    {
        // Subscribe to pause event
        GameEvents.OnPauseAction += HandlePauseAction;
        
        // Ensure game starts unpaused and at normal speed
        IsPaused = false;
        currentTimeScale = 1f;
        Time.timeScale = currentTimeScale;
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

    public void ToggleTimeScale()
    {
        currentTimeScale = (currentTimeScale == 1f) ? 2f : 1f;
        if (!IsPaused)
        {
            Time.timeScale = currentTimeScale;
        }
        Debug.Log($"[PauseManager] Time scale toggled to {currentTimeScale}x");
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
        Time.timeScale = currentTimeScale;
        Debug.Log($"[PauseManager] Game Resumed at {currentTimeScale}x speed");
        
        // Trigger event for UI to hide pause menu
        GameEvents.TriggerPauseStateChanged(this, false);
    }
}

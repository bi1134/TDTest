using TMPro;
using UnityEngine;

/// <summary>
/// Game Over UI - Displays wave count when player dies.
/// Visibility controlled by UIManager - this just updates the display.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI levelDefendedText;

    private int lastCompletedWave = 0;

    private void Start()
    {
        // Subscribe to events to track wave progress
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDestroy()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandleWaveCompleted(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        // Track last completed wave
        lastCompletedWave = e.waveNumber -1; //-1 because waveNumber is 1-indexed for display, but we want to show how many were fully defended
        Debug.Log($"[GameOverUI] Tracked wave completion: {lastCompletedWave}");
    }

    private void HandlePlayerDied(object sender, System.EventArgs e)
    {
        // Update the display text (UIManager will handle showing this panel)
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (levelDefendedText != null)
        {
            levelDefendedText.text = $"{lastCompletedWave} Level{(lastCompletedWave == 1 ? "" : "s")}";
        }

        Debug.Log($"[GameOverUI] Updated display - Defended {lastCompletedWave} waves.");
    }

    // Optional: Button callbacks
    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// Game Over UI Controller - Manages visibility and data display.
/// This script should be on an ALWAYS-ACTIVE parent object.
/// Assign the actual UI panel as a child reference.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelContainer; // The actual UI panel (can be inactive)
    [SerializeField] private TextMeshProUGUI levelDefendedText;

    private int lastCompletedWave = 0;

    private void Start()
    {
        // This runs even if panelContainer is inactive
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
        GameEvents.OnPlayerDied += HandlePlayerDied;
        
        // Ensure panel starts hidden
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    private void HandleWaveCompleted(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        // Track last completed wave
        lastCompletedWave = e.waveNumber;
        Debug.Log($"[GameOverUI] Tracked wave completion: {lastCompletedWave}");
    }

    private void HandlePlayerDied(object sender, System.EventArgs e)
    {
        // Update the display text
        UpdateDisplay();
        
        // Show panel
        if (panelContainer != null)
        {
            panelContainer.SetActive(true);
        }
    }

    private void UpdateDisplay()
    {
        if (levelDefendedText != null)
        {
            levelDefendedText.text = $"{lastCompletedWave} Level{(lastCompletedWave == 1 ? "" : "s")}";
        }

        Debug.Log($"[GameOverUI] Updated display - Defended {lastCompletedWave} waves.");
    }

    // Called by UIManager
    public void Show()
    {
        if (panelContainer != null)
        {
            panelContainer.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
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

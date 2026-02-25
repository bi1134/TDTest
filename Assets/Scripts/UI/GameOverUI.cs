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

    // Seed UI
    [SerializeField] private TextMeshProUGUI seedText;
    [SerializeField] private UnityEngine.UI.Button copySeedButton;
    private int currentSeed = 0;

    [Header("Buttons")]
    [SerializeField] private UnityEngine.UI.Button retryButton;
    [SerializeField] private UnityEngine.UI.Button quitButton; // Quit returns to Main Menu

    private void Awake()
    {
        // Subscribe early
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
        GameEvents.OnPlayerDied += HandlePlayerDied;
        GameEvents.OnRunSeedSet += HandleRunSeedSet;

        if (copySeedButton != null)
        {
            copySeedButton.onClick.AddListener(() =>
            {
                SoundEvents.TriggerButtonClicked(this);
                if (seedText != null)
                {
                    GUIUtility.systemCopyBuffer = seedText.text.Replace("Seed: ", "");
                    Debug.Log("[GameOverUI] Seed copied to clipboard");
                }
            });
        }
        
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(() =>
            {
                SoundEvents.TriggerButtonClicked(this);
                RestartGame();
            });
        }

        if (quitButton != null)
        {
            // Quit button goes to Main Menu (as requested)
            quitButton.onClick.AddListener(() =>
            {
                SoundEvents.TriggerCancelButtonClicked(this);
                ReturnToMainMenu();
            });
        }
        
        // Ensure panel starts hidden
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
    }
    
    // Remove Start since we used Awake
    // OnDestroy must match Awake subscriptions
    private void OnDestroy()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
        GameEvents.OnRunSeedSet -= HandleRunSeedSet;
    }

    private void HandleRunSeedSet(object sender, int seed)
    {
        currentSeed = seed;
    }

    private void HandleWaveCompleted(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        lastCompletedWave = e.waveNumber;
    }

    private void HandlePlayerDied(object sender, System.EventArgs e)
    {
        Show();
    }

    public void Hide()
    {
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
    }

    private void UpdateDisplay()
    {
        if (levelDefendedText != null)
        {
            levelDefendedText.text = $"{lastCompletedWave} Wave{(lastCompletedWave == 1 ? "" : "s")}";
        }
    }

    public void RestartGame()
    {
        Loader.Load(Loader.Scene.GameScene);
    }

    public void ReturnToMainMenu()
    {
        Loader.Load(Loader.Scene.MainMenuScene);
    }
    
    public void Show()
    {
        UpdateDisplay();
        
        // Update Seed
         if (seedText != null)
        {
            // Use local currentSeed
            seedText.text = "Seed: " + currentSeed.ToString();
        }

        if (panelContainer != null)
        {
            panelContainer.SetActive(true);
        }
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

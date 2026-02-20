using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause UI Controller - Manages pause menu visibility and button clicks.
/// This script should be on an ALWAYS-ACTIVE parent object.
/// Assign the actual pause menu panel as a child reference.
/// </summary>
public class PauseUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelContainer; // The actual pause menu panel (can be inactive)
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton; // New Restart Button
    [SerializeField] private Button quitButton; // Optional
    [SerializeField] private TMPro.TMP_Text seedText; // New Seed Text
    [SerializeField] private Button copySeedButton; // New Copy Seed Button

    [Header("Manager Reference")]
    [SerializeField] private PauseManager pauseManager; // Reference to pause manager

    private int currentSeed = 0;

    private void Awake()
    {
        // Subscribe to events EARLY
        GameEvents.OnPauseStateChanged += HandlePauseStateChanged;
        GameEvents.OnRunSeedSet += HandleRunSeedSet;
        
        // Set up button listeners (like MainMenuUI pattern)
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(() =>
            {
                if (pauseManager != null)
                {
                    pauseManager.Resume();
                }
            });
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(() =>
            {
                if (pauseManager != null)
                {
                    pauseManager.Resume(); // Unpause first to avoid timeScale issues
                }
                // Reload current scene for clean restart
                Loader.Load(Loader.Scene.GameScene);
            });
        }

        if (copySeedButton != null)
        {
            copySeedButton.onClick.AddListener(() =>
            {
                if (seedText != null)
                {
                    GUIUtility.systemCopyBuffer = seedText.text.Replace("Seed: ", "");
                    Debug.Log("[PauseUI] Seed copied to clipboard: " + GUIUtility.systemCopyBuffer);
                }
            });
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() =>
            {
                if (pauseManager != null)
                {
                    pauseManager.Resume(); // Unpause first
                }

                // Load main menu scene
                Loader.Load(Loader.Scene.MainMenuScene);
            });
        }
        
        // Ensure panel starts hidden
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
    }
    
    // Start removed

    private void OnDestroy()
    {
        GameEvents.OnPauseStateChanged -= HandlePauseStateChanged;
        GameEvents.OnRunSeedSet -= HandleRunSeedSet;
    }

    private void HandleRunSeedSet(object sender, int seed)
    {
        currentSeed = seed;
        UpdateSeedDisplay();
    }

    private void HandlePauseStateChanged(object sender, GameEvents.PauseStateChangedEventArgs e)
    {
        if (panelContainer != null)
        {
            panelContainer.SetActive(e.isPaused);
            
            if (e.isPaused)
            {
                UpdateSeedDisplay();
            }
        }
    }

    private void UpdateSeedDisplay()
    {
        if (seedText != null)
        {
            // If seed is 0, it might not be set yet or it's actually 0. 
            // Better to show it if we have it.
            seedText.text = "Seed: " + currentSeed.ToString();
        }
    }
}

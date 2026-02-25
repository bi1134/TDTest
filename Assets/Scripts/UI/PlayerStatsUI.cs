using System;
using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI livesText; 
    [SerializeField] private TextMeshProUGUI waveText;

    private void OnEnable()
    {
        AssignSignal();

        GameUIEvent.MoneyChanged(this, PlayerStats.wallet);
        GameUIEvent.LivesChanged(this, PlayerStats.Lives);
        
        GameEvents.OnWaveCompleted += UpdateWaveText;
        // Optionally update it as soon as the manager starts
    }

    private void Start()
    {
        // Force an initial update to show "Wave: 0" or whatever the current wave is
        UpdateWaveDisplay();
        
        GameEvents.OnWaveStarted += HandleWaveStarted;
    }

    private void HandleWaveStarted(object sender, EventArgs e)
    {
        UpdateWaveDisplay();
    }

    private void UpdateWaveDisplay()
    {
        if (waveText != null)
        {
            var wm = FindAnyObjectByType<WaveManager>();
            if (wm != null)
            {
                waveText.text = "Wave: " + wm.activeWaveCount;
            }
            else
            {
                waveText.text = "Wave: 0";
            }
        }
    }
    
    private void UpdateMoney(object sender, GameUIEvent.OnMoneyChangedEventArgs e)
    {
        moneyText.text = "$" + e.currentMoney;
    }

    private void UpdateLives(object sender, GameUIEvent.OnLivesChangedEventArgs e)
    {
               livesText.text = "Lives: " + e.currentLives;
    }

    private void UpdateWaveText(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        // We can just call the shared method to ensure it's synced
        UpdateWaveDisplay();
    }

    [Header("Pause Control")]
    [SerializeField] private UnityEngine.UI.Button pauseButton;
    [SerializeField] private PauseManager pauseManager;

    [Header("Speed Toggle")]
    [SerializeField] private UnityEngine.UI.Toggle speedToggle;

    private void Awake()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(() =>
            {
                SoundEvents.TriggerButtonClicked(this);
                if (pauseManager != null)
                {
                    pauseManager.Pause();
                }
                else
                {
                    // Fallback search
                    var pm = FindAnyObjectByType<PauseManager>();
                    if (pm != null) pm.Pause();
                }
            });
        }

        // Speed Toggle
        if (speedToggle != null)
        {
            speedToggle.onValueChanged.AddListener((isOn) =>
            {
                SoundEvents.TriggerButtonClicked(this);
                var pm = pauseManager != null ? pauseManager : FindAnyObjectByType<PauseManager>();
                if (pm == null) return;

                // Force the time scale to match the toggle state, rather than calling ToggleTimeScale
                // (avoids the toggle fighting with ToggleTimeScale which flips regardless of direction)
                pm.currentTimeScale = isOn ? 2f : 1f;
                if (!pm.IsPaused) Time.timeScale = pm.currentTimeScale;
            });
        }
    }

    private void AssignSignal()
    {
        GameUIEvent.OnMoneyChanged += UpdateMoney;
        GameUIEvent.OnLivesChanged += UpdateLives;
    }

    private void OnDisable()
    {
        GameUIEvent.OnMoneyChanged -= UpdateMoney;
        GameUIEvent.OnLivesChanged -= UpdateLives;
        GameEvents.OnWaveCompleted -= UpdateWaveText;
        GameEvents.OnWaveStarted -= HandleWaveStarted;
    }
}

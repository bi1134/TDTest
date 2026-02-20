using System;
using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI livesText; 

    private void OnEnable()
    {
        AssignSignal();

        GameUIEvent.MoneyChanged(this, PlayerStats.wallet);
        GameUIEvent.LivesChanged(this, PlayerStats.Lives);
    }
    
    private void UpdateMoney(object sender, GameUIEvent.OnMoneyChangedEventArgs e)
    {
        moneyText.text = "$" + e.currentMoney;
    }

    private void UpdateLives(object sender, GameUIEvent.OnLivesChangedEventArgs e)
    {
               livesText.text = "Lives: " + e.currentLives;
    }

    [Header("Pause Control")]
    [SerializeField] private UnityEngine.UI.Button pauseButton;
    [SerializeField] private PauseManager pauseManager;

    private void Awake()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(() =>
            {
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
    }

    private void AssignSignal()
    {
        GameUIEvent.OnMoneyChanged += UpdateMoney;
        GameUIEvent.OnLivesChanged += UpdateLives;
    }

}

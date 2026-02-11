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
        GameUIEvent.LivesChanged(this, PlayerStats.lives);
    }
    
    private void UpdateMoney(object sender, GameUIEvent.OnMoneyChangedEventArgs e)
    {
        moneyText.text = "$" + e.currentMoney;
    }

    private void UpdateLives(object sender, GameUIEvent.OnLivesChangedEventArgs e)
    {
               livesText.text = "Lives: " + e.currentLives;
    }

    private void AssignSignal()
    {
        GameUIEvent.OnMoneyChanged += UpdateMoney;
        GameUIEvent.OnLivesChanged += UpdateLives;
    }

}

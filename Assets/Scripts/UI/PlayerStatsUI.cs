using System;
using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;

    private void OnEnable()
    {
        AssignSignal();

        GameUIEvent.MoneyChanged(this, PlayerStats.wallet);
    }
    
    private void UpdateMoney(object sender, GameUIEvent.OnMoneyChangedEventArgs e)
    {
        moneyText.text = "$" + e.currentMoney;
    }
    private void AssignSignal()
    {
        GameUIEvent.OnMoneyChanged += UpdateMoney;
    }

}

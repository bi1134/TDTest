using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    // public static event System.Action OnStatsChanged; // Removed in favor of GameUIEvent

    private static int _wallet;
    public static int wallet
    {
        get => _wallet;
        set
        {
            if (_wallet != value)
            {
                _wallet = value;
                // OnStatsChanged?.Invoke();
                // Trigger GameUIEvent
                // Since this is static, we pass null sender or a dummy object?
                // GameUIEvent expects object sender.
                GameUIEvent.MoneyChanged(null, _wallet);
            }
        }
    }
    public int startMoney = 400;

    private static int _lives;
    public static int Lives
    {
        get => _lives;
        set
        {
            if (_lives != value)
            {
                _lives = value;
                // OnStatsChanged?.Invoke();
                GameUIEvent.LivesChanged(null, _lives);
                
                // Trigger game over when lives reach 0
                if (_lives <= 0)
                {
                    GameEvents.TriggerPlayerDied(null);
                }
            }
        }
    }
    public int startLives = 20;

    /// <summary>
    /// Reset all static data - called when starting a new game.
    /// </summary>
    public static void ResetStaticData()
    {
        // Use backing fields to avoid triggering side effects (like Death event)
        _wallet = 0;
        _lives = 0;
        Debug.Log("[PlayerStats] Static data reset");
    }
    
    private void Start()
    {
        wallet = startMoney;
        Lives = startLives;
    }
}

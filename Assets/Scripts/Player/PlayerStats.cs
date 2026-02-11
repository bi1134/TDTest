using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static int wallet;
    public int startMoney = 400;

    public static int lives;
    public int startLives = 20;
    
    private void OnEnable()
    {
        wallet = startMoney;
        lives = startLives;
    }
}

using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static int wallet;
    public int startMoney = 400;

    private void OnEnable()
    {
        wallet = startMoney;
    }
}

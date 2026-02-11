using System;

public static class GameUIEvent
{
    public static EventHandler<OnMoneyChangedEventArgs> OnMoneyChanged;
    public static EventHandler<OnLivesChangedEventArgs> OnLivesChanged;

    public class OnMoneyChangedEventArgs : EventArgs
    {
        public int currentMoney;
    }

    public class OnLivesChangedEventArgs : EventArgs
    {
        public int currentLives;
    }

    public static void MoneyChanged(object sender, int currentMoney)
    {
        OnMoneyChanged?.Invoke(sender, new OnMoneyChangedEventArgs { currentMoney = currentMoney });
    }

    public static void LivesChanged(object sender, int currentLives)
    {
        OnLivesChanged?.Invoke(sender, new OnLivesChangedEventArgs { currentLives = currentLives });
    }
}

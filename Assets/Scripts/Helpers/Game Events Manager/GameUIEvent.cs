using System;

public static class GameUIEvent
{
    public static EventHandler<OnMoneyChangedEventArgs> OnMoneyChanged;

    public class OnMoneyChangedEventArgs : EventArgs
    {
        public int currentMoney;
    }

    public static void MoneyChanged(object sender, int currentMoney)
    {
        OnMoneyChanged?.Invoke(sender, new OnMoneyChangedEventArgs { currentMoney = currentMoney });
    }
}

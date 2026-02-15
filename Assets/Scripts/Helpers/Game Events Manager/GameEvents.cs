using System;
using UnityEngine;

/// <summary>
/// Central event manager for game-play events (non-UI).
/// </summary>
public static class GameEvents
{
    // Event Arguments
    public class GameStateChangedEventArgs : EventArgs
    {
        public GameHandler.GameState newState;
    }

    public class WaveCompletedEventArgs : EventArgs
    {
        public int waveNumber; // 1-indexed wave number (for display)
    }

    // Map & Path
    public static event EventHandler OnMapExpansionStarted;
    public static event EventHandler OnPathfinderGraphRebuilt;

    public static void TriggerMapExpansionStarted(object sender)
    {
        OnMapExpansionStarted?.Invoke(sender, EventArgs.Empty);
    }

    public static void TriggerPathfinderGraphRebuilt(object sender)
    {
        OnPathfinderGraphRebuilt?.Invoke(sender, EventArgs.Empty);
    }

    public static event EventHandler<WaveCompletedEventArgs> OnWaveCompleted;
    public static void TriggerWaveCompleted(object sender, int waveNumber)
    {
        OnWaveCompleted?.Invoke(sender, new WaveCompletedEventArgs { waveNumber = waveNumber });
    }

    // Game State Events
    public static event EventHandler<GameStateChangedEventArgs> OnGameStateChanged;
    public static void TriggerGameStateChanged(object sender, GameHandler.GameState newState)
    {
        OnGameStateChanged?.Invoke(sender, new GameStateChangedEventArgs { newState = newState });
    }

    public static event EventHandler OnPlayerDied;
    public static void TriggerPlayerDied(object sender)
    {
        Debug.Log("[GameEvents] Player Died");
        OnPlayerDied?.Invoke(sender, EventArgs.Empty);
    }
}

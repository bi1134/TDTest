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

    public class PauseStateChangedEventArgs : EventArgs
    {
        public bool isPaused;
    }

    public class AugmentSelectionStartedEventArgs : EventArgs
    {
        public System.Collections.Generic.List<AugmentSO> options;
    }

    public class AugmentSelectedEventArgs : EventArgs
    {
        public AugmentSO selectedAugment;
    }

    // Map & Path
    public static event EventHandler OnMapExpansionStarted;
    public static event EventHandler OnPathfinderGraphRebuilt;
    public static event EventHandler OnPauseAction;

    public static event EventHandler<ChunkGeneratedEventArgs> OnChunkGenerated;
    public class ChunkGeneratedEventArgs : EventArgs
    {
        public Vector2Int chunkCoord;
    }

    public static void TriggerMapExpansionStarted(object sender)
    {
        OnMapExpansionStarted?.Invoke(sender, EventArgs.Empty);
    }
    
    public static void TriggerChunkGenerated(object sender, Vector2Int coord)
    {
        OnChunkGenerated?.Invoke(sender, new ChunkGeneratedEventArgs { chunkCoord = coord });
    }

    public static void TriggerPathfinderGraphRebuilt(object sender)
    {
        OnPathfinderGraphRebuilt?.Invoke(sender, EventArgs.Empty);
    }

    public static void TriggerPauseAction(object sender)
    {
        OnPauseAction?.Invoke(sender, EventArgs.Empty);
    }

    public static event EventHandler<PauseStateChangedEventArgs> OnPauseStateChanged;
    public static void TriggerPauseStateChanged(object sender, bool isPaused)
    {
        OnPauseStateChanged?.Invoke(sender, new PauseStateChangedEventArgs { isPaused = isPaused });
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

    // Augment Events
    public static event EventHandler<AugmentSelectionStartedEventArgs> OnAugmentSelectionStarted;
    public static void TriggerAugmentSelectionStarted(object sender, System.Collections.Generic.List<AugmentSO> options)
    {
        OnAugmentSelectionStarted?.Invoke(sender, new AugmentSelectionStartedEventArgs { options = options });
    }

    public static event EventHandler<AugmentSelectedEventArgs> OnAugmentSelected;
    public static void TriggerAugmentSelected(object sender, AugmentSO selectedAugment)
    {
       OnAugmentSelected?.Invoke(sender, new AugmentSelectedEventArgs { selectedAugment = selectedAugment });
    }
}

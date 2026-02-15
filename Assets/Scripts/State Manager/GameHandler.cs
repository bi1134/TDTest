using UnityEngine;

/// <summary>
/// Event-driven game state manager.
/// Controls the flow: WaitingToStart → Preparation → Playing → (back to Preparation or GameOver)
/// </summary>
public class GameHandler : MonoBehaviour
{
    public enum GameState
    {
        WaitingToStart,  // Initial loading
        Preparation,     // Can place turrets, expand map (buttons shown)
        Playing,         // Wave active (buttons hidden)
        GameOver         // Player died
    }

    [Header("Settings")]
    [Tooltip("Time to wait before allowing game to start")]
    public float initialWaitTime = 1f;

    [Header("State")]
    public GameState currentState { get; private set; }

    private float waitTimer;

    private void Awake()
    {
        currentState = GameState.WaitingToStart;
        waitTimer = initialWaitTime;
    }

    private void OnEnable()
    {
        // Listen to game events
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
        GameEvents.OnPlayerDied += HandlePlayerDied;
        GameEvents.OnMapExpansionStarted += HandleMapExpansionStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
        GameEvents.OnMapExpansionStarted -= HandleMapExpansionStarted;
    }

    private void Update()
    {
        // Only use Update for WaitingToStart timer
        if (currentState == GameState.WaitingToStart)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                ChangeState(GameState.Preparation);
            }
        }
    }

    // Event Handlers
    private void HandleMapExpansionStarted(object sender, System.EventArgs e)
    {
        // When player clicks expand button → Start wave → Playing state
        if (currentState == GameState.Preparation)
        {
            ChangeState(GameState.Playing);
        }
    }

    private void HandleWaveCompleted(object sender, System.EventArgs e)
    {
        // When wave ends → Back to Preparation (show buttons)
        if (currentState == GameState.Playing)
        {
            ChangeState(GameState.Preparation);
        }
    }

    private void HandlePlayerDied(object sender, System.EventArgs e)
    {
        // When player lives <= 0 → Game Over
        ChangeState(GameState.GameOver);
    }

    // State Management
    private void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        Debug.Log($"[GameHandler] State: {currentState} → {newState}");

        currentState = newState;

        // Broadcast state change WITH state data
        GameEvents.TriggerGameStateChanged(this, newState);

        // State-specific logic
        OnStateEnter(newState);
    }

    private void OnStateEnter(GameState state)
    {
        switch (state)
        {
            case GameState.WaitingToStart:
                // Initial setup if needed
                break;

            case GameState.Preparation:
                Debug.Log("[GameHandler] Preparation Phase - Place turrets and expand map");
                // MapExpansionManager already listens to OnWaveCompleted to show buttons
                break;

            case GameState.Playing:
                Debug.Log("[GameHandler] Wave Active - Defend!");
                // WaveManager already handles spawning
                break;

            case GameState.GameOver:
                Debug.Log("[GameHandler] Game Over!");
                // TODO: Show Game Over UI
                break;
        }
    }

    // Public API for querying state
    public bool IsGamePlaying()
    {
        return currentState == GameState.Playing;
    }

    public bool IsPreparation()
    {
        return currentState == GameState.Preparation;
    }

    public bool IsGameOver()
    {
        return currentState == GameState.GameOver;
    }
}

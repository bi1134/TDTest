using UnityEngine;
using System.Collections.Generic;
using TerrainGenerator;

public class MapExpansionManager : MonoBehaviour
{
    public WFCWorldManager worldManager;
    public MapExpansionButton buttonPrefab;
    public Transform buttonParent;
    
    // UI Settings
    public float buttonHeightOffset = 2.0f; // Height above ground
    public float edgeOffset = 1.0f;         // Push button slightly out from edge
    
    private List<MapExpansionButton> activeButtons = new List<MapExpansionButton>();

    private bool isPreparationPhase = false;
    private bool isPendingWave = false; // Prevent buttons from showing while waiting for wave to start 

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleGameStateChanged;
        GameEvents.OnChunkGenerated += HandleChunkGenerated;
        GameEvents.OnMapExpansionStarted += HandleMapExpansionStarted;
        GameEvents.OnWaveStartFailed += HandleWaveStartFailed;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.OnChunkGenerated -= HandleChunkGenerated;
        GameEvents.OnMapExpansionStarted -= HandleMapExpansionStarted;
        GameEvents.OnWaveStartFailed -= HandleWaveStartFailed;
    }

    private void HandleGameStateChanged(object sender, GameEvents.GameStateChangedEventArgs e)
    {
        // Show buttons during Preparation, hide during Playing/GameOver
        if (e.newState == GameHandler.GameState.Preparation)
        {
            isPreparationPhase = true;
            isPendingWave = false; 
            ShowExpansionOptions();
        }
        else
        {
            isPreparationPhase = false;
            isPendingWave = false;
            HideAllButtons();
        }
    }

    private void HandleMapExpansionStarted(object sender, System.EventArgs e)
    {
        // Player clicked expand. Maps are generating. Pathfinding updating.
        // Hide buttons immediately to prevent double clicks or confusion.
        isPendingWave = true;
        HideAllButtons();
    }
    
    private void HandleWaveStartFailed(object sender, System.EventArgs e)
    {
        // Wave failed to start (e.g. no path).
        // Re-enable buttons so user can try expanding elsewhere or fixes it.
        isPendingWave = false;
        ShowExpansionOptions();
        Debug.LogWarning("[MapExpansionManager] Wave start failed - Reshowing buttons.");
    }

    private void HandleChunkGenerated(object sender, GameEvents.ChunkGeneratedEventArgs e)
    {
        // Play VFX + poof sound at center of the newly generated chunk
        if (worldManager != null)
        {
            Vector3Int size = worldManager.chunkSize;
            float scale = worldManager.worldScale;
            float cx = e.chunkCoord.x * size.x * scale + size.x * scale * 0.5f;
            float cz = e.chunkCoord.y * size.z * scale + size.z * scale * 0.5f;
            Vector3 chunkCenter = new Vector3(cx, 0f, cz);

            VFXManager.Instance?.PlayEffect(VFXType.ChunkExpand, chunkCenter);
            SoundEvents.TriggerChunkExpand(this, chunkCenter);
        }

        // If a new chunk appears while we are in Preparation mode (e.g. late load on start), refresh buttons
        // BUT if we are pending a wave start, DO NOT show buttons.
        if (isPreparationPhase && !isPendingWave)
        {
            ShowExpansionOptions();
        }
    }



    [ContextMenu("Show Expansion Options")]
    public void ShowExpansionOptions()
    {
        HideAllButtons(); // Clear old

        if (worldManager == null || buttonPrefab == null)
        {
            Debug.LogError("[MapExpansionManager] Missing references!");
            return;
        }
        
        Debug.Log("[MapExpansionManager] ShowExpansionOptions called!");

        Vector3Int chunkSize = worldManager.chunkSize;
        float scale = worldManager.worldScale;
        
        // Deduplication Set: Track which new chunk coordinates already have a button
        HashSet<Vector2Int> targetedCoords = new HashSet<Vector2Int>();

        foreach (var kvp in worldManager.LoadedChunks)
        {
            Vector2Int coord = kvp.Key;
            
            // Check 4 sides
            CheckAndSpawnButton(coord, EdgeSide.Left, targetedCoords, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Right, targetedCoords, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Top, targetedCoords, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Bottom, targetedCoords, chunkSize, scale);
        }
    }

    private void CheckAndSpawnButton(Vector2Int coord, EdgeSide side, HashSet<Vector2Int> targetedCoords, Vector3Int size, float scale)
    {
        // 1. Calculate and Check Neighbor Coord
        Vector2Int neighborCoord = coord;
        switch (side)
        {
            case EdgeSide.Left:   neighborCoord += Vector2Int.left; break;
            case EdgeSide.Right:  neighborCoord += Vector2Int.right; break;
            case EdgeSide.Top:    neighborCoord += Vector2Int.up; break;
            case EdgeSide.Bottom: neighborCoord += Vector2Int.down; break;
        }

        // 2. Existence Checks
        if (worldManager.LoadedChunks.ContainsKey(neighborCoord)) return; // Chunk already exists
        if (targetedCoords.Contains(neighborCoord)) return; // Button already spawned for this target

        // 3. Path Endpoint Check
        var endpoints = worldManager.GetEndPointsForChunk(coord);
        bool hasEndpointOnThisEdge = false;
        
        foreach (var endPoint in endpoints)
        {
            EdgeSide mappedSide = (EdgeSide)((int)endPoint.edge);
            if (mappedSide == side)
            {
                hasEndpointOnThisEdge = true;
                break;
            }
        }
        
        if (!hasEndpointOnThisEdge) return;

        // 4. Spawn Button at Center of NEW Chunk
        Vector3 buttonPos = CalculateButtonPosition(neighborCoord, size, scale);
        
        MapExpansionButton btn = Instantiate(buttonPrefab, buttonPos, Quaternion.identity, buttonParent != null ? buttonParent : transform);
        btn.Setup(worldManager, coord, side);
        activeButtons.Add(btn);
        
        // Add to dedupe list
        targetedCoords.Add(neighborCoord);
        
        Debug.Log($"[MapExpansionManager] Spawned button for new chunk at {neighborCoord} (triggered by {coord} {side})");
    }

    private Vector3 CalculateButtonPosition(Vector2Int targetCoord, Vector3Int size, float scale)
    {
        // Calculate World Position of the new chunk's origin
        float x = targetCoord.x * size.x * scale;
        float z = targetCoord.y * size.z * scale;
        
        // Center
        float centerX = x + (size.x * scale * 0.5f);
        float centerZ = z + (size.z * scale * 0.5f);
        
        return new Vector3(centerX, buttonHeightOffset, centerZ);
    }

    public void HideAllButtons()
    {
        foreach (var btn in activeButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        activeButtons.Clear();
    }
}

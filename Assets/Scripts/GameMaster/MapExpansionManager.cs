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

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(object sender, GameEvents.GameStateChangedEventArgs e)
    {
        // Show buttons during Preparation, hide during Playing/GameOver
        if (e.newState == GameHandler.GameState.Preparation)
        {
            ShowExpansionOptions();
        }
        else
        {
            HideAllButtons();
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

        foreach (var kvp in worldManager.LoadedChunks)
        {
            Vector2Int coord = kvp.Key;
            
            // Calculate base position of this chunk (Bottom-Left corner in world space)
            float chunkWorldX = coord.x * chunkSize.x * scale;
            float chunkWorldZ = coord.y * chunkSize.z * scale;
            
            // Check 4 sides
            CheckAndSpawnButton(coord, EdgeSide.Left,   chunkWorldX, chunkWorldZ, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Right,  chunkWorldX, chunkWorldZ, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Top,    chunkWorldX, chunkWorldZ, chunkSize, scale);
            CheckAndSpawnButton(coord, EdgeSide.Bottom, chunkWorldX, chunkWorldZ, chunkSize, scale);
        }
    }

    private void CheckAndSpawnButton(Vector2Int coord, EdgeSide side, float chunkX, float chunkZ, Vector3Int size, float scale)
    {
        // 1. Check if Neighbor Exists
        Vector2Int neighborCoord = coord;
        switch (side)
        {
            case EdgeSide.Left:   neighborCoord += Vector2Int.left; break;
            case EdgeSide.Right:  neighborCoord += Vector2Int.right; break;
            case EdgeSide.Top:    neighborCoord += Vector2Int.up; break;
            case EdgeSide.Bottom: neighborCoord += Vector2Int.down; break;
        }

        if (worldManager.LoadedChunks.ContainsKey(neighborCoord)) return; // Already exists

        // 2. Check if Path Endpoint exists at this Edge
        var endpoints = worldManager.GetEndPointsForChunk(coord);
        bool hasEndpointOnThisEdge = false;
        
        foreach (var endPoint in endpoints)
        {
            // Map TowerDefensePathGenerator.EdgeSide to TerrainGenerator.EdgeSide
            EdgeSide mappedSide = (EdgeSide)((int)endPoint.edge);
            if (mappedSide == side)
            {
                hasEndpointOnThisEdge = true;
                break;
            }
        }
        
        if (!hasEndpointOnThisEdge) return;

        // 3. Spawn Button
        Vector3 buttonPos = CalculateButtonPosition(side, chunkX, chunkZ, size, scale);
        
        // Instantiate
        MapExpansionButton btn = Instantiate(buttonPrefab, buttonPos, Quaternion.identity, buttonParent != null ? buttonParent : transform);
        btn.Setup(worldManager, coord, side);
        activeButtons.Add(btn);
        
        Debug.Log($"[MapExpansionManager] Spawned button at {buttonPos} for {side} side of chunk {coord}");
    }

    private Vector3 CalculateButtonPosition(EdgeSide side, float x, float z, Vector3Int size, float scale)
    {
        // Center of the edge
        float centerX = x + (size.x * scale * 0.5f);
        float centerZ = z + (size.z * scale * 0.5f);
        
        Vector3 pos = Vector3.zero;
        
        switch (side)
        {
            case EdgeSide.Left:
                // Left edge is at x
                pos = new Vector3(x - edgeOffset, buttonHeightOffset, centerZ);
                break;
            case EdgeSide.Right:
                // Right edge is at x + width
                pos = new Vector3(x + (size.x * scale) + edgeOffset, buttonHeightOffset, centerZ);
                break;
            case EdgeSide.Top:
                // Top edge is at z + length (Z+)
                pos = new Vector3(centerX, buttonHeightOffset, z + (size.z * scale) + edgeOffset);
                break;
            case EdgeSide.Bottom:
                // Bottom edge is at z (Z-)
                pos = new Vector3(centerX, buttonHeightOffset, z - edgeOffset);
                break;
        }
        
        return pos;
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

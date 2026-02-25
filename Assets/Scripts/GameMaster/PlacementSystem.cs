using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    public static PlacementSystem Instance { get; private set; }

    [Header("Base References")]
    [SerializeField] private Transform mouseind, cellInd;
    [SerializeField] private CameraController cameraSystem;
    [Tooltip("Reference to the scene's Grid component.")]
    [SerializeField] private Grid grid;

    [Header("Visual Prefabs")]
    [SerializeField] private GameObject buildGhostPrefab;
    [SerializeField] private GameObject selectionRingPrefab;
    [SerializeField] private LayerMask groundMask;

    private GameObject currentGhost;
    private GameObject currentSelectionRing;
    private Vector3Int lastGridPos = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Initialize instances once and keep them hidden
        if (buildGhostPrefab != null)
        {
            currentGhost = Instantiate(buildGhostPrefab);
            currentGhost.SetActive(false);
        }
        
        if (selectionRingPrefab != null)
        {
            currentSelectionRing = Instantiate(selectionRingPrefab);
            currentSelectionRing.SetActive(false);
        }
    }

    void Update()
    {
        if (grid == null) return;
        
        Vector3 mousePos = cameraSystem.GetMousePosition();
        
        // Use the Grid component directly
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        if (mouseind != null) mouseind.transform.position = mousePos;
        
        // Exact flat world position of the cell center
        Vector3 cellCenterPos = grid.GetCellCenterWorld(gridPos);
        // Force Y to 0 for the 2D base indicator
        Vector3 flatCellPos = new Vector3(cellCenterPos.x, 0f, cellCenterPos.z);
        
        if (cellInd != null) cellInd.transform.position = flatCellPos;

        UpdateGhostPlacement(gridPos, cellCenterPos);
    }

    private void UpdateGhostPlacement(Vector3Int gridPos, Vector3 cellCenterPos)
    {
        if (currentGhost == null) return;

        bool hasItemInHand = BuildManager.instance != null && BuildManager.instance.HasTurretSelection;

        if (!hasItemInHand || !cameraSystem.IsMouseOverGround)
        {
            if (currentGhost.activeSelf) currentGhost.SetActive(false);
            return; // No item or mouse not over the terrain
        }

        // Only raycast and update position if the cell has actually changed
        if (gridPos != lastGridPos)
        {
            lastGridPos = gridPos;
            
            // Raycast straight down from high up to find the true terrain height at this exact cell center
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(cellCenterPos.x, 100f, cellCenterPos.z), Vector3.down, out hit, 200f, groundMask))
            {
                // Snap ghost to the exact raycast hit point (sitting perfectly on top of the terrain)
                currentGhost.SetActive(true);
                currentGhost.transform.position = hit.point;
            }
            else
            {
                // Invalid ground (e.g. out of bounds) -> hide the ghost!
                currentGhost.SetActive(false);
            }
        }
    }

    // --- Selection Ring Management for Upgrades ---
    public void PositionSelectionRing(TurretBaseModule turret)
    {
        if (turret == null || currentSelectionRing == null) return;
        
        currentSelectionRing.SetActive(true);
        // Slightly offset so it doesn't z-fight with the floor or the turret base itself
        currentSelectionRing.transform.position = turret.transform.position + Vector3.up * 0.05f; 
    }

    public void HideSelectionRing()
    {
        if (currentSelectionRing != null)
        {
            currentSelectionRing.SetActive(false);
        }
    }
}


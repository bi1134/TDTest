using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private Transform mouseind, cellInd;
    [SerializeField] private CameraController cameraSystem;
    [SerializeField] Grid grid;

    void Update()
    {
        Vector3 mousePos = cameraSystem.GetMousePosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);
        mouseind.transform.position = mousePos;
        cellInd.transform.position = grid.CellToWorld(gridPos);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TerrainGenerator;

public class MapExpansionButton : MonoBehaviour
{
    private WFCWorldManager worldManager;
    private Vector2Int chunkCoord;
    private EdgeSide direction;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    public void Setup(WFCWorldManager manager, Vector2Int coord, EdgeSide dir)
    {
        worldManager = manager;
        // Adjust for coord vs direction: WFCWorldManager expects "ExpandInDirection(fromChunk, direction)"
        // But we are placing the button ON the edge.
        // So the button represents expanding FROM `coord` TOWARDS `dir`.
        chunkCoord = coord;
        direction = dir;
    }

    private void OnClick()
    {
        if (worldManager != null)
        {
            SoundEvents.TriggerButtonClicked(this);
            Debug.Log($"[ExpansionButton] Clicked! Expanding {direction} from {chunkCoord}");
            worldManager.ExpandInDirection(chunkCoord, direction);
            
            // Buttons will be hidden by MapExpansionManager listening to OnMapExpansionStarted
        }
    }

    private void Update()
    {
        if (Camera.main != null)
        {
            // Billboard logic: Face the camera
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }
}

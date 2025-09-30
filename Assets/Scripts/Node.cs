using UnityEngine;
using UnityEngine.EventSystems;

public class Node : MonoBehaviour
{
    public Color hoverColor;
    private Color startColor;

    [SerializeField] private GameObject positionOffset; // Offset for turret placement

    public GameObject turretBase;

    private Renderer rend;

    private BuildManager buildManager;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        startColor = rend.material.color; // Store the original color

        buildManager = BuildManager.instance;
    }

    private void OnMouseEnter()
    {
        //if theres nothing to build or the mouse is over a UI element, do nothing
        if (!buildManager.HasTurretSelection || EventSystem.current.IsPointerOverGameObject()) return;

        rend.material.color = hoverColor;   
    }

    private void OnMouseDown()
    {
        if(turretBase != null || !buildManager.HasTurretSelection || EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Cannot build here! Node already has a turret base. or theres no turret base to build");
            return;
        }

        buildManager.BuildTurretOn(this);
    }

    private void OnMouseExit()
    {
        rend.material.color = startColor; // Reset to original color
    }

    public Vector3 GetBuildPosition()
    {
        return positionOffset.transform.position;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

public class Node : MonoBehaviour
{
    public Color hoverColor;
    private Color startColor;

    public GameObject positionOffset; // Offset for turret placement

    [SerializeField] private Renderer rend;
    [Header("Optional")]
    public GameObject turretBase;


    private BuildManager buildManager;


    private void Start()
    {
        startColor = rend.material.color; // Store the original color

        buildManager = BuildManager.instance;
    }

    private void OnMouseEnter()
    {
        //if theres nothing to build or the mouse is over a UI element, do nothing
        if (turretBase != null || !buildManager.HasTurretSelection || EventSystem.current.IsPointerOverGameObject()) return;

        if(buildManager.HasEnoughMoney)
        rend.material.color = hoverColor;
        else
        rend.material.color = Color.red;
    }

    private void OnMouseDown()
    {
        if(turretBase != null || !buildManager.HasTurretSelection || EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Cannot build here! Node already has a turret base. or theres no turret base to build");
            return;
        }

        buildManager.TryBuildTurretOn(this);
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

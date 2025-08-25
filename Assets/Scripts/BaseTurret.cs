using UnityEngine;

public class BaseTurret : MonoBehaviour
{
    public Color hoverColor;
    private Color startColor;

    public GameObject positionOffset; // Offset for turret placement

    private GameObject turret;

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
        if (turret != null) return;

        rend.material.color = hoverColor;
    }

    private void OnMouseDown()
    {
        if (turret != null)
        {
            Debug.Log("Cannot build here! Node already has a turret.");
            return;
        }

        //build turretBase
        GameObject turretToBuild = buildManager.GetBarrelToBuild();
        turret = Instantiate(turretToBuild, positionOffset.transform.position, Quaternion.identity);
    }

    private void OnMouseExit()
    {
        rend.material.color = startColor; // Reset to original color
    }
}

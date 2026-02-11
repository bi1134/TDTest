using UnityEngine;
using UnityEngine.EventSystems;

public class Node : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        //if theres nothing to build or the mouse is over a UI element, do nothing
        // EventSystem handles blocking by UI if structured correctly, but checking IsPointerOverGameObject is a safeguard or for specific behavior.
        // However, OnPointerEnter generally implies the raycast hit THIS object.
        if (turretBase != null || !buildManager.HasTurretSelection) return;

        if (buildManager.HasEnoughMoney)
            rend.material.color = hoverColor;
        else
            rend.material.color = Color.red;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (turretBase != null || !buildManager.HasTurretSelection)
        {
            Debug.Log("Cannot build here! Node already has a turret base. or theres no turret base to build");
            return;
        }

        buildManager.TryBuildTurretOn(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rend.material.color = startColor; // Reset to original color
    }

    public Vector3 GetBuildPosition()
    {
        return positionOffset.transform.position;
    }
}

using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager instance;

    public GameObject standardTurretBasePrefab;
    public GameObject standardBarrelPrefab;
    private GameObject turretBaseToBuild;
    private GameObject barrelToBuild;

    public GameObject anotherTurretBasePrefab;
    public GameObject anotherBarrelPrefab;

    public void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("More than one BuildManager in the scene!");
            return;
        }
        instance = this;
    }

    public void SetTurretToBuild(GameObject turretBase)
    {
        turretBaseToBuild = turretBase;
    }

    public void SetBarrelToBuild(GameObject barrel)
    {
        barrelToBuild = barrel;
    }

    public GameObject GetTurretBaseToBuild()
    {
        return turretBaseToBuild;
    }
    
    public GameObject GetBarrelToBuild()
    {
        return barrelToBuild;
    }
}

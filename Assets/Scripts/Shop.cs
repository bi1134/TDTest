using UnityEngine;

public class Shop : MonoBehaviour
{
    private BuildManager buildManager;

    //assign form start  because instance is awake
    private void Start()
    {
        buildManager = BuildManager.instance;
    }

    public void PurchaseStandartTurret()
    {
        print("standard Turret purchased");

        buildManager.SetTurretToBuild(buildManager.standardTurretBasePrefab);
    }

    public void PurchaseAnotherTurret()
    {
        print("Another Turret purchased");
        buildManager.SetTurretToBuild(buildManager.anotherTurretBasePrefab);
    }

    public void PurchaseTurretBarrel()
    {
        print("Turret barrel purchased");
        buildManager.SetBarrelToBuild(buildManager.standardBarrelPrefab);
    }

    public void PurchasedAnotherBarrel()
    {
        print("Another barrel purchased");
        buildManager.SetBarrelToBuild(buildManager.anotherBarrelPrefab);
    }
}

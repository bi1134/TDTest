using UnityEngine;

public class Shop : MonoBehaviour
{
    private BuildManager buildManager;

    //assign form start  because instance is awake
    private void Start()
    {
        buildManager = BuildManager.instance;
    }

    public void PurchaseStandardTurret()
    {
        print("standard Turret purchased");

        buildManager.SetTurretToBuild(buildManager.standardTurretBasePrefab);
    }

    public void PurchaseAnotherTurret()
    {
        print("Another Turret purchased");
        buildManager.SetTurretToBuild(buildManager.anotherTurretBasePrefab);
    }

    public void PurchaseTurretBullet()
    {
        print("Turret barrel purchased");
        buildManager.SetBullet(buildManager.standardBullet);
    }

    public void PurchasedAnotherBullet()
    {
        print("Another barrel purchased");
        buildManager.SetBullet(buildManager.anotherBulletType);
    }
}

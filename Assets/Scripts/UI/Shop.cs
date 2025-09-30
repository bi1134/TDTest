using UnityEngine;

public class Shop : MonoBehaviour
{
    public TurretBaseBlueprint standardTurret;
    public TurretBaseBlueprint gatlingTurret;

    public BulletBlueprint standardBullet;
    public BulletBlueprint anotherBullet;

    private BuildManager buildManager;

    //assign form start  because instance is awake
    private void Start()
    {
        buildManager = BuildManager.instance;
    }

    public void SelectStandardTurret()
    {
        print("standard Turret purchased");

        buildManager.SelectTurret(standardTurret);
    }

    public void SelectAnotherTurret()
    {
        print("Another Turret purchased");
        buildManager.SelectTurret(gatlingTurret);
    }

    public void SelectTurretBullet()
    {
        print("Turret barrel purchased");
        buildManager.SelectBullet(standardBullet);
    }

    public void SelectAnotherBullet()
    {
        print("Another barrel purchased");
        buildManager.SelectBullet(anotherBullet);
    }
}

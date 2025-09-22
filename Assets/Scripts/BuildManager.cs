using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager instance;

    public GameObject standardTurretBasePrefab;
    public BulletProjectile standardBullet;
    private GameObject turretBaseToBuild;
    private BulletProjectile bulletType;

    public GameObject anotherTurretBasePrefab;
    public BulletProjectile anotherBulletType;

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

    public void SetBullet(BulletProjectile bullet)
    {
        bulletType = bullet;
    }

    public GameObject GetTurretBaseToBuild()
    {
        return turretBaseToBuild;
    }
    
    public BulletProjectile GetBulletType()
    {
        return bulletType;
    }
}

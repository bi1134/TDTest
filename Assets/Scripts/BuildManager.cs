using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager instance;

    private TurretBaseBlueprint turretBaseToBuild;
    private BulletBlueprint bulletType;

    public bool HasTurretSelection { get { return turretBaseToBuild != null; } }
    public bool HasBulletSelection { get { return bulletType != null; } }

    public void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("More than one BuildManager in the scene!");
            return;
        }
        instance = this;
    }

    public void SelectTurretToBuild(TurretBaseBlueprint turretBase)
    {
        turretBaseToBuild = turretBase;
    }

    public void SelectBullet(BulletBlueprint bullet)
    {
        bulletType = bullet;
    }

    public void BuildTurretOn(Node node)
    {
        var turret = Instantiate(turretBaseToBuild.prefab, node.GetBuildPosition(), Quaternion.identity, node.transform);
        node.turretBase = turret;
    }

    public void InstallBullet(TurretBaseModule turretBase)
    {
        if(turretBase == null || bulletType == null) return;
        turretBase.SetBulletType(bulletType);
    }
}

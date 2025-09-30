using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager instance;


    //selection state only
    public TurretBaseBlueprint SelectedTurret { get; private set; }
    public BulletBlueprint SelectedBullet { get; private set; }

    public bool HasTurretSelection => SelectedTurret != null;
    public bool HasBulletSelection => SelectedBullet != null;

    public bool HasEnoughMoney => PlayerStats.wallet >= (SelectedTurret != null ? SelectedTurret.cost : SelectedBullet != null ? SelectedBullet.cost : 0);

    [SerializeField] private bool continuousBuild = false;
    [SerializeField] private bool continuousInstall = false;

    public void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("More than one BuildManager in the scene!");
            return;
        }
        instance = this;
    }

    public void SelectTurret(TurretBaseBlueprint turretBase)
    {
        SelectedTurret = turretBase;
    }

    public void SelectBullet(BulletBlueprint bullet)
    {
        SelectedBullet = bullet;
    }

    public void ClearTurretSelection() => SelectedTurret = null;
    public void ClearBulletSelection() => SelectedBullet = null;

    public bool TryBuildTurretOn(Node node)
    {
        if (!HasTurretSelection || node == null || node.turretBase != null) return false;

        //if player cant afford turret then return false
        if (PlayerStats.wallet < SelectedTurret.cost)
        {
            print("Not enough money to build that!");
            return false;
        }

        PlayerStats.wallet -= SelectedTurret.cost;
        var go = Instantiate(SelectedTurret.prefab, node.GetBuildPosition(), Quaternion.identity, node.positionOffset.transform);
        node.turretBase = go;

        GameUIEvent.MoneyChanged(this, PlayerStats.wallet);

        if (!continuousBuild) ClearTurretSelection();
        return true;
    }

    public bool TryInstallBullet(TurretBaseModule turretBase)
    {
        if (!HasBulletSelection || turretBase == null) return false;

        turretBase.SetBulletType(SelectedBullet);
        if (!continuousInstall) ClearBulletSelection();
        return true;
    }
}

using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager instance;


    //selection state only
    public TurretBlueprintSO SelectedTurret { get; private set; }
    public BulletBlueprintSO SelectedBullet { get; private set; }

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

    public void SelectTurret(TurretBlueprintSO turretBase)
    {
        SelectedTurret = turretBase;
    }

    public void SelectBullet(BulletBlueprintSO bullet)
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

        // Initialize with default bullet if one exists
        if (SelectedTurret.defaultBullet != null)
        {
            var turretBase = go.GetComponentInChildren<TurretBaseModule>();
            if (turretBase != null)
            {
                turretBase.SetBulletType(SelectedTurret.defaultBullet);
            }
        }

        GameUIEvent.MoneyChanged(this, PlayerStats.wallet);

        if (!continuousBuild) ClearTurretSelection();
        return true;
    }

    public bool TryInstallBullet(TurretBaseModule turretBase)
    {
        if (!HasBulletSelection || turretBase == null) return false;

        // Check if player can afford the bullet
        if (PlayerStats.wallet < SelectedBullet.cost)
        {
            print("Not enough money to buy that bullet!");
            return false;
        }

        // Deduct cost
        PlayerStats.wallet -= SelectedBullet.cost;
        
        // Install bullet
        turretBase.SetBulletType(SelectedBullet);
        
        if (!continuousInstall) ClearBulletSelection();
        return true;
    }
}

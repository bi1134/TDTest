using UnityEngine;
using System.Collections.Generic;

public class Shop : MonoBehaviour
{
    [Header("Shop Config")]
    public List<TurretBlueprintSO> availableTurrets;
    public List<BulletBlueprintSO> availableBullets;
    public GameObject shopItemPrefab;
    public Transform shopItemsContainer;

    [Header("Cancel Selection")]
    [SerializeField] private UnityEngine.UI.Button cancelButton;

    private BuildManager buildManager;

    //assign form start  because instance is awake


    private void PopulateShop()
    {
        if (shopItemPrefab == null || shopItemsContainer == null)
        {
            Debug.LogWarning("Shop: Missing references for dynamic generation.");
            return;
        }

        // Generate Turrets
        foreach (var turret in availableTurrets)
        {
            if (turret == null) continue;
            GenerateShopItem(turret, SelectTurret);
        }

        // Generate Bullets
        foreach (var bullet in availableBullets)
        {
            if (bullet == null) continue;
            GenerateShopItem(bullet, SelectBullet);
        }
    }

    public void SelectTurret(TurretBlueprintSO turret)
    {
        print("Turret Selected: " + turret.turretName);
        buildManager.SelectTurret(turret);
        if (TurretUpgradeUI.Instance != null) TurretUpgradeUI.Instance.Close();
    }

    public void SelectBullet(BulletBlueprintSO bullet)
    {
        print("Bullet Selected: " + bullet.bulletName);
        buildManager.SelectBullet(bullet);
        if (TurretUpgradeUI.Instance != null) TurretUpgradeUI.Instance.Close();
    }

    private void OnEnable()
    {
        GameEvents.OnTurretUnlocked += HandleTurretUnlocked;
        GameEvents.OnBulletUnlocked += HandleBulletUnlocked;
    }

    private void OnDisable()
    {
        GameEvents.OnTurretUnlocked -= HandleTurretUnlocked;
        GameEvents.OnBulletUnlocked -= HandleBulletUnlocked;
    }

    private void HandleTurretUnlocked(object sender, TurretBlueprintSO turret)
    {
        if (turret == null || availableTurrets.Contains(turret)) return;
        
        availableTurrets.Add(turret);
        GenerateShopItem(turret, SelectTurret);
    }

    private void HandleBulletUnlocked(object sender, BulletBlueprintSO bullet)
    {
        if (bullet == null || availableBullets.Contains(bullet)) return;
        
        availableBullets.Add(bullet);
        GenerateShopItem(bullet, SelectBullet);
    }

    // Generic helper for unified instantiation logic
    private void GenerateShopItem<T>(T itemData, System.Action<T> selectAction) where T : ScriptableObject
    {
        if (shopItemPrefab == null || shopItemsContainer == null) return;
        
        GameObject itemObj = Instantiate(shopItemPrefab, shopItemsContainer);
        ShopItemUI itemUI = itemObj.GetComponent<ShopItemUI>();
        if (itemUI != null)
        {
            if (typeof(T) == typeof(TurretBlueprintSO))
                itemUI.Setup(itemData as TurretBlueprintSO, selectAction as System.Action<TurretBlueprintSO>);
            else if (typeof(T) == typeof(BulletBlueprintSO))
                itemUI.Setup(itemData as BulletBlueprintSO, selectAction as System.Action<BulletBlueprintSO>);
        }
    }

    private void Start()
    {
        buildManager = BuildManager.instance;
        PopulateShop();
        
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void Update()
    {
        if (buildManager == null) return;

        bool hasSelection = buildManager.HasTurretSelection || buildManager.HasBulletSelection;
        
        if (cancelButton != null && cancelButton.gameObject.activeSelf != hasSelection)
        {
            cancelButton.gameObject.SetActive(hasSelection);
        }
    }

    private void OnCancelClicked()
    {
        SoundEvents.TriggerCancelButtonClicked(this);
        if (buildManager != null)
        {
            buildManager.ClearTurretSelection();
            buildManager.ClearBulletSelection();
        }
    }
}

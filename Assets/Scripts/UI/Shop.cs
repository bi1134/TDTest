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

            GameObject itemObj = Instantiate(shopItemPrefab, shopItemsContainer);
            ShopItemUI itemUI = itemObj.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(turret, SelectTurret);
            }
        }

        // Generate Bullets
        foreach (var bullet in availableBullets)
        {
            if (bullet == null) continue;

            GameObject itemObj = Instantiate(shopItemPrefab, shopItemsContainer);
            ShopItemUI itemUI = itemObj.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(bullet, SelectBullet);
            }
        }
    }

    public void SelectTurret(TurretBlueprintSO turret)
    {
        print("Turret Selected: " + turret.turretName);
        buildManager.SelectTurret(turret);
    }

    public void SelectBullet(BulletBlueprintSO bullet)
    {
        print("Bullet Selected: " + bullet.bulletName);
        buildManager.SelectBullet(bullet);
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
        if (buildManager != null)
        {
            buildManager.ClearTurretSelection();
            buildManager.ClearBulletSelection();
        }
    }
}

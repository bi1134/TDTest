using UnityEngine;
using System.Collections.Generic;

public class Shop : MonoBehaviour
{
    public static Shop Instance { get; private set; }

    [Header("Shop Config")]
    public List<TurretBlueprintSO> availableTurrets;
    public List<BulletBlueprintSO> availableBullets;
    public GameObject shopItemPrefab;
    public Transform shopItemsContainer;

    [Header("Cancel Selection")]
    [SerializeField] private UnityEngine.UI.Button cancelButton;

    private BuildManager buildManager;

    // --- Inventory: tracks count + UI per blueprint ---
    private class ShopEntry
    {
        public ScriptableObject blueprint;
        public ShopItemUI ui;
        public int count;
        public bool removeWhenEmpty;
    }

    private readonly Dictionary<ScriptableObject, ShopEntry> inventory = new();

    // --- ShopItemUI Pool ---
    private readonly Stack<ShopItemUI> uiPool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        buildManager = BuildManager.instance;
        PopulateShop();

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void PopulateShop()
    {
        if (shopItemPrefab == null || shopItemsContainer == null)
        {
            Debug.LogWarning("Shop: Missing references for dynamic generation.");
            return;
        }

        foreach (var turret in availableTurrets)
        {
            if (turret == null) continue;
            AddTurretItem(turret, 1, turret.removeWhenEmpty);
        }

        foreach (var bullet in availableBullets)
        {
            if (bullet == null) continue;
            AddBulletItem(bullet, 1, bullet.removeWhenEmpty);
        }
    }

    // ─── Public API for adding/consuming items ──────────────────────────────

    /// <summary>Add turret copies to the shop. Creates UI if new, increments count if existing.</summary>
    public void AddTurretItem(TurretBlueprintSO turret, int amount = 1, bool? overrideRemoveWhenEmpty = null)
    {
        if (turret == null) return;

        if (inventory.TryGetValue(turret, out var entry))
        {
            entry.count += amount;
            entry.ui.SetQuantity(entry.removeWhenEmpty ? entry.count : 100);
            return;
        }

        bool removable = overrideRemoveWhenEmpty ?? turret.removeWhenEmpty;
        var ui = GetOrCreateUI();
        ui.Setup(turret, SelectTurret);
        ui.SetQuantity(removable ? amount : 100); // 100 → shows x99+

        inventory[turret] = new ShopEntry
        {
            blueprint = turret,
            ui = ui,
            count = amount,
            removeWhenEmpty = removable
        };
    }

    /// <summary>Add bullet copies to the shop. Creates UI if new, increments count if existing.</summary>
    public void AddBulletItem(BulletBlueprintSO bullet, int amount = 1, bool? overrideRemoveWhenEmpty = null)
    {
        if (bullet == null) return;

        if (inventory.TryGetValue(bullet, out var entry))
        {
            entry.count += amount;
            entry.ui.SetQuantity(entry.removeWhenEmpty ? entry.count : 100);
            return;
        }

        bool removable = overrideRemoveWhenEmpty ?? bullet.removeWhenEmpty;
        var ui = GetOrCreateUI();
        ui.Setup(bullet, SelectBullet);
        ui.SetQuantity(removable ? amount : 100);

        inventory[bullet] = new ShopEntry
        {
            blueprint = bullet,
            ui = ui,
            count = amount,
            removeWhenEmpty = removable
        };
    }

    /// <summary>Consume one copy of an item. Returns false if unavailable.</summary>
    public bool ConsumeItem(ScriptableObject blueprint)
    {
        if (blueprint == null) return false;
        if (!inventory.TryGetValue(blueprint, out var entry)) return false;
        if (entry.removeWhenEmpty && entry.count <= 0) return false;

        if (entry.removeWhenEmpty)
        {
            entry.count--;
            entry.ui.SetQuantity(entry.count);

            if (entry.count <= 0)
            {
                ReturnUIToPool(entry.ui);
                inventory.Remove(blueprint);
            }
        }
        // Non-removable items: count stays, always available

        return true;
    }

    /// <summary>Check if an item is available (has count > 0 or is unlimited).</summary>
    public bool HasItem(ScriptableObject blueprint)
    {
        if (blueprint == null) return false;
        if (!inventory.TryGetValue(blueprint, out var entry)) return false;
        return !entry.removeWhenEmpty || entry.count > 0;
    }

    /// <summary>Get the current count of an item. Returns -1 for unlimited.</summary>
    public int GetItemCount(ScriptableObject blueprint)
    {
        if (blueprint == null) return 0;
        if (!inventory.TryGetValue(blueprint, out var entry)) return 0;
        return entry.removeWhenEmpty ? entry.count : -1;
    }

    // ─── Selection ──────────────────────────────────────────────────────────

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

    // ─── Events ─────────────────────────────────────────────────────────────

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
        AddTurretItem(turret);
    }

    private void HandleBulletUnlocked(object sender, BulletBlueprintSO bullet)
    {
        AddBulletItem(bullet);
    }

    // ─── UI Pooling ─────────────────────────────────────────────────────────

    private ShopItemUI GetOrCreateUI()
    {
        ShopItemUI ui;
        if (uiPool.Count > 0)
        {
            ui = uiPool.Pop();
            ui.gameObject.SetActive(true);
            ui.transform.SetAsLastSibling(); // maintain layout order
        }
        else
        {
            var obj = Instantiate(shopItemPrefab, shopItemsContainer);
            ui = obj.GetComponent<ShopItemUI>();
        }
        return ui;
    }

    private void ReturnUIToPool(ShopItemUI ui)
    {
        if (ui == null) return;
        ui.ResetForPool();
        ui.gameObject.SetActive(false);
        uiPool.Push(ui);
    }

    // ─── Cancel / Update ────────────────────────────────────────────────────

    private void Update()
    {
        if (buildManager == null) return;

        bool hasSelection = buildManager.HasTurretSelection || buildManager.HasBulletSelection;

        if (cancelButton != null && cancelButton.gameObject.activeSelf != hasSelection)
            cancelButton.gameObject.SetActive(hasSelection);
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

using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class TurretUpgradeUI : MonoBehaviour
{
    public static TurretUpgradeUI Instance { get; private set; }

    [Header("Main Settings")]
    public GameObject uiPanel;
    public TMP_Text turretNameText;
    public Button sellButton;
    public TMP_Text sellButtonText;
    public Button closeButton;
    // Backdrop button removed
    
    [Header("Positioning Settings")]
    [Tooltip("If true, converts world position to screen space (Use Canvas: Screen Space - Overlay). If false, uses absolute world position (Use Canvas: World Space).")]
    public bool useScreenSpace = false; 
    public Vector3 targetOffset = new Vector3(2, 0, 0);
    
    [Header("Smart Avoidance")]
    public bool enableSmartPositioning = true;
    public LayerMask obstacleLayer; 
    public float checkRadius = 1.0f;
    
    private Vector3 currentActiveOffset;

    // [Header("Turret Reference")] 
    private TurretBaseModule targetModule; 
    
    [Header("Tabs (Panels)")]
    // User wants both visible, so we treat these as panels to toggle or keep active
    public GameObject upgradePanel;
    public GameObject statsPanel;
    public GameObject infoPanel;

    [Header("Common Info")]
    public TMP_Text levelText;
    public TMP_Text descriptionText;

    [Header("XP / Free Points Panel")]
    [Tooltip("Parent panel for XP bar + free points text. Hidden entirely when no free points and no XP to show.")]
    public GameObject freePointsPanel;
    public TMP_Text freePointsText;
    public Image xpFillBar;

    [Header("Turret Info Panel")]
    public Image turretIconImage;
    [Tooltip("Parent transform for 3D turret mesh preview.")]
    public Transform turretModelContainer;

    private GameObject currentTurretModelInstance;

    [Header("Bullet Info Panel")]
    public GameObject bulletInfoPanel;
    public TMP_Text bulletNameText;
    public TMP_Text bulletDescriptionText;
    public Image bulletIconImage;
    [Tooltip("Parent transform for 3D bullet mesh preview.")]
    public Transform bulletModelContainer;

    private GameObject currentBulletModelInstance;

    // Accessors for other systems
    public bool HasTarget => targetModule != null;
    public Vector3 GetTargetPosition() => targetModule != null ? targetModule.transform.position : Vector3.zero;

    // --- Manual Reference Classes ---
    
    [System.Serializable]
    public class UpgradeRowReferences
    {
        public GameObject rowObject;
        // public TMP_Text valueText; // Removed as per request (redundant)
        public TMP_Text costText;
        [Tooltip("Separate TMP for showing the discounted price ($0) when stat points are available")]
        public TMP_Text discountText;
        public Button upgradeButton;
    }

    [System.Serializable]
    public class StatDisplayReferences
    {
        public GameObject rowObject;
        // labelText removed as per request
        public TMP_Text valueText;
    }

    [Header("Upgrade Tab Rows")]
    public UpgradeRowReferences damageUpgrade;
    public UpgradeRowReferences fireRateUpgrade;
    public UpgradeRowReferences rangeUpgrade;
    public UpgradeRowReferences bulletsPerTapUpgrade;
    public UpgradeRowReferences burstCountUpgrade;
    public UpgradeRowReferences beamDurationUpgrade;
    public UpgradeRowReferences shotIntervalUpgrade; 

    [Header("Stats Tab Rows")]
    public StatDisplayReferences damageStat;
    public StatDisplayReferences fireRateStat;
    public StatDisplayReferences rangeStat;
    public StatDisplayReferences bulletsPerTapStat;
    public StatDisplayReferences burstCountStat;
    public StatDisplayReferences beamDurationStat;
    public StatDisplayReferences shotIntervalStat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (uiPanel != null) uiPanel.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(() => { SoundEvents.TriggerCancelButtonClicked(this); Close(); });
        // Backdrop listener removed
        if (sellButton != null) sellButton.onClick.AddListener(() => { SoundEvents.TriggerButtonClicked(this); OnSellClicked(); });

        // Ensure panels are active if assigned (Simultaneous Display)
        if (upgradePanel != null) upgradePanel.SetActive(true);
        if (statsPanel != null) statsPanel.SetActive(true);
        if (infoPanel != null) infoPanel.SetActive(true);

        // Create Listeners for Upgrade Buttons
        BindUpgradeButton(damageUpgrade, TurretBaseModule.StatType.Damage);
        BindUpgradeButton(fireRateUpgrade, TurretBaseModule.StatType.FireRate);
        BindUpgradeButton(rangeUpgrade, TurretBaseModule.StatType.Range);
        BindUpgradeButton(bulletsPerTapUpgrade, TurretBaseModule.StatType.BulletsPerTap);
        BindUpgradeButton(burstCountUpgrade, TurretBaseModule.StatType.BurstCount);
        BindUpgradeButton(beamDurationUpgrade, TurretBaseModule.StatType.BeamDuration);
        BindUpgradeButton(beamDurationUpgrade, TurretBaseModule.StatType.BeamDuration);
        BindUpgradeButton(shotIntervalUpgrade, TurretBaseModule.StatType.BeamShotInterval);

        if (targetingButton != null)
        {
            targetingButton.onClick.AddListener(() => { SoundEvents.TriggerButtonClicked(this); OnTargetingClicked(); });
        }
    }

    private void OnEnable()
    {
        GameEvents.OnAugmentSelected += OnAugmentSelected;
    }

    private void OnDisable()
    {
        GameEvents.OnAugmentSelected -= OnAugmentSelected;
    }

    private void OnAugmentSelected(object sender, GameEvents.AugmentSelectedEventArgs e)
    {
        // Only refresh if we are currently showing a turret
        if (targetModule != null)
        {
            RefreshUI();
        }
    }
    
    [Header("Targeting UI")]
    public Button targetingButton;
    public TMP_Text targetingText;

    public void OnTargetingClicked()
    {
        if (targetModule == null) return;
        
        Turret turret = targetModule.GetComponentInParent<Turret>();
        if (turret == null) return;

        // Cycle Mode
        int current = (int)turret.targetingMode;
        int count = System.Enum.GetValues(typeof(Turret.TargetingMode)).Length;
        int next = (current + 1) % count;
        
        turret.targetingMode = (Turret.TargetingMode)next;
        
        RefreshTargetingUI(turret);
    }

    private void RefreshTargetingUI(Turret turret)
    {
        if (targetingText != null && turret != null)
        {
            targetingText.text = $"Target: {turret.targetingMode}";
        }
    }
    
    [Header("Interaction Settings")]
    public LayerMask closeOnClickLayers; // Added as requested

    private void Update()
    {
        // "Click Outside" to Close Logic
        // Use new Input System
        if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame && uiPanel.activeSelf) 
        {
            Debug.Log($"[TurretUI] Pointer Pressed! MousePos: {UnityEngine.InputSystem.Pointer.current.position.ReadValue()}");

            // Enhanced Logic: Only block if the HIT object is ACTUALLY on the UI Layer (Layer 5)
            // This prevents "Default" or 3D objects with PhysicsRaycasters from blocking the input.
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                 UnityEngine.EventSystems.PointerEventData pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                 {
                     position = UnityEngine.InputSystem.Pointer.current.position.ReadValue()
                 };

                 System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                 UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

                 // Check if any result is on the "UI" layer (Index 5)
                 // Or a child of a Canvas? Usually checking layer is safest/simplest.
                 // "Default" layer (0) should NOT block.
                 bool blockedByActualUI = false;
                 
                 foreach(var result in results)
                 {
                     if (result.gameObject.layer == LayerMask.NameToLayer("UI")) 
                     {
                         blockedByActualUI = true;
                         // Debug.Log($"[TurretUI] BLOCKED by UI: {result.gameObject.name}");
                         break;
                     }
                 }

                 if (blockedByActualUI)
                 {
                     return;
                 }
                 else
                 {
                     // Debug.Log("[TurretUI] Pointer over EventSystem object (e.g. 3D Collider), but NOT 'UI' layer. Proceeding.");
                 }
            }

            // Raycast to check what we hit
            Vector2 mousePos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
            
            if (Camera.main == null) { Debug.LogError("Camera.main is NULL!"); return; }

            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log($"[TurretUI] Ray Hit: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");

                // If the hit object is in the "Close On Click" layers -> Close
                if (((1 << hit.collider.gameObject.layer) & closeOnClickLayers) != 0)
                {
                    Debug.Log("[TurretUI] Hit Close Layer - Closing UI");
                    Close();
                    return;
                }
                else
                {
                    Debug.Log("[TurretUI] Hit Object NOT in Close Layer - Ignoring (Passthrough)");
                }
            }
            else
            {
                 Debug.Log("[TurretUI] Ray Hit Nothing (Sky) - Closing UI");
                 Close();
            }
        }
    }

    private void LateUpdate()
    {
        if (targetModule != null && uiPanel.activeSelf)
        {
            if (useScreenSpace)
            {
                // Screen Space Overlay Setup
                Vector3 screenPos = Camera.main.WorldToScreenPoint(targetModule.transform.position + currentActiveOffset);
                if (screenPos.z < 0) 
                {
                    uiPanel.transform.position = new Vector3(-1000, -1000, 0); 
                }
                else
                {
                    uiPanel.transform.position = screenPos;
                }
            }
            else
            {
                // World Space Setup
                transform.position = targetModule.transform.position + currentActiveOffset;
                
                if (Camera.main != null)
                {
                    transform.rotation = Camera.main.transform.rotation;
                }
            }
        }
    }
    
    private void BindUpgradeButton(UpgradeRowReferences row, TurretBaseModule.StatType type)
    {
        if (row != null && row.upgradeButton != null)
        {
            row.upgradeButton.onClick.AddListener(() => { SoundEvents.TriggerButtonClicked(this); OnUpgradeClicked(type); });
        }
    }

    public void SetTarget(TurretBaseModule turret)
    {
        // Disable old range visual if switching targets
        if (targetModule != null)
        {
            Turret oldTurret = targetModule.GetComponentInParent<Turret>();
            if (oldTurret != null) oldTurret.SetRangeVisual(false);
        }

        targetModule = turret;
        targetModule.AcknowledgeLevelUp();
        CalculateSmartOffset();
        // Tell PlacementSystem to position the selection ring
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.PositionSelectionRing(targetModule);
        }
        uiPanel.SetActive(true);
        
        // Auto-Focus Camera on Open
        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FocusOn(turret.transform.position);

        // Update Targeting & Range UI
        Turret t = turret.GetComponentInParent<Turret>();
        if (t != null)
        {
             RefreshTargetingUI(t);
             t.SetRangeVisual(true);
             if (targetingButton != null) targetingButton.gameObject.SetActive(true);
        }
        else
        {
             if (targetingButton != null) targetingButton.gameObject.SetActive(false);
        }

        RefreshUI();
    }
    
    private void CalculateSmartOffset()
    {
        currentActiveOffset = targetOffset; 

        if (!enableSmartPositioning || targetModule == null) return;

        Vector3[] candidates = new Vector3[]
        {
            targetOffset,                    
            new Vector3(-targetOffset.x, targetOffset.y, targetOffset.z), 
            new Vector3(0, targetOffset.y, 2f),  
            new Vector3(0, targetOffset.y, -2f),
            new Vector3(0, 3, 0)             
        };

        foreach (Vector3 offset in candidates)
        {
            Vector3 worldPos = targetModule.transform.position + offset;
            
            // Check for colliders at this position
            Collider[] hits = Physics.OverlapSphere(worldPos, checkRadius, obstacleLayer);
            
            bool safe = true;
            foreach (var hit in hits)
            {
                // Ignore the target itself and children
                if (hit.transform != targetModule.transform && !hit.transform.IsChildOf(targetModule.transform))
                {
                    safe = false;
                    break;
                }
            }
            
            if (safe)
            {
                currentActiveOffset = offset;
                return;
            }
        }
    }

    public void Close()
    {
        uiPanel.SetActive(false);
        ClearTurretModel();
        ClearBulletModel();
        if (targetModule != null)
        {
            Turret t = targetModule.GetComponentInParent<Turret>();
            if (t != null) t.SetRangeVisual(false);
        }
        targetModule = null;

        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.HideSelectionRing();
        }

        // NOTE: Do NOT clear turret selection here - player may have just
        // closed this panel via Shop.SelectTurret and needs the selection to place on a Node.
    }



    public void RefreshUI()
    {
        if (targetModule == null) return;

        if (turretNameText != null) turretNameText.text = targetModule.turretName;
        // Simplified Level Text as requested: "Lv. 5" instead of "Lv. 5/100"
        if (levelText != null) levelText.text = $"Lv. {targetModule.currentLevel}";
        if (descriptionText != null) descriptionText.text = targetModule.description;

        // Free points panel (hide entire panel including background when no points)
        bool hasFreePoints = targetModule.freeUpgradePoints > 0;
        if (freePointsPanel != null)
            freePointsPanel.SetActive(hasFreePoints);

        if (freePointsText != null)
        {
            freePointsText.gameObject.SetActive(hasFreePoints);
            if (hasFreePoints)
                freePointsText.text = $"Stat Points: {targetModule.freeUpgradePoints}";
        }

        // XP fill bar
        if (xpFillBar != null)
        {
            int currentXP = targetModule.TotalExperience;
            int currentLevelXP = targetModule.CurrentLevelXP;
            int nextXP = targetModule.NextLevelXP;
            float xpRange = nextXP - currentLevelXP;
            xpFillBar.fillAmount = xpRange > 0 ? (currentXP - currentLevelXP) / xpRange : 0f;
        }

        // Turret Info Panel (3D model or icon)
        RefreshTurretInfoPanel();

        // Sell Button
        int sellValue = targetModule.GetSellValue();
        if (sellButtonText != null) sellButtonText.text = $"(+${sellValue})";

        // === UPGRADE TAB & STATS TAB POPULATION ===
        TurretPropertiesSO stats = targetModule.GetTurretProperties();
        
        // Damage & Fire Rate (Every turret has these)
        UpdateUpgradeRow(damageUpgrade, GetDetailedStat(stats.damage, AugmentType.Damage), targetModule.GetUpgradeCost(TurretBaseModule.StatType.Damage));
        UpdateStatRow(damageStat, "Damage", GetDetailedStat(stats.damage, AugmentType.Damage));

        UpdateUpgradeRow(fireRateUpgrade, GetDetailedStat(stats.fireRate, AugmentType.FireRate), targetModule.GetUpgradeCost(TurretBaseModule.StatType.FireRate));
        UpdateStatRow(fireRateStat, "Fire Rate", GetDetailedStat(stats.fireRate, AugmentType.FireRate));
        
        // Range (Component check)
        var turret = targetModule.GetComponentInParent<Turret>();
        if (turret != null)
        {
            UpdateUpgradeRow(rangeUpgrade, GetDetailedStat(turret.range, AugmentType.Range), targetModule.GetUpgradeCost(TurretBaseModule.StatType.Range));
            UpdateStatRow(rangeStat, "Range", GetDetailedStat(turret.range, AugmentType.Range));
        }
        else
        {
             HideUpgradeRow(rangeUpgrade);
             HideStatRow(rangeStat);
        }
        
        // Fire Mode Conditionals
        bool isMulti = stats.fireMode == FireMode.MultiShot || stats.fireMode == FireMode.Single; 
        bool isBurst = stats.fireMode == FireMode.Burst;
        bool isBeam = stats.fireMode == FireMode.Beam;

        SetRowVisibility(bulletsPerTapUpgrade, bulletsPerTapStat, isMulti, "Bullets/Tap", stats.bulletsPerTap.ToString("F0"), targetModule.GetUpgradeCost(TurretBaseModule.StatType.BulletsPerTap));
        SetRowVisibility(burstCountUpgrade, burstCountStat, isBurst, "Burst Count", stats.burstCount.ToString("F0"), targetModule.GetUpgradeCost(TurretBaseModule.StatType.BurstCount));
        
        // Beam Stuff
        SetRowVisibility(shotIntervalUpgrade, shotIntervalStat, isBeam, "Interval", stats.beamShotInterval.ToString("F2"), targetModule.GetUpgradeCost(TurretBaseModule.StatType.BeamShotInterval));
        SetRowVisibility(beamDurationUpgrade, beamDurationStat, isBeam, "Duration", stats.beamDuration.ToString("F1"), targetModule.GetUpgradeCost(TurretBaseModule.StatType.BeamDuration));

        // Bullet Info Panel
        RefreshBulletInfoPanel();
    }

    private void SetRowVisibility(UpgradeRowReferences upRow, StatDisplayReferences statRow, bool active, string label, string val, int cost)
    {
        if (active)
        {
            UpdateUpgradeRow(upRow, val, cost);
            UpdateStatRow(statRow, label, val);
        }
        else
        {
            HideUpgradeRow(upRow);
            HideStatRow(statRow);
        }
    }

    private void UpdateUpgradeRow(UpgradeRowReferences row, string value, int cost)
    {
        if (row == null) return;
        if (row.rowObject != null) row.rowObject.SetActive(true);

        bool hasFreePoints = targetModule.freeUpgradePoints > 0;
        bool capped = targetModule.currentLevel >= targetModule.maxLevel;

        if (row.costText != null)
        {
            if (hasFreePoints)
            {
                // Crossed-out old price, dimmed via component alpha
                row.costText.text = $"<s>${cost}</s>";
                Color c = row.costText.color;
                c.a = 0.5f;
                row.costText.color = c;
            }
            else
            {
                row.costText.text = $"${cost}";
                Color c = row.costText.color;
                c.a = 1f;
                row.costText.color = c;
            }
        }

        // Separate discount text component (controlled in Inspector for font size etc.)
        if (row.discountText != null)
        {
            if (hasFreePoints)
            {
                row.discountText.gameObject.SetActive(true);
                row.discountText.text = "$0";
            }
            else
            {
                row.discountText.gameObject.SetActive(false);
            }
        }

        if (row.upgradeButton != null)
        {
            if (capped)
                row.upgradeButton.interactable = false;
            else if (hasFreePoints)
                row.upgradeButton.interactable = true;
            else
                row.upgradeButton.interactable = PlayerStats.wallet >= cost;
        }
    }

    private void UpdateStatRow(StatDisplayReferences row, string label, string value)
    {
        if (row == null) return;
        if (row.rowObject != null) row.rowObject.SetActive(true);
        // labelText assignment removed
        if (row.valueText != null) row.valueText.text = value;
    }
    
    private void HideUpgradeRow(UpgradeRowReferences row) { if (row?.rowObject != null) row.rowObject.SetActive(false); }
    private void HideStatRow(StatDisplayReferences row) { if (row?.rowObject != null) row.rowObject.SetActive(false); }

    public void OnUpgradeClicked(TurretBaseModule.StatType type)
    {
        if (targetModule == null) return;
        
        targetModule.UpgradeStat(type);
        RefreshUI(); // Update values and buttons

        // Immediately update the range ring so it reflects the new range without re-opening the UI
        Turret t = targetModule.GetComponentInParent<Turret>();
        if (t != null) t.UpdateRangeVisuals();

        GameUIEvent.MoneyChanged(BuildManager.instance, PlayerStats.wallet); 
    }

    public void OnSellClicked()
    {
        Debug.Log("Sell Clicked! " + (targetModule != null ? targetModule.name : "Null Target"));
        if (targetModule != null)
        {
            targetModule.Sell();
        }
    }

    [Space(10)]
    [Header("Detailed Stats UI Colors")]
    public Color percentBonusColor = new Color(1f, 0.65f, 0f); // Orange default
    public Color flatBonusColor = Color.green;

    private string GetDetailedStat(float baseVal, AugmentType type)
    {
        float mult = UpgradesManager.GetStatMultiplier(type);
        float flat = UpgradesManager.GetStatFlatBonus(type);
        
        float finalVal = (baseVal * mult) + flat;
        
        // Format: "Final (Base +% +Flat)"
        
        if (Mathf.Approximately(mult, 1f) && Mathf.Approximately(flat, 0f))
        {
            return finalVal.ToString("F1");
        }
        
        string breakdown = "(";
        
        if (mult > 1.001f)
        {
            float pct = (mult - 1f) * 100f;
            string colorHex = "#" + ColorUtility.ToHtmlStringRGB(percentBonusColor);
            breakdown += $"<color={colorHex}>+{pct:F0}%</color>";
        }
        
        if (flat > 0.001f)
        {
             // Add space if there is already a percent
            if (breakdown.Length > 1) breakdown += " ";
            string colorHex = "#" + ColorUtility.ToHtmlStringRGB(flatBonusColor);
            breakdown += $"<color={colorHex}>+{flat:F1}</color>";
        }
        
        breakdown += ")";
        
        return $"{finalVal:F1} {breakdown}";
    }

    // ─── Turret Info Panel Helpers ─────────────────────────────────────────

    private void RefreshTurretInfoPanel()
    {
        var turretBP = targetModule.InstalledTurretBlueprint;
        if (turretBP == null) return;

        SetupTurretModel(turretBP);
    }

    private void SetupTurretModel(TurretBlueprintSO bp)
    {
        ClearTurretModel();

        if (turretModelContainer != null && bp.displayMesh != null)
        {
            currentTurretModelInstance = Instantiate(bp.displayMesh, turretModelContainer);
            currentTurretModelInstance.transform.localPosition = Vector3.zero;
            currentTurretModelInstance.transform.localRotation = Quaternion.identity;

            if (currentTurretModelInstance.GetComponent<ModelSpinner>() == null)
                currentTurretModelInstance.AddComponent<ModelSpinner>();

            SetLayerRecursively(currentTurretModelInstance, LayerMask.NameToLayer("GUI"));

            if (turretIconImage != null) turretIconImage.enabled = false;
        }
        else
        {
            if (turretIconImage != null)
            {
                turretIconImage.enabled = true;
                turretIconImage.sprite = bp.icon;
            }
        }
    }

    private void ClearTurretModel()
    {
        if (currentTurretModelInstance != null)
        {
            Destroy(currentTurretModelInstance);
            currentTurretModelInstance = null;
        }
    }

    // ─── Bullet Info Panel Helpers ──────────────────────────────────────────

    private void RefreshBulletInfoPanel()
    {
        if (bulletInfoPanel == null) return;

        var bulletBP = targetModule.InstalledBulletBlueprint;
        if (bulletBP == null)
        {
            bulletInfoPanel.SetActive(false);
            ClearBulletModel();
            return;
        }

        bulletInfoPanel.SetActive(true);
        if (bulletNameText != null) bulletNameText.text = bulletBP.bulletName;
        if (bulletDescriptionText != null) bulletDescriptionText.text = bulletBP.description;
        SetupBulletModel(bulletBP);
    }

    private void SetupBulletModel(BulletBlueprintSO bp)
    {
        ClearBulletModel();

        if (bulletModelContainer != null && bp.displayMesh != null)
        {
            currentBulletModelInstance = Instantiate(bp.displayMesh, bulletModelContainer);
            currentBulletModelInstance.transform.localPosition = Vector3.zero;
            currentBulletModelInstance.transform.localRotation = Quaternion.identity;

            if (currentBulletModelInstance.GetComponent<ModelSpinner>() == null)
                currentBulletModelInstance.AddComponent<ModelSpinner>();

            // Set layer recursively for world UI camera rendering
            SetLayerRecursively(currentBulletModelInstance, LayerMask.NameToLayer("UI"));

            if (bulletIconImage != null) bulletIconImage.enabled = false;
        }
        else
        {
            if (bulletIconImage != null)
            {
                bulletIconImage.enabled = true;
                bulletIconImage.sprite = bp.icon;
            }
        }
    }

    private void ClearBulletModel()
    {
        if (currentBulletModelInstance != null)
        {
            Destroy(currentBulletModelInstance);
            currentBulletModelInstance = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}


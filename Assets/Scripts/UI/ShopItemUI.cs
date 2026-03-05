using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ShopItemUI : MonoBehaviour, IPointerEnterHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button button;

    [Header("3D Preview")]
    [Tooltip("Parent transform for 3D mesh preview. Mesh is spawned as child.")]
    [SerializeField] private Transform modelContainer;

    private TurretBlueprintSO turretBlueprint;
    private BulletBlueprintSO bulletBlueprint;
    private GameObject currentModelInstance;

    public TurretBlueprintSO TurretBlueprint => turretBlueprint;
    public BulletBlueprintSO BulletBlueprint => bulletBlueprint;

    public void Setup(TurretBlueprintSO turret, System.Action<TurretBlueprintSO> onSelect)
    {
        turretBlueprint = turret;
        bulletBlueprint = null;

        if (nameText != null) nameText.text = turret.turretName;
        if (costText != null) costText.text = "$" + turret.cost;

        SetupModelOrIcon(turret.displayMesh, turret.icon);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            SoundEvents.TriggerButtonClicked(this);
            onSelect?.Invoke(turretBlueprint);
        });
    }

    public void Setup(BulletBlueprintSO bullet, System.Action<BulletBlueprintSO> onSelect)
    {
        bulletBlueprint = bullet;
        turretBlueprint = null;

        if (nameText != null) nameText.text = bullet.bulletName;
        if (costText != null) costText.text = "$" + bullet.cost;

        SetupModelOrIcon(bullet.displayMesh, bullet.icon);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            SoundEvents.TriggerButtonClicked(this);
            onSelect?.Invoke(bulletBlueprint);
        });
    }

    /// <summary>
    /// Update the quantity display. count > 99 shows "x99+".
    /// </summary>
    public void SetQuantity(int count)
    {
        if (quantityText == null) return;
        quantityText.text = count > 99 ? "x99+" : $"x{count}";
    }

    /// <summary>
    /// Reset state when returning to pool.
    /// </summary>
    public void ResetForPool()
    {
        turretBlueprint = null;
        bulletBlueprint = null;
        button.onClick.RemoveAllListeners();
        ClearModel();
    }

    private void SetupModelOrIcon(GameObject displayMesh, Sprite icon)
    {
        ClearModel();

        if (modelContainer != null && displayMesh != null)
        {
            currentModelInstance = Instantiate(displayMesh, modelContainer);
            currentModelInstance.transform.localPosition = Vector3.zero;
            currentModelInstance.transform.localRotation = Quaternion.identity;

            // Match the layer of the UI so it renders on the correct camera
            SetLayerRecursively(currentModelInstance, modelContainer.gameObject.layer);

            if (iconImage != null) iconImage.enabled = false;
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = icon;
            }
        }
    }

    private void ClearModel()
    {
        if (currentModelInstance != null)
        {
            Destroy(currentModelInstance);
            currentModelInstance = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SoundEvents.TriggerButtonHovered(this);
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ShopItemUI : MonoBehaviour, IPointerEnterHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText; // New reference for Name
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button button;

    private TurretBlueprintSO turretBlueprint;
    private BulletBlueprintSO bulletBlueprint; // Optional: if bullets are also SOs

    public void Setup(TurretBlueprintSO turret, System.Action<TurretBlueprintSO> onSelect)
    {
        turretBlueprint = turret;
        
        if (iconImage != null) iconImage.sprite = turret.icon;
        if (nameText != null) nameText.text = turret.turretName;
        if (costText != null) costText.text = "$" + turret.cost;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => 
        {
            SoundEvents.TriggerButtonClicked(this);
            onSelect?.Invoke(turretBlueprint);
        });
    }
    
    // Overload for Bullets if needed
    public void Setup(BulletBlueprintSO bullet, System.Action<BulletBlueprintSO> onSelect)
    {
        bulletBlueprint = bullet;
        
        if (iconImage != null) iconImage.sprite = bullet.icon;
        if (nameText != null) nameText.text = bullet.bulletName;
        if (costText != null) costText.text = "$" + bullet.cost;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => 
        {
            SoundEvents.TriggerButtonClicked(this);
            onSelect?.Invoke(bulletBlueprint);
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SoundEvents.TriggerButtonHovered(this);
    }
}

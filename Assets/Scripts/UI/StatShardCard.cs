using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Component for displaying an active randomized Stat Shard.
/// </summary>
public class StatShardCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button cardButton;

    [Header("Rarity Colors (Optional)")]
    [SerializeField] private Color commonColor = Color.grey;
    [SerializeField] private Color uncommonColor = new Color(0.2f, 0.8f, 0.2f); // Light Green
    [SerializeField] private Color rareColor = Color.blue;
    [SerializeField] private Color epicColor = new Color(0.6f, 0f, 0.8f); // Purple
    [SerializeField] private Color legendaryColor = new Color(1f, 0.8f, 0f); // Gold

    private UpgradesManager.ActiveStatShard currentShard;
    private System.Action<UpgradesManager.ActiveStatShard> onCardSelected;

    /// <summary>
    /// Initialize card with a rolled stat shard.
    /// </summary>
    public void Initialize(UpgradesManager.ActiveStatShard shard, System.Action<UpgradesManager.ActiveStatShard> onSelected)
    {
        currentShard = shard;
        onCardSelected = onSelected;

        if (shard == null || shard.shardDef == null)
        {
            Debug.LogWarning("[StatShardCard] Tried to initialize with null shard data!");
            gameObject.SetActive(false);
            return;
        }

        var def = shard.shardDef;

        if (iconImage != null)
        {
            iconImage.sprite = def.icon;
            iconImage.gameObject.SetActive(def.icon != null);
        }

        if (nameText != null)
        {
            nameText.text = def.shardName;
        }

        if (descriptionText != null)
        {
            string sign = shard.rolledValue >= 0 ? "+" : "";
            string percentStr = def.isPercentage ? "%" : "";
            var bounds = def.GetBounds(shard.rarity);
            
            // Format: +21% fire rate (11% to 22%)
            // We use def.statType.ToString() or a mapped string. For now, ToString() is decent.
            string typeString = def.statType.ToString();
            
            // Add spaces to CamelCase enum (e.g. FireRate -> Fire Rate)
            typeString = System.Text.RegularExpressions.Regex.Replace(typeString, "([a-z])([A-Z])", "$1 $2").ToLower();

            descriptionText.text = $"{sign}{shard.rolledValue}{percentStr} {typeString} ({bounds.min}{percentStr} to {bounds.max}{percentStr})";
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = GetRarityColor(shard.rarity);
        }

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }

        gameObject.SetActive(true);
    }

    private void OnCardClicked()
    {
        if (currentShard != null && onCardSelected != null)
        {
            SoundEvents.TriggerCardClicked(this);
            Debug.Log($"[StatShardCard] Card clicked: {currentShard.shardDef.shardName}");
            // Disable button to prevent double clicks
            if (cardButton != null) cardButton.interactable = false;
            
            onCardSelected.Invoke(currentShard);
        }
    }

    private Color GetRarityColor(AugmentRarity rarity)
    {
        return rarity switch
        {
            AugmentRarity.Common => commonColor,
            AugmentRarity.Uncommon => uncommonColor,
            AugmentRarity.Rare => rareColor,
            AugmentRarity.Epic => epicColor,
            AugmentRarity.Legendary => legendaryColor,
            _ => commonColor
        };
    }
}


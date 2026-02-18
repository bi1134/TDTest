using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Augment Card - Individual card UI component.
/// Displays augment data and handles click events.
/// </summary>
public class AugmentCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button cardButton;
    [SerializeField] private Button rerollButton; // New Reroll Button

    [Header("Rarity Colors (Optional)")]
    [SerializeField] private Color commonColor = Color.grey;
    [SerializeField] private Color rareColor = Color.blue;
    [SerializeField] private Color epicColor = new Color(0.6f, 0f, 0.8f); // Purple
    [SerializeField] private Color legendaryColor = new Color(1f, 0.8f, 0f); // Gold

    private AugmentSO currentAugment;
    private System.Action<AugmentSO> onCardSelected;
    private System.Action<AugmentCard> onCardRerolled; // New Callback

    private void Awake()
    {
        // Listeners assigned in Initialize to handle recycling
    }

    /// <summary>
    /// Initialize card with augment data.
    /// </summary>
    public void Initialize(AugmentSO augment, System.Action<AugmentSO> onSelected, System.Action<AugmentCard> onRerolled)
    {
        currentAugment = augment;
        onCardSelected = onSelected;
        onCardRerolled = onRerolled;

        if (augment == null)
        {
            Debug.LogWarning("[AugmentCard] Tried to initialize with null augment!");
            gameObject.SetActive(false);
            return;
        }

        // Populate UI
        if (iconImage != null)
        {
            iconImage.sprite = augment.icon;
            iconImage.gameObject.SetActive(augment.icon != null);
        }

        if (nameText != null)
        {
            nameText.text = augment.augmentName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = augment.description;
        }

        // Set rarity color
        if (backgroundImage != null)
        {
            backgroundImage.color = GetRarityColor(augment.rarity);
        }

        // Setup Buttons
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnRerollClicked);
        }

        gameObject.SetActive(true);
    }

    private void OnCardClicked()
    {
        if (currentAugment != null && onCardSelected != null)
        {
            Debug.Log($"[AugmentCard] Card clicked: {currentAugment.augmentName}");
            onCardSelected.Invoke(currentAugment);
        }
    }

    private void OnRerollClicked()
    {
        if (onCardRerolled != null)
        {
            Debug.Log($"[AugmentCard] Reroll clicked for: {currentAugment.augmentName}");
            // Disable button to prevent multiple rerolls
            if (rerollButton != null)
            {
                rerollButton.interactable = false;
            }
            onCardRerolled.Invoke(this);
        }
    }

    private Color GetRarityColor(AugmentRarity rarity)
    {
        return rarity switch
        {
            AugmentRarity.Common => commonColor,
            AugmentRarity.Rare => rareColor,
            AugmentRarity.Epic => epicColor,
            AugmentRarity.Legendary => legendaryColor,
            _ => commonColor
        };
    }
}

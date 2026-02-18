using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Augment Selection UI - Shows 3 augment cards for player to choose from.
/// Pauses game while showing cards, resumes after selection.
/// </summary>
public class AugmentSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelContainer; // The panel to show/hide
    [SerializeField] private Transform cardParent; // Parent for instantiated cards (e.g., HorizontalLayoutGroup)
    [SerializeField] private AugmentCard cardPrefab; // Prefab to instantiate

    [Header("Manager Reference")]
    [SerializeField] private AugmentManager augmentManager;

    [Header("Debug")]
    [SerializeField] private bool logSelection = true;

    private List<AugmentCard> spawnedCards = new List<AugmentCard>();
    private List<AugmentSO> currentAugmentOptions = new List<AugmentSO>();

    private void Start()
    {
        // Subscribe to augment selection event
        GameEvents.OnAugmentSelectionStarted += HandleAugmentSelectionStarted;

        // Ensure panel starts hidden
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnAugmentSelectionStarted -= HandleAugmentSelectionStarted;
    }

    private void HandleAugmentSelectionStarted(object sender, GameEvents.AugmentSelectionStartedEventArgs e)
    {
        if (logSelection)
        {
            Debug.Log($"[AugmentSelectionUI] Showing {e.options.Count} augment options");
        }

        ShowCards(e.options);
    }

    private void ShowCards(List<AugmentSO> options)
    {
        // Validate
        if (cardPrefab == null || cardParent == null)
        {
            Debug.LogError("[AugmentSelectionUI] Card prefab or parent not assigned!");
            return;
        }

        // Store options for reroll logic
        currentAugmentOptions = new List<AugmentSO>(options);

        // Clear any existing cards
        ClearCards();

        // Pause game directly (don't use PauseManager to avoid showing pause UI)
        Time.timeScale = 0f;

        // Show panel
        if (panelContainer != null)
        {
            panelContainer.SetActive(true);
        }

        // Instantiate and initialize cards
        foreach (var option in options)
        {
            AugmentCard card = Instantiate(cardPrefab, cardParent);
            card.Initialize(option, OnCardSelected, OnCardRerolled);
            spawnedCards.Add(card);
        }
    }

    private void OnCardSelected(AugmentSO selectedAugment)
    {
        if (logSelection)
        {
            Debug.Log($"[AugmentSelectionUI] Player selected: {selectedAugment.augmentName}");
        }

        // Tell AugmentManager to apply
        if (augmentManager != null)
        {
            augmentManager.ApplyAugment(selectedAugment);
        }

        // Hide panel
        HidePanel();

        // Resume game directly
        Time.timeScale = 1f;
    }

    private void OnCardRerolled(AugmentCard cardToReroll)
    {
        if (cardToReroll == null) return;

        // 1. Collect currently displayed augments to exclude them
        // (We don't want to reroll into an augment that's already shown on another card)
        List<AugmentSO> currentOptionsForExclusion = new List<AugmentSO>(currentAugmentOptions);

        // 2. Get new unique augment
        AugmentSO newAugment = augmentManager.GetRandomAugment(currentOptionsForExclusion); // Use augmentManager instance

        if (newAugment != null)
        {
            // Find the index of this card in spawnedCards
            int index = spawnedCards.IndexOf(cardToReroll);
            if (index != -1)
            {
                // Update the currentAugmentOptions list so future rerolls know about this new augment
                currentAugmentOptions[index] = newAugment;

                // Re-initialize the card
                cardToReroll.Initialize(newAugment, OnCardSelected, OnCardRerolled);

                Debug.Log($"[AugmentSelectionUI] Rerolled card {index} -> {newAugment.augmentName}");
            }
        }
        else
        {
             Debug.LogWarning("[AugmentSelectionUI] Reroll failed - no valid unique augments left!");
        }
    }

    private void HidePanel()
    {
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }
        
        // Clear cards after hiding
        ClearCards();
    }

    private void ClearCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        spawnedCards.Clear();
    }
}

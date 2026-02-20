using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Upgrade Selection UI - General UI for showing either Augments or Stat Shards.
/// Hooked into the SelectionQueueManager to prevent overlapping popups.
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelContainer; 
    [SerializeField] private Transform cardParent; 

    [Header("Prefabs")]
    [SerializeField] private AugmentCard augmentCardPrefab;
    [SerializeField] private StatShardCard statShardCardPrefab;

    [Header("Manager Reference")]
    [SerializeField] private UpgradesManager UpgradesManager;

    [Header("Debug")]
    [SerializeField] private bool logSelection = true;

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<AugmentSO> currentAugmentOptions = new List<AugmentSO>();

    private void Start()
    {
        GameEvents.OnAugmentSelectionStarted += HandleAugmentSelectionStarted;
        GameEvents.OnStatShardSelectionStarted += HandleStatShardSelectionStarted;

        if (panelContainer != null) panelContainer.SetActive(false);
    }

    private void OnDestroy()
    {
        GameEvents.OnAugmentSelectionStarted -= HandleAugmentSelectionStarted;
        GameEvents.OnStatShardSelectionStarted -= HandleStatShardSelectionStarted;
    }

    private void HandleAugmentSelectionStarted(object sender, GameEvents.AugmentSelectionStartedEventArgs e)
    {
        if (logSelection) Debug.Log($"[UpgradeSelectionUI] Showing {e.options.Count} augment options");
        ShowAugmentCards(e.options);
    }

    private void HandleStatShardSelectionStarted(object sender, GameEvents.StatShardSelectionStartedEventArgs e)
    {
        if (logSelection) Debug.Log($"[UpgradeSelectionUI] Showing {e.options.Count} stat shard options");
        ShowStatShardCards(e.options);
    }

    private void ShowAugmentCards(List<AugmentSO> options)
    {
        Debug.Log($"[UpgradeSelectionUI] ShowAugmentCards called! Prefab assign check: AugmentPrefab={augmentCardPrefab!=null}, Parent={cardParent!=null}");
        if (augmentCardPrefab == null || cardParent == null) return;
        
        currentAugmentOptions = new List<AugmentSO>(options);
        ClearCards();

        if (panelContainer != null) panelContainer.SetActive(true);

        foreach (var option in options)
        {
            AugmentCard card = Instantiate(augmentCardPrefab, cardParent);
            card.Initialize(option, OnAugmentCardSelected, OnAugmentCardRerolled);
            spawnedCards.Add(card.gameObject);
        }
    }

    private void ShowStatShardCards(List<UpgradesManager.ActiveStatShard> options)
    {
        Debug.Log($"[UpgradeSelectionUI] ShowStatShardCards called! Prefab assign check: ShardPrefab={statShardCardPrefab!=null}, Parent={cardParent!=null}");
        if (statShardCardPrefab == null || cardParent == null) return;
        
        ClearCards();
        if (panelContainer != null) panelContainer.SetActive(true);

        foreach (var option in options)
        {
            StatShardCard card = Instantiate(statShardCardPrefab, cardParent);
            card.Initialize(option, OnStatShardCardSelected);
            spawnedCards.Add(card.gameObject);
        }
    }

    private void OnAugmentCardSelected(AugmentSO selectedAugment)
    {
        if (UpgradesManager != null) UpgradesManager.ApplyAugment(selectedAugment);
        CompleteSelection();
    }

    private void OnStatShardCardSelected(UpgradesManager.ActiveStatShard selectedShard)
    {
        if (UpgradesManager != null) UpgradesManager.ApplyStatShard(selectedShard);
        CompleteSelection();
    }

    private void OnAugmentCardRerolled(AugmentCard cardToReroll)
    {
        if (cardToReroll == null) return;

        List<AugmentSO> currentOptionsForExclusion = new List<AugmentSO>(currentAugmentOptions);
        AugmentSO newAugment = UpgradesManager.GetRandomAugment(currentOptionsForExclusion);

        if (newAugment != null)
        {
            int index = spawnedCards.FindIndex(x => x == cardToReroll.gameObject);
            if (index != -1)
            {
                currentAugmentOptions[index] = newAugment;
                cardToReroll.Initialize(newAugment, OnAugmentCardSelected, OnAugmentCardRerolled);
                if (logSelection) Debug.Log($"[UpgradeSelectionUI] Rerolled card {index} -> {newAugment.augmentName}");
            }
        }
    }

    private void CompleteSelection()
    {
        if (panelContainer != null) panelContainer.SetActive(false);
        ClearCards();

        // Tell Queue Manager we're done so it can show the next or resume the game
        if (SelectionQueueManager.Instance != null)
        {
            SelectionQueueManager.Instance.CompleteCurrentSelection();
        }
        else
        {
            // Fallback
            Time.timeScale = 1f;
        }
    }

    private void ClearCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();
    }
}


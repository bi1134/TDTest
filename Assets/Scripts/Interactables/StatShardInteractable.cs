using System.Collections.Generic;
using UnityEngine;

using UnityEngine.EventSystems;

/// <summary>
/// A physical orb/chest dropped by enemies. Collect it by clicking, which adds it to the Selection Queue.
/// </summary>
public class StatShardInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private bool collected = false;

    [Header("Hover Feedback")]
    [Tooltip("Material to swap to when hovered, or simply tint if left null")]
    [SerializeField] private Color hoverTint = Color.yellow;
    private Color originalColor;
    private Renderer objRenderer;

    private void Start()
    {
        objRenderer = GetComponentInChildren<Renderer>();
        if (objRenderer != null && objRenderer.material != null)
        {
            originalColor = objRenderer.material.color;
        }

        // Auto-collect at end of wave
        GameEvents.OnWaveCompleted += HandleWaveCompleted;
    }

    private void OnDestroy()
    {
        GameEvents.OnWaveCompleted -= HandleWaveCompleted;
    }

    private void HandleWaveCompleted(object sender, GameEvents.WaveCompletedEventArgs e)
    {
        CollectOrb();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (collected || objRenderer == null) return;
        
        // Skip logic if event data is hitting UI (EventSystem check built-in)
        objRenderer.material.color = hoverTint;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (collected || objRenderer == null) return;
        objRenderer.material.color = originalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (collected) return;
        CollectOrb();
    }

    /// <summary>
    /// Adds 3 random shard choices to the Queue Manager.
    /// </summary>
    public void CollectOrb()
    {
        if (collected) return;
        collected = true;

        if (SelectionQueueManager.Instance != null && UpgradesManager.Instance != null)
        {
            // Get 3 random rolled shards from the manager
            List<UpgradesManager.ActiveStatShard> choices = UpgradesManager.Instance.GetRandomStatShardChoices(3);
            
            if (choices.Count > 0)
            {
                SelectionQueueManager.Instance.EnqueueStatShardSelection(choices);
            }
        }
        
        Destroy(gameObject);
    }
}


using System.Collections.Generic;
using UnityEngine;

using UnityEngine.EventSystems;

/// <summary>
/// A physical orb/chest dropped by enemies. Collect it by clicking, which adds it to the Selection Queue.
/// </summary>
public class StatShardInteractable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [HideInInspector] public GameObject SourcePrefab { get; set; }

    private bool collected = false;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private void Start()
    {
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
        if (collected) return;
        
        // Skip logic if event data is hitting UI (EventSystem check built-in)
        if (animator != null) animator.SetBool("Open", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (collected) return;
        if (animator != null) animator.SetBool("Open", false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (collected) return;
        CollectOrb();
    }

    public void ResetShard()
    {
        collected = false;
        
        // Trigger Appear animation if Animator is present
        if (animator != null)
        {
            animator.SetTrigger("Appear");
            // Reset 'Open' state
            animator.SetBool("Open", false);
        }
    }

    /// <summary>
    /// Adds 3 random shard choices to the Queue Manager.
    /// </summary>
    public void CollectOrb()
    {
        if (collected) return;
        collected = true;

        if (animator != null)
        {
            animator.SetBool("Open", true);
            StartCoroutine(CollectSequence(1.2f)); // Adjust time to match your Open animation length
        }
        else
        {
            StartCoroutine(CollectSequence(0f)); // Instant collection if no animator
        }
    }
    
    private System.Collections.IEnumerator CollectSequence(float delaySeconds)
    {
        // 1. Immediately queue the UI to pop up (No delay!)
        if (SelectionQueueManager.Instance != null && UpgradesManager.Instance != null)
        {
            // Get 3 random rolled shards from the manager
            List<UpgradesManager.ActiveStatShard> choices = UpgradesManager.Instance.GetRandomStatShardChoices(3);
            
            if (choices.Count > 0)
            {
                SelectionQueueManager.Instance.EnqueueStatShardSelection(choices);
            }
        }
        
        // 2. Wait for the Open animation to finish playing
        yield return Helpers.GetWaitForSecond(delaySeconds);

        // 3. Play disappear effects
        SoundEvents.TriggerEnemyPoof(this, transform.position, null); // reuse poof event for shard (plays 2D default poof)
        VFXManager.Instance?.PlayEffect(VFXType.ItemDisappear, transform.position);

        // 4. Return the physical object back to the pool
        if (StatShardPoolManager.Instance != null)
        {
            StatShardPoolManager.Instance.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a queue of UI selection screens (e.g., Augments, Stat Shards)
/// to prevent them from overlapping if triggered simultaneously.
/// </summary>
public class SelectionQueueManager : MonoBehaviour
{
    public static SelectionQueueManager Instance { get; private set; }

    public enum SelectionType { Augment, StatShard }

    public class QueuedSelection
    {
        public SelectionType Type;
        public List<AugmentSO> AugmentOptions;
        public List<UpgradesManager.ActiveStatShard> ShardOptions;
    }

    private Queue<QueuedSelection> selectionQueue = new Queue<QueuedSelection>();
    private bool isSelectionActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Adds an Augment Selection screen to the queue.
    /// </summary>
    public void EnqueueAugmentSelection(List<AugmentSO> options)
    {
        Debug.Log($"[SelectionQueueManager] Enqueuing Augment Selection. Queue size before: {selectionQueue.Count}");
        selectionQueue.Enqueue(new QueuedSelection 
        { 
            Type = SelectionType.Augment, 
            AugmentOptions = options 
        });
        
        CheckQueue();
    }

    /// <summary>
    /// Adds a Stat Shard Selection screen to the queue.
    /// </summary>
    public void EnqueueStatShardSelection(List<UpgradesManager.ActiveStatShard> options)
    {
        Debug.Log($"[SelectionQueueManager] Enqueuing Stat Shard Selection. Queue size before: {selectionQueue.Count}");
        selectionQueue.Enqueue(new QueuedSelection 
        { 
            Type = SelectionType.StatShard, 
            ShardOptions = options 
        });
        
        CheckQueue();
    }

    private void CheckQueue()
    {
        Debug.Log($"[SelectionQueueManager] CheckQueue called. isSelectionActive: {isSelectionActive}, Queue count: {selectionQueue.Count}");
        if (isSelectionActive) return; // Wait for current UI to finish
        if (selectionQueue.Count == 0) return; // Nothing to show

        isSelectionActive = true;
        
        // Pause the game when starting queue resolution
        if (Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
        }

        QueuedSelection next = selectionQueue.Dequeue();
        
        if (next.Type == SelectionType.Augment)
        {
            // This triggers the UI to actually open
            GameEvents.TriggerAugmentSelectionStarted(this, next.AugmentOptions);
        }
        else if (next.Type == SelectionType.StatShard)
        {
            // This triggers the UI to actually open
            GameEvents.TriggerStatShardSelectionStarted(this, next.ShardOptions);
        }
    }

    /// <summary>
    /// Must be called by the Selection UI when it closes (e.g. after a choice is made).
    /// </summary>
    public void CompleteCurrentSelection()
    {
        isSelectionActive = false;
        
        if (selectionQueue.Count > 0)
        {
            // Show the next UI immediately
            CheckQueue(); 
        }
        else
        {
            // All queues resolved, resume game
            Time.timeScale = 1f;
        }
    }
}


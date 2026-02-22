using UnityEngine;

/// <summary>
/// Controls the Enemy Animator based on state read directly from Enemy.cs.
/// Uses Animator.StringToHash for performance.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Animator component here (can be on a child object)")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Drag the Enemy component here")]
    [SerializeField] private Enemy enemy;

    // Cached Animator Hashes
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isStunnedHash = Animator.StringToHash("IsStunned");
    private readonly int hasBarrierHash = Animator.StringToHash("HasBarrier");
    private readonly int dieHash = Animator.StringToHash("Die");

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (enemy == null) enemy = GetComponentInParent<Enemy>();

        if (animator != null)
        {
            defaultLocalPos = animator.transform.localPosition;
            defaultLocalRot = animator.transform.localRotation;
        }
    }

    private void OnEnable()
    {
        if (enemy != null)
        {
            enemy.OnDeath += TriggerDeathAnimation;
            enemy.OnRespawn += ResetAnimator;
        }
    }

    private void OnDisable()
    {
        if (enemy != null)
        {
            enemy.OnDeath -= TriggerDeathAnimation;
            enemy.OnRespawn -= ResetAnimator;
        }
    }

    private void Update()
    {
        if (enemy == null || animator == null) return;
        if (!enemy.IsAlive) return;

        UpdateAnimatorParameters();
    }

    private void UpdateAnimatorParameters()
    {
        // Calculate animation speed based on movement speed. 
        // If spawning, visually they shouldn't be running.
        float currentSpeed = enemy.IsSpawning ? 0 : enemy.CurrentSpeed;
        animator.SetFloat(speedHash, currentSpeed);

        // Adjust Animator playback speed based on slowness (Ice traps)
        // If default speed is 3, and current is 1.5, playback should be half speed
        if (enemy.baseSpeed > 0 && currentSpeed > 0)
        {
            animator.speed = currentSpeed / enemy.baseSpeed;
        }
        else
        {
            // Reset to default play speed for static/idle animations
            animator.speed = 1f; 
        }

        // States
        animator.SetBool(isStunnedHash, enemy.IsStunned);
        animator.SetBool(hasBarrierHash, enemy.HasBarrier);
    }

    private void TriggerDeathAnimation()
    {
        // Force speed to 0 so 'Any State -> Walk' condition becomes false and doesn't interrupt death!
        animator.SetFloat(speedHash, 0f);
        
        // Just in case they died while slowed, reset the animator speed so they die at normal speed
        animator.speed = 1f;
        animator.SetTrigger(dieHash);
    }

    private void ResetAnimator()
    {
        // Clear the death trigger so it doesn't instantly jump back to Death from Any State
        animator.ResetTrigger(dieHash);
        
        // Rebind resets the properties and state machine to default
        animator.Rebind();
        animator.Update(0f);

        // Explicitly restore the exact local position/rotation it had on Awake.
        // This fixes Generic animations that move the root (like laying down)
        // without ruining prefabs that have a natural Y-offset (like -0.5y).
        animator.transform.localPosition = defaultLocalPos;
        animator.transform.localRotation = defaultLocalRot;
    }

    /// <summary>
    /// To be called by an Animation Event at the end of the Jump/Drop "Spawn" animation
    /// </summary>
    public void FinishSpawning()
    {
        if (enemy != null)
        {
            enemy.FinishSpawning();
        }
    }
}

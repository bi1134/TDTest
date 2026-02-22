using UnityEngine;
using System.Collections.Generic;

using System;

/// <summary>
/// GDD Enemy Properties:
/// - Health, Speed, ShieldHP
/// - Path-following only
/// - Shield HP absorbs damage before normal health
/// - Debuff system: Slow, Fire DOT, Vulnerability, Electric stun
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("GDD Enemy Properties")]
    public float maxHealth = 100f;
    public float baseSpeed = 3f;
    [Tooltip("Damage dealt to player lives when reaching the end")]
    public int damageToPlayer = 1;
    
    [Header("Shield HP (GDD: separate health bar)")]
    [Tooltip("Shield HP absorbs incoming damage before normal health")]
    public float maxShieldHP = 0f;
    
    [Header("Reward")]
    [SerializeField] private int moneyReward = 10;

    [Header("Drops")]
    public GameObject statShardPrefab;
    [Range(0f, 1f)] public float statShardDropChance = 0.05f; // 5% chance

    [Header("Runtime State")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentShieldHP;
    [SerializeField] private float currentSpeed;
    
    /// <summary>
    /// GDD: Some enemies reduce damage from frontal direct-fire attacks (Barrier)
    /// </summary>
    [Header("Barrier (GDD: directional damage reduction)")]
    public float maxBarrierHP = 0f;
    [Range(0f, 1f)]
    [Tooltip("Damage reduction from frontal attacks (0 = no reduction, 1 = full block)")]
    public float barrierReduction = 0.5f;

    [Header("Visuals")]
    [Tooltip("If true, hits on unshielded enemy play Blood. If false, they play DefaultHit sparks.")]
    public bool isFleshy = true;

    [Header("Active Debuffs (Runtime)")]
    [SerializeField] private float slowMultiplier = 1f;
    [SerializeField] private float vulnerabilityMultiplier = 1f;
    [SerializeField] private float fireDOTDamagePerSecond = 0f;
    [SerializeField] private float fireDOTRemainingTime = 0f;
    [SerializeField] private bool isStunned = false;
    [SerializeField] private float stunRemainingTime = 0f;
    [SerializeField] private float currentBarrierHP = 0f;
    [SerializeField] private bool isSpawning = true;

    // Object Pooling
    public GameObject SourcePrefab { get; set; }

    // Public Events
    public event Action OnDeath;
    public event Action OnRespawn;

    // Public accessors
    public float CurrentHealth => currentHealth;
    public float CurrentShieldHP => currentShieldHP;
    public float CurrentSpeed => currentSpeed;
    public float SlowMultiplier => slowMultiplier;
    public float VulnerabilityMultiplier => vulnerabilityMultiplier;
    public bool IsStunned => isStunned;
    public bool IsAlive => currentHealth > 0;
    public bool HasActiveShield => currentShieldHP > 0;
    public bool HasBarrier => currentBarrierHP > 0;
    public bool IsSpawning => isSpawning;


    // Navigation
    private List<Vector3> pathWaypoints;
    private int targetWaypointIndex;
    private float reachThreshold = 0.5f;
    
    // Cached references
    private Collider col;

    public Vector3 TargetPoint => col != null ? col.bounds.center : transform.position + Vector3.up * 0.5f;

    protected virtual void Start()
    {
        col = GetComponentInChildren<Collider>();
        currentHealth = maxHealth;
        currentBarrierHP = maxBarrierHP;
        // Optional: currentShieldHP = maxShieldHP; if desired
    }

    /// <summary>
    /// Called by EnemyAnimator when the initial drop/jump animation completes
    /// </summary>
    public void FinishSpawning()
    {
        isSpawning = false;
    }

    /// <summary>
    /// Resets the enemy to its pristine spawned state for Object Pooling.
    /// </summary>
    public virtual void ResetEnemy()
    {
        // Restore Health / Shields
        currentHealth = maxHealth;
        currentBarrierHP = maxBarrierHP;
        currentShieldHP = maxShieldHP;

        // Clear Debuffs
        slowMultiplier = 1f;
        vulnerabilityMultiplier = 1f;
        fireDOTDamagePerSecond = 0f;
        fireDOTRemainingTime = 0f;
        isStunned = false;
        stunRemainingTime = 0f;

        // Reset Spawn State
        isSpawning = true;

        // Re-enable Colliders
        if (col != null) col.enabled = true;

        // Stop any currently running coroutines (like slow expiration)
        StopAllCoroutines();

        // Cancel pending Invokes (like Death Poof or ReturnToPool)
        CancelInvoke();

        // Notify subscribers (like Animator) to reset their state
        OnRespawn?.Invoke();
    }

    /// <summary>
    /// Initialize enemy with a path to follow
    /// </summary>
    public void SetPath(List<Vector3> newPath)
    {
        pathWaypoints = newPath;
        targetWaypointIndex = 0;
        
        if (pathWaypoints != null && pathWaypoints.Count > 0)
        {
            Debug.Log($"[Enemy] Path set with {pathWaypoints.Count} waypoints. First: {pathWaypoints[0]}");
            // Optional: teleport to start if just spawned?
            // transform.position = pathWaypoints[0];
        }
        else
        {
             Debug.LogWarning("[Enemy] SetPath called with empty path!");
        }
    }

    protected virtual void Update()
    {
        // Update stats
        UpdateCurrentSpeed();
        ProcessFireDOT();
        ProcessStun();
        
        // Move
        if (!isStunned && !isSpawning && IsAlive)
        {
            MoveAlongPath();
        }
    }

    protected virtual void MoveAlongPath()
    {
        if (pathWaypoints == null || targetWaypointIndex >= pathWaypoints.Count) return;

        Vector3 targetPos = pathWaypoints[targetWaypointIndex];
        Vector3 dir = targetPos - transform.position;
        // Flatten Y for 2D movement logic in 3D world (if needed)
        dir.y = 0; 

        float dist = dir.magnitude;

        if (dist <= reachThreshold)
        {
            // Reached waypoint
            targetWaypointIndex++;
            if (targetWaypointIndex >= pathWaypoints.Count)
            {
                ReachEnd();
            }
        }
        else
        {
            // Move
            transform.Translate(dir.normalized * currentSpeed * Time.deltaTime, Space.World);
            
            // Rotate to face direction
            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Lerp(transform.rotation, lookRot, Time.deltaTime * 10f);
            }
        }
    }

    /// <summary>
    /// Calculates approximate distance to the end of the path.
    /// Used for "First" and "Last" targeting modes.
    /// </summary>
    public float GetDistanceToGoal()
    {
        if (pathWaypoints == null || targetWaypointIndex >= pathWaypoints.Count) return 0f;

        // Distance to next waypoint
        float dist = Vector3.Distance(transform.position, pathWaypoints[targetWaypointIndex]);

        // Add remaining segments
        for (int i = targetWaypointIndex; i < pathWaypoints.Count - 1; i++)
        {
            dist += Vector3.Distance(pathWaypoints[i], pathWaypoints[i + 1]);
        }

        return dist;
    }

    protected virtual void ReachEnd()
    {
        // Damage Player
        PlayerStats.Lives -= damageToPlayer;
        
        Debug.Log($"Enemy reached the end! Dealt {damageToPlayer} damage.");
        Die();
    }

    protected virtual void Die()
    {
        if (currentHealth <= 0 && currentSpeed > 0) 
            currentSpeed = 0; // Precaution
            
        Debug.Log($"{gameObject.name} has died!");
        SoundEvents.TriggerEnemyDeath(this);
        
        // Initial blood splatter (or metal spark) on death
        if (isFleshy)
        {
            VFXManager.Instance?.PlayEffect(VFXType.Blood, transform.position);
        }
        else
        {
            VFXManager.Instance?.PlayEffect(VFXType.DefaultHit, transform.position);
        }
        
        // Disable physics/colliders so it stops blocking rays and bullets
        if (col != null) col.enabled = false;
        
        // Roll for Stat Shard Drop
        if (statShardPrefab != null && UnityEngine.Random.value <= statShardDropChance)
        {
            Instantiate(statShardPrefab, transform.position, Quaternion.identity);
        }

        // Assuming PlayerStats and WaveManager are accessible
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnEnemyDeath();
        }

        // Add Money Reward
        PlayerStats.wallet += moneyReward;
        Debug.Log($"Rewarded {moneyReward} money. New Balance: {PlayerStats.wallet}");

        // Trigger animation event and delay destroy
        OnDeath?.Invoke();
        
        // Ensure it doesn't stay alive in logic
        currentHealth = 0;
        
        // Spawn poof right before destruction (2 seconds from now)
        Invoke(nameof(SpawnDeathPoof), 1.95f);
        
        // Delay pool return 2 seconds for animation to play
        Invoke(nameof(ReturnToPool), 2f);
    }
    
    private void ReturnToPool()
    {
        if (EnemyPoolManager.Instance != null && SourcePrefab != null)
        {
            EnemyPoolManager.Instance.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void SpawnDeathPoof()
    {
        VFXManager.Instance?.PlayEffect(VFXType.DeathPoof, transform.position);
    }


    private void UpdateCurrentSpeed()
    {
        if (isStunned)
        {
            currentSpeed = 0f;
        }
        else
        {
            currentSpeed = baseSpeed * slowMultiplier;
        }
    }

    private void ProcessFireDOT()
    {
        if (fireDOTRemainingTime > 0 && fireDOTDamagePerSecond > 0)
        {
            fireDOTRemainingTime -= Time.deltaTime;
            TakeDamageRaw(fireDOTDamagePerSecond * Time.deltaTime);
            
            if (fireDOTRemainingTime <= 0)
            {
                fireDOTDamagePerSecond = 0f;
            }
        }
    }

    private void ProcessStun()
    {
        if (stunRemainingTime > 0)
        {
            stunRemainingTime -= Time.deltaTime;
            if (stunRemainingTime <= 0)
            {
                isStunned = false;
            }
        }
    }

    #region Damage Methods

    /// <summary>
    /// Take damage with GDD damage rules:
    /// - Shield HP absorbs damage first
    /// - Vulnerability affects normal HP only
    /// </summary>
    public void TakeDamage(float damage, bool bypassShield = false, Vector3 hitSourcePos = default)
    {
        if (!IsAlive) return;

        float remainingDamage = damage;

        // 1. Barrier Reduction & Damage
        if (currentBarrierHP > 0 && hitSourcePos != default)
        {
            // Simple frontal check (dot product > 0.5 ~ 60 degrees front cone)
            Vector3 dirToSource = (hitSourcePos - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dirToSource) > 0.5f)
            {
                // Attack hit the barrier!
                // Barrier mitigates damage for the health pool
                float mitigatedDamage = remainingDamage * barrierReduction;
                remainingDamage -= mitigatedDamage;
                
                // But the Barrier itself takes the brute force of the attack
                if (damage >= currentBarrierHP)
                {
                    currentBarrierHP = 0; // Barrier breaks!
                    VFXManager.Instance?.PlayEffect(VFXType.ShieldSpark, transform.position, dirToSource);
                }
                else
                {
                    currentBarrierHP -= damage;
                }
            }
        }

        // 2. Shield HP absorbs remaining damage
        if (currentShieldHP > 0 && !bypassShield)
        {
            SoundEvents.TriggerEnemyHit(this, true); // Shield Hit
            
            // Spawn Spark if hit source is known
            if (hitSourcePos != default)
            {
                Vector3 dirToSource = (hitSourcePos - transform.position).normalized;
                VFXManager.Instance?.PlayEffect(VFXType.ShieldSpark, transform.position, dirToSource);
            }

            if (remainingDamage >= currentShieldHP)
            {
                remainingDamage -= currentShieldHP;
                currentShieldHP = 0;
            }
            else
            {
                currentShieldHP -= remainingDamage;
                remainingDamage = 0;
            }
        }
        else if (remainingDamage > 0)
        {
            SoundEvents.TriggerEnemyHit(this, false); // Flesh Hit
            
            // Spawn Default Spark on normal hits (Blood is reserved for Death)
            if (hitSourcePos != default && !bypassShield)
            {
                Vector3 dirToSource = (hitSourcePos - transform.position).normalized;
                VFXManager.Instance?.PlayEffect(VFXType.DefaultHit, transform.position, dirToSource);
            }
        }

        // Apply vulnerability multiplier to normal health damage only
        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage * vulnerabilityMultiplier;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Raw damage that bypasses all modifiers (used for DOT)
    /// </summary>
    private void TakeDamageRaw(float damage)
    {
        if (!IsAlive) return;
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
    }

    #endregion

    #region Debuff Application Methods

    /// <summary>
    /// Apply slow debuff (GDD: Ice slow or Utility slow)
    /// </summary>
    /// <param name="slowPercent">Slow percentage (0-1), e.g., 0.3 = 30% slow</param>
    /// <param name="duration">Duration in seconds</param>
    public void ApplySlow(float slowPercent, float duration)
    {
        // Slow stacks by taking the strongest slow
        float newMultiplier = 1f - Mathf.Clamp01(slowPercent);
        if (newMultiplier < slowMultiplier)
        {
            slowMultiplier = newMultiplier;
        }
        
        // Start coroutine to remove slow after duration
        StartCoroutine(RemoveSlowAfterDelay(duration, newMultiplier));
        
        Debug.Log($"{gameObject.name}: Slowed by {slowPercent * 100}% for {duration}s. Current speed: {currentSpeed}");
    }

    private System.Collections.IEnumerator RemoveSlowAfterDelay(float delay, float appliedMultiplier)
    {
        yield return Helpers.GetWaitForSecond(delay);
        // Only remove if this was the active slow
        if (Mathf.Approximately(slowMultiplier, appliedMultiplier))
        {
            slowMultiplier = 1f;
        }
    }
    
    /// <summary>
    /// GDD: Vulnerability increases damage to normal health only (no Shield HP effect)
    /// </summary>
    /// <param name="vulnerabilityPercent">Extra damage percentage (0-1), e.g., 0.25 = 25% more damage</param>
    /// <param name="duration">Duration in seconds</param>
    public void ApplyVulnerability(float vulnerabilityPercent, float duration)
    {
        float newMultiplier = 1f + Mathf.Clamp01(vulnerabilityPercent);
        if (newMultiplier > vulnerabilityMultiplier)
        {
            vulnerabilityMultiplier = newMultiplier;
        }
        
        StartCoroutine(RemoveVulnerabilityAfterDelay(duration, newMultiplier));
        
        Debug.Log($"{gameObject.name}: Vulnerable! Taking {vulnerabilityPercent * 100}% extra damage for {duration}s");
    }

    private System.Collections.IEnumerator RemoveVulnerabilityAfterDelay(float delay, float appliedMultiplier)
    {
        yield return Helpers.GetWaitForSecond(delay);
        if (Mathf.Approximately(vulnerabilityMultiplier, appliedMultiplier))
        {
            vulnerabilityMultiplier = 1f;
        }
    }

    /// <summary>
    /// GDD: Shield Shred - Reduce Shield HP by percentage of max shield
    /// User spec: Shield Shred should decrease current Shield HP by 35% of total shield
    /// </summary>
    /// <param name="shredPercent">Percentage of MAX shield HP to remove (default 0.35 = 35%)</param>
    public void ApplyShieldShred(float shredPercent = 0.35f)
    {
        if (currentShieldHP <= 0) return;
        
        float shredAmount = maxShieldHP * shredPercent;
        currentShieldHP = Mathf.Max(0, currentShieldHP - shredAmount);
        
        Debug.Log($"{gameObject.name}: Shield Shred! Lost {shredAmount} shield HP. Remaining: {currentShieldHP}/{maxShieldHP}");
    }

    /// <summary>
    /// GDD: Fire - Damage over time
    /// </summary>
    /// <param name="damagePerSecond">DOT damage per second</param>
    /// <param name="duration">Duration in seconds</param>
    public void ApplyFireDOT(float damagePerSecond, float duration)
    {
        // Refresh or upgrade DOT
        if (damagePerSecond > fireDOTDamagePerSecond)
        {
            fireDOTDamagePerSecond = damagePerSecond;
        }
        fireDOTRemainingTime = Mathf.Max(fireDOTRemainingTime, duration);
        
        Debug.Log($"{gameObject.name}: Burning! {damagePerSecond} DPS for {duration}s");
    }

    /// <summary>
    /// GDD: Electric - Chain/stun effect
    /// </summary>
    /// <param name="duration">Stun duration in seconds</param>
    public void ApplyStun(float duration)
    {
        isStunned = true;
        stunRemainingTime = Mathf.Max(stunRemainingTime, duration);
        
        Debug.Log($"{gameObject.name}: Stunned for {duration}s!");
    }

    #endregion

    #region Barrier Methods

    /// <summary>
    /// Check if attack is from front (for Barrier mechanic)
    /// </summary>
    public bool IsAttackFromFront(Vector3 attackDirection)
    {
        if (!HasBarrier) return false;
        
        float dot = Vector3.Dot(transform.forward, -attackDirection.normalized);
        return dot > 0.5f; // Within ~60 degree frontal cone
    }

    /// <summary>
    /// Apply barrier reduction if attack is frontal
    /// </summary>
    public float ApplyBarrierReduction(float damage, Vector3 attackDirection)
    {
        if (IsAttackFromFront(attackDirection))
        {
            float reduced = damage * (1f - barrierReduction);
            Debug.Log($"{gameObject.name}: Barrier blocked! {damage} -> {reduced}");
            return reduced;
        }
        return damage;
    }

    #endregion



    #region Debug/Editor Helpers

    private void OnGUI()
    {
        // Simple debug UI in game view
        if (!IsAlive) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2);
        if (screenPos.z > 0)
        {
            float y = Screen.height - screenPos.y;
            
            // Health bar
            GUI.Label(new Rect(screenPos.x - 50, y, 100, 20), 
                $"HP: {currentHealth:F0}/{maxHealth}");
            
            // Shield bar
            if (maxShieldHP > 0)
            {
                GUI.Label(new Rect(screenPos.x - 50, y + 15, 100, 20), 
                    $"Shield: {currentShieldHP:F0}/{maxShieldHP}");
            }
            
            // Speed
            GUI.Label(new Rect(screenPos.x - 50, y + 30, 100, 20), 
                $"Speed: {currentSpeed:F1}");
            
            // Debuffs
            string debuffs = "";
            if (slowMultiplier < 1f) debuffs += "[SLOW] ";
            if (vulnerabilityMultiplier > 1f) debuffs += "[VULN] ";
            if (fireDOTRemainingTime > 0) debuffs += "[BURN] ";
            if (isStunned) debuffs += "[STUN] ";
            if (HasBarrier) debuffs += "[BARRIER] ";
            
            if (!string.IsNullOrEmpty(debuffs))
            {
                GUI.Label(new Rect(screenPos.x - 50, y + 45, 150, 20), debuffs);
            }
        }
    }

    #endregion

    // TODO: Path-following behavior (GDD: path-following only)
}

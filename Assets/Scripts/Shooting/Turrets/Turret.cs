using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TurretBaseModule baseModule;
    [SerializeField] public Transform partToRotate;
    [SerializeField] public TurretBarrelModule turretBarrel;

    [Header("Targeting")]
    public float range = 15f;
    public float rotationSpeed = 10f;
    public string enemyTag = "Enemy";

    private Transform target;

    // Phase I: Prediction Vars
    public float accuracyError = 0f; // Set from SO
    private Vector3 prevTargetPos;
    private Vector3 targetVelocity;
    
    // Optimization
    private float losCheckTimer = 0f;
    private bool isBlocked = false;
    private bool isTracking = true; // Allow tracking by default (e.g. Arc)

    //awake, hide the barrel until we bought a bullet type
    private void Awake()
    {
       // SetBarrelActive(false); // Removed per new design
    }

    private void Start()
    {
        // 0.25s Retarget Rate per user request
        InvokeRepeating(nameof(UpdateTarget), 0f, 0.25f);
    }

    private void FixedUpdate()
    {
        if (target != null)
        {
            Vector3 curPos = target.position;
            targetVelocity = (curPos - prevTargetPos) / Time.fixedDeltaTime;
            prevTargetPos = curPos;
        }
    }

    private void Update()
    {
        if (target == null)
        {
            baseModule.SetTarget(null); // Clear target in module
            return;
        }

        // --- Prediction Logic ---
        Vector3 aimPoint = target.position;
        TurretPropertiesSO props = baseModule.GetTurretProperties();
        
        if (props != null)
        {
            if (props.fireMode == FireMode.Arc)
            {
                // Arc Prediction
                // Use default gravity (9.81) or props.gravityScale if available? 
                // TurretProperties doesn't have gravityScale, BulletProperties does. 
                // Assuming standard gravity for now or approximations.
                // Arc Prediction - Fixed Height Logic
                aimPoint = PredictionHelpers.PredictArcInterception(
                    transform.position, 
                    target.position, 
                    targetVelocity, 
                    props.upwardForce, // Use upwardForce instead of speed
                    Physics.gravity.magnitude
                );
            }
            else if (props.bulletSpeed > 0)
            {
                // Linear Prediction
                aimPoint = PredictionHelpers.PredictPosition(transform.position, target.position, targetVelocity, props.bulletSpeed);
            }
            
            // LOS Check (only if not Arc)
            if (props.fireMode != FireMode.Arc)
            {
                // Visualization
                Debug.DrawLine(transform.position + Vector3.up, aimPoint, Color.yellow); // Aim Line
                
                Vector3 start = transform.position + Vector3.up * 0.5f;
                // Check LOS to actual target center
                Vector3 end = target.position + Vector3.up * 0.5f;
                
                // Throttled LOS Check
                losCheckTimer -= Time.deltaTime;
                if (losCheckTimer <= 0f)
                {
                    losCheckTimer = 0.15f; // Check every 0.15s
                    
                    // Cone Logic: 9 Rays Horizontal Spread
                    // Center 3 (Indices 3,4,5) = Lock In (Fire)
                    // Outer 3 (0,1,2 & 6,7,8) = Aware (Track only)
                    
                    int mask = ~LayerMask.GetMask("Bullet", "Ignore Raycast", "UI");
                    int clearCenterCount = 0;
                    int clearOuterCount = 0;
                    
                    Vector3 lookDir = (end - start).normalized;
                    Vector3 right = Vector3.Cross(lookDir, Vector3.up);
                    // float coneWidth = 2.0f; // Unused
                    // Let's use Angle. 
                    float angleStep = 5f; // 5 degrees per ray.
                    
                    for (int i = -4; i <= 4; i++)
                    {
                        // -4, -3, -2 (Outer Left)
                        // -1, 0, 1 (Center)
                        // 2, 3, 4 (Outer Right)
                        
                        Vector3 origin = start;
                        // Spread destination perpendicular to lookDir?
                        // Or Rotate direction?
                        // Rotating direction is better for Cone.
                        Vector3 rayDir = Quaternion.Euler(0, i * angleStep * 0.5f, 0) * lookDir; 
                        
                        // Debug visuals handled later? 
                        // Actually let's just Linecast to a spread of points at target distance
                        // Use target width? 
                        // User said "cone pattern".
                        
                        Vector3 rayEnd = start + rayDir * Vector3.Distance(start, end);
                        
                        bool rayClear = false;
                        if (Physics.Linecast(origin, rayEnd, out RaycastHit hit, mask))
                        {
                             if (hit.collider.gameObject == target.gameObject || hit.collider.transform.IsChildOf(target.transform) || hit.collider.CompareTag(enemyTag))
                             {
                                 rayClear = true;
                             }
                        }
                        else
                        {
                            // Didn't hit anything? Means clear path to infinity/range?
                            // If we didn't hit the target (because spread is wide), is it clear?
                            // If we didn't hit a WALL, it is 'Clear' in terms of obstruction.
                            // But we want to know if we see the TARGET.
                            // If the ray misses the target, it's not "seeing" it.
                            rayClear = false; // Rigid check: Must hit target.
                        }
                        
                        // Debug Rays
                        if (i == 0) Debug.DrawLine(origin, rayEnd, rayClear ? Color.green : Color.red, 0.15f); // Main
                        else Debug.DrawLine(origin, rayEnd, rayClear ? (Mathf.Abs(i) <= 1 ? Color.cyan : Color.gray) : Color.red, 0.15f);

                        if (rayClear)
                        {
                            if (Mathf.Abs(i) <= 1) clearCenterCount++;
                            else clearOuterCount++;
                        }
                    }
                    
                    // Logic:
                    // If Center has clear rays -> Fully Aware -> Fire allowed.
                    // If only Outer has clear rays -> Aware -> Track but Block Fire.
                    // If None -> Blocked -> Stop Tracking & Fire.
                    
                    if (clearCenterCount > 0)
                    {
                        isBlocked = false; // Fire Allowed
                        isTracking = true; // Can Track
                    }
                    else if (clearOuterCount > 0)
                    {
                         isBlocked = true; // Fire Blocked
                         isTracking = true; // But we are 'Aware', so we allow rotation below.
                    }
                    else
                    {
                        isBlocked = true; // Fire Blocked
                        isTracking = false; // Stop Tracking
                    }
                }
                
                // BLOCK FIRE if blocked
                if (isBlocked)
                {
                    baseModule.SetTarget(null); // Stop firing
                    // But if we want to rotate/track, we should SetTarget(target) but tell Module not to fire?
                    // The TurretBaseModule doesn't have "TrackOnly".
                    // For now, nulling target stops everything including rotation.
                    // If we want "Aware" behavior (rotate but don't shoot), we need to modify TurretBaseModule or just aim manually here.
                    // Turret.cs handles rotation in Update() INDEPENDENT of baseModule.SetTarget!
                    // Line 128: `if (dir != Vector3.zero) ...` uses `aimPoint` which is `target.position`.
                    // So rotation happens as long as `target` variable is set locally in Turret.cs!
                    // `baseModule.SetTarget(null)` ONLY stops the shooting logic in BaseModule.
                    // PERFECT!
                }
                else
                {
                     baseModule.SetTarget(target);
                }
            }
            else
            {
                // Arc Line
                Debug.DrawLine(transform.position, aimPoint, Color.magenta);
                baseModule.SetTarget(target); // Arc always fires (unless range check fails elsewhere)
                isTracking = true; // Arc turrets always track if target is in range
            }
        }

        // --- Rotate ---
        if (partToRotate != null && isTracking)
        {
            Vector3 dir = aimPoint - transform.position;
            // Flatten Y if needed? Most turrets rotate Y only, or look at target.
            // partToRotate usually rotates on Y axis. 
            // If we want full 3D aiming, we need more parts. 
            // Assuming simplified Y-rotation aiming for now, or full if LookRotation handles it.
            
            if (dir != Vector3.zero)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                Vector3 rot = Quaternion.Lerp(partToRotate.rotation, look, Time.deltaTime * rotationSpeed).eulerAngles;
                partToRotate.rotation = Quaternion.Euler(0f, rot.y, 0f); // Limit to Y axis for main body??
                // If partToRotate is the HEAD, it might pitch too?
                // The original code did: partToRotate.rotation = Quaternion.Euler(0f, rot.y, 0f);
                // I will preserve that behavior.
            }
        }

    }

    public enum TargetingMode
    {
        First,
        Last,
        Closest,
        Strongest,
        Weakest,
        Fastest,
        NearExit // Same as First
    }

    [Header("Targeting Mode")]
    public TargetingMode targetingMode = TargetingMode.Closest;
    private float retargetTimer = 0f;
    private const float RETARGET_INTERVAL = 0.25f;

    private void UpdateTarget()
    {
        // Retarget cooldown to prevent jitter
        // However, if current target is NULL or DEAD or OUT OF RANGE, we must retarget immediately.
        bool forceRetarget = (target == null || !target.gameObject.activeInHierarchy || Vector3.Distance(transform.position, target.position) > range);
        
        if (!forceRetarget)
        {
            retargetTimer -= 0.5f; // InvokeRepeating is 0.5s... wait, original code used InvokeRepeating 0.5f?
            // "InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);"
            // The user requested 0.1-0.25s. The current 0.5s is too slow!
            // I should probably change InvokeRepeating to 0.25f in Start() or manage timer in Update()
            // Let's rely on the InvokeRepeating for now but change the rate to 0.25f in Start().
            // If I change it here, I should update Start() too.
        }
        
        // Find ALL enemies
        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        
        GameObject bestCandidate = null;
        float bestScore = Mathf.Infinity; // Min is better for most
        // For Max types, we can negate value or use different var.

        // Pre-calculate Turret Pos
        Vector3 myPos = transform.position;

        foreach (var enemyGO in enemies)
        {
            Enemy enemyScript = enemyGO.GetComponent<Enemy>();
            if (enemyScript == null || !enemyScript.IsAlive) continue;

            float distToTurret = Vector3.Distance(myPos, enemyGO.transform.position);
            float effectiveRange = GetEffectiveRange();
            if (distToTurret > effectiveRange) continue;

            // --- LOS Check (Condensed) ---
            Vector3 start = myPos + Vector3.up * 1f; 
            Vector3 end = enemyGO.transform.position + Vector3.up * 1f;
            if (Physics.Linecast(start, end, out RaycastHit hit))
            {
                 if (hit.collider.gameObject != enemyGO && !hit.collider.CompareTag(enemyTag))
                 {
                     var props = baseModule.GetTurretProperties();
                     if (props != null && props.fireMode != FireMode.Arc) continue; // Blocked
                 }
            }

            // --- Scoring Logic ---
            float score = 0f;
            bool isMinBetter = true; // Default: Lower score is better

            switch (targetingMode)
            {
                case TargetingMode.Closest:
                    score = distToTurret;
                    isMinBetter = true;
                    break;
                case TargetingMode.First:
                case TargetingMode.NearExit:
                    score = enemyScript.GetDistanceToGoal(); // Lower is closer to exit
                    isMinBetter = true;
                    break;
                case TargetingMode.Last:
                    score = enemyScript.GetDistanceToGoal(); // Higher is further from exit (closer to start)
                    isMinBetter = false; // Max is better
                    break;
                case TargetingMode.Strongest:
                    score = enemyScript.CurrentHealth + enemyScript.CurrentShieldHP;
                    isMinBetter = false; // Max is better
                    break;
                case TargetingMode.Weakest:
                    score = enemyScript.CurrentHealth;
                    isMinBetter = true; // Min is better
                    break;
                case TargetingMode.Fastest:
                    score = enemyScript.CurrentSpeed;
                    isMinBetter = false; // Max is better
                    break;
            }

            // Evaluate
            if (bestCandidate == null)
            {
                bestCandidate = enemyGO;
                bestScore = score;
            }
            else
            {
                if (isMinBetter)
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCandidate = enemyGO;
                    }
                }
                else
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = enemyGO;
                    }
                }
            }
        }
        
        // Sticky Selection:
        // If we have a current valid target, and we found a candidate.
        // Should we switch?
        // If we are forcing retarget (current invalid), yes.
        // If not forcing, we only switch if the new candidate is "Significantly better"?
        // Or if we strictly follow the mode.
        // User said: "Lock target until... retarget cooldown triggers".
        // Since this method IS the retarget trigger (called periodically), we should just pick the best one found.
        
        if (bestCandidate != null)
        {
            target = bestCandidate.transform;
            prevTargetPos = target.position;
        }
        else
        {
            target = null;
        }
    }

    public void SetBarrelActive(bool isActive)
    {
        if (turretBarrel != null)
        {
            turretBarrel.gameObject.SetActive(isActive);
        }
    }

    public float GetEffectiveRange()
    {
        float mult = AugmentManager.GetStatMultiplier(AugmentType.Range);
        return range * mult;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Use effective range for visual debugging if playing, else base range
        float r = Application.isPlaying ? GetEffectiveRange() : range;
        Gizmos.DrawWireSphere(transform.position, r);
    }
}

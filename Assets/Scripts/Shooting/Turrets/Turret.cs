using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TurretBaseModule baseModule;
    [UnityEngine.Serialization.FormerlySerializedAs("partToRotate")]
    [SerializeField] public Transform baseRotationPart;
    [Tooltip("Assign if using Split_BaseY_BarrelX rotation mode")]
    [SerializeField] public Transform barrelRotationPart;
    [SerializeField] public TurretBarrelModule turretBarrel;
    [Tooltip("Optional: Where the LOS raycasts originate from. If empty, uses Turret base + 0.5f Y.")]
    [SerializeField] public Transform losStartPoint;

    public enum TurretRotationMode
    {
        None,
        SinglePart_BothAxes,
        Split_BaseY_BarrelX, 
        Base_Y_Only
    }

    [Header("Targeting")]
    public TurretRotationMode rotationMode = TurretRotationMode.Base_Y_Only;
    public float range = 15f;
    public float rotationSpeed = 10f;
    public string enemyTag = "Enemy";
    [Tooltip("Layers the turret will ignore when checking Line of Sight (e.g. Turret, Bullet, UI)")]
    public LayerMask obstacleExcludeMask;

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
            Vector3 instantVelocity = (curPos - prevTargetPos) / Time.fixedDeltaTime;
            // Smooth velocity to prevent aim jitter from frame-to-frame noise
            targetVelocity = Vector3.Lerp(targetVelocity, instantVelocity, 0.25f);
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
        Enemy enemyScript = target.GetComponent<Enemy>();
        Vector3 trueTargetPos = enemyScript != null ? enemyScript.TargetPoint : (target.position + Vector3.up * 0.5f);
        
        Vector3 aimPoint = trueTargetPos;
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
                    trueTargetPos, 
                    targetVelocity, 
                    props.upwardForce, // Use upwardForce instead of speed
                    Physics.gravity.magnitude
                );
            }
            else if (props.bulletSpeed > 0)
            {
                // Linear Prediction
                aimPoint = PredictionHelpers.PredictPosition(transform.position, trueTargetPos, targetVelocity, props.bulletSpeed);
            }
            
            // LOS Check (only if not Arc)
            if (props.fireMode != FireMode.Arc)
            {
                // Visualization
                Debug.DrawLine(transform.position + Vector3.up, aimPoint, Color.yellow); // Aim Line
                
                Vector3 start = losStartPoint != null ? losStartPoint.position : (transform.position + Vector3.up * 0.5f);
                // Check LOS to actual target center
                Vector3 end = trueTargetPos;
                
                // Throttled LOS Check
                losCheckTimer -= Time.deltaTime;
                if (losCheckTimer <= 0f)
                {
                    losCheckTimer = 0.15f; // Check every 0.15s
                    
                    int mask = ~obstacleExcludeMask;
                    int clearCenterCount = 0;
                    int clearOuterCount = 0;
                    
                    Vector3 lookDir = (end - start).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, lookDir).normalized;
                    if (right == Vector3.zero) right = Vector3.right;
                    
                    float yawStep = 5f;
                    float pitchStep = 5f;
                    
                    // 3-Layered spread logic:
                    // Top: -7.5 to +7.5 (4 rays)
                    // Mid: -12.5 to +12.5 (6 rays)
                    // Bot: -7.5 to +7.5 (4 rays)
                    
                    for (int pitchIdx = -1; pitchIdx <= 1; pitchIdx++)
                    {
                        float pitchAngle = pitchIdx * pitchStep;
                        int rayCount = (pitchIdx == 0) ? 6 : 4;
                        float startYaw = (pitchIdx == 0) ? -12.5f : -7.5f;
                        
                        for (int i = 0; i < rayCount; i++)
                        {
                            float yawAngle = startYaw + (i * yawStep);
                            
                            // Rotate lookDir by yaw and pitch
                            Quaternion spreadRot = Quaternion.AngleAxis(yawAngle, Vector3.up) * Quaternion.AngleAxis(pitchAngle, right);
                            Vector3 rayDir = spreadRot * lookDir;
                            Vector3 rayEnd = start + rayDir * Vector3.Distance(start, end);
                            
                            bool rayClear = false;
                            if (Physics.Linecast(start, rayEnd, out RaycastHit hit, mask))
                            {
                                if (hit.collider.gameObject == target.gameObject || hit.collider.transform.IsChildOf(target.transform) || hit.collider.CompareTag(enemyTag))
                                {
                                    rayClear = true;
                                }
                            }
                            
                            // Visualize
                            bool isCenter = (pitchIdx == 0 && (i == 2 || i == 3)); // The two middle rays of the 6-ray mid layer
                            if (isCenter) Debug.DrawLine(start, rayEnd, rayClear ? Color.green : Color.red, 0.15f);
                            else Debug.DrawLine(start, rayEnd, rayClear ? Color.cyan : Color.red, 0.15f);
                            
                            if (rayClear)
                            {
                                if (isCenter) clearCenterCount++;
                                else clearOuterCount++;
                            }
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
        if (isTracking && rotationMode != TurretRotationMode.None)
        {
            Transform primaryPart = baseRotationPart != null ? baseRotationPart : barrelRotationPart;
            Vector3 origin = primaryPart != null ? primaryPart.position : transform.position;
            Vector3 dir = aimPoint - origin;
            
            if (dir != Vector3.zero && primaryPart != null)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                Vector3 rot = Quaternion.Lerp(primaryPart.rotation, look, Time.deltaTime * rotationSpeed).eulerAngles;
                
                switch (rotationMode)
                {
                    case TurretRotationMode.Base_Y_Only:
                        primaryPart.rotation = Quaternion.Euler(0f, rot.y, 0f);
                        break;
                        
                    case TurretRotationMode.SinglePart_BothAxes:
                        primaryPart.rotation = Quaternion.Euler(rot.x, rot.y, 0f);
                        // Also apply to barrel if they assigned BOTH by accident, to prevent confusion
                        if (baseRotationPart != null && barrelRotationPart != null)
                        {
                             barrelRotationPart.rotation = Quaternion.Euler(rot.x, rot.y, 0f);
                        }
                        break;
                        
                    case TurretRotationMode.Split_BaseY_BarrelX:
                        if (baseRotationPart != null) 
                            baseRotationPart.rotation = Quaternion.Euler(0f, rot.y, 0f);
                            
                        if (barrelRotationPart != null && baseRotationPart != null)
                        {
                            Vector3 localDir = baseRotationPart.InverseTransformDirection(dir);
                            if (localDir != Vector3.zero)
                            {
                                Quaternion localLook = Quaternion.LookRotation(localDir);
                                Vector3 barrelRot = Quaternion.Lerp(barrelRotationPart.localRotation, localLook, Time.deltaTime * rotationSpeed).eulerAngles;
                                barrelRotationPart.localRotation = Quaternion.Euler(barrelRot.x, 0f, 0f);
                            }
                        }
                        else if (barrelRotationPart != null && baseRotationPart == null)
                        {
                             // Fallback
                             barrelRotationPart.rotation = Quaternion.Euler(rot.x, rot.y, 0f);
                        }
                        break;
                }
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
            int mask = ~obstacleExcludeMask;
            if (Physics.Linecast(start, end, out RaycastHit hit, mask))
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

    [Header("Range Visual")]
    [Tooltip("Material for the range ring and trajectory arc. If null, uses Sprites/Default fallback.")]
    public Material rangeLineMaterial;
    [Tooltip("Where the trajectory arc line starts. Assign any child Transform on the turret (e.g. barrel tip). If empty, falls back to the barrel module's fire point.")]
    public Transform trajectoryOriginOverride;

    private LineRenderer rangeLineRenderer;
    private GameObject rangeVisualObject;
    private LineRenderer trajectoryLineRenderer;
    private GameObject trajectoryVisualObject;
    private bool visualsActive = false;

    public void SetRangeVisual(bool isActive)
    {
        visualsActive = isActive;

        // Auto-resolve baseModule if not assigned in inspector
        if (baseModule == null)
            baseModule = GetComponentInChildren<TurretBaseModule>();

        if (isActive)
        {
            // --- Range Circle ---
            if (rangeVisualObject == null)
            {
                rangeVisualObject = new GameObject("RangeVisualizer");
                rangeVisualObject.transform.SetParent(this.transform);
                rangeVisualObject.transform.localPosition = Vector3.up * 0.1f;
                
                rangeLineRenderer = rangeVisualObject.AddComponent<LineRenderer>();
                rangeLineRenderer.material = rangeLineMaterial != null
                    ? rangeLineMaterial
                    : new Material(Shader.Find("Sprites/Default"));
                
                Color rangeColor = new Color(0.2f, 0.8f, 1f, 0.35f);
                rangeLineRenderer.startColor = rangeColor;
                rangeLineRenderer.endColor = rangeColor;
                rangeLineRenderer.startWidth = 0.15f;
                rangeLineRenderer.endWidth = 0.15f;
                rangeLineRenderer.loop = true;
                rangeLineRenderer.useWorldSpace = false;
            }
            rangeVisualObject.SetActive(true);
            UpdateRangeVisuals();

            // --- Trajectory Arc (Arc fire mode only) ---
            // Re-check every time (fire mode may have changed or baseModule just resolved)
            TurretPropertiesSO props = baseModule != null ? baseModule.GetTurretProperties() : null;
            bool isArc = props != null && props.fireMode == FireMode.Arc;

            if (trajectoryVisualObject == null)
            {
                trajectoryVisualObject = new GameObject("TrajectoryVisualizer");
                trajectoryVisualObject.transform.SetParent(this.transform);
                trajectoryVisualObject.transform.localPosition = Vector3.zero;

                trajectoryLineRenderer = trajectoryVisualObject.AddComponent<LineRenderer>();
                trajectoryLineRenderer.material = rangeLineMaterial != null
                    ? rangeLineMaterial
                    : new Material(Shader.Find("Sprites/Default"));

                Color arcColor = new Color(1f, 0.7f, 0.1f, 0.8f);
                trajectoryLineRenderer.startColor = arcColor;
                trajectoryLineRenderer.endColor = new Color(1f, 0.4f, 0.1f, 0f);
                trajectoryLineRenderer.startWidth = 0.08f;
                trajectoryLineRenderer.endWidth = 0.02f;
                trajectoryLineRenderer.useWorldSpace = true;
            }
            // Always sync visibility based on current fire mode check
            trajectoryVisualObject.SetActive(isArc);
        }
        else
        {
            if (rangeVisualObject != null) rangeVisualObject.SetActive(false);
            if (trajectoryVisualObject != null) trajectoryVisualObject.SetActive(false);
        }
    }

    public void UpdateRangeVisuals()
    {
        if (rangeLineRenderer == null || rangeVisualObject == null || !rangeVisualObject.activeSelf) return;

        int segments = 50;
        rangeLineRenderer.positionCount = segments;
        float currentTargetRange = GetEffectiveRange();
        float angle = 0f;
        for (int i = 0; i < segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentTargetRange;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * currentTargetRange;
            rangeLineRenderer.SetPosition(i, new Vector3(x, 0, z));
            angle += (360f / segments);
        }
    }

    private void LateUpdate()
    {
        if (!visualsActive || trajectoryVisualObject == null || !trajectoryVisualObject.activeSelf) return;
        UpdateTrajectoryVisuals();
    }

    private void UpdateTrajectoryVisuals()
    {
        if (trajectoryLineRenderer == null || turretBarrel == null) return;

        TurretPropertiesSO props = baseModule?.GetTurretProperties();
        if (props == null || props.fireMode != FireMode.Arc) return;

        // Mirror TurretBarrelModule.FireArc velocity calculation exactly so
        // the preview arc matches the real bullet flight path.

        // Use override slot first, then barrel fire point, then barrel root
        Transform firePoint = trajectoryOriginOverride != null
            ? trajectoryOriginOverride
            : turretBarrel.GetPrimaryFirePoint();
        Vector3 origin = firePoint.position;

        // Use current target position or project forward at max range if no target
        Vector3 targetPos;
        if (target != null)
        {
            targetPos = target.position;
        }
        else
        {
            // No target: aim forward at range distance
            Vector3 fwd = barrelRotationPart != null ? barrelRotationPart.forward : transform.forward;
            fwd.y = 0;
            fwd = fwd.normalized;
            targetPos = origin + fwd * GetEffectiveRange();
        }

        // ── Fixed-height arc ballistic math (from FireArc) ──
        float Vy = props.upwardForce;
        float g  = Physics.gravity.magnitude;
        float dy = targetPos.y - origin.y;

        float a = 0.5f * g;
        float b = -Vy;
        float c = dy;
        float det = b * b - 4f * a * c;
        float T;
        if (det < 0)
            T = Vector3.Distance(origin, targetPos) / 20f;
        else
            T = (-b + Mathf.Sqrt(det)) / (2f * a);
        if (T <= 0.01f) T = 0.1f;

        Vector3 d = targetPos - origin;
        d.y = 0;
        float dist = d.magnitude;
        float Vx = dist / T;
        Vector3 v0 = d.normalized * Vx + Vector3.up * Vy;

        // Feed the matched velocity into TrajectoryPredictor
        Vector3[] points = TrajectoryPredictor.GetPoints3D(origin, v0, Physics.gravity, 0f, 0.92f, 300);
        if (points == null || points.Length < 2) return;

        trajectoryLineRenderer.positionCount = points.Length;
        trajectoryLineRenderer.SetPositions(points);
    }

    private void OnDrawGizmosSelected()
    {
        // Use effective range for visual debugging if playing, else base range
        float r = Application.isPlaying ? GetEffectiveRange() : range;

        // Default range sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, r);

        // Draw cone fan for Fire element turrets
        TurretPropertiesSO props = baseModule != null ? baseModule.GetTurretProperties() : null;
        if (props == null) return;

        // Only draw cone if it's a Beam or Pulse mode turret
        if (props.fireMode != FireMode.Beam && props.fireMode != FireMode.Pulse) return;

        // Check if Fire bullet is installed
        bool isFire = turretBarrel != null
            && turretBarrel.CurrentBulletSO != null
            && turretBarrel.CurrentBulletSO.bulletType == BulletType.Fire;
        if (!isFire) return;

        // Draw the cone fan lines in yellow
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 forward = transform.forward;
        float halfAngle = props.coneAngle;

        // Left, right and center boundary lines
        Vector3 leftDir  = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0,  halfAngle, 0) * forward;
        Gizmos.DrawLine(origin, origin + forward  * r);
        Gizmos.DrawLine(origin, origin + leftDir  * r);
        Gizmos.DrawLine(origin, origin + rightDir * r);

        // Arc lines every 10 degrees
        int steps = Mathf.Max(4, Mathf.RoundToInt(halfAngle * 2 / 10));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float angleDeg = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0, angleDeg, 0) * forward;
            if (i > 0)
            {
                float prevAngleDeg = Mathf.Lerp(-halfAngle, halfAngle, (float)(i - 1) / steps);
                Vector3 prevDir = Quaternion.Euler(0, prevAngleDeg, 0) * forward;
                Gizmos.DrawLine(origin + prevDir * r, origin + dir * r);
            }
        }
    }

    /// <summary>Turret range factoring in range augments.</summary>
    public float GetEffectiveRange()
    {
        float mult = Application.isPlaying
            ? UpgradesManager.GetStatMultiplier(AugmentType.Range)
            : 1f;
        return range * mult;
    }
}


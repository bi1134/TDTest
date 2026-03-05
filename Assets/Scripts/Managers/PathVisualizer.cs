using UnityEngine;
using System.Collections.Generic;
using GuidanceLine;

/// <summary>
/// PathVisualizer - Drives GuidanceLine components to display enemy paths in-game.
/// Supports multiple paths (split/fork) by cloning the primary GuidanceLine for each spawn point.
///
/// Scene Setup:
///   1. Create a GameObject with: GuidanceLine + LineRenderer components.
///   2. Assign it to the `guidanceLine` slot here.
///   3. Assign your glow material to the LineRenderer.
///   4. Optionally assign your Pathfinder scene object directly (falls back to singleton).
///   5. Call SetVisualizerActive(true) at runtime to show it (hidden by default).
/// </summary>
public class PathVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The GuidanceLine component drawing the primary path spline (needs a LineRenderer on same GO).")]
    public GuidanceLine.GuidanceLine guidanceLine;

    [Tooltip("Pathfinder to query. Falls back to Pathfinder.Instance if empty.")]
    [SerializeField] private Pathfinder pathfinder;

    [Header("Visual Settings")]
    [Tooltip("Y offset above the raw path position. Increase until the line floats above your tiles.")]
    [SerializeField] public float heightOffset = 2f;

    [Tooltip("Show the path line immediately when the game starts.")]
    [SerializeField] public bool showOnStart = true;

    [Tooltip("Refresh path when map expands (Preparation phase).")]
    public bool refreshOnPreparation = true;

    [Header("Multiple Paths")]
    [Tooltip("Minimum distance between two spawn points for their paths to be considered separate.")]
    [SerializeField] private float minSpawnSeparation = 3f;

    // Internal state — primary path
    private Transform waypointContainer;
    private readonly List<Transform> spawnedWaypoints = new List<Transform>();
    private bool isVisible = false;
    private LineRenderer guidanceLineLR;

    // Internal state — additional cloned GuidanceLine paths
    private readonly List<GameObject> additionalPathObjects = new List<GameObject>();

    private void Awake()
    {
        if (pathfinder == null)
            pathfinder = Pathfinder.Instance;

        if (guidanceLine != null)
        {
            guidanceLineLR = guidanceLine.GetComponent<LineRenderer>();
            if (guidanceLineLR != null) guidanceLineLR.enabled = false;
            guidanceLine.enabled = false;
            // Primary path is also static (no checkpoint removal)
            guidanceLine.staticPath = true;
        }
    }

    private void OnEnable()
    {
        GameEvents.OnPathfinderGraphRebuilt += OnGraphRebuilt;
        if (refreshOnPreparation)
            GameEvents.OnGameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPathfinderGraphRebuilt -= OnGraphRebuilt;
        GameEvents.OnGameStateChanged -= OnGameStateChanged;
    }

    private void Start()
    {
        if (pathfinder == null)
            pathfinder = Pathfinder.Instance;

        waypointContainer = new GameObject("_PathWaypointContainer").transform;
        waypointContainer.SetParent(transform);

        if (showOnStart)
            isVisible = true;

        if (pathfinder != null)
        {
            BuildAllPaths();
            if (showOnStart && spawnedWaypoints.Count > 0)
                ApplyVisibility(true);
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public void SetVisualizerActive(bool active)
    {
        isVisible = active;
        ApplyVisibility(active);

        if (active && spawnedWaypoints.Count == 0 && pathfinder != null)
            BuildAllPaths();
    }

    // ─── Event Handlers ───────────────────────────────────────────────────────

    private void OnGraphRebuilt(object sender, System.EventArgs e)
    {
        if (pathfinder == null) pathfinder = Pathfinder.Instance;
        BuildAllPaths();
        if (isVisible || showOnStart)
        {
            isVisible = true;
            ApplyVisibility(true);
        }
    }

    private void OnGameStateChanged(object sender, GameEvents.GameStateChangedEventArgs e)
    {
        if (e.newState == GameHandler.GameState.Preparation)
        {
            if (pathfinder == null) pathfinder = Pathfinder.Instance;
            BuildAllPaths();
            if (isVisible) ApplyVisibility(true);
        }
    }

    [ContextMenu("Rebuild Path Now")]
    public void RebuildPath()
    {
        BuildAllPaths();
        ApplyVisibility(isVisible);
    }

    // ─── Core ─────────────────────────────────────────────────────────────────

    private void BuildAllPaths()
    {
        if (pathfinder == null || guidanceLine == null)
        {
            Debug.LogWarning($"[PathVisualizer] Missing reference — pathfinder:{pathfinder != null}, guidanceLine:{guidanceLine != null}");
            return;
        }

        CleanupAll();

        // Get all unique spawn points
        List<Vector3> allSpawns = pathfinder.GetAllSpawnPoints();
        if (allSpawns == null || allSpawns.Count == 0)
        {
            Vector3 furthest = pathfinder.GetFurthestSpawnPoint();
            if (furthest == Vector3.zero)
            {
                Debug.Log("[PathVisualizer] No spawn points — Pathfinder graph not ready yet.");
                return;
            }
            allSpawns = new List<Vector3> { furthest };
        }

        List<Vector3> uniqueSpawns = FilterUniqueSpawns(allSpawns);

        // Build primary path from furthest spawn
        Vector3 primarySpawn = pathfinder.GetFurthestSpawnPoint();
        if (primarySpawn == Vector3.zero && uniqueSpawns.Count > 0)
            primarySpawn = uniqueSpawns[0];

        List<Vector3> primaryPath = pathfinder.GetPathToBase(primarySpawn);
        if (primaryPath == null || primaryPath.Count < 2)
        {
            Debug.LogWarning("[PathVisualizer] Primary path returned no usable path.");
            return;
        }

        AssignPathToGuidanceLine(guidanceLine, primaryPath);

        // Build additional paths from other spawn points using cloned GuidanceLines
        foreach (var spawn in uniqueSpawns)
        {
            if (Vector3.Distance(spawn, primarySpawn) < minSpawnSeparation) continue;

            List<Vector3> path = pathfinder.GetPathToBase(spawn);
            if (path != null && path.Count >= 2)
            {
                BuildClonedPath(path);
            }
        }

        Debug.Log($"[PathVisualizer] Built {1 + additionalPathObjects.Count} path(s) from {uniqueSpawns.Count} spawn(s).");
    }

    private List<Vector3> FilterUniqueSpawns(List<Vector3> allSpawns)
    {
        List<Vector3> unique = new List<Vector3>();
        foreach (var spawn in allSpawns)
        {
            bool tooClose = false;
            foreach (var existing in unique)
            {
                if (Vector3.Distance(spawn, existing) < minSpawnSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) unique.Add(spawn);
        }
        return unique;
    }

    /// <summary>Assign a path to an existing GuidanceLine by creating waypoint Transforms.</summary>
    private void AssignPathToGuidanceLine(GuidanceLine.GuidanceLine gl, List<Vector3> pathPositions)
    {
        List<Transform> waypoints = new List<Transform>();
        for (int i = 0; i < pathPositions.Count; i++)
        {
            var wpGO = new GameObject($"WP_{gl.GetInstanceID()}_{i}");
            wpGO.transform.SetParent(waypointContainer);
            wpGO.transform.position = pathPositions[i] + Vector3.up * heightOffset;
            waypoints.Add(wpGO.transform);
            spawnedWaypoints.Add(wpGO.transform);
        }

        gl.startPoint = waypoints[0];
        gl.endPoint = waypoints[waypoints.Count - 1];

        if (waypoints.Count > 2)
        {
            var checkpoints = new Transform[waypoints.Count - 2];
            for (int i = 1; i < waypoints.Count - 1; i++)
                checkpoints[i - 1] = waypoints[i];
            gl.checkPoints = checkpoints;
        }
        else
        {
            gl.checkPoints = new Transform[0];
        }
    }

    /// <summary>Clone the primary GuidanceLine GO and assign a new path to the clone.</summary>
    private void BuildClonedPath(List<Vector3> pathPositions)
    {
        var clone = Instantiate(guidanceLine.gameObject, transform);
        clone.name = $"_PathLine_{additionalPathObjects.Count}";

        var clonedGL = clone.GetComponent<GuidanceLine.GuidanceLine>();
        clonedGL.staticPath = true; // No checkpoint removal

        AssignPathToGuidanceLine(clonedGL, pathPositions);

        clone.SetActive(isVisible);
        additionalPathObjects.Add(clone);
    }

    private void CleanupAll()
    {
        // Cleanup waypoints
        foreach (var wp in spawnedWaypoints)
            if (wp != null) Destroy(wp.gameObject);
        spawnedWaypoints.Clear();

        // Cleanup cloned GuidanceLine GameObjects
        foreach (var go in additionalPathObjects)
            if (go != null) Destroy(go);
        additionalPathObjects.Clear();
    }

    private void ApplyVisibility(bool active)
    {
        // Primary path
        if (guidanceLine != null)
        {
            guidanceLine.enabled = active;
            guidanceLine.gameObject.SetActive(active);
        }
        if (guidanceLineLR != null) guidanceLineLR.enabled = active;

        // Additional cloned paths
        foreach (var go in additionalPathObjects)
        {
            if (go == null) continue;
            go.SetActive(active);
            var lr = go.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = active;
            var gl = go.GetComponent<GuidanceLine.GuidanceLine>();
            if (gl != null) gl.enabled = active;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using GuidanceLine;

/// <summary>
/// PathVisualizer - Drives the GuidanceLine component to display enemy paths in-game.
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
    [Tooltip("The GuidanceLine component drawing the path spline (needs a LineRenderer on same GO).")]
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

    // Internal state
    private Transform waypointContainer;
    private readonly List<Transform> spawnedWaypoints = new List<Transform>();
    private bool isVisible = false;
    private LineRenderer guidanceLineLR;

    private void Awake()
    {
        if (pathfinder == null)
            pathfinder = Pathfinder.Instance;

        // Disable the LineRenderer immediately before GuidanceLine.Start() fires,
        // so no default-position line flickers on screen at game start.
        if (guidanceLine != null)
        {
            guidanceLineLR = guidanceLine.GetComponent<LineRenderer>();
            if (guidanceLineLR != null) guidanceLineLR.enabled = false;
            guidanceLine.enabled = false;
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

        // Don't try to build here — Pathfinder likely hasn't scanned the map yet.
        // The OnPathfinderGraphRebuilt event will fire once the graph is ready.
        // If showOnStart=true, set isVisible so the event handler shows it automatically.
        if (showOnStart)
            isVisible = true;

        // Only attempt immediate build if Pathfinder already has data (e.g. pre-built maps)
        if (pathfinder != null)
        {
            BuildPathData();
            if (showOnStart && spawnedWaypoints.Count > 0)
                ApplyVisibility(true);
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Toggle the path line on or off at runtime.</summary>
    public void SetVisualizerActive(bool active)
    {
        isVisible = active;
        ApplyVisibility(active);

        if (active && spawnedWaypoints.Count == 0 && pathfinder != null)
            BuildPathData();
    }

    // ─── Event Handlers ───────────────────────────────────────────────────────

    private void OnGraphRebuilt(object sender, System.EventArgs e)
    {
        if (pathfinder == null) pathfinder = Pathfinder.Instance;
        BuildPathData();
        // If showOnStart was requested, show on first successful graph build
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
            BuildPathData();
            if (isVisible) ApplyVisibility(true);
        }
    }

    [ContextMenu("Rebuild Path Now")]
    public void RebuildPath()
    {
        BuildPathData();
        ApplyVisibility(isVisible);
    }

    // ─── Core ─────────────────────────────────────────────────────────────────

    private void BuildPathData()
    {
        if (pathfinder == null || guidanceLine == null)
        {
            Debug.LogWarning($"[PathVisualizer] Missing reference — pathfinder:{pathfinder != null}, guidanceLine:{guidanceLine != null}");
            return;
        }

        Vector3 spawnPoint = pathfinder.GetFurthestSpawnPoint();
        if (spawnPoint == Vector3.zero)
        {
            Debug.Log("[PathVisualizer] Spawn point is zero — Pathfinder graph not ready yet.");
            return;
        }

        List<Vector3> pathPositions = pathfinder.GetVariedPath(spawnPoint);
        if (pathPositions == null || pathPositions.Count < 2)
        {
            Debug.LogWarning("[PathVisualizer] GetVariedPath returned no usable path.");
            return;
        }

        foreach (var wp in spawnedWaypoints)
            if (wp != null) Destroy(wp.gameObject);
        spawnedWaypoints.Clear();

        for (int i = 0; i < pathPositions.Count; i++)
        {
            var wpGO = new GameObject($"Waypoint_{i}");
            wpGO.transform.SetParent(waypointContainer);
            wpGO.transform.position = pathPositions[i] + Vector3.up * heightOffset;
            spawnedWaypoints.Add(wpGO.transform);
        }

        guidanceLine.startPoint  = spawnedWaypoints[0];
        guidanceLine.endPoint    = spawnedWaypoints[spawnedWaypoints.Count - 1];

        if (spawnedWaypoints.Count > 2)
        {
            var checkpoints = new Transform[spawnedWaypoints.Count - 2];
            for (int i = 1; i < spawnedWaypoints.Count - 1; i++)
                checkpoints[i - 1] = spawnedWaypoints[i];
            guidanceLine.checkPoints = checkpoints;
        }
        else
        {
            guidanceLine.checkPoints = new Transform[0];
        }

        Debug.Log($"[PathVisualizer] Built {spawnedWaypoints.Count} waypoints. " +
                  $"Start:{spawnedWaypoints[0].position} End:{spawnedWaypoints[spawnedWaypoints.Count-1].position}");
    }

    private void ApplyVisibility(bool active)
    {
        if (guidanceLine != null)
            {
                guidanceLine.enabled = active;
                guidanceLine.gameObject.SetActive(active);
            }
        if (guidanceLineLR != null) guidanceLineLR.enabled = active;
    }
}

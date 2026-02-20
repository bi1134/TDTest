using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TerrainGenerator;

public class Pathfinder : MonoBehaviour
{
    public static Pathfinder Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Name of the module that represents the path for enemies")]
        public List<string> pathKeywords = new List<string>() { "Path" }; // Changed to list for flexibility 
        [Tooltip("Name of the module that represents the Player Base (destination)")]
        public string baseModuleName = "Base";
        public bool debugDrawGraph = false;

        // Graph Data
        private HashSet<Vector3Int> pathNodes = new HashSet<Vector3Int>();
        private Dictionary<Vector3Int, List<Vector3Int>> adjacency = new Dictionary<Vector3Int, List<Vector3Int>>();

        // References
        private WFCWorldManager worldManager;
        private Vector3Int baseNode = Vector3Int.zero;
        private bool baseFound = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            worldManager = FindFirstObjectByType<WFCWorldManager>();
            if (worldManager == null)
            {
                Debug.LogError("[Pathfinder] WFCWorldManager not found!");
            }
            else
            {
                // Initial check if chunks already exist
                if (worldManager.LoadedChunks.Count > 0) RebuildGraph();
            }
        }

        private void OnEnable()
        {
            WFCEvent.OnChunkGenerated += OnChunkGeneratedHandler;
        }

        private void OnDisable()
        {
            WFCEvent.OnChunkGenerated -= OnChunkGeneratedHandler;
        }

        private void OnChunkGeneratedHandler(WFCBuilder chunk)
        {
            // Immediate rebuild so WaveManager has correct graph
            RebuildGraphImmediate();
            GameEvents.TriggerPathfinderGraphRebuilt(this);
        }

        [ContextMenu("Debug Rebuild")]
        public void DebugRebuild()
        {
            RebuildGraph();
        }

        private void Update()
        {
            if (debugDrawGraph)
            {
                DrawDebugGraph();
            }
        }

        /// <summary>
        /// Scans the WFC world and rebuilds the navigation graph.
        /// Call this after map expansion.
        /// </summary>
        private bool rebuildPending = false;

        /// <summary>
        /// Scans the WFC world and rebuilds the navigation graph.
        /// Call this after map expansion.
        /// </summary>
        public void RebuildGraph()
        {
            if (rebuildPending) return;
            StartCoroutine(RebuildRoutine());
        }



        /// <summary>
        /// Scans the WFC world and rebuilds the navigation graph immediately.
        /// Use this for synchronous operations like WFC generation loops.
        /// </summary>
        public void RebuildGraphImmediate()
        {
            PerformRebuild();
            GameEvents.TriggerPathfinderGraphRebuilt(this);
        }

        private System.Collections.IEnumerator RebuildRoutine()
        {
            rebuildPending = true;
            yield return new WaitForEndOfFrame(); // Wait for all chunks to likely finish

            PerformRebuild();
            GameEvents.TriggerPathfinderGraphRebuilt(this);
            rebuildPending = false;
        }

        private void PerformRebuild()
        {
            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WFCWorldManager>();
                if (worldManager == null) return;
            }

            pathNodes.Clear();
            adjacency.Clear();
            baseFound = false;

            // 1. Identify Path Nodes (and Base)
            foreach (var kvp in worldManager.LoadedChunks)
            {
                Vector2Int chunkCoord = kvp.Key;
                WFCBuilder chunk = kvp.Value;

                if (chunk == null || chunk.solver == null) continue;

                var cells = chunk.solver.allCells; // Ensure this is accessible
                foreach (var cell in cells)
                {
                    if (cell.Collapsed && cell.PossibleModules.Count > 0)
                    {
                        // Check if it's a Path
                        // We check if the module Name contains our keyword
                        var module = cell.PossibleModules[0];
                        if (module == null) continue;

                        if (module == null) continue;

                        string modName = module.name;


                        bool isPath = pathKeywords.Any(k => modName.Contains(k)) && !modName.Contains("Empty");
                        bool isBase = modName.Contains(baseModuleName);

                        // Special case for "Path_Variant" if needed, though Contains("Path") should cover it


                        if (isPath || isBase)
                        {
                            Vector3Int globalPos = GetGlobalPos(chunkCoord, cell.GridPosition, chunk.gridSize);
                            pathNodes.Add(globalPos);

                            if (isBase)
                            {
                                baseNode = globalPos;
                                baseFound = true;
                            }
                        }
                    }
                }
            }

            // If no explicit Base module found, try to use the center of Chunk (0,0)
            if (!baseFound)
            {
                // Fallback: Center of Chunk 0,0
                // Assuming chunk size is even, e.g. 20x10x20 -> center at 10,0,10
                Vector3Int size = worldManager.chunkSize;
                baseNode = new Vector3Int(size.x / 2, 0, size.z / 2);

                // Ensure it's in pathNodes to be reachable (force add if likely accurate)
                if (!pathNodes.Contains(baseNode) && pathNodes.Count > 0)
                {
                    // If fallback isn't a path, navigation might fail. 
                    // Search for closest path node to (0,0,0)?
                    Debug.LogWarning("[Pathfinder] Base module not found and default center is not a path. Navigation may fail.");

                    // Try to find closest path node to center
                    float minDist = float.MaxValue;
                    Vector3Int closest = Vector3Int.zero;
                    foreach (var node in pathNodes)
                    {
                        float d = node.sqrMagnitude;
                        if (d < minDist)
                        {
                            minDist = d;
                            closest = node;
                        }
                    }
                    baseNode = closest;
                }
            }

            // 2. Build Connectivity
            Vector3Int[] directions = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };

            foreach (var node in pathNodes)
            {
                List<Vector3Int> neighbors = new List<Vector3Int>();
                foreach (var dir in directions)
                {
                    Vector3Int neighbor = node + dir;
                    if (pathNodes.Contains(neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
                adjacency[node] = neighbors;
            }
            
            GenerateDistanceMap(); // Pre-calculate distances for AI

        }

        // --- Distance Map for Varied Pathing ---
        private Dictionary<Vector3Int, int> distanceMap = new Dictionary<Vector3Int, int>();

        private void GenerateDistanceMap()
        {
            distanceMap.Clear();
            if (!pathNodes.Contains(baseNode)) return;

            Queue<Vector3Int> frontier = new Queue<Vector3Int>();
            frontier.Enqueue(baseNode);
            distanceMap[baseNode] = 0;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (adjacency.TryGetValue(current, out var neighbors))
                {
                    foreach (var next in neighbors)
                    {
                        if (!distanceMap.ContainsKey(next))
                        {
                            distanceMap[next] = distanceMap[current] + 1;
                            frontier.Enqueue(next);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a path from start to base.
        /// Uses the distance map to randomly choose valid next steps, ensuring variety.
        /// </summary>
        public List<Vector3> GetVariedPath(Vector3 startWorldPos)
        {
            if (distanceMap.Count == 0) GenerateDistanceMap();

            Vector3Int current = WorldToGrid(startWorldPos);
            
            // Validate Start (Find closest if off-grid)
            if (!pathNodes.Contains(current))
            {
                current = GetClosestPathNode(current);
                if (!pathNodes.Contains(current)) return null;
            }

            List<Vector3> path = new List<Vector3>();
            path.Add(GridToWorld(current));
            
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            visited.Add(current);

            // Max steps safety to prevent infinite loops (though BFS guarantees no loops if strictly decreasing)
            int safety = 0;
            while (current != baseNode && safety < 1000)
            {
                safety++;
                
                if (!adjacency.TryGetValue(current, out var neighbors)) break;

                // Find all neighbors that are closer to the base
                int currentDist = distanceMap.ContainsKey(current) ? distanceMap[current] : int.MaxValue;
                List<Vector3Int> validNextSteps = new List<Vector3Int>();

                // Find all neighbors that rely on the distance map
                // To allow variety (sub-optimal paths), we consider neighbors even if they are 'farther' 
                // but we weight them lower. We MUST avoid backtracking to visited nodes to prevent loops.
                
                List<Vector3Int> candidates = new List<Vector3Int>();
                List<float> weights = new List<float>();
                float totalWeight = 0f;

                foreach (var next in neighbors)
                {
                    if (distanceMap.TryGetValue(next, out int nextDist))
                    {
                        // Prevent backtracking (simple check against current path)
                        // Note: This makes it O(N^2) effectively if path is long, but N is small (map size).
                        // Use a HashSet for 'visited' if optimization needed. 
                        // For now transforming path to grid coords for check is slow.
                        // Faster: Keep a HashSet<Vector3Int> visited locally in this method
                        if (visited.Contains(next)) continue;


                        
                        // Weight Logic
                        // Shorter distance = High weight
                        // Equal distance = Low weight (side step)
                        // Higher distance = Forbidden (to prevent backtracking/wandering to other spawns)
                        
                        float w = 0f;
                        if (nextDist < currentDist)
                        {
                            w = 10f; // Prefer progress
                        }
                        else if (nextDist == currentDist) 
                        {
                             w = 2f; // Side steps allowed for variety
                        }
                        // else w = 0f (Forbidden)

                        if (w > 0f)
                        {
                            candidates.Add(next);
                            weights.Add(w);
                            totalWeight += w;
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    // Weighted Random Selection
                    float rnd = UnityEngine.Random.Range(0f, totalWeight);
                    float sum = 0f;
                    Vector3Int selected = candidates[0];
                    
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        sum += weights[i];
                        if (rnd <= sum)
                        {
                            selected = candidates[i];
                            break;
                        }
                    }
                    
                    current = selected;
                    path.Add(GridToWorld(current));
                    visited.Add(current);
                }
                else
                {
                    // Dead end
                    break;
                }
            }
            
            return path;
        }

        public List<Vector3> GetPathToBase(Vector3 startWorldPos)
        {
            Vector3Int startNode = WorldToGrid(startWorldPos);

            // Validate Start
            if (!pathNodes.Contains(startNode))
            {
                // Find closest valid node
                startNode = GetClosestPathNode(startNode);
                if (!pathNodes.Contains(startNode)) return null;
            }

            // BFS
            Queue<Vector3Int> frontier = new Queue<Vector3Int>();
            frontier.Enqueue(startNode);

            Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            cameFrom[startNode] = startNode;

            bool found = false;

            // Optimized: Bi-directional search could be faster, but simple BFS is fine for this scale
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (current == baseNode)
                {
                    found = true;
                    break;
                }

                if (adjacency.TryGetValue(current, out var neighbors))
                {
                    foreach (var next in neighbors)
                    {
                        if (!cameFrom.ContainsKey(next))
                        {
                            frontier.Enqueue(next);
                            cameFrom[next] = current;
                        }
                    }
                }
            }

            if (!found) return null;

            // Reconstruct
            List<Vector3> path = new List<Vector3>();
            Vector3Int curr = baseNode;

            // Trace back from End to Start
            while (curr != startNode)
            {
                path.Add(GridToWorld(curr));
                curr = cameFrom[curr];
            }
            path.Add(GridToWorld(startNode));

            // Path is currently [Base, ..., Start]
            // We want [Start, ..., Base]?
            // Usually path followers want [Next, Next, ..., End]
            // So we reverse it.
            path.Reverse();

            return path;
        }

        /// <summary>
        /// Finds a valid spawn point on the path at the furthest distance from the base.
        /// Useful for testing or spawning waves.
        /// </summary>
        public Vector3 GetFurthestSpawnPoint()
        {
            if (pathNodes.Count == 0) return Vector3.zero;

            Vector3Int furthest = baseNode;
            float maxDist = 0f;

            foreach (var node in pathNodes)
            {
                float dist = Vector3.SqrMagnitude(node - baseNode);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    furthest = node;
                }
            }

            return GridToWorld(furthest);
        }

        public List<Vector3> GetAllSpawnPoints()
        {
            List<Vector3> spawns = new List<Vector3>();
            if (pathNodes.Count == 0) return spawns;

            foreach (var node in pathNodes)
            {
                if (node == baseNode) continue;

                // Check if leaf (1 neighbor)
                if (adjacency.ContainsKey(node) && adjacency[node].Count == 1)
                {
                    spawns.Add(GridToWorld(node));
                }
            }

            // Fallback: If no leaves found (e.g. loop), use furthest
            if (spawns.Count == 0)
            {
                spawns.Add(GetFurthestSpawnPoint());
            }

            return spawns;
        }

        // --- Helpers ---

        private Vector3Int GetClosestPathNode(Vector3Int gridPos)
        {
            if (pathNodes.Contains(gridPos)) return gridPos;

            Vector3Int closest = gridPos;
            int minSqDist = int.MaxValue;

            foreach (var node in pathNodes)
            {
                int d = (node.x - gridPos.x) * (node.x - gridPos.x) +
                        (node.z - gridPos.z) * (node.z - gridPos.z);

                if (d < minSqDist)
                {
                    minSqDist = d;
                    closest = node;
                }
            }
            return closest;
        }

        private Vector3Int GetGlobalPos(Vector2Int chunkCoord, Vector3Int localPos, Vector3Int chunkSize)
        {
            // Global = ChunkOrigin + Local
            // ChunkOrigin X = chunkX * sizeX
            // ChunkOrigin Z = chunkY * sizeZ
            return new Vector3Int(
                chunkCoord.x * chunkSize.x + localPos.x,
                localPos.y,
                chunkCoord.y * chunkSize.z + localPos.z
            );
        }

        private Vector3Int WorldToGrid(Vector3 worldPos)
        {
            float s = worldManager.worldScale;
            // Assuming 0,0,0 world is 0,0,0 grid
            // Grid Coords = Floor(World / Scale)

            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / s),
                Mathf.FloorToInt(worldPos.y / s),
                Mathf.FloorToInt(worldPos.z / s)
            );
        }

        private Vector3 GridToWorld(Vector3Int gridPos)
        {
            float s = worldManager.worldScale;
            // Center of cell = grid * scale + half_scale
            return new Vector3(
                gridPos.x * s + (s * 0.5f),
                gridPos.y * s, // Keep Y flat? Or add half height?
                gridPos.z * s + (s * 0.5f)
            );
        }

        private void DrawDebugGraph()
        {
            float s = worldManager.worldScale;
            float h = s * 0.5f;
            Vector3 offset = new Vector3(0, 1f, 0); // Lift up

            foreach (var kvp in adjacency)
            {
                Vector3 origin = GridToWorld(kvp.Key) + offset;
                foreach (var neighbor in kvp.Value)
                {
                    Vector3 dest = GridToWorld(neighbor) + offset;
                    Debug.DrawLine(origin, dest, Color.blue);
                }
            }

            // Draw Base
            if (baseFound || pathNodes.Contains(baseNode))
            {
                Vector3 b = GridToWorld(baseNode);
                Debug.DrawRay(b, Vector3.up * 10, Color.red);
            }
        }
        /// <summary>
        /// Checks if a path node exists at the specified edge of a chunk.
        /// Used for visualizing expansion opportunities.
        /// </summary>
        public bool HasPathEndingAt(Vector2Int chunkCoord, TerrainGenerator.EdgeSide side)
        {
            if (worldManager == null) return false;

            Vector3Int chunkSize = worldManager.chunkSize;

            // Use Global Grid Coordinates
            int startX = chunkCoord.x * chunkSize.x;
            int startZ = chunkCoord.y * chunkSize.z;
            int endX = startX + chunkSize.x - 1;
            int endZ = startZ + chunkSize.z - 1;

            // Iterate through all path nodes and check boundary conditions
            foreach (var node in pathNodes)
            {
                if (side == TerrainGenerator.EdgeSide.Left && node.x == startX) return true;
                if (side == TerrainGenerator.EdgeSide.Right && node.x == endX) return true;
                // Top = Z+ (Forward)
                if (side == TerrainGenerator.EdgeSide.Top && node.z == endZ) return true;
                // Bottom = Z- (Back)
                if (side == TerrainGenerator.EdgeSide.Bottom && node.z == startZ) return true;
            }

            return false;
        }
    }


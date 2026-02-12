using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TerrainGenerator;

namespace Systems
{
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
            // Should valid check?
            RebuildGraph();
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

        private System.Collections.IEnumerator RebuildRoutine()
        {
            rebuildPending = true;
            yield return new WaitForEndOfFrame(); // Wait for all chunks to likely finish
            
            PerformRebuild();
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
                    foreach(var node in pathNodes)
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

            Debug.Log($"[Pathfinder] Graph Rebuilt. Nodes: {pathNodes.Count}, Base: {baseNode}");
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

            foreach(var node in pathNodes)
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
    }
}

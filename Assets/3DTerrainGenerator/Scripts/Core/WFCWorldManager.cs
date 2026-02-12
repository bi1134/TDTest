using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TerrainGenerator
{
    public class WFCWorldManager : MonoBehaviour
    {
        [Header("Settings")]
        public Vector3Int chunkSize = new Vector3Int(20, 10, 20);
        public float worldScale = 4.0f; // Multiplier for cell size (e.g. 4 if cells are 4x4)
        
        [Tooltip("WFCBuilder prefab for the initial/start chunk (fixed rules, no random)")]
        public WFCBuilder startChunkPrefab;
        [Tooltip("WFCBuilder prefab for expansion chunks (randomly generated)")]
        public WFCBuilder expansionChunkPrefab;
        
        public int viewDistance = 1;
        
        [Header("Run Seed")]
        [Tooltip("Seed for reproducible generation. 0 = random seed")]
        public int runSeed = 0;
        public bool useFixedSeed = false;
        private System.Random runRNG;
        
        /// <summary>Public access to the run RNG for WFCSolver</summary>
        public System.Random RunRNG => runRNG;
        
        [Header("Expansion State")]
        [Tooltip("Current expansion ring (0 = center only, 1 = first ring, etc.)")]
        public int currentExpansionRing = 0;
        private Queue<Vector2Int> pendingExpansionCoords = new Queue<Vector2Int>();
        
        [Header("State")]
        public Vector2Int centerChunk;
        
        // Storage
        private Dictionary<Vector2Int, WFCBuilder> loadedChunks = new Dictionary<Vector2Int, WFCBuilder>();
        public IReadOnlyDictionary<Vector2Int, WFCBuilder> LoadedChunks => loadedChunks;
        
        // This class is responsible for Spawning Chunks and linking them.
        
        private void Start()
        {
            InitializeRNG();
            GenerateInitialChunk();
        }
        
        private void InitializeRNG()
        {
            if (useFixedSeed && runSeed != 0)
            {
                runRNG = new System.Random(runSeed);
            }
            else
            {
                int seed = System.Environment.TickCount;
                runSeed = seed; // Store for debugging
                runRNG = new System.Random(seed);
            }
        }

        private void OnEnable()
        {
            WFCEvent.OnChunkGenerated += HandleChunkGenerated;
        }

        private void OnDisable()
        {
            WFCEvent.OnChunkGenerated -= HandleChunkGenerated;
        }

        private void HandleChunkGenerated(WFCBuilder chunk)
        {
            // Reverse lookup coord from chunk name or position
            // Name format: "Chunk_X_Y"
            
            string[] parts = chunk.name.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                RefreshNeighborBorders(new Vector2Int(x, y));
            }
        }

        [ContextMenu("Generate World")]
        public void GenerateWorld()
        {
             // Clear Old
             foreach(var kvp in loadedChunks)
             {
                 if(kvp.Value != null) DestroyImmediate(kvp.Value.gameObject);
             }
             loadedChunks.Clear();
             
             // Pass 1: Instantiate All Chunks
             for(int x = -viewDistance; x <= viewDistance; x++)
             {
                 for(int y = -viewDistance; y <= viewDistance; y++)
                 {
                     Vector2Int coord = centerChunk + new Vector2Int(x, y);
                     bool isCenter = (x == 0 && y == 0);
                     var prefab = isCenter ? startChunkPrefab : expansionChunkPrefab;
                     CreateChunkInitialized(coord, prefab, useRNG: !isCenter);
                 }
             }

             // Pass 2: Trigger Generation
             foreach(var kvp in loadedChunks)
             {
                 if (kvp.Value != null)
                 {
                     kvp.Value.Generate();
                 }
             }
        }
        
        /// <summary>
        /// Generate only the initial center chunk on game start.
        /// Called from Start() instead of GenerateWorld().
        /// </summary>
        [ContextMenu("Generate Initial Chunk")]
        public void GenerateInitialChunk()
        {
            // Clear any existing chunks
            foreach(var kvp in loadedChunks)
            {
                if(kvp.Value != null) DestroyImmediate(kvp.Value.gameObject);
            }
            loadedChunks.Clear();
            currentExpansionRing = 0;
            pendingExpansionCoords.Clear();
            
            // Generate only the center chunk (fixed rules, no random)
            CreateChunkInitialized(centerChunk, startChunkPrefab, useRNG: false);
            if (loadedChunks.TryGetValue(centerChunk, out var chunk))
            {
                chunk.Generate();
            }
            
            Debug.Log($"[WFCWorldManager] Initial chunk generated at {centerChunk} (fixed, no RNG)");
        }
        
        /// <summary>
        /// Expand the world by one ring of chunks.
        /// Call this after each wave ends.
        /// </summary>
        [ContextMenu("Expand Next Ring")]
        public void ExpandNextRing()
        {
            currentExpansionRing++;
            var ringCoords = GetRingCoords(currentExpansionRing);
            
            // Pass 1: Instantiate all chunks in this ring (random expansion)
            foreach (var coord in ringCoords)
            {
                CreateChunkInitialized(coord, expansionChunkPrefab, useRNG: true);
            }
            
            // Pass 2: Generate all chunks in this ring
            foreach (var coord in ringCoords)
            {
                if (loadedChunks.TryGetValue(coord, out var chunk))
                {
                    chunk.Generate();
                }
            }
            
            Debug.Log($"[WFCWorldManager] Expanded to ring {currentExpansionRing} ({ringCoords.Count} chunks)");
        }
        
        /// <summary>
        /// Track the last chunk coordinate generated (for directional chaining)
        /// </summary>
        private Vector2Int lastExpandedChunk;
        
        /// <summary>
        /// Expand one chunk in the specified direction from a given chunk.
        /// This is the main method for TD game - triggered by wave end button.
        /// </summary>
        public void ExpandInDirection(Vector2Int fromChunk, EdgeSide direction)
        {
            Vector2Int offset = direction switch
            {
                EdgeSide.Left => Vector2Int.left,
                EdgeSide.Right => Vector2Int.right,
                EdgeSide.Top => Vector2Int.up,       // +Y in chunk grid = Forward/Top
                EdgeSide.Bottom => Vector2Int.down,  // -Y in chunk grid = Back/Bottom
                _ => Vector2Int.zero
            };
            
            Vector2Int newCoord = fromChunk + offset;
            
            if (loadedChunks.ContainsKey(newCoord))
            {
                Debug.LogWarning($"[WFCWorldManager] Chunk at {newCoord} already exists!");
                return;
            }
            
            CreateChunkInitialized(newCoord, expansionChunkPrefab, useRNG: true);
            if (loadedChunks.TryGetValue(newCoord, out var chunk))
            {
                chunk.Generate();
                lastExpandedChunk = newCoord;
            }
            
            Debug.Log($"[WFCWorldManager] Expanded {direction} from {fromChunk} to {newCoord}");
        }
        
        /// <summary>
        /// Expand from the last generated chunk in the specified direction.
        /// Convenience method for chaining expansions.
        /// </summary>
        public void ExpandFromLast(EdgeSide direction)
        {
            // First expansion uses center chunk
            if (loadedChunks.Count <= 1)
                lastExpandedChunk = centerChunk;
                
            ExpandInDirection(lastExpandedChunk, direction);
        }
        
        // Context Menu shortcuts for testing
        [ContextMenu("Expand Forward (Top)")] 
        public void ExpandForward() => ExpandFromLast(EdgeSide.Top);
        
        [ContextMenu("Expand Back (Bottom)")] 
        public void ExpandBack() => ExpandFromLast(EdgeSide.Bottom);
        
        [ContextMenu("Expand Left")] 
        public void ExpandLeft() => ExpandFromLast(EdgeSide.Left);
        
        [ContextMenu("Expand Right")] 
        public void ExpandRight() => ExpandFromLast(EdgeSide.Right);
        
        /// <summary>
        /// Get all coordinates for a given ring number around the center.
        /// Ring 1 = 8 chunks around center, Ring 2 = 16 chunks, etc.
        /// </summary>
        private List<Vector2Int> GetRingCoords(int ring)
        {
            var coords = new List<Vector2Int>();
            if (ring <= 0) return coords;
            
            // Walk around the perimeter of the ring
            int n = ring;
            
            // Top edge (left to right)
            for (int x = -n; x <= n; x++)
                coords.Add(centerChunk + new Vector2Int(x, n));
            
            // Right edge (top-1 to bottom+1)
            for (int y = n - 1; y >= -n + 1; y--)
                coords.Add(centerChunk + new Vector2Int(n, y));
            
            // Bottom edge (right to left)
            for (int x = n; x >= -n; x--)
                coords.Add(centerChunk + new Vector2Int(x, -n));
            
            // Left edge (bottom+1 to top-1)
            for (int y = -n + 1; y <= n - 1; y++)
                coords.Add(centerChunk + new Vector2Int(-n, y));
            
            return coords;
        }
        
        // Renamed from CreateChunk to separate instantiation from generation
        private void CreateChunkInitialized(Vector2Int coord, WFCBuilder prefab, bool useRNG)
        {
             if (loadedChunks.ContainsKey(coord)) return;
             if (prefab == null)
             {
                 Debug.LogError($"[WFCWorldManager] No prefab assigned for chunk at {coord}!");
                 return;
             }
             
             // 1. Instantiate
             float xPos = coord.x * chunkSize.x * worldScale;
             float zPos = coord.y * chunkSize.z * worldScale;
             Vector3 pos = new Vector3(xPos, 0, zPos); 
             
             WFCBuilder newChunk = Instantiate(prefab, pos, Quaternion.identity, this.transform);
             newChunk.name = $"Chunk_{coord.x}_{coord.y}";
             newChunk.gridSize = chunkSize;
             newChunk.autoResizeGridToMap = false; 
             newChunk.generateOnStart = false; 
             newChunk.solver.globalLookup = GetGlobalCell;
             newChunk.solver.globalCellLookup = GetGlobalCellAny; 
             newChunk.solver.globalChunkExists = IsChunkLoaded; 
             newChunk.solver.chunkCoordinate = coord;
             newChunk.solver.worldScale = worldScale;
             newChunk.solver.chunkCoordinate = coord;
             newChunk.solver.worldScale = worldScale;
             
             // Override Seed Settings for consistency
             if (useRNG)
             {
                 // Use Coordinate-Based Deterministic Seed
                 // This ensures Chunk (1,0) is always the same regardless of expansion order
                 int coordHash = coord.x * 73856093 ^ coord.y * 19349663;
                 int chunkSeed = runSeed ^ coordHash;
                 
                 newChunk.solver.runRNG = new System.Random(chunkSeed); 
                 newChunk.useRandomSeed = false; // We use our deterministic RNG
                 newChunk.seed = chunkSeed;      // For inspection
             }
             else
             {
                 // Start Chunk (Fixed)
                 newChunk.solver.runRNG = null; // Will trigger WFCBuilder internal init? Or should we set it?
                 // Start Chunk usually uses WFCBuilder's own settings. But let's enforce runSeed if set.
                 if (runSeed != 0 && useFixedSeed)
                 {
                     newChunk.solver.runRNG = new System.Random(runSeed);
                     newChunk.seed = runSeed;
                     newChunk.useRandomSeed = false;
                 }
                 else
                 {
                     // Use whatever is in prefab or WFCBuilder default behavior
                     newChunk.solver.runRNG = null;
                     newChunk.useRandomSeed = false;
                 }
             }
             
             // Clear shared blueprint textures
             foreach (var bp in newChunk.definedBlueprints)
                 bp.outputMap = null;
             
             // 2. Stitching Logic
             foreach(var bp in newChunk.definedBlueprints)
             {
                 var stitchable = bp.modifiers.FirstOrDefault(m => m is INeighborStitchable) as INeighborStitchable;
                 if (stitchable == null) continue;
                 
                 stitchable.ClearStitching();
                 
                 // Notify neighbors existence first (crucial for "Wall Detection")
                 NotifyNeighborExistence(stitchable, coord + Vector2Int.left, EdgeSide.Left);
                 NotifyNeighborExistence(stitchable, coord + Vector2Int.right, EdgeSide.Right);
                 NotifyNeighborExistence(stitchable, coord + Vector2Int.up, EdgeSide.Top);
                 NotifyNeighborExistence(stitchable, coord + Vector2Int.down, EdgeSide.Bottom);

                 // Then Stitch
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.left,   EdgeSide.Left,  EdgeSide.Right);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.right,  EdgeSide.Right, EdgeSide.Left);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.up,     EdgeSide.Top,   EdgeSide.Bottom);
                 StitchWithNeighbor(stitchable, bp.layerName, coord + Vector2Int.down,   EdgeSide.Bottom,EdgeSide.Top);
             }
             
             // NO Generate() here.
             
             loadedChunks.Add(coord, newChunk);
        }
        
        private void RefreshNeighborBorders(Vector2Int coord)
        {
             // Cardinals
             // Notify Neighbor to refresh THEIR edge facing ME
             // And Refresh MY edge facing THEM (Handshake)
             
             if (!loadedChunks.TryGetValue(coord, out WFCBuilder me)) return;
             if (me == null || me.solver == null) return;

             // Left Neighbor (My Left)
             NotifyNeighbor(coord + Vector2Int.left, Vector3Int.right);
             me.solver.RefreshVisualsOnEdge(Vector3Int.left);

             // Right Neighbor (My Right)
             NotifyNeighbor(coord + Vector2Int.right, Vector3Int.left);
             me.solver.RefreshVisualsOnEdge(Vector3Int.right);

             // Up Neighbor (My Back - Z+)
             NotifyNeighbor(coord + Vector2Int.up, Vector3Int.back); // Neighbor's Back Edge
             me.solver.RefreshVisualsOnEdge(Vector3Int.forward); // My Forward Edge (Z+)

             // Down Neighbor (My Forward - Z-)
             NotifyNeighbor(coord + Vector2Int.down, Vector3Int.forward); // Neighbor's Forward Edge
             me.solver.RefreshVisualsOnEdge(Vector3Int.back); // My Back Edge (Z-)
        }
        
        private void NotifyNeighbor(Vector2Int neighborCoord, Vector3Int edgeToRefresh)
        {
            if (loadedChunks.TryGetValue(neighborCoord, out WFCBuilder neighbor))
            {
                if (neighbor != null && neighbor.solver != null)
                {
                    neighbor.solver.RefreshVisualsOnEdge(edgeToRefresh);
                }
            }
        }
        
        private void StitchWithNeighbor(INeighborStitchable myGen, string layerName, Vector2Int neighborCoord, EdgeSide myEdge, EdgeSide neighborEdge)
        {
            if (!loadedChunks.TryGetValue(neighborCoord, out WFCBuilder neighbor)) return;
            
            // Find matching blueprint
            var neighborBP = neighbor.GetBlueprint(layerName);
            if (neighborBP == null) return;
            
            // Find Neighbor Stitchable
            var neighborGen = neighborBP.modifiers.FirstOrDefault(m => m is INeighborStitchable) as INeighborStitchable;
            if (neighborGen == null) return;
            
            // Generic Data Packet Exchange
            object neighborData = neighborGen.GetEdgeData(neighborEdge);
            if (neighborData != null)
            {
                 Debug.Log($"[WFCWorld] Stitching {layerName}: Connecting {myEdge} with {neighborCoord}'s {neighborEdge}.");
                 myGen.InjectEdgeData(neighborData, myEdge);
            }
        }
        
        private void NotifyNeighborExistence(INeighborStitchable myGen, Vector2Int neighborCoord, EdgeSide myEdge)
        {
             bool exists = loadedChunks.ContainsKey(neighborCoord);
             myGen.SetNeighborExistence(myEdge, exists);
        }

        // --- Global Query for Visualizer ---
        
        // This allows a cell in Chunk A to query a neighbor cell in Chunk B using GLOBAL GRID COORDINATES
        public WFCCell GetGlobalCell(Vector3Int globalGridPos)
        {
            // Grid Size per chunk
            int sx = chunkSize.x;
            int sz = chunkSize.z; // y is ignored/not chunked in this logic (vertical stacking? assumed single layer of chunks for now)
            
            if (sx == 0 || sz == 0) return null;

            // 1. Calculate Chunk Coordinate
            // Using Floor Division for standard tiling
            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            Vector2Int chunkCoord = new Vector2Int(chunkX, chunkY);
            
            if (loadedChunks.TryGetValue(chunkCoord, out WFCBuilder builder))
            {
                if (builder == null || builder.solver == null) return null;
                
                // 2. Calculate Local Index
                // Proper Modulo for negative numbers: (x % m + m) % m
                int localX = (globalGridPos.x % sx + sx) % sx;
                int localZ = (globalGridPos.z % sz + sz) % sz;
                int localY = globalGridPos.y; // Height is local = global in this 2D chunking setup
                
                return builder.solver.GetVisualCellAt(new Vector3Int(localX, localY, localZ));
            }
            return null;
        }
        
        /// <summary>
        /// Get any cell (WFCCell or WFCCellData) at a global grid position.
        /// Used for dual grid cross-chunk lookups.
        /// </summary>
        public IWFCCell GetGlobalCellAny(Vector3Int globalGridPos)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            
            if (sx == 0 || sz == 0) return null;

            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            Vector2Int chunkCoord = new Vector2Int(chunkX, chunkY);
            
            if (loadedChunks.TryGetValue(chunkCoord, out WFCBuilder builder))
            {
                if (builder == null || builder.solver == null) return null;
                
                int localX = (globalGridPos.x % sx + sx) % sx;
                int localZ = (globalGridPos.z % sz + sz) % sz;
                int localY = globalGridPos.y;
                
                return builder.solver.GetCellAt(new Vector3Int(localX, localY, localZ));
            }
            return null;
        }

        public bool IsChunkLoaded(Vector3Int globalGridPos)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            if (sx == 0 || sz == 0) return false;

            int chunkX = Mathf.FloorToInt((float)globalGridPos.x / sx);
            int chunkY = Mathf.FloorToInt((float)globalGridPos.z / sz);
            
            return loadedChunks.ContainsKey(new Vector2Int(chunkX, chunkY));
        }
    }
}


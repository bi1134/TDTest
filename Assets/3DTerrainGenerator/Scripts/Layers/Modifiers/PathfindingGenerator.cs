using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PathPoint = TerrainGenerator.TowerDefensePathGenerator.PathPoint;
using EdgeSide = TerrainGenerator.EdgeSide;

namespace TerrainGenerator
{
    /// <summary>
    /// A simplified pathfinding generator using A* algorithm.
    /// Now supports Edge Stitching and Seed/Random logic.
    /// </summary>
    [System.Serializable]
    public class PathfindingGenerator : WFCModifier, INeighborStitchable
    {

        [Header("Path Settings")]
        public bool useRandomSeed = true;
        public int seed;
        
        [Header("Points Logic")]
        // public bool randomPoints = true; // REPLACED
        [Tooltip("If true, Start Point is randomized. If false, uses fixedStart.")]
        public bool randomizeStart = true;
        [Tooltip("If randomized, force Start onto an edge?")]
        public bool startOnEdge = true;
        public Vector2Int fixedStart = Vector2Int.zero;
        
        [Space]
        [Tooltip("If true, End Point is randomized. If false, uses fixedEnd.")]
        public bool randomizeEnd = true;
        [Tooltip("If randomized, force End onto an edge?")]
        public bool endOnEdge = true;
        public Vector2Int fixedEnd = new Vector2Int(19, 19);
        
        [Header("Debug Info")]
        [SerializeField] private int debugLastSeed; // visible in inspector

        [Header("Stitching")]
        [Tooltip("If true, connects to neighbors.")]
        public bool stitchEnabled = true;

        [Header("Visuals")]
        public int pathWidth = 1;
        public bool markEndpoints = false;
        public Color startColor = Color.green;
        public Color endColor = Color.red;

        [Header("Algorithm")]
        [Tooltip("Cost for turns. Higher = straighter paths.")]
        public float turnCost = 0.5f;
        
        [Header("Cost Map")]
        public string costLayerName; 

        [Header("Object Spawning")]
        public GameObject spawnerPrefab;
        public GameObject endPrefab;

        // Internal State
        private List<PathPoint> startPoints = new List<PathPoint>();
        private List<PathPoint> endPoints = new List<PathPoint>();
        private List<Vector2Int> generatedPath = new List<Vector2Int>();
        
        // Purely Internal Points (Not Stitched)
        private List<Vector2Int> internalStartPoints = new List<Vector2Int>();
        private List<Vector2Int> internalEndPoints = new List<Vector2Int>();
        
        // Neighbor State
        private bool[] neighborExists = new bool[4]; // 0=Left, 1=Right, 2=Top, 3=Bottom

        public PathfindingGenerator() { }

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // Seed Logic
            int currentSeed = seed;
            if (injectedSeed != -1) currentSeed = injectedSeed;
            else if (useRandomSeed) currentSeed = Random.Range(1, 999999);
            
            debugLastSeed = currentSeed; // For Inspector
            System.Random prng = new System.Random(currentSeed);
            
            // 1. Prepare Obstacles (Moved Up)
            bool[,] obstacles = new bool[w, h];
            if (!string.IsNullOrEmpty(costLayerName) && context != null)
            {
                var costL = context.Find(l => l.layerName == costLayerName);
                if (costL != null && costL.outputMap != null)
                {
                    for(int x=0; x<w; x++) for(int y=0; y<h; y++)
                    {
                        if (x < costL.outputMap.width && y < costL.outputMap.height)
                        {
                            Color c = costL.outputMap.GetPixel(x, y);
                            if (!IsColorMatch(c, costL.BackgroundColor)) obstacles[x, y] = true;
                        }
                    }
                }
            }
            
            // 2. Determine Start/End Points (Now with Obstacle awareness)
            GenerateSmartPoints(w, h, prng, obstacles);

            // 3. Resolve Coords
            List<Vector2Int> sCoords = new List<Vector2Int>();
            List<Vector2Int> eCoords = new List<Vector2Int>();
            
            // Add Internal Points
            sCoords.AddRange(internalStartPoints);
            eCoords.AddRange(internalEndPoints);
            
            // Add Edge Points
            foreach(var p in startPoints) sCoords.Add(GetEdgePoint(p.edge, p.position, w, h));
            foreach(var p in endPoints) eCoords.Add(GetEdgePoint(p.edge, p.position, w, h));
            
            // Remove Duplicates
            sCoords = sCoords.Distinct().ToList();
            eCoords = eCoords.Distinct().ToList();

            // Force Clear Obstacles at Start/End Points (Prevent Dead Ends)
            foreach(var p in sCoords) obstacles[p.x, p.y] = false;
            foreach(var p in eCoords) obstacles[p.x, p.y] = false;

            // 4. Generate Paths (Tree / Forest)
            if (sCoords.Count > 0 && eCoords.Count > 0)
            {
                // Simple strategy: Connect S[0] to E[0], then others to that path.
                Dictionary<Vector2Int, Vector2Int> parentMap = new Dictionary<Vector2Int, Vector2Int>(); 
                HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
                
                Vector2Int mainStart = sCoords[0];
                Vector2Int mainEnd = eCoords[0];
                
                // Trunk
                var trunk = FindPath(mainStart, mainEnd, obstacles, w, h);
                if (trunk != null)
                {
                    foreach(var p in trunk) pathCells.Add(p);
                    
                    // Connect other Starts to Trunk
                    for(int i=1; i<sCoords.Count; i++)
                    {
                        var branch = ConnectToSet(sCoords[i], pathCells, obstacles, w, h);
                        if (branch != null) foreach(var p in branch) pathCells.Add(p);
                    }
                    
                    // Connect other Ends to Trunk
                    for(int i=1; i<eCoords.Count; i++)
                    {
                        var branch = ConnectToSet(eCoords[i], pathCells, obstacles, w, h);
                        if (branch != null) foreach(var p in branch) pathCells.Add(p);
                    }
                    
                    // Store for later use (Step 5)
                    generatedPath.Clear();
                    generatedPath.AddRange(pathCells);

                    // Draw
                    foreach(var p in pathCells) DrawPoint(map, p, layer.activeColor, w, h);
                }
            }
            
            if (markEndpoints)
            {
                 foreach(var p in sCoords) DrawPoint(map, p, startColor, w, h);
                 foreach(var p in eCoords) DrawPoint(map, p, endColor, w, h);
            }

            // 5. Generate Spawn Commands
            layer.spawnCommands.Clear();
            
            // Build Quick Lookup for Path
            HashSet<Vector2Int> spawnPathLookup = new HashSet<Vector2Int>(generatedPath);

            if (spawnerPrefab != null)
            {
                foreach(var start in sCoords)
                {
                    Vector3 worldPos = new Vector3(start.x, 0, start.y); // Local to chunk, Y=0 (unless heightmap?)
                    // Rotate towards path neighbor
                    Vector3 lookDir = Vector3.forward;
                    foreach(var d in new Vector2Int[]{Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right})
                    {
                        if (spawnPathLookup.Contains(start + d)) 
                        {
                            lookDir = new Vector3(d.x, 0, d.y);
                            break; 
                        }
                    }
                    if (lookDir != Vector3.zero)
                    {
                        layer.spawnCommands.Add(new WFCBlueprintLayer.SpawnCommand {
                            position = worldPos,
                            rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(0, 90, 0), // Adjust if needed (e.g. model side-facing)
                            prefab = spawnerPrefab
                        });
                    }
                }
            }
            
            if (endPrefab != null)
            {
                foreach(var end in eCoords)
                {
                    Vector3 worldPos = new Vector3(end.x, 0, end.y);
                    Vector3 lookDir = Vector3.forward;
                    // Usually ends face INTO the path (entrance) or OUT (exit)?
                    // Base usually faces where enemies come FROM.
                    // So look at neighbor.
                     foreach(var d in new Vector2Int[]{Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right})
                    {
                        if (spawnPathLookup.Contains(end + d)) 
                        {
                            lookDir = new Vector3(d.x, 0, d.y);
                            break; 
                        }
                    }
                    if (lookDir != Vector3.zero)
                    {
                         layer.spawnCommands.Add(new WFCBlueprintLayer.SpawnCommand {
                            position = worldPos,
                            rotation = Quaternion.LookRotation(lookDir),
                            prefab = endPrefab
                        });
                    }
                }
            }

            map.Apply();
        }
        
        private void GenerateSmartPoints(int w, int h, System.Random prng, bool[,] obstacles)
        {
             // Clear internal lists (regenerate per run)
             internalStartPoints.Clear();
             internalEndPoints.Clear();
             
             // 1. Mandatory Connections (Already injected into startPoints/endPoints by Stitching)
             // We just need to ensure we have enough points and DON'T pick blocked edges.
             
             // Edges that have Neighbors
             List<EdgeSide> blockedEdges = new List<EdgeSide>();
             for(int i=0; i<4; i++)
             {
                 EdgeSide side = (EdgeSide)i;
                 if (neighborExists[i])
                 {
                     var tdSide = (TowerDefensePathGenerator.EdgeSide)side;
                     bool hasStart = startPoints.Any(p => p.edge == tdSide);
                     bool hasEnd = endPoints.Any(p => p.edge == tdSide);
                     
                     if (!hasStart && !hasEnd) blockedEdges.Add(side);
                 }
                 else
                 {
                     // New Check: Blocked by Terrain (Obstacles)?
                     // Check entire edge? Or just treat as potentially valid unless ALL blocked?
                     // For simplicity, we check individually in TryAddEdgePoint.
                 }
             }
             
             // 3. Add Points if Missing
             if (startPoints.Count == 0 && internalStartPoints.Count == 0)
             {
                 if (randomizeStart)
                 {
                     if (startOnEdge) TryAddEdgePoint(startPoints, blockedEdges, prng, obstacles, w, h);
                     else AddInternalPoint(internalStartPoints, prng, w, h);
                 }
                 else
                 {
                     // Fixed
                     AddFixedPoint(internalStartPoints, fixedStart, w, h);
                 }
             }
             
             if (endPoints.Count == 0 && internalEndPoints.Count == 0)
             {
                 if (randomizeEnd)
                 {
                     // Avoid placing End on same edge as Start if possible?
                     if (endOnEdge) TryAddEdgePoint(endPoints, blockedEdges, prng, obstacles, w, h);
                     else AddInternalPoint(internalEndPoints, prng, w, h);
                 }
                 else
                 {
                     AddFixedPoint(internalEndPoints, fixedEnd, w, h);
                 }
             }
        }
        
        private void TryAddEdgePoint(List<PathPoint> list, List<EdgeSide> blocked, System.Random prng, bool[,] obstacles, int w, int h)
        {
             List<EdgeSide> allowed = new List<EdgeSide> { EdgeSide.Left, EdgeSide.Right, EdgeSide.Top, EdgeSide.Bottom };
             allowed.RemoveAll(e => blocked.Contains(e));
             
             if (allowed.Count == 0) return; // Trapped! Use internal fallback?
             
             // Try up to 10 times to find a valid spot
             for(int i=0; i<10; i++)
             {
                 EdgeSide side = allowed[prng.Next(allowed.Count)];
                 float pos = (float)prng.NextDouble();
                 
                 // Check Obstacles
                 Vector2Int coord = GetEdgePoint((TowerDefensePathGenerator.EdgeSide)side, pos, w, h);
                 if (!obstacles[coord.x, coord.y])
                 {
                     list.Add(new PathPoint { edge = (TowerDefensePathGenerator.EdgeSide)side, position = pos });
                     return;
                 }
             }
             
             // If failed, force it: Pick first allowed side and average pos?
             EdgeSide fallback = allowed[0];
             list.Add(new PathPoint { edge = (TowerDefensePathGenerator.EdgeSide)fallback, position = 0.5f });
        }

        private void AddInternalPoint(List<PathPoint> list, System.Random prng)
        {
            // Internal point represented as special EdgeSide? Or Position > 1?
            // PathPoint struct has float 'position'. 
            // We need 2D coordinates. 
            // Hack: Store X in 'position' and Y in... edge? No.
            // Let's rely on a custom convention or update the Struct.
            // We can't update Struct (compatibility).
            // Let's use EdgeSide.Left but encode (x, y) into 'position' somehow? No precision loss.
            // Better: Add a flag?
            // Actually, we can just treat 'position' as packed float? 
            // x = floor(pos), y = frac(pos)? No.
            // Let's assume PathPoint supports "Internal" via negative edge enum?
            // No, enum is fixed.
            
            // Wait, tower defense path generator points are strictly Edge Points (0-1).
            // If I want internal points, I should handle them separately in THIS class.
            // BUT startPoints list is type PathPoint.
            
            // Solution: Use a separate list for Internal Points?
            // OR overload PathPoint usage: 
            // If position > 2.0f, it's packed coordinate? (x + y * width)?
            // Max map size 100x100 -> 10000. Float precision is fine.
            // Let's use: position = x + (y * 10000). 
            // And maybe edge = (EdgeSide)(-1) or ignore edge.
            
            float packed = prng.Next(0, 10000) / 10000f; // Just a random 0-1 for now placeholder?
            // Real internal random:
            // We need 0..1 for X and 0..1 for Y.
            // Let's define: EdgeSide = Left (0), Position = -1 (Indicator) -> Then stored somewhere else?
            
            // Alternative: Add internal list.
            // BUT Apply() uses startPoints to generate coords.
            // So let's add `internalStartPoints` list and merge them in `Resolve Coords`.
            
            // For now, I'll use the hack: position = -1.0f means "Internal Random to be resolved later"?
            // No, we need to store the Coordinate.
            // Let's just use `fixedStart` variable for internal random result?
            // If random internal -> Overwrite `fixedStart` with random value?
            // But `fixedStart` is public field. Modifying it at runtime in editor is weird but okay for runtime logic.
            // But we have `startPoints` list.
            
            // Let's stick with:
            // `startPoints` list is for EDGE points (from neighbors or random edge).
            // `internalStarts` list for internal points.
            
            // No, simpler: 
            // Use `PathPoint` with `edge = (EdgeSide)99` (Use a cast to int then cast back, Enum can hold any int).
            // `position` = x + y * 0.001f? (assuming 1000x1000 max).
        }
        
        // BETTER APPROACH:
        // Don't use PathPoint for Internal.
        // In Step 3 (Resolve Coords), we iterate `startPoints` AND `internalStartPoints`.
        // `GenerateSmartPoints` will populate `internalStartPoints` if needed.
        




        // --- Stitching Implementation ---
        
        public void SetNeighborExistence(EdgeSide side, bool exists)
        {
            neighborExists[(int)side] = exists;
        }

        public void ClearStitching()
        {
            startPoints.Clear();
            endPoints.Clear();
            for(int i=0; i<4; i++) neighborExists[i] = false;
        }

        public object GetEdgeData(EdgeSide side)
        {
             // Package our points on this edge
             PathStitchData data = new PathStitchData();
             foreach(var p in startPoints) if (p.edge == (TowerDefensePathGenerator.EdgeSide)side) data.starts.Add(p);
             foreach(var p in endPoints) if (p.edge == (TowerDefensePathGenerator.EdgeSide)side) data.ends.Add(p);
             return data;
        }

        public void InjectEdgeData(object data, EdgeSide side)
        {
             var packet = data as PathStitchData;
             if (packet == null) return;
             
             // Neighbor's Ends -> My Starts
             foreach(var p in packet.ends)
             {
                 startPoints.Add(new PathPoint { edge = (TowerDefensePathGenerator.EdgeSide)side, position = p.position });
             }
             // Neighbor's Starts -> My Ends
             foreach(var p in packet.starts)
             {
                 endPoints.Add(new PathPoint { edge = (TowerDefensePathGenerator.EdgeSide)side, position = p.position });
             }
        }

        // --- Helpers ---

        private void AddInternalPoint(List<Vector2Int> list, System.Random prng, int w, int h)
        {
            list.Add(new Vector2Int(prng.Next(1, w - 1), prng.Next(1, h - 1)));
        }
        
        private void AddFixedPoint(List<Vector2Int> list, Vector2Int point, int w, int h)
        {
            // Clamp
            point.x = Mathf.Clamp(point.x, 0, w - 1);
            point.y = Mathf.Clamp(point.y, 0, h - 1);
            list.Add(point);
        }

        // --- Helpers ---
        
        private bool IsInternal(PathPoint p)
        {
            // Unused with separate lists approach
            return false;
        }
        
        private Vector2Int GetInternalPoint(PathPoint p, int w, int h)
        {
            return Vector2Int.zero; 
        }

        private Vector2Int GetEdgePoint(TowerDefensePathGenerator.EdgeSide edge, float t, int w, int h)
        {
             t = Mathf.Clamp01(t);
             switch (edge)
             {
                 case TowerDefensePathGenerator.EdgeSide.Left: return new Vector2Int(0, Mathf.RoundToInt(t * (h - 1)));
                 case TowerDefensePathGenerator.EdgeSide.Right: return new Vector2Int(w - 1, Mathf.RoundToInt(t * (h - 1)));
                 case TowerDefensePathGenerator.EdgeSide.Top: return new Vector2Int(Mathf.RoundToInt(t * (w - 1)), h - 1);
                 case TowerDefensePathGenerator.EdgeSide.Bottom: return new Vector2Int(Mathf.RoundToInt(t * (w - 1)), 0);
             }
             return Vector2Int.zero;
        }
        
        private void DrawPoint(Texture2D map, Vector2Int center, Color c, int w, int h)
        {
             for(int dx = -pathWidth/2; dx <= pathWidth/2; dx++) for(int dy = -pathWidth/2; dy <= pathWidth/2; dy++)
             {
                 int px = center.x + dx; int py = center.y + dy;
                 if(px >=0 && px < w && py>=0 && py < h) map.SetPixel(px, py, c);
             }
        }
        
        private bool IsColorMatch(Color a, Color b)
        {
             return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
        }

        // A* Logic (Simplified)
        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, bool[,] walls, int w, int h)
        {
             // ... Standard A* ...
             var openSet = new List<Node>();
             var closedSet = new HashSet<Vector2Int>();
             openSet.Add(new Node(start, 0, GetHeuristic(start, end), null));
             
             int safety = 0;
             while(openSet.Count > 0 && safety < 10000)
             {
                 safety++;
                 openSet.Sort((a,b)=>a.f.CompareTo(b.f));
                 Node current = openSet[0];
                 openSet.RemoveAt(0);
                 
                 if (current.pos == end) return Retrace(current);
                 if (closedSet.Contains(current.pos)) continue;
                 closedSet.Add(current.pos);
                 
                 foreach(var d in new Vector2Int[]{Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right})
                 {
                     Vector2Int np = current.pos + d;
                     if(np.x<0||np.x>=w||np.y<0||np.y>=h) continue;
                     if(walls[np.x, np.y]) continue;
                     
                     float cost = 1 + turnCost; 
                     openSet.Add(new Node(np, current.g+cost, GetHeuristic(np, end), current));
                 }
             }
             return null;
        }
        
        private List<Vector2Int> ConnectToSet(Vector2Int start, HashSet<Vector2Int> targets, bool[,] walls, int w, int h)
        {
             // BFS to find nearest
             // Or A* to nearest (Multi-target A*)
             // Simplified: Just A* to the closest Euclidean target
             Vector2Int best = Vector2Int.zero;
             float dist = float.MaxValue;
             foreach(var t in targets)
             {
                 float d = GetHeuristic(start, t);
                 if (d < dist) { dist = d; best = t; }
             }
             return FindPath(start, best, walls, w, h);
        }

        private List<Vector2Int> Retrace(Node n)
        {
            List<Vector2Int> p = new List<Vector2Int>();
            while(n!=null) { p.Add(n.pos); n=n.parent; }
            p.Reverse();
            return p;
        }
        
        private float GetHeuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        
        class Node
        {
            public Vector2Int pos;
            public float g, h;
            public float f => g+h;
            public Node parent;
            public Node(Vector2Int p, float g, float h, Node parent) { pos=p; this.g=g; this.h=h; this.parent=parent; }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TerrainGenerator.Jobs;

namespace TerrainGenerator
{
    public class WFCBuilder : MonoBehaviour
    {
        [Header("Grid Configuration")]
        public Grid unityGrid;
        public Vector3Int gridSize = new Vector3Int(5, 5, 5);
        [Tooltip("Measurement offset within cell. (0.5, 0, 0.5) puts anchor on ground, centered.")]
        public Vector3 cellAlignment = new Vector3(0.5f, 0f, 0.5f);
        public WFCCell cellPrefab;
        public bool autoResizeGridToMap = true;
        
        [Header("Strict Layer Settings")]
        public bool strictLayerHeight = true;
        [Tooltip("Module to use for empty/undefined space.")]
        public WFCModule defaultEmptyModule; // User must assign "Empty" or "Air" logic
        
        // [Header("Dual Grid Visualization")]
        // useDualGrid moved to Layers
        public WFCDualGridManager dualGridManager = new WFCDualGridManager();
        
        [Header("Seed Settings")]
        public int seed;
        public bool useRandomSeed = true;

        [Header("Blueprints Definitions")]
        public List<WFCBlueprintLayer> definedBlueprints = new List<WFCBlueprintLayer>();

        [Header("Build Layers (Stack)")]
        public List<WFCBuildLayer> buildLayers = new List<WFCBuildLayer>();

        public WFCSolver solver = new WFCSolver();
        
        [Tooltip("Use Jobs + Burst for faster WFC generation. Requires Burst package.")]
        public bool useBurstAcceleration = false;


        [Header("Runtime")]
        public bool generateOnStart = true; // Control whether to auto-run on Start

        private void Awake()
        {
            // Solver is pure class, no component needed.
        }

        private void Start()
        {
             if (generateOnStart) 
             {
                 Generate();
             }
        }

        [ContextMenu("Build (Exec Layers)")]
        public void Generate()
        {
            // if (solver == null) solver = new WFCSolver(); // Already initialized inline
            
            solver.OnCellCollapsed = null; // Reset events
            solver.runRNG = null; // FORCE UNSET RNG so ExecuteAllBlueprints re-initializes it!
            
            // Validation
            if (cellPrefab == null) { Debug.LogError("WFCBuilder: Cell Prefab is missing!"); return; }
            if (unityGrid == null) { Debug.LogError("WFCBuilder: Unity Grid is missing!"); unityGrid = GetComponent<Grid>(); }

            if (this != null && gameObject.activeInHierarchy) 
            {
               StartCoroutine(GenerateRoutine());
            }
        }
        
        private System.Collections.IEnumerator GenerateRoutine()
        {
            // 0. Ensure Blueprints are Generated (Texture Data Ready)
            ExecuteAllBlueprints();

            // 1. Calculate Grid Size
            Vector3Int size = gridSize; // Default
            if (autoResizeGridToMap && buildLayers.Count > 0)
            {
                foreach (var layer in buildLayers)
                {
                     var bp = GetBlueprint(layer.blueprintName);
                     if (bp != null && bp.outputMap != null)
                     {
                         size.x = bp.outputMap.width;
                         size.z = bp.outputMap.height;
                         break;
                     }
                }
            }

            // 2. Gather Modules
            List<WFCModule> allModules = new List<WFCModule>();
            HashSet<WFCModule> uniqueModules = new HashSet<WFCModule>();
            
            foreach(var layer in buildLayers)
            {
                if (layer != null && layer.active)
                {
                    foreach(var item in layer.presets)
                    {
                        if (item.preset != null)
                        {
                            foreach(var m in item.preset.modules) 
                                if(m!=null) uniqueModules.Add(m);
                        }
                    }
                }
            }
            // Fix for Determinism: Sort modules by name to ensure consistent index mapping
            allModules = uniqueModules.OrderBy(m => m.name).ToList();

            // 3. Setup Container
            Transform container = transform.Find("Cells Container");
            if (container == null)
            {
                GameObject go = new GameObject("Cells Container");
                go.transform.SetParent(this.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                container = go.transform;
            }

            // 4. Initialize Solver
            // Cleanup previous Dual Grid visuals
            if (dualGridManager != null) dualGridManager.Clear();

            HashSet<int> activeYLevels = null;
            HashSet<int> dualGridYLevels = new HashSet<int>();

            if (strictLayerHeight)
            {
                activeYLevels = new HashSet<int>();
                foreach(var layer in buildLayers)
                {
                    if(layer.active) 
                    {
                        activeYLevels.Add(layer.yOffset);
                        if (layer.useDualGrid) dualGridYLevels.Add(layer.yOffset);
                    }
                }
            }
            else
            {
                // If not strict, checking for dual grid globally is harder unless we scan active layers anyway
                foreach(var layer in buildLayers)
                     if(layer.active && layer.useDualGrid) dualGridYLevels.Add(layer.yOffset);
            }
            
            // Check if any layer needs Dual Grid
            // bool anyDualGrid = buildLayers.Any(l => l.active && l.useDualGrid);
            
            // Initialize Solver with Dual Grid Mask
            solver.Initialize(size, allModules, unityGrid, cellPrefab, cellAlignment, this, container, activeYLevels, dualGridYLevels);
            
            // Always initialize manager to ensure it has reference to container (for clearing or future use)
            // But we only subscribe if active
            dualGridManager.Initialize(solver, this.transform, dualGridYLevels); // Passes active levels
            
            if (dualGridYLevels.Count > 0)
            {
                solver.OnCellCollapsed += dualGridManager.UpdateAround;
            }
            
            // Listen for completion
            solver.OnFinished = () => {
                WFCEvent.TriggerChunkGenerated(this);
            };
            
            // Hook up variations resolution after WFC completes
            solver.OnFinished += () => solver.ResolveVariations();

            // 5. Apply Map Constraints (Pre-Collapse)
            ApplyBlueprints(size);

            // 6. Run WFC Solver
            if (useBurstAcceleration)
            {
                var burstRunner = new WFCBurstRunner(
                    solver.visualCells,
                    solver.allModules,
                    GetConstraintsFromSolver(),
                    size,
                    solver.OnFinished,
                    solver.OnCellCollapsed
                );
                yield return StartCoroutine(burstRunner.Run(solver.maxRetries));
            }
            else
            {
                yield return StartCoroutine(solver.RunWFC());
            }
            
            // 7. Generate Blueprints (Textures)
            // Note: We run this AGAIN here just in case? Or is it redundant?
            // ExecuteAllBlueprints(); // Redundant. Removed. Or maybe needed for final map? 
             // If modifiers are deterministic, it's fine.
            // But wait, generate routine flow:
            // 1. Solve (Cells collapsed)
            // 2. Resolve Variations (in OnFinished)
            // 3. Generate Blueprints?
            // Usually blueprints define constraints BEFORE solving.
            // But PathfindingGenerator runs AFTER? Or is it Modifier?
            // Modifiers run inside ApplyBlueprints? No.
            // Wait, ApplyBlueprints iterates BuildLayers and calls ForceCollapse based on COLOR.
            // The COLOR comes from `bp.outputMap`.
            // So `bp.Generate()` MUST run BEFORE `ApplyBlueprints`.
            // So `ExecuteAllBlueprints()` at start (Step 0) is correct.
            // Do we need to run it AGAIN after?
            // Only if Modifiers depend on WFC result (e.g. modify texture based on collapse).
            // PathfindingGenerator seems independent?
            // But wait, visualizer applies textures to MESHES.
            // If Modifiers update texture, we want meshes to update?
            // Visualizer `ApplyToGrid` (if it existed) would update meshes.
            // But Visualizer uses `solver.visualCells`.
            // So Modifiers only affect WFC via constraints (ForceCollapse).
            // So running ExecuteAllBlueprints at end is irrelevant for WFC constraints.
            // BUT maybe for `SpawnObjectsFromBlueprints` we need fresh data?
            // `Generate()` clears data.
            // So running it once at start is enough.
            
            // 8. Spawn Objects (New)
            SpawnObjectsFromBlueprints();
        }
        
        private List<GameObject> spawnedObjects = new List<GameObject>();
        
        private void ClearSpawnedObjects()
        {
            foreach(var obj in spawnedObjects)
            {
                if (obj != null) 
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }
            spawnedObjects.Clear();
        }
        
        private void SpawnObjectsFromBlueprints()
        {
            foreach(var bp in definedBlueprints)
            {
                if (bp.spawnCommands != null)
                {
                    foreach(var cmd in bp.spawnCommands)
                    {
                        if (cmd.prefab != null)
                        {
                            // Convert local grid pos to World Pos
                            // cmd.position is likely (x, 0, y) in Grid Coords.
                            // We need to scale by Grid Size?
                            // Wait, PathfindingGenerator uses (x, 0, y) which are Integer Grid Coordinates.
                            // WFCBuilder visuals are scaled by 'gridSize' (or worldScale?).
                            // solver.worldScale is used for positioning chunks.
                            // But CreateChunkInitialized sets: `float xPos = coord.x * chunkSize.x * worldScale;`
                            // So the Chunk Transform is at the corner.
                            // Local Position inside chunk = GridCoord * worldScale?
                            // Let's check ApplyToGrid or visualizer.
                            // `solver.GetVisualCellAt`...
                            
                            // WFCModule usually has size 1x1x1 in local space if scale is 1?
                            // `worldScale` public float.
                            // If `worldScale` is 4.0f, then cell (1,0) is at local (4, 0, 0).
                            
                            Vector3 localPos = new Vector3(cmd.position.x * solver.worldScale, cmd.position.y * solver.worldScale, cmd.position.z * solver.worldScale);
                            // Adjust for cell center? Paths are on grid nodes (0..w).
                            // If x=0, that's the center of cell 0? Or corner?
                            // Usually center.
            
                            GameObject instance = Instantiate(cmd.prefab, this.transform);
                            instance.transform.localPosition = localPos;
                            instance.transform.localRotation = cmd.rotation;
                            spawnedObjects.Add(instance);
                        }
                    }
                }
            }
        }
        
        public WFCBlueprintLayer GetBlueprint(string name)
        {
            return definedBlueprints.Find(b => b.layerName == name);
        }
        
        // Editor Helper
        public void ExecuteAllBlueprints()
        {
             int w = gridSize.x;
             int h = gridSize.z;
             List<WFCBlueprintLayer> context = new List<WFCBlueprintLayer>();

             // Initialize RNG if not set (Standalone Mode)
             if (solver.runRNG == null)
             {
                 int initSeed = seed;
                 if (useRandomSeed) 
                 {
                     initSeed = Random.Range(0, 1000000); // Unity Random
                     seed = initSeed; // Save for Replayability
                     Debug.Log($"[WFCBuilder] Initializing Standalone RNG with Random Seed: {initSeed}");
                 }
                 else
                 {
                     Debug.Log($"[WFCBuilder] Initializing Standalone RNG with Fixed Seed: {initSeed}");
                 }
                 solver.runRNG = new System.Random(initSeed);
             }
             
             // Generate a master seed for blueprints
             int blueprintSeed = solver.runRNG.Next(); 

             foreach(var bp in definedBlueprints)
             {
                 if(bp.active)
                 {
                     bp.Generate(w, h, context, blueprintSeed);
                     context.Add(bp);
                 }
             }
        }

        private void ApplyBlueprints(Vector3Int size)
        {
            // Apply Layers (Pre-Collapse) & Strict Height
            // Apply Layers (Pre-Collapse) & Strict Height
            // Note: strict check is now handled during Initialization (Sparse Grid)


            foreach(var layer in buildLayers)
            {
                if (!layer.active) continue;
                var bp = GetBlueprint(layer.blueprintName);
                if (bp == null) continue; // Or handle pure generators
                
                // For each cell in Map...
                int w = (bp.outputMap != null) ? bp.outputMap.width : size.x;
                int h = (bp.outputMap != null) ? bp.outputMap.height : size.z;
                
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        Color pixel = (bp.outputMap != null) ? bp.outputMap.GetPixel(x, y) : Color.white;
                        int targetLayerIdx;
                        WFCModule forcedMod = layer.GetModuleForColor(pixel, out targetLayerIdx);
                        
                        if (forcedMod != null)
                        {
                            Vector3Int pos = new Vector3Int(x, layer.yOffset, y); 
                            solver.ForceCollapse(pos, forcedMod);
                        }
                    }
                }
            }

            // Strict Layer Logic: Loop REMOVED. 
            // We now skip creating cells at invalid Y levels in Solver.Initialize
            if (strictLayerHeight && defaultEmptyModule == null)
            {
                 // Debug.LogWarning("WFCBuilder: Strict Layer Height is ON (Sparse Mode)");
            }

            // 5. Run Solver
            // NOTE: Moved to Generate() to support Burst toggle
        }

        // Helper to get constraints for Burst runner
        private Dictionary<Vector3Int, WFCModule> GetConstraintsFromSolver()
        {
            // Build constraints from what was applied via ForceCollapse
            // Since constraints is private in solver, we rebuild from initial queue state
            var result = new Dictionary<Vector3Int, WFCModule>();
            foreach (var cell in solver.allCells)
            {
                if (cell.Collapsed && cell.PossibleModules.Count > 0)
                {
                    result[cell.GridPosition] = cell.PossibleModules[0];
                }
            }
            return result;
        }
    }
}

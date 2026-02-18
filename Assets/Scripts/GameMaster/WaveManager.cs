using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TerrainGenerator; // Namespace for WFC

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Settings")]
    public float timeBetweenWaves = 5f;
    public float timeBetweenSpawns = 0.5f;

    public GameInputs inputs;

    [Header("Wave Configuration (Simple List for Phase E)")]
    public List<WaveConfig> waves = new List<WaveConfig>();

    public int currentWaveIndex = 0;
    public bool isWaveActive = false;
    public int enemiesAlive = 0;
    
    // Phase F: Event Driven
    private bool startWaveWhenPathReady = false;

    // References
    private WFCWorldManager worldManager;
    private Pathfinder pathfinder;

    [System.Serializable]
    public class WaveConfig
    {
        public WaveConfigSO config; // Optional Override
        public List<EnemyGroup> groups = new List<EnemyGroup>();
    }

    [System.Serializable]
    public class EnemyGroup
    {
        public GameObject enemyPrefab;
        public int count;
        public float rate;
        public float delayBefore; // Delay before starting this group
    }

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
        if (pathfinder == null) 
            pathfinder = FindFirstObjectByType<Pathfinder>();

        if (pathfinder == null) Debug.LogError("WaveManager needs Pathfinder!");
    }

    private void OnEnable()
    {
        GameEvents.OnMapExpansionStarted += HandleMapExpansionStarted;
        GameEvents.OnPathfinderGraphRebuilt += HandlePathfinderGraphRebuilt;
    }

    private void OnDisable()
    {
        GameEvents.OnMapExpansionStarted -= HandleMapExpansionStarted;
        GameEvents.OnPathfinderGraphRebuilt -= HandlePathfinderGraphRebuilt;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleMapExpansionStarted(object sender, System.EventArgs e)
    {
        // When map expands, we want to start wave as soon as path is ready
        startWaveWhenPathReady = true;
    }

    private void HandlePathfinderGraphRebuilt(object sender, System.EventArgs e)
    {
        if (startWaveWhenPathReady)
        {
            StartNextWave();
            startWaveWhenPathReady = false;
        }
    }

    private void Update()
    {
        if (inputs.IsTestPressed())
        {
            Debug.Log("[WaveManager] Space pressed - Starting Next Wave");
            StartNextWave();
        }
    }

    // Public method to start next wave (UI button hook)
    public void StartNextWave()
    {
        if (isWaveActive) return;
        
        // Before starting, ensure path is valid!
        RebuildPath();
        
        if (currentWaveIndex < waves.Count)
        {
            StartCoroutine(SpawnWave(waves[currentWaveIndex]));
        }
        else
        {
            Debug.Log("All waves complete! Infinite mode?");
            // Loop last wave with scaling difficulty?
            if (waves.Count > 0)
            {
                var lastWave = waves[waves.Count - 1]; // Reuse last config
                // Create scaled copy? Hard to deep copy struct in list.
                // Just respawn it.
                // Difficulty scaling via Enemy? Not implemented yet.
                StartCoroutine(SpawnWave(lastWave));
            }
        }
    }

    private void RebuildPath()
    {
        if (pathfinder != null)
        {
            pathfinder.RebuildGraph();
        }
    }

    private IEnumerator SpawnWave(WaveConfig wave)
    {
        isWaveActive = true;
        
        // Calculate Total Enemies
        enemiesAlive = 0;
        if (wave.config != null)
        {
             foreach(var g in wave.config.groups) enemiesAlive += g.count;
        }
        else
        {
             foreach(var g in wave.groups) enemiesAlive += g.count;
        }
        
        // Get ALL spawn points
        List<Vector3> spawnPoints = pathfinder.GetAllSpawnPoints();
        
        if (spawnPoints.Count == 0 || enemiesAlive == 0)
        {
           if (spawnPoints.Count == 0) Debug.LogError("No valid spawn points found! Is the map generated?");
           isWaveActive = false;
           yield break;
        }

        // Pre-calculate paths for all points
        Dictionary<Vector3, List<Vector3>> spawnPaths = new Dictionary<Vector3, List<Vector3>>();
        List<Vector3> validSpawns = new List<Vector3>();

        foreach(var sp in spawnPoints)
        {
            List<Vector3> p = pathfinder.GetPathToBase(sp);
            if (p != null && p.Count > 0)
            {
                spawnPaths[sp] = p;
                validSpawns.Add(sp);
            }
        }

        if (validSpawns.Count == 0)
        {
            Debug.LogError("No valid paths to base found from any spawn point!");
            isWaveActive = false;
            yield break;
        }

        // Spawn Groups Sequentially
        List<EnemyGroup> groupsToSpawn = (wave.config != null) ? wave.config.groups : wave.groups;

        foreach(var group in groupsToSpawn)
        {
            if (group.delayBefore > 0) yield return new WaitForSeconds(group.delayBefore);

            for (int i = 0; i < group.count; i++)
            {
                // Pick Random Spawn Point (Round Robin or Pure Random)
                Vector3 chosenSpawn = validSpawns[Random.Range(0, validSpawns.Count)];
                List<Vector3> chosenPath = spawnPaths[chosenSpawn];
                
                SpawnEnemy(group.enemyPrefab, chosenSpawn, chosenPath);
                
                if (group.rate > 0) yield return new WaitForSeconds(1f / group.rate);
            }
        }
        
        currentWaveIndex++;
    }

    private void SpawnEnemy(GameObject prefab, Vector3 position, List<Vector3> path)
    {
        if (prefab == null) return;
        
        // Fix: Spawn slightly above path to avoid clipping
        Vector3 spawnPos = position + Vector3.up * 1.0f; // +1 Y offset
        
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
        Enemy enemy = go.GetComponent<Enemy>();
        
        if (enemy != null)
        {
            enemy.SetPath(path);
        }
    }

    public void OnEnemyDeath()
    {
        enemiesAlive--;
        if (enemiesAlive <= 0)
        {
            EndWave();
        }
    }

    private void EndWave()
    {
        isWaveActive = false;
        Debug.Log("Wave Complete!");
        
        // Pass wave number (1-indexed for display)
        GameEvents.TriggerWaveCompleted(this, currentWaveIndex + 1);
    }
}

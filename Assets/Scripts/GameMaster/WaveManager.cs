using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Systems; // Namespace for Pathfinder
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

    [Header("Debug")]
    public int currentWaveIndex = 0;
    public bool isWaveActive = false;
    public int enemiesAlive = 0;

    // References
    private WFCWorldManager worldManager;
    private Pathfinder pathfinder;

    [System.Serializable]
    public class WaveConfig
    {
        public GameObject enemyPrefab;
        public int count;
        public float rate;
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
        pathfinder = FindFirstObjectByType<Pathfinder>();

        if (pathfinder == null) Debug.LogError("WaveManager needs Pathfinder!");
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
                lastWave.count += 5; // Difficulty scaling
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
        enemiesAlive = wave.count;
        
        // Get ALL spawn points
        List<Vector3> spawnPoints = pathfinder.GetAllSpawnPoints();
        
        if (spawnPoints.Count == 0)
        {
           Debug.LogError("No valid spawn points found! Is the map generated?");
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

        for (int i = 0; i < wave.count; i++)
        {
            // Pick Random Spawn Point (Round Robin or Pure Random)
            // Use Random.Range to pick one
            Vector3 chosenSpawn = validSpawns[Random.Range(0, validSpawns.Count)];
            List<Vector3> chosenPath = spawnPaths[chosenSpawn];
            
            SpawnEnemy(wave.enemyPrefab, chosenSpawn, chosenPath);
            yield return new WaitForSeconds(1f / wave.rate);
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
        
        // Phase F Integration: Trigger Map Expansion Here?
        // Or Enable "Expand" Button in UI?
        
        // For now, auto-expand for testing?
        if (worldManager != null)
        {
             // worldManager.ExpandNextRing(); // Commented out for now, let player control or UI flow
             // Debug.Log("Map Expansion Available!");
        }
    }
}

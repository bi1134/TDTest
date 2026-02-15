using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWave", menuName = "TowerDefense/Wave Config")]
public class WaveConfigSO : ScriptableObject
{
    // Reuse WaveManager.EnemyGroup struct? Or define here?
    // It's cleaner to define it here or in a centralized file.
    // For now, let's redefine it or move it out of WaveManager if possible.
    // WaveManager.EnemyGroup is nested. Can be messy.
    // Let's make EnemyGroup a standalone struct or nested here?
    // Changing WaveManager structure is risky if user assigned data.
    // But WaveManager uses WaveConfig class.
    
    // Use WaveManager's EnemyGroup for compatibility
    public List<WaveManager.EnemyGroup> groups = new List<WaveManager.EnemyGroup>();
}

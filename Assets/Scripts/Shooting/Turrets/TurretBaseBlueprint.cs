using UnityEngine;

[System.Serializable]
public class TurretBaseBlueprint
{
    public GameObject prefab;
    public int cost;
}

[System.Serializable]
public class BulletBlueprint
{
    public BulletProjectile bulletPrefab;
    
    [Tooltip("Bullet properties SO for elemental effects")]
    public BulletPropertiesSO bulletProperties;
    
    public int cost;
}

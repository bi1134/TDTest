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
    public int cost;
}

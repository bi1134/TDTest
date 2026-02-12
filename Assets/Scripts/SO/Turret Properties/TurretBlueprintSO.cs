using UnityEngine;

[CreateAssetMenu(fileName = "New Turret Blueprint", menuName = "Tower Defense/Turret Blueprint")]
public class TurretBlueprintSO : ScriptableObject
{
    public string turretName;
    public GameObject prefab;
    public int cost;
    public Sprite icon;
    
    [Header("Default Configuration")]
    public BulletBlueprintSO defaultBullet;

    [TextArea]
    public string description;
}

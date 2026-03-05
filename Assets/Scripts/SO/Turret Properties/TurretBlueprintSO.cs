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

    [Header("Shop Display")]
    [Tooltip("Optional 3D mesh prefab for UI preview. Falls back to icon sprite if null.")]
    public GameObject displayMesh;

    [Tooltip("If true, this item is removed from shop when quantity reaches 0.")]
    public bool removeWhenEmpty = false;
}

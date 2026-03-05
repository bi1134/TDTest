using UnityEngine;

[CreateAssetMenu(fileName = "New Bullet Blueprint", menuName = "Tower Defense/Bullet Blueprint")]
public class BulletBlueprintSO : ScriptableObject
{
    public string bulletName;
    public BulletPropertiesSO bulletProperties; // Holds Visuals + Stats
    public int cost;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Shop Display")]
    [Tooltip("Optional 3D mesh prefab for UI preview. Falls back to icon sprite if null.")]
    public GameObject displayMesh;

    [Tooltip("If true, this item is removed from shop when quantity reaches 0.")]
    public bool removeWhenEmpty = false;
}

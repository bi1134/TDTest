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
}

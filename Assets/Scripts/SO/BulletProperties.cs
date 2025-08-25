using UnityEngine;

[CreateAssetMenu(fileName = "BulletProperties", menuName = "Scriptable Objects/BulletProperties")]
public class BulletPropertiesSO : ScriptableObject
{
    public BulletType bulletType;
    public int maxBounces = 2;
    public float maxLifeTime = 3f;
    public float bulletDrop;


    [Header("AOE stuff")]
    //AOE stuff
    public float explosionRadius = 0f;
    public float explosionForce = 0f;
    public LayerMask explosionMask;

    public string bulletPoolTag = "Bullet";
}

public enum BulletType
{
    Normal,
    Explosive,
    Electric,
    Fire,
    Ice
}
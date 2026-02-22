using UnityEngine;

[CreateAssetMenu(fileName = "VFXRefSO", menuName = "Scriptable Objects/VFXRef")]
public class VFXRefSO : ScriptableObject
{
    [Header("Enemy Impacts")]
    [Tooltip("Played when fleshy enemies take normal damage")]
    public GameObject[] bloodSplatters;
    [Tooltip("Played when hitting an enemy's active Barrier/Shield")]
    public GameObject[] shieldSparks;
    [Tooltip("Played when hitting an enemy with no special fleshy or shield property (Robots, generic hits, etc.)")]
    public GameObject[] defaultHits;
    [Tooltip("Played when an enemy dies")]
    public GameObject[] deathPoofs;

    [Header("World Impacts")]
    [Tooltip("Played when a bullet hits the ground")]
    public GameObject[] groundDust;
    
    [Header("Generic Effects")]
    [Tooltip("Standard explosion if the bullet doesn't have a unique one")]
    public GameObject[] genericExplosions;
}

public enum VFXType
{
    Blood,
    ShieldSpark,
    GroundDust,
    GenericExplosion,
    DefaultHit,
    DeathPoof
}

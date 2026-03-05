using UnityEngine;

[CreateAssetMenu(fileName = "VFXRefSO", menuName = "Scriptable Objects/VFXRef")]
public class VFXRefSO : ScriptableObject
{
    [Header("Enemy Impacts")]
    [Tooltip("Played when fleshy enemies take normal damage")]
    public GameObject[] bloodSplatters;
    [Tooltip("Played when hitting an enemy's active Barrier/Shield")]
    public GameObject[] shieldSparks;
    [Tooltip("Played when an enemy's shield is fully depleted")]
    public GameObject[] shieldBreaks;
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

    [Header("Item Effects")]
    [Tooltip("Played when an item/shard/interactable disappears or is collected")]
    public GameObject[] itemDisappear;
    
    [Header("Elemental Strike VFX (Beam / Pulse)")]
    [Tooltip("Lightning strike effect spawned at enemy feet for Electric beam/pulse attacks")]
    public GameObject[] electricStrike;
    [Tooltip("Ice zone effect spawned at enemy feet for Ice beam/pulse attacks")]
    public GameObject[] iceStrike;

    [Header("World Events")]
    [Tooltip("Effect played at the center of an expanding map chunk")]
    public GameObject[] chunkExpand;

    [Header("Turret Indicators")]
    [Tooltip("Looping effect on turrets with unacknowledged level-ups (set particle to Loop)")]
    public GameObject[] upgradeReady;
}

public enum VFXType
{
    Blood,
    ShieldSpark,
    ShieldBreak,
    GroundDust,
    GenericExplosion,
    DefaultHit,
    DeathPoof,
    ItemDisappear,
    ElectricStrike,
    IceStrike,
    ChunkExpand,
    UpgradeReady
}

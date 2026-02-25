using UnityEngine;

[CreateAssetMenu(fileName = "AudioRefClip", menuName = "Scriptable Objects/AudioRefClip")]
public class AudioRefClip : ScriptableObject
{
    [Header("UI Sounds")]
    public AudioClip[] buttonClick;
    public AudioClip[] buttonHover;
    public AudioClip[] uiCancelSounds;
    public AudioClip[] cardAppear;
    public AudioClip[] cardClick;
    public AudioClip[] cardHover;
    public AudioClip[] coinCollect;

    [Header("Game State")]
    public AudioClip[] gameResultWin;
    public AudioClip[] gameResultLose;

    [Header("Enemy Sounds")]
    public AudioClip[] enemyWalk;
    public AudioClip[] enemySprint;
    public AudioClip[] enemyGrunt;
    public AudioClip[] enemyHitFlesh;
    public AudioClip[] enemyHitShield;
    public AudioClip[] enemyDeath;
    public AudioClip[] enemyPoof;

    [Header("Turret Sounds")]
    public AudioClip[] turretShootCannon;
    public AudioClip[] turretShootCrossbow;
    public AudioClip[] turretShootMachineGun;
    public AudioClip[] turretShootMagic;
    public AudioClip[] turretBuild;
    public AudioClip[] turretSell;

    [Header("Elemental Turret Sounds")]
    public AudioClip[] elementalFire;
    public AudioClip[] elementalIce;
    public AudioClip[] elementalElectric;

    [Header("Bullet Sounds")]
    public AudioClip[] bulletHitEnemy;
    public AudioClip[] bulletHitGround;
    public AudioClip[] aoeExplosion;

    [Header("Ambience & Music")]
    public AudioClip[] backgroundMusic;
    public AudioClip[] ambientLoops;

    [Header("World Events")]
    [Tooltip("Poof sound when a map chunk is expanded")]
    public AudioClip[] chunkExpand;
    [Tooltip("Sound when an enemy's shield is completely destroyed")]
    public AudioClip[] shieldBreak;
    [Tooltip("Sound when an enemy's barrier/directional shield is destroyed")]
    public AudioClip[] barrierBreak;
}

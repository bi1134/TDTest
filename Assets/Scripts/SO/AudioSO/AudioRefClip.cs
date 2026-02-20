using UnityEngine;

[CreateAssetMenu(fileName = "AudioRefClip", menuName = "Scriptable Objects/AudioRefClip")]
public class AudioRefClip : ScriptableObject
{
    [Header("UI Sounds")]
    public AudioClip[] buttonClick;
    public AudioClip[] buttonHover;
    public AudioClip[] cardAppear;
    public AudioClip[] cardClick;
    public AudioClip[] cardHover;
    public AudioClip[] coinCollect;

    [Header("Game State")]
    public AudioClip[] gameResultWin;
    public AudioClip[] gameResultLose;

    [Header("Enemy Sounds")]
    public AudioClip[] enemyWalk;
    public AudioClip[] enemyGrunt;
    public AudioClip[] enemyHitFlesh;
    public AudioClip[] enemyHitShield;
    public AudioClip[] enemyDeath;

    [Header("Turret Sounds")]
    public AudioClip[] turretShootProjectile; // Physical bullets (Pistol, Sniper, etc)
    public AudioClip[] turretShootMagic;      // Magic/Elemental (Staff)
    public AudioClip[] turretBuild;
    public AudioClip[] turretSell;

    [Header("Bullet Sounds")]
    public AudioClip[] bulletHitEnemy;
    public AudioClip[] bulletHitGround;
    public AudioClip[] aoeExplosion;

    [Header("Ambience & Music")]
    public AudioClip[] backgroundMusic;
    public AudioClip[] ambientLoops;
}

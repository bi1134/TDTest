using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioRefClip audioClipRefsSO;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource audioSourceFixed;

    // Ensure AudioSource arrays or generic refs are used safely
    private void OnEnable()
    {
        AssignSignal();
    }
    #region UI Sounds
    private void OnAnyButtonClicked(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.buttonClick);
    private void OnAnyButtonHovered(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.buttonHover);
    private void OnCardAppeared(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.cardAppear);
    private void OnCardClicked(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.cardClick);
    private void OnCardHovered(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.cardHover);
    private void OnCoinCollected(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.coinCollect);
    #endregion

    #region Game State
    private void OnGameWon(object sender, System.EventArgs e)
    {
        PlaySoundFixed(audioClipRefsSO.gameResultWin);
    }
    private void OnGameLost(object sender, System.EventArgs e)
    {
        PlaySoundFixed(audioClipRefsSO.gameResultLose);
    }
    #endregion

    #region Enemy Sounds
    private void OnEnemyWalk(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.enemyWalk, 0.5f);
    private void OnEnemyGrunt(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.enemyGrunt);
    private void OnEnemyHit(object sender, SoundEvents.EnemyHitEventArgs e)
    {
        if (e.hitShield) PlaySound(audioClipRefsSO.enemyHitShield);
        else PlaySound(audioClipRefsSO.enemyHitFlesh);
    }
    private void OnEnemyDeath(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.enemyDeath);
    #endregion

    #region Turret Sounds
    private void OnTurretBuilt(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.turretBuild);
    private void OnTurretSold(object sender, System.EventArgs e) => PlaySound(audioClipRefsSO.turretSell);
    private void OnTurretShoot(object sender, SoundEvents.TurretShootEventArgs e)
    {
        if (e.weaponName == WeaponName.Staff)
             PlaySound(audioClipRefsSO.turretShootMagic); // Magic sounds generic Staff type
        else
             PlaySound(audioClipRefsSO.turretShootProjectile); // Physical weapons
    }
    #endregion

    #region Bullet / Explosion Sounds
    private void OnBulletImpact(object sender, SoundEvents.BulletImpactEventArgs e)
    {
        if (e.hitEnemy) PlaySound(audioClipRefsSO.bulletHitEnemy);
        else PlaySound(audioClipRefsSO.bulletHitGround);
    }
    
    private void OnAOEExplosion(object sender, System.EventArgs e)
    {
        PlaySound(audioClipRefsSO.aoeExplosion);
    }
    #endregion

    private void PlaySound(AudioClip[] audioClipArray, float volume = 1f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClipArray == null || audioClipArray.Length == 0)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClips for event triggered by: {callerName}");
            return;
        }
        PlaySound(audioClipArray[Random.Range(0, audioClipArray.Length)], volume, true, 0.95f, 1.08f, callerName);
    }

    private void PlaySound(AudioClip audioClip, float volume = 1f, bool randomizePitch = true, float minRange = 0.95f, float maxRange = 1.08f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClip == null)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClip for event triggered by: {callerName}");
            return;
        }

        audioSource.pitch = randomizePitch ? Random.Range(minRange, maxRange) : 1f;
        audioSource.PlayOneShot(audioClip, volume * 0.5f); // Master Volume dampening baseline
    }


    private void PlaySoundFixed(AudioClip[] audioClipArray, float volume = 1f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClipArray == null || audioClipArray.Length == 0)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClips for fixed sound event triggered by: {callerName}");
            return;
        }
        PlaySoundFixed(audioClipArray[0], volume, callerName);
    }

    private void PlaySoundFixed(AudioClip audioClip, float volume = 1f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClip == null)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClip for fixed sound event triggered by: {callerName}");
            return;
        }
        audioSourceFixed.pitch = 1f;
        audioSourceFixed.PlayOneShot(audioClip, volume * 0.5f); // Master Volume Dampening
    }

    private void OnDisable()
    {
        ResetSignal();
    }

    private void AssignSignal()
    {
        // UI
        SoundEvents.OnAnyButtonClicked += OnAnyButtonClicked;
        SoundEvents.OnAnyButtonHovered += OnAnyButtonHovered;
        SoundEvents.OnCardAppeared += OnCardAppeared;
        SoundEvents.OnCardClicked += OnCardClicked;
        SoundEvents.OnCardHovered += OnCardHovered;
        SoundEvents.OnCoinCollected += OnCoinCollected;

        // Game State
        SoundEvents.OnGameWon += OnGameWon;
        SoundEvents.OnGameLost += OnGameLost;

        // Enemies
        SoundEvents.OnEnemyWalk += OnEnemyWalk;
        SoundEvents.OnEnemyGrunt += OnEnemyGrunt;
        SoundEvents.OnEnemyHit += OnEnemyHit;
        SoundEvents.OnEnemyDeath += OnEnemyDeath;

        // Turrets
        SoundEvents.OnTurretBuilt += OnTurretBuilt;
        SoundEvents.OnTurretSold += OnTurretSold;
        SoundEvents.OnTurretShoot += OnTurretShoot;

        // Bullets
        SoundEvents.OnBulletImpact += OnBulletImpact;
        SoundEvents.OnAOEExplosion += OnAOEExplosion;
    }

    private void ResetSignal()
    {
        // UI
        SoundEvents.OnAnyButtonClicked -= OnAnyButtonClicked;
        SoundEvents.OnAnyButtonHovered -= OnAnyButtonHovered;
        SoundEvents.OnCardAppeared -= OnCardAppeared;
        SoundEvents.OnCardClicked -= OnCardClicked;
        SoundEvents.OnCardHovered -= OnCardHovered;
        SoundEvents.OnCoinCollected -= OnCoinCollected;

        // Game State
        SoundEvents.OnGameWon -= OnGameWon;
        SoundEvents.OnGameLost -= OnGameLost;

        // Enemies
        SoundEvents.OnEnemyWalk -= OnEnemyWalk;
        SoundEvents.OnEnemyGrunt -= OnEnemyGrunt;
        SoundEvents.OnEnemyHit -= OnEnemyHit;
        SoundEvents.OnEnemyDeath -= OnEnemyDeath;

        // Turrets
        SoundEvents.OnTurretBuilt -= OnTurretBuilt;
        SoundEvents.OnTurretSold -= OnTurretSold;
        SoundEvents.OnTurretShoot -= OnTurretShoot;

        // Bullets
        SoundEvents.OnBulletImpact -= OnBulletImpact;
        SoundEvents.OnAOEExplosion -= OnAOEExplosion;
    }
}

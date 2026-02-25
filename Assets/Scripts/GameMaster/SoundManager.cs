using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioRefClip audioClipRefsSO;
    public AudioSource audioSource;
    public AudioSource audioSourceFixed;

    // Ensure AudioSource arrays or generic refs are used safely
    private void OnEnable()
    {
        AssignSignal();
    }

    private void Start()
    {
        // Setup Background Music
        if (audioClipRefsSO.backgroundMusic != null && audioClipRefsSO.backgroundMusic.Length > 0)
        {
            AudioSource musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = 0.3f; // Background volume
            musicSource.clip = audioClipRefsSO.backgroundMusic[Random.Range(0, audioClipRefsSO.backgroundMusic.Length)];
            musicSource.Play();
        }

        // Setup Ambient Loops
        if (audioClipRefsSO.ambientLoops != null && audioClipRefsSO.ambientLoops.Length > 0)
        {
            AudioSource ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.volume = 0.4f;
            ambientSource.clip = audioClipRefsSO.ambientLoops[Random.Range(0, audioClipRefsSO.ambientLoops.Length)];
            ambientSource.Play();
        }
    }
    #region UI Sounds (2D Fixed Source)
    private void OnAnyButtonClicked(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.buttonClick);
    private void OnAnyButtonHovered(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.buttonHover);
    private void OnCancelButtonClicked(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.uiCancelSounds);
    private void OnCardAppeared(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.cardAppear);
    private void OnCardClicked(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.cardClick);
    private void OnCardHovered(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.cardHover);
    private void OnCoinCollected(object sender, System.EventArgs e) => PlaySoundFixed(audioClipRefsSO.coinCollect);
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

    #region Enemy Sounds (3D Spatial Source)
    private void OnEnemyWalk(object sender, SoundEvents.WorldPositionEventArgs e) => PlaySound3D(audioClipRefsSO.enemyWalk, e.position, 0.5f);
    private void OnEnemySprint(object sender, SoundEvents.WorldPositionEventArgs e) => PlaySound3D(audioClipRefsSO.enemySprint, e.position, 0.5f);
    private void OnEnemyGrunt(object sender, SoundEvents.EnemyAudioEventArgs e) 
    {
        var clips = (e.overrideClips != null && e.overrideClips.Length > 0) ? e.overrideClips : audioClipRefsSO.enemyGrunt;
        PlaySound3D(clips, e.position);
    }
    private void OnEnemyHit(object sender, SoundEvents.EnemyHitEventArgs e)
    {
        if (e.hitShield) PlaySound3D(audioClipRefsSO.enemyHitShield, e.position);
        else PlaySound3D(audioClipRefsSO.enemyHitFlesh, e.position);
    }
    private void OnEnemyDeath(object sender, SoundEvents.EnemyAudioEventArgs e)
    {
        var clips = (e.overrideClips != null && e.overrideClips.Length > 0) ? e.overrideClips : audioClipRefsSO.enemyDeath;
        PlaySound3D(clips, e.position);
    }
    private void OnEnemyPoof(object sender, SoundEvents.EnemyAudioEventArgs e)
    {
        var clips = (e.overrideClips != null && e.overrideClips.Length > 0) ? e.overrideClips : audioClipRefsSO.enemyPoof;
        PlaySound3D(clips, e.position);
    }
    #endregion

    #region Turret Sounds
    private void OnTurretBuilt(object sender, SoundEvents.WorldPositionEventArgs e) => PlaySound3D(audioClipRefsSO.turretBuild, e.position);
    private void OnTurretSold(object sender, SoundEvents.WorldPositionEventArgs e) => PlaySound3D(audioClipRefsSO.turretSell, e.position);
    private void OnTurretShoot(object sender, SoundEvents.TurretShootEventArgs e)
    {
        AudioClip[] clips = e.weaponName switch
        {
            WeaponName.Cannon => audioClipRefsSO.turretShootCannon,
            WeaponName.Crossbow => audioClipRefsSO.turretShootCrossbow,
            WeaponName.MachineGun => audioClipRefsSO.turretShootMachineGun,
            WeaponName.Magic => audioClipRefsSO.turretShootMagic,
            _ => audioClipRefsSO.turretShootCannon
        };
        PlaySound3D(clips, e.position);
    }
    #endregion

    #region Elemental Strike Sounds
    // Throttle: each element type plays at most once per 0.15s globally to prevent audio spam
    private readonly System.Collections.Generic.Dictionary<BulletType, float> elementalSoundTimestamps = new();
    private const float ELEMENTAL_SOUND_THROTTLE = 0.15f;

    private void OnElementalStrike(object sender, SoundEvents.ElementalStrikeEventArgs e)
    {
        elementalSoundTimestamps.TryGetValue(e.element, out float lastTime);
        if (Time.time - lastTime < ELEMENTAL_SOUND_THROTTLE) return;
        elementalSoundTimestamps[e.element] = Time.time;

        AudioClip[] clips = e.element switch
        {
            BulletType.Fire     => audioClipRefsSO.elementalFire,
            BulletType.Ice      => audioClipRefsSO.elementalIce,
            BulletType.Electric => audioClipRefsSO.elementalElectric,
            _ => null
        };
        if (clips != null) PlaySound3D(clips, e.position);
    }

    private void OnShieldBreak(object sender, SoundEvents.WorldPositionEventArgs e)
        => PlaySound3D(audioClipRefsSO.shieldBreak, e.position);

    private void OnBarrierBreak(object sender, SoundEvents.WorldPositionEventArgs e)
        => PlaySound3D(audioClipRefsSO.barrierBreak, e.position);

    private void OnChunkExpand(object sender, SoundEvents.WorldPositionEventArgs e)
        => PlaySoundFixed(audioClipRefsSO.chunkExpand); // 2D – no world position
    #endregion

    #region Bullet / Explosion Sounds
    private void OnBulletImpact(object sender, SoundEvents.BulletImpactEventArgs e)
    {
        if (e.hitEnemy) PlaySound3D(audioClipRefsSO.bulletHitEnemy, e.position);
        else PlaySound3D(audioClipRefsSO.bulletHitGround, e.position);
    }
    
    private void OnAOEExplosion(object sender, SoundEvents.WorldPositionEventArgs e)
    {
        PlaySound3D(audioClipRefsSO.aoeExplosion, e.position);
    }
    #endregion

    private void PlaySound3D(AudioClip[] audioClipArray, Vector3 position, float volume = 1f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClipArray == null || audioClipArray.Length == 0)
        {
            return;
        }
        PlaySound3D(audioClipArray[Random.Range(0, audioClipArray.Length)], position, volume, true, 0.95f, 1.08f, callerName);
    }

    // Generic Audio Clustering to prevent 100 enemies dying from lagging the game
    private readonly System.Collections.Generic.Dictionary<AudioClip, (int count, float lastPlayTime)> clipClusterTracker = new();
    private const float CLUSTER_TIME_WINDOW = 0.1f; // 100ms window
    private const int MAX_CLUSTERED_CLIPS = 3;

    private void PlaySound3D(AudioClip audioClip, Vector3 position, float volume = 1f, bool randomizePitch = true, float minRange = 0.95f, float maxRange = 1.08f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClip == null) return;

        float actualVolume = volume * 0.5f; // Master Volume dampening baseline

        // --- Audio Clustering Logic ---
        if (clipClusterTracker.TryGetValue(audioClip, out var tracker))
        {
            if (Time.time - tracker.lastPlayTime > CLUSTER_TIME_WINDOW)
            {
                // Window expired, reset counter
                clipClusterTracker[audioClip] = (1, Time.time);
            }
            else
            {
                if (tracker.count >= MAX_CLUSTERED_CLIPS)
                {
                    // Too many of this exact sound playing at once. Skip instantiating a new one to save performance.
                    return;
                }
                else
                {
                    // Inside window & under limit. Increment count.
                    clipClusterTracker[audioClip] = (tracker.count + 1, tracker.lastPlayTime);
                    
                    // Boost volume to composite the effect of multiple sounds
                    actualVolume *= 1.3f; 
                }
            }
        }
        else
        {
            clipClusterTracker[audioClip] = (1, Time.time);
        }
        // ------------------------------

        float pitch = randomizePitch ? Random.Range(minRange, maxRange) : 1f;

        if (AudioPoolManager.Instance != null)
        {
            AudioPoolManager.Instance.PlayPooledSound(audioClip, position, actualVolume, pitch, audioSource);
        }
        else
        {
            // Fallback (for testing / if manager forgot to be placed)
            Debug.LogWarning("[SoundManager] AudioPoolManager is missing! Using inefficient Instantiate/Destroy fallback.");
            GameObject soundObj = new GameObject("3D_Audio_Fallback_" + audioClip.name);
            soundObj.transform.position = position;
            
            AudioSource tempSource = soundObj.AddComponent<AudioSource>();
            tempSource.spatialBlend = audioSource.spatialBlend;
            tempSource.rolloffMode = audioSource.rolloffMode;
            tempSource.minDistance = audioSource.minDistance;
            tempSource.maxDistance = audioSource.maxDistance;
            tempSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            
            tempSource.clip = audioClip;
            tempSource.volume = actualVolume;
            tempSource.pitch = pitch;
            
            tempSource.Play();
            Destroy(soundObj, (audioClip.length / pitch) + 0.1f);
        }
    }


    private void PlaySoundFixed(AudioClip[] audioClipArray, float volume = 1f, bool randomizePitch = true, float minRange = 0.95f, float maxRange = 1.08f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClipArray == null || audioClipArray.Length == 0)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClips for fixed sound event triggered by: {callerName}");
            return;
        }
        PlaySoundFixed(audioClipArray[Random.Range(0, audioClipArray.Length)], volume, randomizePitch, minRange, maxRange, callerName);
    }

    private void PlaySoundFixed(AudioClip audioClip, float volume = 1f, bool randomizePitch = true, float minRange = 0.95f, float maxRange = 1.08f, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (audioClip == null)
        {
            Debug.LogWarning($"[SoundManager] Missing AudioClip for fixed sound event triggered by: {callerName}");
            return;
        }
        
        audioSourceFixed.pitch = randomizePitch ? Random.Range(minRange, maxRange) : 1f;
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
        SoundEvents.OnCancelButtonClicked += OnCancelButtonClicked;
        SoundEvents.OnCardAppeared += OnCardAppeared;
        SoundEvents.OnCardClicked += OnCardClicked;
        SoundEvents.OnCardHovered += OnCardHovered;
        SoundEvents.OnCoinCollected += OnCoinCollected;

        // Game State
        SoundEvents.OnGameWon += OnGameWon;
        SoundEvents.OnGameLost += OnGameLost;

        // Enemies
        SoundEvents.OnEnemyWalk += OnEnemyWalk;
        SoundEvents.OnEnemySprint += OnEnemySprint;
        SoundEvents.OnEnemyGrunt += OnEnemyGrunt;
        SoundEvents.OnEnemyHit += OnEnemyHit;
        SoundEvents.OnEnemyDeath += OnEnemyDeath;
        SoundEvents.OnEnemyPoof += OnEnemyPoof;

        // Turrets
        SoundEvents.OnTurretBuilt += OnTurretBuilt;
        SoundEvents.OnTurretSold += OnTurretSold;
        SoundEvents.OnTurretShoot += OnTurretShoot;

        // Bullets
        SoundEvents.OnBulletImpact += OnBulletImpact;
        SoundEvents.OnAOEExplosion += OnAOEExplosion;

        // Elemental
        SoundEvents.OnElementalStrike += OnElementalStrike;

        // World
        SoundEvents.OnShieldBreak  += OnShieldBreak;
        SoundEvents.OnBarrierBreak += OnBarrierBreak;
        SoundEvents.OnChunkExpand  += OnChunkExpand;
    }

    private void ResetSignal()
    {
        // UI
        SoundEvents.OnAnyButtonClicked -= OnAnyButtonClicked;
        SoundEvents.OnAnyButtonHovered -= OnAnyButtonHovered;
        SoundEvents.OnCancelButtonClicked -= OnCancelButtonClicked;
        SoundEvents.OnCardAppeared -= OnCardAppeared;
        SoundEvents.OnCardClicked -= OnCardClicked;
        SoundEvents.OnCardHovered -= OnCardHovered;
        SoundEvents.OnCoinCollected -= OnCoinCollected;

        // Game State
        SoundEvents.OnGameWon -= OnGameWon;
        SoundEvents.OnGameLost -= OnGameLost;

        // Enemies
        SoundEvents.OnEnemyWalk -= OnEnemyWalk;
        SoundEvents.OnEnemySprint -= OnEnemySprint;
        SoundEvents.OnEnemyGrunt -= OnEnemyGrunt;
        SoundEvents.OnEnemyHit -= OnEnemyHit;
        SoundEvents.OnEnemyDeath -= OnEnemyDeath;
        SoundEvents.OnEnemyPoof -= OnEnemyPoof;

        // Turrets
        SoundEvents.OnTurretBuilt -= OnTurretBuilt;
        SoundEvents.OnTurretSold -= OnTurretSold;
        SoundEvents.OnTurretShoot -= OnTurretShoot;

        // Bullets
        SoundEvents.OnBulletImpact -= OnBulletImpact;
        SoundEvents.OnAOEExplosion -= OnAOEExplosion;

        // Elemental
        SoundEvents.OnElementalStrike -= OnElementalStrike;
        SoundEvents.OnShieldBreak  -= OnShieldBreak;
        SoundEvents.OnBarrierBreak -= OnBarrierBreak;
        SoundEvents.OnChunkExpand  -= OnChunkExpand;
    }
}

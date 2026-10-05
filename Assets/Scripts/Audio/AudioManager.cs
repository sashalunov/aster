using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central Audio Manager supporting:
/// - 2-channel background music (BGM) crossfading.
/// - Pooled 3D spatial sound effects (explosions, impacts, collisions).
/// - 2D UI / one-shot SFX.
/// - Ship engine audio management and global volume modulation.
/// - Volume controls (Master, Music, SFX, Engine) with optional PlayerPrefs persistence.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;

    /// <summary>
    /// Checks whether an instance of AudioManager currently exists without triggering auto-instantiation.
    /// </summary>
    public static bool HasInstance => _instance != null;

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    GameObject existingGo = GameObject.Find("[AudioManager]");
                    if (existingGo != null)
                    {
                        _instance = existingGo.GetComponent<AudioManager>();
                    }
                }

                // Only create dynamically if no instance exists anywhere in the scene and we are in play mode
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("[AudioManager]");
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Persistence")]
    [Tooltip("If true, this AudioManager persists across scene transitions. Set to false for clean scene-level audio management.")]
    [SerializeField] private bool persistAcrossScenes = false;

    [Header("Settings & Persistence")]
    [Tooltip("If enabled, volume levels will be loaded from PlayerPrefs on start. If disabled, Inspector values are preserved.")]
    [SerializeField] private bool loadFromPlayerPrefs = false;

    [Tooltip("If enabled, changing volume levels via code will automatically save to PlayerPrefs.")]
    [SerializeField] private bool saveToPlayerPrefs = false;

    [Header("Default Audio Clips (Optional Presets)")]
    [Tooltip("Default background music to start playing automatically.")]
    [SerializeField] private AudioClip defaultMusic;


    [Tooltip("Ship explosion sound (e.g. ship_explode.ogg).")]
    [SerializeField] private AudioClip shipExplosionClip;

    [Tooltip("Secondary hull explosion sound (e.g. hull_explode2.ogg).")]
    [SerializeField] private AudioClip hullExplosionClip;

    [Tooltip("Hull impact / collision sound (e.g. hull_hit.ogg).")]
    [SerializeField] private AudioClip collisionClip;

    [Tooltip("Shield hit sound (e.g. hit_shield.ogg or shield_impact.ogg).")]
    [SerializeField] private AudioClip shieldHitClip;

    [Tooltip("Shield broken / depleted sound (e.g. shield_lost.ogg).")]
    [SerializeField] private AudioClip shieldBreakClip;

    [Tooltip("Default looping engine clip for ships (e.g. sfx_engine1.ogg).")]
    [SerializeField] private AudioClip defaultEngineClip;

    [Header("3D Spatial Audio Settings")]
    [Tooltip("Number of 3D AudioSources pre-allocated in pool.")]
    [SerializeField] private int initial3DPoolSize = 16;
    [SerializeField] private int max3DPoolSize = 48;

    [Tooltip("Min distance for 3D sounds. Calibrated for top-down camera (Z=-36) so on-screen sounds are clear.")]
    [SerializeField] private float defaultMinDistance = 35f;

    [Tooltip("Max distance where 3D sounds fully fade out.")]
    [SerializeField] private float defaultMaxDistance = 90f;

    [SerializeField] private AudioRolloffMode defaultRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Volume Levels (0.0 to 1.0)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float engineVolume = 1.0f;

    // PlayerPrefs keys
    public const string PREF_MASTER_VOL = "Aster_Audio_Master";
    public const string PREF_MUSIC_VOL = "Aster_Audio_Music";
    public const string PREF_SFX_VOL = "Aster_Audio_SFX";
    public const string PREF_ENGINE_VOL = "Aster_Audio_Engine";

    // Music crossfading state
    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private bool _isSourceAActive = true;
    private Coroutine _musicFadeCoroutine;
    private AudioClip _currentMusicClip;

    // 2D SFX source
    private AudioSource _sfx2DSource;

    // 3D Spatial Pool
    private readonly Queue<AudioSource> _pool3D = new Queue<AudioSource>();
    private readonly List<AudioSource> _active3DSources = new List<AudioSource>();
    private Transform _pool3DContainer;

    // Registered ship engines
    private readonly HashSet<ShipEngineAudio> _registeredEngines = new HashSet<ShipEngineAudio>();

    // Properties
    public AudioClip DefaultEngineClip => defaultEngineClip;
    public float MasterVolume
    {
        get => masterVolume;
        set => SetMasterVolume(value);
    }
    public float MusicVolume
    {
        get => musicVolume;
        set => SetMusicVolume(value);
    }
    public float SFXVolume
    {
        get => sfxVolume;
        set => SetSFXVolume(value);
    }
    public float EngineVolume
    {
        get => engineVolume;
        set => SetEngineVolume(value);
    }

    public float MusicEffectiveVolume => masterVolume * musicVolume;
    public float SFXEffectiveVolume => masterVolume * sfxVolume;
    public float EngineEffectiveVolume => masterVolume * engineVolume;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // If existing instance was a blank auto-spawned one and this one has clips or is scene-placed, replace it
            if (_instance.defaultMusic == null && defaultMusic != null)
            {
                Destroy(_instance.gameObject);
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        _instance = this;

        if (persistAcrossScenes && transform.parent == null && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (loadFromPlayerPrefs)
        {
            LoadSettings();
        }

        InitializeComponents();
        Initialize3DPool();
    }

    private void Start()
    {
       
    }

    private void InitializeComponents()
    {
        if (_musicSourceA != null && _musicSourceB != null && _sfx2DSource != null) return;

        // Setup BGM Sources
        Transform musicChannel = transform.Find("Music_Channels");
        GameObject musicObj = musicChannel != null ? musicChannel.gameObject : new GameObject("Music_Channels");
        musicObj.transform.SetParent(transform);

        AudioSource[] musicSources = musicObj.GetComponents<AudioSource>();
        _musicSourceA = musicSources.Length > 0 ? musicSources[0] : musicObj.AddComponent<AudioSource>();
        _musicSourceA.spatialBlend = 0f;
        _musicSourceA.loop = true;
        _musicSourceA.playOnAwake = false;
        _musicSourceA.volume = 0f;

        _musicSourceB = musicSources.Length > 1 ? musicSources[1] : musicObj.AddComponent<AudioSource>();
        _musicSourceB.spatialBlend = 0f;
        _musicSourceB.loop = true;
        _musicSourceB.playOnAwake = false;
        _musicSourceB.volume = 0f;

        // Setup 2D SFX Source
        Transform sfxChannel = transform.Find("2D_SFX");
        GameObject sfxObj = sfxChannel != null ? sfxChannel.gameObject : new GameObject("2D_SFX");
        sfxObj.transform.SetParent(transform);

        _sfx2DSource = sfxObj.GetComponent<AudioSource>();
        if (_sfx2DSource == null)
        {
            _sfx2DSource = sfxObj.AddComponent<AudioSource>();
        }
        _sfx2DSource.spatialBlend = 0f;
        _sfx2DSource.loop = false;
        _sfx2DSource.playOnAwake = false;
    }

    private void Initialize3DPool()
    {
        if (_pool3DContainer != null && _pool3D.Count > 0) return;

        Transform existingPool = transform.Find("3D_SFX_Pool");
        GameObject poolObj = existingPool != null ? existingPool.gameObject : new GameObject("3D_SFX_Pool");
        poolObj.transform.SetParent(transform);
        _pool3DContainer = poolObj.transform;

        for (int i = 0; i < initial3DPoolSize; i++)
        {
            CreatePooled3DSource();
        }
    }

    private AudioSource CreatePooled3DSource()
    {
        GameObject go = new GameObject($"SpatialAudioSource_{_pool3D.Count + _active3DSources.Count}");
        go.transform.SetParent(_pool3DContainer);

        AudioSource src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode = defaultRolloffMode;
        src.minDistance = defaultMinDistance;
        src.maxDistance = defaultMaxDistance;
        src.playOnAwake = false;
        src.loop = false;
        src.dopplerLevel = 0f;

        go.SetActive(false);
        _pool3D.Enqueue(src);
        return src;
    }

    #region Settings & Persistence

    public void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, masterVolume);
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, sfxVolume);
        engineVolume = PlayerPrefs.GetFloat(PREF_ENGINE_VOL, engineVolume);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, masterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, musicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
        PlayerPrefs.SetFloat(PREF_ENGINE_VOL, engineVolume);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float vol)
    {
        masterVolume = Mathf.Clamp01(vol);
        ApplyMusicVolumeImmediate();
        if (saveToPlayerPrefs) SaveSettings();
    }

    public void SetMusicVolume(float vol)
    {
        musicVolume = Mathf.Clamp01(vol);
        ApplyMusicVolumeImmediate();
        if (saveToPlayerPrefs) SaveSettings();
    }

    public void SetSFXVolume(float vol)
    {
        sfxVolume = Mathf.Clamp01(vol);
        if (saveToPlayerPrefs) SaveSettings();
    }

    public void SetEngineVolume(float vol)
    {
        engineVolume = Mathf.Clamp01(vol);
        if (saveToPlayerPrefs) SaveSettings();
    }

    private void ApplyMusicVolumeImmediate()
    {
        if (_musicFadeCoroutine == null)
        {
            AudioSource active = _isSourceAActive ? _musicSourceA : _musicSourceB;
            if (active != null && active.isPlaying)
            {
                active.volume = MusicEffectiveVolume;
            }
        }
    }

    #endregion

    #region Music (BGM) Control

public void PlayBGM()
    {
        if (defaultMusic != null && _currentMusicClip == null)
        {
            PlayMusic(defaultMusic, 0, false);
        }
    }
    /// <summary>
    /// Plays background music with a smooth crossfade from any currently playing track.
    /// </summary>
    public void PlayMusic(AudioClip clip, float fadeDuration = 0.0f, bool loop = false)
    {
        if (_musicSourceA == null || _musicSourceB == null)
        {
            InitializeComponents();
        }

        if (clip == null)
        {
            StopMusic(fadeDuration);
            return;
        }

        // If the same clip is already playing, just ensure volume is correct
        if (_currentMusicClip == clip)
        {
            AudioSource current = _isSourceAActive ? _musicSourceA : _musicSourceB;
            if (current != null && current.isPlaying)
            {
                return;
            }
        }

        _currentMusicClip = clip;

        AudioSource incoming = _isSourceAActive ? _musicSourceB : _musicSourceA;
        AudioSource outgoing = _isSourceAActive ? _musicSourceA : _musicSourceB;
        _isSourceAActive = !_isSourceAActive;

        if (_musicFadeCoroutine != null)
        {
            StopCoroutine(_musicFadeCoroutine);
        }

        _musicFadeCoroutine = StartCoroutine(CrossfadeMusicCoroutine(incoming, outgoing, clip, fadeDuration, loop));
    }

    private IEnumerator CrossfadeMusicCoroutine(AudioSource incoming, AudioSource outgoing, AudioClip newClip, float duration, bool loop)
    {
        incoming.clip = newClip;
        incoming.loop = loop;
        incoming.volume = 0f;
        incoming.Play();

        float startOutgoingVol = outgoing != null ? outgoing.volume : 0f;
        float targetIncomingVol = MusicEffectiveVolume;

        if (duration <= 0f)
        {
            incoming.volume = targetIncomingVol;
            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
            }
            _musicFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            targetIncomingVol = MusicEffectiveVolume;
            incoming.volume = Mathf.Lerp(0f, targetIncomingVol, t);

            if (outgoing != null)
            {
                outgoing.volume = Mathf.Lerp(startOutgoingVol, 0f, t);
            }

            yield return null;
        }

        incoming.volume = MusicEffectiveVolume;
        if (outgoing != null)
        {
            outgoing.volume = 0f;
            outgoing.Stop();
        }

        _musicFadeCoroutine = null;
    }

    public void StopMusic(float fadeDuration = 1.0f)
    {
        _currentMusicClip = null;
        AudioSource active = _isSourceAActive ? _musicSourceA : _musicSourceB;

        if (active == null || !active.isPlaying) return;

        if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
        _musicFadeCoroutine = StartCoroutine(FadeOutMusicCoroutine(active, fadeDuration));
    }

    private IEnumerator FadeOutMusicCoroutine(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        _musicFadeCoroutine = null;
    }

    public void PauseMusic()
    {
        if (_musicSourceA != null && _musicSourceA.isPlaying) _musicSourceA.Pause();
        if (_musicSourceB != null && _musicSourceB.isPlaying) _musicSourceB.Pause();
    }

    public void ResumeMusic()
    {
        if (_musicSourceA != null && _musicSourceA.clip != null && !_musicSourceA.isPlaying && _isSourceAActive) _musicSourceA.UnPause();
        if (_musicSourceB != null && _musicSourceB.clip != null && !_musicSourceB.isPlaying && !_isSourceAActive) _musicSourceB.UnPause();
    }

    #endregion

    #region 3D Spatial Audio

    /// <summary>
    /// Plays a sound effect in 3D world space using an AudioSource from the object pool.
    /// </summary>
    public AudioSource Play3D(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.05f)
    {
        return Play3D(clip, position, volume, defaultMinDistance, defaultMaxDistance, pitchVariance);
    }

    /// <summary>
    /// Plays a sound effect in 3D world space with explicit min/max distance attenuation.
    /// </summary>
    public AudioSource Play3D(AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance, float pitchVariance = 0.05f)
    {
        if (clip == null) return null;

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        if (_pool3DContainer == null)
        {
            Initialize3DPool();
        }

        AudioSource src = GetAvailable3DSource();
        if (src == null) return null;

        src.transform.position = position;
        src.spatialBlend = 1f;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.clip = clip;

        float finalVol = Mathf.Clamp01(volume) * SFXEffectiveVolume;
        src.volume = finalVol;

        float pitch = 1f;
        if (pitchVariance > 0f)
        {
            pitch += Random.Range(-pitchVariance, pitchVariance);
        }
        src.pitch = Mathf.Clamp(pitch, 0.2f, 3f);

        src.gameObject.SetActive(true);
        src.Play();

        _active3DSources.Add(src);
        StartCoroutine(Recycle3DSourceAfterPlay(src, clip.length / Mathf.Max(src.pitch, 0.1f)));

        return src;
    }

    private AudioSource GetAvailable3DSource()
    {
        while (_pool3D.Count > 0)
        {
            AudioSource src = _pool3D.Dequeue();
            if (src != null)
            {
                return src;
            }
        }

        // Expand pool if within limit
        if (_active3DSources.Count < max3DPoolSize)
        {
            return CreatePooled3DSource();
        }

        // If pool is maxed out, steal the oldest active source
        if (_active3DSources.Count > 0)
        {
            AudioSource oldest = _active3DSources[0];
            _active3DSources.RemoveAt(0);
            if (oldest != null)
            {
                oldest.Stop();
                return oldest;
            }
        }

        return null;
    }

    private IEnumerator Recycle3DSourceAfterPlay(AudioSource src, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (src != null)
        {
            src.Stop();
            src.clip = null;
            src.gameObject.SetActive(false);
            _active3DSources.Remove(src);
            _pool3D.Enqueue(src);
        }
    }

    #endregion

    #region 2D SFX (UI / Weapons / Direct Audio)

    /// <summary>
    /// Plays a non-spatial 2D sound effect (ideal for UI, progression notifications, etc).
    /// </summary>
    public void Play2D(AudioClip clip, float volume = 1f, float pitchVariance = 0.05f)
    {
        if (clip == null) return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        if (_sfx2DSource == null)
        {
            InitializeComponents();
        }

        if (_sfx2DSource == null) return;

        float finalVol = Mathf.Clamp01(volume) * SFXEffectiveVolume;
        if (pitchVariance > 0f)
        {
            _sfx2DSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        }
        else
        {
            _sfx2DSource.pitch = 1f;
        }

        _sfx2DSource.PlayOneShot(clip, finalVol);
    }

    #endregion

    #region Semantic Event Helpers

    /// <summary>
    /// Plays an explosion sound effect at a 3D world position.
    /// </summary>
    public void PlayExplosion(Vector3 position, float volume = 1f, bool large = false)
    {
        AudioClip clip = large ? (hullExplosionClip ?? shipExplosionClip) : (shipExplosionClip ?? hullExplosionClip);
        if (clip != null)
        {
            Play3D(clip, position, volume, defaultMinDistance, defaultMaxDistance, 0.08f);
        }
    }

    /// <summary>
    /// Plays a collision sound effect with volume scaled by impact speed.
    /// </summary>
    public void PlayCollision(Vector3 position, float relativeSpeed, float maxSpeed = 15f)
    {
        if (collisionClip == null || relativeSpeed < 0.1f) return;

        float normalized = Mathf.Clamp01(relativeSpeed / maxSpeed);
        float volume = Mathf.Lerp(0.35f, 1f, normalized);
        float pitchVariance = Mathf.Lerp(0.02f, 0.10f, normalized);

        Play3D(collisionClip, position, volume, defaultMinDistance, defaultMaxDistance, pitchVariance);
    }

    /// <summary>
    /// Plays a shield impact sound effect at world position.
    /// </summary>
    public void PlayShieldHit(Vector3 position, float volume = 1f, bool isPlayer = false)
    {
        if (shieldHitClip != null)
        {
            Play3D(shieldHitClip, position, volume, defaultMinDistance, defaultMaxDistance, 0.06f);
        }
    }

    /// <summary>
    /// Plays a shield broken / lost sound effect at world position.
    /// </summary>
    public void PlayShieldBreak(Vector3 position, float volume = 1f, bool isPlayer = false)
    {
        if (shieldBreakClip != null)
        {
            Play3D(shieldBreakClip, position, volume, defaultMinDistance, defaultMaxDistance, 0.04f);
        }
    }

    #endregion

    #region Engine Audio Registry

    public void RegisterEngine(ShipEngineAudio engine)
    {
        if (engine != null)
        {
            _registeredEngines.Add(engine);
        }
    }

    public void UnregisterEngine(ShipEngineAudio engine)
    {
        if (engine != null)
        {
            _registeredEngines.Remove(engine);
        }
    }

    #endregion

}

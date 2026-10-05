using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dynamic engine sound controller for ships (Player and Enemy).
/// Automatically modulates volume and pitch based on ship velocity or throttle.
/// Configures 2D audio for Player (consistent stereo presence) and 3D spatial audio for AI enemies.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ShipEngineAudio : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Rigidbody used to read ship movement speed. Auto-detected if null.")]
    [SerializeField] private Rigidbody rb;

    [Tooltip("AudioSource used to play the looping engine sound. Auto-detected if null.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Optional reference to player script to mute when ship dies.")]
    [SerializeField] private player playerRef;

    [Header("Audio Settings")]
    [Tooltip("Looping engine sound clip (e.g. sfx_engine1.ogg).")]
    [SerializeField] private AudioClip engineClip;

    [Tooltip("If true, engine is treated as player ship (2D stereo). If false, treated as enemy ship (3D spatial).")]
    [SerializeField] private bool isPlayer = true;

    [Header("Pitch & Volume Modulation")]
    [Range(0.1f, 2f)] [SerializeField] private float idlePitch = 0.75f;
    [Range(0.1f, 3f)] [SerializeField] private float maxPitch = 1.35f;
    [Range(0f, 1f)]   [SerializeField] private float idleVolume = 0.15f;
    [Range(0f, 1f)]   [SerializeField] private float maxVolume = 0.55f;

    [Tooltip("Speed (units/sec) at which engine reaches maximum pitch and volume.")]
    [SerializeField] private float maxSpeed = 15f;

    [Tooltip("Speed at which volume and pitch interpolate towards target values.")]
    [SerializeField] private float smoothing = 4f;

    [Tooltip("Whether to stop engine audio when ship dies or is disabled.")]
    [SerializeField] private bool stopOnDeath = true;

    [Header("3D Spatial Settings (for Enemy Ships)")]
    [SerializeField] private float minDistance = 30f;
    [SerializeField] private float maxDistance = 80f;

    private float _customThrottle = -1f; // When >= 0, overrides velocity calculation
    private bool _isMuted = false;
    private bool _hasStarted = false;

    public bool IsPlayer
    {
        get => isPlayer;
        set
        {
            isPlayer = value;
            ApplySpatialSettings();
        }
    }

    public AudioSource Source
    {
        get
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            return audioSource;
        }
    }

    private void Awake()
    {
        if (audioSource == null) audioSource = Source;
        if (rb == null) rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
        if (playerRef == null) playerRef = GetComponent<player>() ?? GetComponentInParent<player>();

        ConfigureAudioSource();
    }

    private void OnEnable()
    {
        // Only register if already past Start to avoid early Awake/OnEnable race conditions
        if (_hasStarted && AudioManager.HasInstance)
        {
            AudioManager.Instance.RegisterEngine(this);
        }

        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void OnDisable()
    {
        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.UnregisterEngine(this);
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void Start()
    {
        _hasStarted = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RegisterEngine(this);

            if (engineClip == null && AudioManager.Instance.DefaultEngineClip != null)
            {
                SetEngineClip(AudioManager.Instance.DefaultEngineClip);
            }
        }
        else if (engineClip != null && audioSource.clip == null)
        {
            SetEngineClip(engineClip);
        }

        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void ConfigureAudioSource()
    {
        if (audioSource == null) return;

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        ApplySpatialSettings();

        if (engineClip != null && audioSource.clip == null)
        {
            audioSource.clip = engineClip;
        }

        audioSource.pitch = idlePitch;
        audioSource.volume = idleVolume;
    }

    private void ApplySpatialSettings()
    {
        if (audioSource == null) return;

        if (isPlayer)
        {
            audioSource.spatialBlend = 0f; // 2D Stereo for Player
        }
        else
        {
            audioSource.spatialBlend = 1f; // 3D Spatial for Enemies
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = 0.5f;
        }
    }

    public void SetEngineClip(AudioClip clip)
    {
        engineClip = clip;
        if (audioSource != null)
        {
            bool wasPlaying = audioSource.isPlaying;
            audioSource.clip = clip;
            if (wasPlaying && clip != null)
            {
                audioSource.Play();
            }
        }
    }

    /// <summary>
    /// Explicitly sets throttle from 0 to 1 (overrides Rigidbody speed calculation).
    /// Pass -1 to return to Rigidbody speed tracking.
    /// </summary>
    public void SetCustomThrottle(float throttle01)
    {
        _customThrottle = throttle01;
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (audioSource != null)
        {
            if (muted && audioSource.isPlaying) audioSource.Pause();
            else if (!muted && !audioSource.isPlaying && audioSource.clip != null) audioSource.Play();
        }
    }

    private void Update()
    {
        if (audioSource == null) return;

        // Check if player died
        if (stopOnDeath && playerRef != null && playerRef.isDead)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            return;
        }

        if (_isMuted) return;

        // Ensure playing if clip is assigned
        if (!audioSource.isPlaying && audioSource.clip != null && isActiveAndEnabled)
        {
            audioSource.Play();
        }

        // Determine current throttle / normalized speed ratio
        float ratio = 0f;
        if (_customThrottle >= 0f)
        {
            ratio = Mathf.Clamp01(_customThrottle);
        }
        else if (rb != null)
        {
            float speed = rb.linearVelocity.magnitude;
            ratio = maxSpeed > 0f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
        }

        // Calculate targets
        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, ratio);
        float globalMultiplier = AudioManager.HasInstance ? AudioManager.Instance.EngineEffectiveVolume : 1f;
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, ratio) * globalMultiplier;

        // Smoothly interpolate
        float dt = Time.deltaTime * smoothing;
        audioSource.pitch = Mathf.MoveTowards(audioSource.pitch, targetPitch, dt);
        audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, dt);
    }
}

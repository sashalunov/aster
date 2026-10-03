using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Abstract base class for all collectible powerups.
/// Handles physics/trigger interactions, magnet attraction towards the player ship,
/// auto-despawn lifetime, visual hover/spin animations, and manager registration.
/// New powerup behaviors can simply inherit from this class and override ApplyEffect().
/// </summary>
[DisallowMultipleComponent]
public abstract class PowerupBase : MonoBehaviour
{
    [Header("Identity & Info")]
    [Tooltip("Unique identifier for this powerup type.")]
    [SerializeField] protected string powerupId = "generic_powerup";

    [Tooltip("Display name used for floating pickup notifications.")]
    [SerializeField] protected string displayName = "POWER UP";

    [Tooltip("Theme tint color for visuals and text.")]
    [SerializeField] protected Color themeColor = Color.yellow;

    [Header("Lifetime")]
    [Tooltip("Lifetime in seconds before automatically despawning. Set <= 0 for infinite.")]
    [SerializeField] protected float lifetime = 25f;

    [Tooltip("Whether this powerup should automatically despawn when lifetime expires.")]
    [SerializeField] protected bool autoDespawn = true;

    [Header("Magnet Attraction")]
    [Tooltip("Whether this powerup is attracted to the player when nearby.")]
    [SerializeField] protected bool enableMagnet = true;

    [Tooltip("Detection radius in world units to begin magnet pull towards player.")]
    [SerializeField] protected float magnetRadius = 5.0f;

    [Tooltip("Speed in units/second at which powerup flies towards player.")]
    [SerializeField] protected float magnetSpeed = 8.0f;

    [Header("Hover & Rotation Animation")]
    [Tooltip("Enable subtle floating hover and rotation.")]
    [SerializeField] protected bool enableIdleAnimation = true;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] protected float rotationSpeed = 60.0f;

    [Tooltip("Bobbing frequency (oscillations per second).")]
    [SerializeField] protected float bobFrequency = 2.0f;

    [Tooltip("Bobbing amplitude (height offset).")]
    [SerializeField] protected float bobAmplitude = 0.15f;

    [Header("Feedback Effects")]
    [Tooltip("Optional prefab to instantiate when collected.")]
    [SerializeField] protected GameObject pickupFxPrefab;

    [Tooltip("Optional audio clip played on collection.")]
    [SerializeField] protected AudioClip pickupSound;

    // Public properties
    public string PowerupId => powerupId;
    public string DisplayName => displayName;
    public Color ThemeColor => themeColor;
    public float Lifetime => lifetime;
    public bool IsCollected => _isCollected;

    // Internal state
    protected bool _isCollected = false;
    protected float _age = 0f;
    protected Vector3 _basePosition;
    protected Transform _playerTransform;
    protected Rigidbody _rigidbody;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            // Ensure 2.5D space plane constraint: no Z drifting or unwanted tumbling
            //_rigidbody.constraints = RigidbodyConstraints.FreezePositionZ |
           //                          RigidbodyConstraints.FreezeRotationX |
            //                         RigidbodyConstraints.FreezeRotationY;
            _rigidbody.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Triggers give a much smoother collection feel than solid collision bouncing
            col.isTrigger = true;
        }
    }

    protected virtual void Start()
    {
        _basePosition = transform.position;
        ResolvePlayerReference();

        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.Register(this);
        }

        UpdateVisuals();
    }

    protected virtual void OnEnable()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.Register(this);
        }
    }

    protected virtual void OnDisable()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.Unregister(this);
        }
    }

    protected virtual void Update()
    {
        if (_isCollected) return;

        float dt = Time.deltaTime;
        _age += dt;

        // Auto-despawn timer
        if (autoDespawn && lifetime > 0f && _age >= lifetime)
        {
            Despawn();
            return;
        }

        // Magnet attraction
        if (enableMagnet)
        {
            if (_playerTransform == null)
            {
                ResolvePlayerReference();
            }

            if (_playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - transform.position;
                toPlayer.z = 0f; // Stay in 2D gameplay plane
                float distSqr = toPlayer.sqrMagnitude;

                if (distSqr <= magnetRadius * magnetRadius && distSqr > 0.001f)
                {
                    float currentSpeed = Mathf.Max(magnetSpeed, magnetSpeed * (1f + (_age * 0.1f)));
                    transform.position = Vector3.MoveTowards(transform.position, _playerTransform.position, currentSpeed * dt);
                    return; // Skip bobbing while pulled by magnet
                }
            }
        }

        // Idle hover & spin animation
        if (enableIdleAnimation)
        {
            transform.Rotate(0f, 0f, rotationSpeed * dt, Space.Self);

            float bobOffset = Mathf.Sin(_age * bobFrequency * Mathf.PI * 2f) * bobAmplitude * dt;
            transform.position += new Vector3(0f, bobOffset, 0f);
        }
    }

    /// <summary>
    /// Attempts to apply this powerup's effect to the given player.
    /// Must be implemented by concrete subclasses.
    /// </summary>
    /// <param name="targetPlayer">Target player component.</param>
    /// <returns>True if the effect was applied, false if rejected/invalid.</returns>
    public abstract bool ApplyEffect(player targetPlayer);

    /// <summary>
    /// Attempts to collect this powerup for the specified player.
    /// Guarded against double-collection in the same frame.
    /// </summary>
    public virtual bool TryCollect(player targetPlayer)
    {
        if (_isCollected || targetPlayer == null || targetPlayer.isDead)
        {
            return false;
        }

        if (ApplyEffect(targetPlayer))
        {
            _isCollected = true;
            OnCollected(targetPlayer);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Invoked immediately after ApplyEffect succeeds.
    /// Handles feedback effects, audio, notification to manager, and destruction.
    /// </summary>
    protected virtual void OnCollected(player targetPlayer)
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.NotifyPowerupCollected(this, targetPlayer);
            PowerupManager.Instance.SpawnFloatingFeedback(displayName, transform.position, themeColor);
        }

        if (pickupFxPrefab != null)
        {
            Instantiate(pickupFxPrefab, transform.position, Quaternion.identity);
        }

        if (pickupSound != null && targetPlayer != null)
        {
            AudioSource audio = targetPlayer.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.PlayOneShot(pickupSound);
            }
        }

        DestroySelf();
    }

    /// <summary>
    /// Gracefully despawns this powerup when its lifetime expires.
    /// </summary>
    public virtual void Despawn()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.NotifyPowerupDespawned(this);
        }
        DestroySelf();
    }

    protected virtual void DestroySelf()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(gameObject);
        else
            Destroy(gameObject);
#else
        Destroy(gameObject);
#endif
    }

    /// <summary>
    /// Updates label text and material tinting to reflect this powerup's attributes.
    /// </summary>
    public virtual void UpdateVisuals()
    {
        TextMeshPro tmp = GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.SetText(displayName);
            tmp.color = themeColor;
        }
    }

    protected void ResolvePlayerReference()
    {
        if (_playerTransform == null)
        {
            player p = FindAnyObjectByType<player>();
            if (p != null)
            {
                _playerTransform = p.transform;
            }
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Ignore sensory/boundary trigger spheres like max_view_radius
        if (other != null && other.isTrigger) return;

        CheckAndCollect(other.gameObject);
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        CheckAndCollect(collision.gameObject);
    }

    private void CheckAndCollect(GameObject go)
    {
        if (_isCollected || go == null) return;

        player p = go.GetComponent<player>() ?? go.GetComponentInParent<player>();
        if (p != null)
        {
            TryCollect(p);
        }
    }
}

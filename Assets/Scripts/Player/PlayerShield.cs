using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the player energy shield visual representation.
/// Creates a runtime material instance to avoid modifying project material assets on disk,
/// scrolls texture coordinates continuously proportional to ship velocity,
/// remembers original albedo color, modifies dynamic emission color,
/// and smoothly transitions alpha and emission when the shield is idle, hit, depleted, or restored.
/// </summary>
[DisallowMultipleComponent]
public class PlayerShield : MonoBehaviour
{
    public enum AlphaTransitionState
    {
        Idle,
        FadeIn,
        FadeOut
    }
     [SerializeField] private bool _useUnscaledTime = true;
    [Header("Material & Texture")]
    [Tooltip("Source material template. If null, will use the attached Renderer's material/sharedMaterial.")]
    public Material _mat;

    [Tooltip("Optional color tint override. If alpha is 0, uses the original material albedo color.")]
    public Color _color = Color.clear;

    [Tooltip("Texture vertical scrolling base speed.")]
    public float _speed = -0.5f;

    [Header("Alpha Settings")]
    [Tooltip("Default idle shield transparency.")]
    [SerializeField] private float _idleAlpha = 0.5f;

    [Tooltip("Shield transparency when struck.")]
    [SerializeField] private float _hitAlpha = 1.0f;

    [Tooltip("Duration in seconds to fade in from idle to hit alpha.")]
    [SerializeField] private float _fadeInDuration = 0.04f;

    [Tooltip("Duration in seconds to fade out from hit alpha back to idle alpha.")]
    [SerializeField] private float _fadeOutDuration = 0.35f;

    [Tooltip("Whether to hide or fade the shield mesh when player shield value is 0.")]
    [SerializeField] private bool _hideWhenDepleted = true;

    [Header("Emission Settings")]
    [Tooltip("Emission color when shield is idle.")]
    [ColorUsage(true, true)]
    public Color emission_idle = new Color(0f, 0.25f, 0.5f, 1f);

    [Tooltip("Emission color when shield is hit/struck.")]
    [ColorUsage(true, true)]
    public Color emission_hit = new Color(0.5f, 1.2f, 1.8f, 1f);

    [Header("Velocity-Driven Scrolling")]
    [Tooltip("Multiplier applied to player velocity to accelerate texture scroll speed.")]
    public float velocity_scroll_multiplier = 0.5f;

    [Header("Shield Hit Feedback")]
    [Tooltip("Temporarily accelerate texture scroll speed during a hit flash.")]
    [SerializeField] private bool _speedBoostOnHit = true;

    [Tooltip("Multiplier applied to scroll speed during a hit flash.")]
    [SerializeField] private float _speedBoostMultiplier = 2.5f;

    [Tooltip("Subtly pulse shield mesh scale on impact.")]
    [SerializeField] private bool _punchScaleOnHit = true;

    [Tooltip("Peak additional scale fraction on impact (e.g. 0.08 = +8%).")]
    [SerializeField] private float _punchScaleAmount = 0.08f;

    // Runtime state
    private Renderer _renderer;
    private Material _materialInstance;
    private Color _originalColor;
    private int _colorPropId;
    private int _mainTexPropId;
    private int _emissionPropId;
    private bool _hasColorProp;
    private bool _hasMainTexProp;
    private bool _hasEmissionProp;
    private bool _isInitialized;

    private float _scrollAccum;
    private float _currentAlpha = 0.5f;
    private float _targetHitAlpha = 1.0f;
    private float _transitionStartAlpha = 0.5f;

    private Color _currentEmissionColor;
    private Color _targetHitEmission;
    private Color _transitionStartEmission;

    private float _transitionTimer;
    private AlphaTransitionState _alphaState = AlphaTransitionState.Idle;

    private Vector3 _baseLocalScale;
    private float _currentScaleMultiplier = 1f;

    private player _playerRef;
    private Rigidbody _playerRigidbody;
    private float _simulatedVelocity = -1f;
    private bool _isShieldDepleted;
    private float _lastShieldValue = -1f;

    #region Public Properties

    public float IdleAlpha
    {
        get => _idleAlpha;
        set => _idleAlpha = Mathf.Clamp01(value);
    }

    public float HitAlpha
    {
        get => _hitAlpha;
        set => _hitAlpha = Mathf.Clamp01(value);
    }

    public float FadeInDuration
    {
        get => _fadeInDuration;
        set => _fadeInDuration = Mathf.Max(0f, value);
    }

    public float FadeOutDuration
    {
        get => _fadeOutDuration;
        set => _fadeOutDuration = Mathf.Max(0f, value);
    }

    public Color EmissionIdle
    {
        get => emission_idle;
        set => emission_idle = value;
    }

    public Color EmissionHit
    {
        get => emission_hit;
        set => emission_hit = value;
    }

    public float VelocityScrollMultiplier
    {
        get => velocity_scroll_multiplier;
        set => velocity_scroll_multiplier = value;
    }

    public float EffectiveScrollSpeed { get; private set; }

    public Color OriginalColor => _originalColor;
    public Material MaterialInstance => _materialInstance;
    public float CurrentAlpha => _currentAlpha;
    public Color CurrentEmissionColor => _currentEmissionColor;
    public AlphaTransitionState State => _alphaState;
    public bool IsInitialized => _isInitialized;

    public float TargetIdleAlpha
    {
        get
        {
            if (_hideWhenDepleted && _isShieldDepleted)
            {
                return 0f;
            }
            return _idleAlpha;
        }
    }

    public Color TargetIdleEmission
    {
        get
        {
            if (_hideWhenDepleted && _isShieldDepleted)
            {
                return Color.black;
            }
            return emission_idle;
        }
    }

    /// <summary>
    /// Overrides player velocity reading for deterministic unit testing.
    /// Set to negative value to resume reading Rigidbody.linearVelocity.
    /// </summary>
    public void SetSimulatedVelocity(float velocity)
    {
        _simulatedVelocity = velocity;
    }

    #endregion

    private void Awake()
    {
        _baseLocalScale = transform.localScale;
        InitMaterialInstance();
    }

    private void Start()
    {
        if (!_isInitialized)
        {
            InitMaterialInstance();
        }
    }

    private void OnEnable()
    {
        BindPlayer();
    }

    private void OnDisable()
    {
        UnbindPlayer();
    }

    private void OnDestroy()
    {
        CleanupMaterialInstance();
    }

    /// <summary>
    /// Instantiates a runtime copy of the material template so that
    /// texture offset, albedo, and emission modifications never alter project assets on disk.
    /// Captures the original albedo color from the template.
    /// </summary>
    public void InitMaterialInstance()
    {
        if (_isInitialized && _materialInstance != null) return;

        if (_renderer == null)
        {
            _renderer = GetComponent<Renderer>();
        }

        if (_baseLocalScale == Vector3.zero)
        {
            _baseLocalScale = transform.localScale;
        }

        Material sourceMat = _mat;
        if (sourceMat == null && _renderer != null)
        {
            sourceMat = _renderer.sharedMaterial;
        }

        if (sourceMat != null)
        {
            // Identify color property (Standard/Particles vs URP)
            if (sourceMat.HasProperty("_Color"))
            {
                _colorPropId = Shader.PropertyToID("_Color");
                _hasColorProp = true;
            }
            else if (sourceMat.HasProperty("_BaseColor"))
            {
                _colorPropId = Shader.PropertyToID("_BaseColor");
                _hasColorProp = true;
            }

            // Identify texture property
            if (sourceMat.HasProperty("_MainTex"))
            {
                _mainTexPropId = Shader.PropertyToID("_MainTex");
                _hasMainTexProp = true;
            }
            else if (sourceMat.HasProperty("_BaseMap"))
            {
                _mainTexPropId = Shader.PropertyToID("_BaseMap");
                _hasMainTexProp = true;
            }

            // Identify emission property
            if (sourceMat.HasProperty("_EmissionColor"))
            {
                _emissionPropId = Shader.PropertyToID("_EmissionColor");
                _hasEmissionProp = true;
            }

            // Remember original material albedo color
            if (_color.a > 0.001f)
            {
                _originalColor = _color;
            }
            else if (_hasColorProp)
            {
                _originalColor = sourceMat.GetColor(_colorPropId);
            }
            else
            {
                _originalColor = Color.cyan;
            }

            // Create isolated runtime instance
            _materialInstance = new Material(sourceMat);
            _materialInstance.name = $"{sourceMat.name} (Instance)";

            if (_hasEmissionProp)
            {
                _materialInstance.EnableKeyword("_EMISSION");
            }

            if (_renderer != null)
            {
                _renderer.material = _materialInstance;
            }
        }

        _currentAlpha = _idleAlpha;
        _currentEmissionColor = TargetIdleEmission;
        _targetHitEmission = emission_hit;
        _alphaState = AlphaTransitionState.Idle;
        _isInitialized = true;

        BindPlayer();
        ApplyMaterialProperties();
    }

    private void CleanupMaterialInstance()
    {
        if (_materialInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_materialInstance);
            }
            else
            {
                DestroyImmediate(_materialInstance);
            }
            _materialInstance = null;
        }
    }

    public void BindPlayer(player playerInstance = null)
    {
        if (playerInstance != null)
        {
            _playerRef = playerInstance;
        }
        else if (_playerRef == null)
        {
            _playerRef = GetComponentInParent<player>();
        }

        if (_playerRef != null)
        {
            _playerRigidbody = _playerRef.GetComponent<Rigidbody>();

            _playerRef.OnShieldHit -= HandleShieldHit;
            _playerRef.OnShieldChanged -= HandleShieldChanged;

            _playerRef.OnShieldHit += HandleShieldHit;
            _playerRef.OnShieldChanged += HandleShieldChanged;

            _lastShieldValue = _playerRef.shield_value;
            _isShieldDepleted = _playerRef.shield_value <= 0.001f;

            if (_hideWhenDepleted && _isShieldDepleted)
            {
                _currentAlpha = 0f;
                _currentEmissionColor = Color.black;
                ApplyMaterialProperties();
            }
        }
        else if (_playerRigidbody == null)
        {
            _playerRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    public void UnbindPlayer()
    {
        if (_playerRef != null)
        {
            _playerRef.OnShieldHit -= HandleShieldHit;
            _playerRef.OnShieldChanged -= HandleShieldChanged;
        }
    }

    private void HandleShieldHit(float damage, Vector3 contactPoint)
    {
        TriggerHit();
    }

    private void HandleShieldChanged(float current, float max)
    {
        bool wasDepleted = _isShieldDepleted;
        _isShieldDepleted = current <= 0.001f;

        // If shield was damaged and hit event wasn't invoked
        if (_lastShieldValue >= 0f && current < _lastShieldValue && _alphaState == AlphaTransitionState.Idle)
        {
            TriggerHit();
        }

        _lastShieldValue = current;

        // If shield was depleted and was idle, hide shield and turn off emission
        if (_hideWhenDepleted && _isShieldDepleted && _alphaState == AlphaTransitionState.Idle)
        {
            _currentAlpha = 0f;
            _currentEmissionColor = Color.black;
            ApplyMaterialProperties();
        }
    }

    private void Update()
    {
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
          UpdateShield(dt);
    }

    /// <summary>
    /// Deterministic update method for texture scrolling, alpha transitions, and emission colors.
    /// Can be called directly by unit tests or simulated ticks.
    /// </summary>
    public void UpdateShield(float deltaTime)
    {
        if (!_isInitialized)
        {
            InitMaterialInstance();
        }

        UpdateTextureScrolling(deltaTime);
        UpdateAlphaTransition(deltaTime);
        ApplyMaterialProperties();
    }

    private void UpdateTextureScrolling(float deltaTime)
    {
        float playerSpeed = 0f;
        if (_simulatedVelocity >= 0f)
        {
            playerSpeed = _simulatedVelocity;
        }
        else if (_playerRigidbody != null)
        {
            playerSpeed = _playerRigidbody.linearVelocity.magnitude;
        }

        float dir = (_speed < 0f) ? -1f : 1f;
        float velocityContribution = dir * (playerSpeed * velocity_scroll_multiplier);
        float baseSpeed = _speed + velocityContribution;

        float effectiveSpeed = baseSpeed;
        if (_speedBoostOnHit && _alphaState != AlphaTransitionState.Idle)
        {
            float boostFactor = Mathf.InverseLerp(_idleAlpha, _hitAlpha, _currentAlpha);
            effectiveSpeed = Mathf.Lerp(baseSpeed, baseSpeed * _speedBoostMultiplier, boostFactor);
        }

        EffectiveScrollSpeed = effectiveSpeed;

        if (_materialInstance == null || !_hasMainTexProp) return;

        _scrollAccum = Mathf.Repeat(_scrollAccum + effectiveSpeed * deltaTime, 1f);
        _materialInstance.SetTextureOffset(_mainTexPropId, new Vector2(0f, _scrollAccum));
    }

    private void UpdateAlphaTransition(float deltaTime)
    {
        float targetIdleAlpha = TargetIdleAlpha;
        Color targetIdleEmission = TargetIdleEmission;

        switch (_alphaState)
        {
            case AlphaTransitionState.FadeIn:
                if (_fadeInDuration > 0.0001f)
                {
                    _transitionTimer += deltaTime;
                    float t = Mathf.Clamp01(_transitionTimer / _fadeInDuration);
                    _currentAlpha = Mathf.Lerp(_transitionStartAlpha, _targetHitAlpha, t);
                    _currentEmissionColor = Color.Lerp(_transitionStartEmission, _targetHitEmission, t);

                    if (t >= 1f)
                    {
                        _alphaState = AlphaTransitionState.FadeOut;
                        _transitionTimer = 0f;
                        _transitionStartAlpha = _targetHitAlpha;
                        _transitionStartEmission = _targetHitEmission;
                    }
                }
                else
                {
                    _currentAlpha = _targetHitAlpha;
                    _currentEmissionColor = _targetHitEmission;
                    _alphaState = AlphaTransitionState.FadeOut;
                    _transitionTimer = 0f;
                    _transitionStartAlpha = _targetHitAlpha;
                    _transitionStartEmission = _targetHitEmission;
                }
                break;

            case AlphaTransitionState.FadeOut:
                if (_fadeOutDuration > 0.0001f)
                {
                    _transitionTimer += deltaTime;
                    float t = Mathf.Clamp01(_transitionTimer / _fadeOutDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    _currentAlpha = Mathf.Lerp(_transitionStartAlpha, targetIdleAlpha, smoothT);
                    _currentEmissionColor = Color.Lerp(_transitionStartEmission, targetIdleEmission, smoothT);

                    if (t >= 1f)
                    {
                        _currentAlpha = targetIdleAlpha;
                        _currentEmissionColor = targetIdleEmission;
                        _alphaState = AlphaTransitionState.Idle;
                    }
                }
                else
                {
                    _currentAlpha = targetIdleAlpha;
                    _currentEmissionColor = targetIdleEmission;
                    _alphaState = AlphaTransitionState.Idle;
                }
                break;

            case AlphaTransitionState.Idle:
                // Smoothly settle towards target idle values if target changed (e.g. shield restored or depleted)
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetIdleAlpha, deltaTime * 3f);
                _currentEmissionColor = Color.Lerp(_currentEmissionColor, targetIdleEmission, deltaTime * 5f);
                break;
        }

        // Handle punch scale relaxation
        if (_punchScaleOnHit && _currentScaleMultiplier > 1f)
        {
            _currentScaleMultiplier = Mathf.MoveTowards(_currentScaleMultiplier, 1f, deltaTime * 3f);
            if (_baseLocalScale != Vector3.zero)
            {
                transform.localScale = _baseLocalScale * _currentScaleMultiplier;
            }
        }
    }

    private void ApplyMaterialProperties()
    {
        if (_materialInstance == null) return;

        if (_renderer != null)
        {
            bool shouldRender = _currentAlpha > 0.001f || _alphaState != AlphaTransitionState.Idle;
            if (_renderer.enabled != shouldRender)
            {
                _renderer.enabled = shouldRender;
            }
        }

        if (_hasColorProp)
        {
            Color c = _originalColor;
            c.a = _currentAlpha;
            _materialInstance.SetColor(_colorPropId, c);
        }

        if (_hasEmissionProp)
        {
            _materialInstance.SetColor(_emissionPropId, _currentEmissionColor);
        }
    }

    /// <summary>
    /// Triggers shield impact flare: ramps alpha to hitAlpha (1.0),
    /// flashes emission to emission_hit, and fades back to idle values.
    /// </summary>
    /// <param name="intensity">Impact intensity normalized between 0 and 1.</param>
    public void TriggerHit(float intensity = 1f)
    {
        if (!_isInitialized)
        {
            InitMaterialInstance();
        }

        intensity = Mathf.Clamp01(intensity <= 0f ? 1f : intensity);
        _targetHitAlpha = Mathf.Lerp(_idleAlpha, _hitAlpha, intensity);
        _targetHitEmission = Color.Lerp(TargetIdleEmission, emission_hit, intensity);

        if (_fadeInDuration > 0.0001f)
        {
            _alphaState = AlphaTransitionState.FadeIn;
            _transitionTimer = 0f;
            _transitionStartAlpha = _currentAlpha;
            _transitionStartEmission = _currentEmissionColor;
        }
        else
        {
            _currentAlpha = _targetHitAlpha;
            _currentEmissionColor = _targetHitEmission;
            _alphaState = AlphaTransitionState.FadeOut;
            _transitionTimer = 0f;
            _transitionStartAlpha = _targetHitAlpha;
            _transitionStartEmission = _targetHitEmission;
        }

        if (_punchScaleOnHit)
        {
            _currentScaleMultiplier = 1f + (_punchScaleAmount * intensity);
            if (_baseLocalScale != Vector3.zero)
            {
                transform.localScale = _baseLocalScale * _currentScaleMultiplier;
            }
        }

        ApplyMaterialProperties();
    }

    /// <summary>
    /// Semantic alias for TriggerHit to support projectile or damage callbacks.
    /// </summary>
    public void OnHit(float damage = 0f, Vector3 hitPoint = default)
    {
        TriggerHit();
    }
}

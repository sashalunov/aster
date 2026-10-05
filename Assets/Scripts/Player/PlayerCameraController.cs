using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controls player camera switching between the top-down combat camera (cam_top)
/// and the close-up orbital briefing camera (cam_brief).
/// Smoothly transitions between modes during menus (title, pause) and wave cleared / intermission phases.
/// Automatically updates UI Canvases (ScreenSpace - Camera) and manages in-game HUD visibility and scene fog density.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraController : MonoBehaviour
{
    public enum CameraMode
    {
        TopDown,
        Briefing
    }

    [Header("Scene Fog Control")]
    [Tooltip("Target fog density when in close-up orbital briefing mode.")]
    [SerializeField] public float briefFogDensity = 0.008f;

    [Tooltip("Target fog density when in top-down combat mode.")]
    [SerializeField] public float gameFogDensity = 0.002f;

    [Tooltip("Whether to automatically manage and transition scene fog density.")]
    [SerializeField] public bool manageFog = true;

    [Header("Camera References")]
    [Tooltip("Primary top-down combat camera.")]
    [SerializeField] private Camera _camTop;

    [Tooltip("Close-up orbital briefing camera (child of briefing_camera_animator).")]
    [SerializeField] private Camera _camBrief;

    [Tooltip("Orbital animator GameObject containing cam_brief and DOTweenAnimation.")]
    [SerializeField] private GameObject _briefingAnimator;

    [Tooltip("Player component reference.")]
    [SerializeField] private player _player;

    [Header("In-Game HUD Control")]
    [Tooltip("In-game combat HUD GameObject to show during TopDown and hide during Briefing.")]
    [SerializeField] private GameObject inGameHUD;

    [Tooltip("Whether to automatically discover the 'HUD' GameObject in the Canvas if unassigned.")]
    [SerializeField] private bool autoFindHUD = true;

    [Tooltip("Whether to smoothly fade the HUD during transitions using a CanvasGroup.")]
    [SerializeField] private bool fadeHUD = true;

    [Header("UI Canvas Switching")]
    [Tooltip("Canvases whose worldCamera will be updated when switching camera modes.")]
    [SerializeField] private List<Canvas> targetCanvases = new List<Canvas>();

    [Tooltip("Whether to automatically discover and update ScreenSpaceCamera Canvases in the scene.")]
    [SerializeField] private bool autoFindCanvases = true;

    [Header("Transition Settings")]
    [Tooltip("Duration of the camera transition in real seconds.")]
    [Range(0.2f, 3.0f)]
    [SerializeField] private float transitionDuration = 0.8f;

    [Tooltip("Easing curve applied to the transition interpolation.")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Whether to use unscaled time for smooth transitions even when the game is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Max linear speed to clamp player ship to when entering wave cleared briefing view.")]
    [SerializeField] private float maxBriefingDriftSpeed = 1.5f;

    [Header("Runtime State")]
    [SerializeField] private CameraMode _currentMode = CameraMode.TopDown;
    [SerializeField] private bool _isTransitioning = false;

    // Home pose for cam_top (relative to Player_ship root)
    private Vector3 _homeLocalPos;
    private Quaternion _homeLocalRot;
    private float _homeFov = 40f;
    private float _homeNearClip = 0.1f;

    // Audio listener caches
    private AudioListener _audioListenerTop;
    private AudioListener _audioListenerBrief;

    // HUD CanvasGroup cache
    private CanvasGroup _hudCanvasGroup;

    // State tracking
    private bool _isMenuOpen = false;
    private bool _isWaveCleared = false;
    private Coroutine _activeTransitionRoutine;
    private Rigidbody _playerRb;
    private DOTweenAnimation _briefingDotweenAnim;

    // Events
    public event Action<CameraMode> OnCameraModeChanged;
    public event Action<CameraMode, CameraMode> OnTransitionStarted;
    public event Action<CameraMode> OnTransitionCompleted;

    public CameraMode CurrentMode => _currentMode;
    public bool IsTransitioning => _isTransitioning;
    public Camera ActiveCamera => _currentMode == CameraMode.Briefing ? _camBrief : _camTop;
    public Camera TopDownCamera => _camTop;
    public Camera BriefingCamera => _camBrief;

    private void Awake()
    {
        ResolveReferences();
        CacheHomeTransform();
        ConfigureBriefingAnimator();
        EnsureSingleAudioListener();
    }

    private void Start()
    {
        SubscribeEvents();
        ResolveHUD();

        // Check if menu is already open on game start
        MainMenu menu = MainMenu.Instance != null ? MainMenu.Instance : FindAnyObjectByType<MainMenu>();
        if (menu != null && menu.IsOpen)
        {
            _isMenuOpen = true;
        }

        // Check if wave is already in cleared/intermission state
        if (WaveManager.Instance != null &&
            (WaveManager.Instance.State == WaveManager.WaveState.WaveCleared ||
             WaveManager.Instance.State == WaveManager.WaveState.Intermission))
        {
            _isWaveCleared = true;
        }

        // On game startup, if in menu or wave cleared, immediately snap to Briefing camera without transition
        bool shouldBeBriefing = _isMenuOpen || _isWaveCleared;
        SetCameraMode(shouldBeBriefing ? CameraMode.Briefing : CameraMode.TopDown, smooth: false);

        // Ensure all UI Canvases match the active camera immediately
        UpdateCanvasCameras(ActiveCamera);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Auto-resolves camera, component, canvas, and HUD references if unassigned.
    /// </summary>
    public void ResolveReferences()
    {
        if (_player == null)
        {
            _player = GetComponent<player>();
        }

        if (_playerRb == null)
        {
            _playerRb = GetComponent<Rigidbody>();
        }

        if (_camTop == null)
        {
            Transform t = transform.Find("cam_top");
            if (t != null) _camTop = t.GetComponent<Camera>();
        }

        if (_briefingAnimator == null)
        {
            Transform t = transform.Find("briefing_camera_animator");
            if (t != null) _briefingAnimator = t.gameObject;
        }

        if (_camBrief == null && _briefingAnimator != null)
        {
            Transform t = _briefingAnimator.transform.Find("cam_brief");
            if (t != null) _camBrief = t.GetComponent<Camera>();
        }

        if (_camTop != null)
        {
            _audioListenerTop = _camTop.GetComponent<AudioListener>();
        }

        if (_camBrief != null)
        {
            _audioListenerBrief = _camBrief.GetComponent<AudioListener>();
        }

        if (_briefingAnimator != null)
        {
            _briefingDotweenAnim = _briefingAnimator.GetComponent<DOTweenAnimation>();
        }

        if (autoFindCanvases)
        {
            DiscoverCanvases();
        }

        ResolveHUD();
    }

    private void CacheHomeTransform()
    {
        if (_camTop != null)
        {
            if (_camTop.nearClipPlane > 0.1f)
            {
                _camTop.nearClipPlane = 0.1f;
            }

            _homeLocalPos = _camTop.transform.localPosition;
            _homeLocalRot = _camTop.transform.localRotation;
            _homeFov = _camTop.fieldOfView;
            _homeNearClip = _camTop.nearClipPlane;
        }
        else
        {
            _homeLocalPos = new Vector3(0f, 0f, -36f);
            _homeLocalRot = Quaternion.identity;
            _homeFov = 40f;
            _homeNearClip = 0.1f;
        }
    }

    private void ConfigureBriefingAnimator()
    {
        if (_briefingDotweenAnim != null)
        {
            _briefingDotweenAnim.isIndependentUpdate = true;
            _briefingDotweenAnim.autoKill = false;
        }
    }

    private void EnsureSingleAudioListener()
    {
        if (_currentMode == CameraMode.Briefing)
        {
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = true;
            if (_audioListenerTop != null) _audioListenerTop.enabled = false;
        }
        else
        {
            if (_audioListenerTop != null) _audioListenerTop.enabled = true;
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = false;
        }
    }

    #region In-Game HUD Control

    /// <summary>
    /// Resolves the in-game HUD GameObject and attaches a CanvasGroup for smooth alpha fading if requested.
    /// </summary>
    public void ResolveHUD()
    {
        if (inGameHUD == null && autoFindHUD)
        {
            for (int i = 0; i < targetCanvases.Count; i++)
            {
                if (targetCanvases[i] != null)
                {
                    Transform t = targetCanvases[i].transform.Find("HUD");
                    if (t != null)
                    {
                        inGameHUD = t.gameObject;
                        break;
                    }
                }
            }

            if (inGameHUD == null)
            {
                GameObject h = GameObject.Find("Canvas/HUD") ?? GameObject.Find("HUD");
                if (h != null) inGameHUD = h;
            }
        }

        if (inGameHUD != null && _hudCanvasGroup == null)
        {
            _hudCanvasGroup = inGameHUD.GetComponent<CanvasGroup>();
            if (_hudCanvasGroup == null && fadeHUD)
            {
                _hudCanvasGroup = inGameHUD.AddComponent<CanvasGroup>();
            }
        }
    }

    /// <summary>
    /// Instantly shows or hides the in-game combat HUD.
    /// </summary>
    public void SetHUDVisibilityImmediate(bool visible)
    {
        if (inGameHUD == null) ResolveHUD();
        if (inGameHUD == null) return;

        if (_hudCanvasGroup != null)
        {
            _hudCanvasGroup.alpha = visible ? 1f : 0f;
            _hudCanvasGroup.interactable = visible;
            _hudCanvasGroup.blocksRaycasts = visible;
        }

        inGameHUD.SetActive(visible);
    }

    /// <summary>
    /// Interpolates HUD alpha during a camera transition.
    /// </summary>
    private void UpdateHUDTransition(float progress, bool showing)
    {
        if (inGameHUD == null) ResolveHUD();
        if (inGameHUD == null) return;

        if (!inGameHUD.activeSelf)
        {
            inGameHUD.SetActive(true);
        }

        if (_hudCanvasGroup != null)
        {
            float targetAlpha = showing ? progress : (1f - progress);
            _hudCanvasGroup.alpha = targetAlpha;
            _hudCanvasGroup.interactable = targetAlpha > 0.1f;
            _hudCanvasGroup.blocksRaycasts = targetAlpha > 0.1f;
        }
    }

    #endregion

    #region Scene Fog Control

    /// <summary>
    /// Sets the scene fog density immediately and ensures fog is enabled.
    /// </summary>
    public void SetFogDensityImmediate(float density)
    {
        if (!RenderSettings.fog)
        {
            RenderSettings.fog = true;
        }
        RenderSettings.fogDensity = density;
    }

    #endregion

    #region Canvas Camera Management

    /// <summary>
    /// Automatically discovers root ScreenSpaceCamera Canvases in the scene.
    /// </summary>
    public void DiscoverCanvases()
    {
        var foundCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < foundCanvases.Length; i++)
        {
            Canvas c = foundCanvases[i];
            if (c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (!targetCanvases.Contains(c))
                {
                    targetCanvases.Add(c);
                }
            }
        }
    }

    /// <summary>
    /// Updates the worldCamera reference on all tracked Canvases so UI remains visible and interactive.
    /// </summary>
    /// <param name="cam">The active Camera to render the UI Canvas.</param>
    public void UpdateCanvasCameras(Camera cam)
    {
        if (cam == null) return;

        if (autoFindCanvases)
        {
            DiscoverCanvases();
        }

        for (int i = 0; i < targetCanvases.Count; i++)
        {
            Canvas c = targetCanvases[i];
            if (c != null && c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                c.worldCamera = cam;
            }
        }
    }

    /// <summary>
    /// Registers a Canvas for automatic camera switching.
    /// </summary>
    public void RegisterCanvas(Canvas canvas)
    {
        if (canvas != null && !targetCanvases.Contains(canvas))
        {
            targetCanvases.Add(canvas);
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = ActiveCamera;
            }
        }
    }

    /// <summary>
    /// Unregisters a Canvas from automatic camera switching.
    /// </summary>
    public void UnregisterCanvas(Canvas canvas)
    {
        if (canvas != null)
        {
            targetCanvases.Remove(canvas);
        }
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeEvents()
    {
        MainMenu menu = MainMenu.Instance != null ? MainMenu.Instance : FindAnyObjectByType<MainMenu>();
        if (menu != null)
        {
            menu.OnMenuOpened += HandleMenuOpened;
            menu.OnMenuClosed += HandleMenuClosed;
            menu.OnGameStarted += HandleGameStarted;
            menu.OnGameResumed += HandleGameResumed;
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted += HandleWaveCompleted;
            WaveManager.Instance.OnStateChanged += HandleWaveStateChanged;
            WaveManager.Instance.OnWaveStarted += HandleWaveStarted;
        }

        if (_player != null)
        {
            _player.OnDeath += HandlePlayerDeath;
        }
    }

    private void UnsubscribeEvents()
    {
        MainMenu menu = MainMenu.Instance != null ? MainMenu.Instance : FindAnyObjectByType<MainMenu>();
        if (menu != null)
        {
            menu.OnMenuOpened -= HandleMenuOpened;
            menu.OnMenuClosed -= HandleMenuClosed;
            menu.OnGameStarted -= HandleGameStarted;
            menu.OnGameResumed -= HandleGameResumed;
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted -= HandleWaveCompleted;
            WaveManager.Instance.OnStateChanged -= HandleWaveStateChanged;
            WaveManager.Instance.OnWaveStarted -= HandleWaveStarted;
        }

        if (_player != null)
        {
            _player.OnDeath -= HandlePlayerDeath;
        }
    }

    private void HandleMenuOpened()
    {
        _isMenuOpen = true;
        EvaluateTargetCamera(smooth: Time.frameCount > 5);
    }

    private void HandleMenuClosed()
    {
        _isMenuOpen = false;
        EvaluateTargetCamera(smooth: true);
    }

    private void HandleGameStarted()
    {
        _isMenuOpen = false;
        EvaluateTargetCamera(smooth: true);
    }

    private void HandleGameResumed()
    {
        _isMenuOpen = false;
        EvaluateTargetCamera(smooth: true);
    }

    private void HandleWaveCompleted(int waveIndex, WaveDefinition config)
    {
        _isWaveCleared = true;
        ClampShipDrift();
        EvaluateTargetCamera(smooth: true);
    }

    private void HandleWaveStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
    {
        if (newState == WaveManager.WaveState.WaveCleared || newState == WaveManager.WaveState.Intermission)
        {
            _isWaveCleared = true;
            ClampShipDrift();
            EvaluateTargetCamera(smooth: true);
        }
        else if (newState == WaveManager.WaveState.Countdown || newState == WaveManager.WaveState.Combat)
        {
            _isWaveCleared = false;
            EvaluateTargetCamera(smooth: true);
        }
    }

    private void HandleWaveStarted(int waveIndex, WaveDefinition config)
    {
        _isWaveCleared = false;
        EvaluateTargetCamera(smooth: true);
    }

    private void HandlePlayerDeath()
    {
        if (_activeTransitionRoutine != null)
        {
            StopCoroutine(_activeTransitionRoutine);
            _activeTransitionRoutine = null;
            _isTransitioning = false;
        }
    }

    private void ClampShipDrift()
    {
        if (_playerRb != null && maxBriefingDriftSpeed > 0f)
        {
            if (_playerRb.linearVelocity.sqrMagnitude > maxBriefingDriftSpeed * maxBriefingDriftSpeed)
            {
                _playerRb.linearVelocity = _playerRb.linearVelocity.normalized * maxBriefingDriftSpeed;
            }
        }
    }

    private void EvaluateTargetCamera(bool smooth)
    {
        if (_player != null && _player.isDead) return;

        bool preferBriefing = _isMenuOpen || _isWaveCleared;
        CameraMode targetMode = preferBriefing ? CameraMode.Briefing : CameraMode.TopDown;

        SetCameraMode(targetMode, smooth);
    }

    #endregion

    #region Camera Switching & Transitions

    /// <summary>
    /// Switches camera mode with optional smooth transition.
    /// </summary>
    /// <param name="targetMode">Desired camera mode.</param>
    /// <param name="smooth">Whether to interpolate smoothly between camera poses.</param>
    public void SetCameraMode(CameraMode targetMode, bool smooth = true)
    {
        if (_camTop == null || _camBrief == null)
        {
            ResolveReferences();
            if (_camTop == null || _camBrief == null) return;
        }

        // If already in target mode and not transitioning, nothing to do
        if (_currentMode == targetMode && !_isTransitioning) return;

        if (_activeTransitionRoutine != null)
        {
            StopCoroutine(_activeTransitionRoutine);
            _activeTransitionRoutine = null;
        }

        if (!smooth || transitionDuration <= 0f)
        {
            ApplyInstantCut(targetMode);
        }
        else
        {
            _activeTransitionRoutine = StartCoroutine(TransitionRoutine(targetMode));
        }
    }

    /// <summary>
    /// Instantly switches camera mode without interpolation.
    /// </summary>
    private void ApplyInstantCut(CameraMode targetMode)
    {
        _isTransitioning = false;
        CameraMode prevMode = _currentMode;
        _currentMode = targetMode;

        if (targetMode == CameraMode.Briefing)
        {
            if (_briefingAnimator != null)
            {
                _briefingAnimator.SetActive(true);
                PlayBriefingAnimation();
            }

            _camBrief.enabled = true;
            _camBrief.tag = "MainCamera";
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = true;

            _camTop.enabled = false;
            _camTop.tag = "Untagged";
            if (_audioListenerTop != null) _audioListenerTop.enabled = false;

            // Update UI Canvas worldCamera to cam_brief
            UpdateCanvasCameras(_camBrief);

            // Hide in-game HUD immediately
            SetHUDVisibilityImmediate(false);

            // Set fog density immediately to briefing density
            if (manageFog)
            {
                SetFogDensityImmediate(briefFogDensity);
            }

            // Reset cam_top to default home local pose
            _camTop.transform.localPosition = _homeLocalPos;
            _camTop.transform.localRotation = _homeLocalRot;
            _camTop.fieldOfView = _homeFov;
            _camTop.nearClipPlane = _homeNearClip;
        }
        else
        {
            _camTop.transform.localPosition = _homeLocalPos;
            _camTop.transform.localRotation = _homeLocalRot;
            _camTop.fieldOfView = _homeFov;
            _camTop.nearClipPlane = _homeNearClip;

            _camTop.enabled = true;
            _camTop.tag = "MainCamera";
            if (_audioListenerTop != null) _audioListenerTop.enabled = true;

            _camBrief.enabled = false;
            _camBrief.tag = "Untagged";
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = false;

            // Update UI Canvas worldCamera to cam_top
            UpdateCanvasCameras(_camTop);

            // Show in-game HUD immediately
            SetHUDVisibilityImmediate(true);

            // Set fog density immediately to game combat density
            if (manageFog)
            {
                SetFogDensityImmediate(gameFogDensity);
            }

            if (_briefingAnimator != null)
            {
                _briefingAnimator.SetActive(false);
            }
        }

        if (_player != null)
        {
            _player._player_cam = _camTop;
        }

        OnCameraModeChanged?.Invoke(_currentMode);
    }

    /// <summary>
    /// Performs a smooth unscaled-time interpolation between camera poses, fog density, and HUD visibility.
    /// Uses cam_top as the continuous rendering camera during the transition.
    /// </summary>
    private IEnumerator TransitionRoutine(CameraMode targetMode)
    {
        _isTransitioning = true;
        CameraMode prevMode = _currentMode;
        OnTransitionStarted?.Invoke(prevMode, targetMode);

        // Record start pose relative to Player_ship root transform so ship movement doesn't cause lag/drift
        Vector3 startLocalPos;
        Quaternion startLocalRot;
        float startFov;
        float startNearClip;

        // Record start and target fog densities
        float startFogDensity = RenderSettings.fogDensity;
        float targetFogDensity = (targetMode == CameraMode.Briefing) ? briefFogDensity : gameFogDensity;
        if (manageFog && !RenderSettings.fog)
        {
            RenderSettings.fog = true;
        }

        if (targetMode == CameraMode.Briefing)
        {
            // Activate briefing animator so it starts orbiting and cam_brief has a live target pose
            if (_briefingAnimator != null)
            {
                _briefingAnimator.SetActive(true);
                PlayBriefingAnimation();
            }

            // Ensure cam_top is rendering while cam_brief is disabled during transition
            _camTop.enabled = true;
            _camTop.tag = "MainCamera";
            if (_audioListenerTop != null) _audioListenerTop.enabled = true;

            _camBrief.enabled = false;
            _camBrief.tag = "Untagged";
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = false;

            // Canvas renders through cam_top during the swoop transition
            UpdateCanvasCameras(_camTop);

            // Start from cam_top's current world pose
            startLocalPos = transform.InverseTransformPoint(_camTop.transform.position);
            startLocalRot = Quaternion.Inverse(transform.rotation) * _camTop.transform.rotation;
            startFov = _camTop.fieldOfView;
            startNearClip = _camTop.nearClipPlane;

            // If entering from gameplay top-down view, update home zoom if player modified it
            if (prevMode == CameraMode.TopDown && !_isTransitioning)
            {
                _homeLocalPos = _camTop.transform.localPosition;
            }

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / transitionDuration);
                float t = transitionCurve != null ? transitionCurve.Evaluate(progress) : Mathf.SmoothStep(0f, 1f, progress);

                // Origin evaluated dynamically from ship root
                Vector3 currentStartWorldPos = transform.TransformPoint(startLocalPos);
                Quaternion currentStartWorldRot = transform.rotation * startLocalRot;

                // Destination dynamically tracks cam_brief's orbital transform
                Vector3 currentTargetWorldPos = _camBrief.transform.position;
                Quaternion currentTargetWorldRot = _camBrief.transform.rotation;
                float targetFov = _camBrief.fieldOfView;
                float targetNearClip = _camBrief.nearClipPlane;

                _camTop.transform.position = Vector3.Lerp(currentStartWorldPos, currentTargetWorldPos, t);
                _camTop.transform.rotation = Quaternion.Slerp(currentStartWorldRot, currentTargetWorldRot, t);
                _camTop.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
                _camTop.nearClipPlane = Mathf.Lerp(startNearClip, targetNearClip, t);

                // Smoothly interpolate fog density and fade out HUD
                if (manageFog)
                {
                    RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);
                }
                UpdateHUDTransition(t, showing: false);

                yield return null;
            }

            // Snap seamlessly to cam_brief and hand over rendering
            _camBrief.enabled = true;
            _camBrief.tag = "MainCamera";
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = true;

            _camTop.enabled = false;
            _camTop.tag = "Untagged";
            if (_audioListenerTop != null) _audioListenerTop.enabled = false;

            // Hand over UI Canvas to cam_brief
            UpdateCanvasCameras(_camBrief);

            // Finalize HUD hidden state and fog density
            SetHUDVisibilityImmediate(false);
            if (manageFog)
            {
                SetFogDensityImmediate(targetFogDensity);
            }

            // Reset cam_top to default home local pose for next transition
            _camTop.transform.localPosition = _homeLocalPos;
            _camTop.transform.localRotation = _homeLocalRot;
            _camTop.fieldOfView = _homeFov;
            _camTop.nearClipPlane = _homeNearClip;
        }
        else // targetMode == CameraMode.TopDown
        {
            // Position cam_top at cam_brief's current world pose before enabling it for an imperceptible handoff
            _camTop.transform.position = _camBrief.transform.position;
            _camTop.transform.rotation = _camBrief.transform.rotation;
            _camTop.fieldOfView = _camBrief.fieldOfView;
            _camTop.nearClipPlane = _camBrief.nearClipPlane;

            _camTop.enabled = true;
            _camTop.tag = "MainCamera";
            if (_audioListenerTop != null) _audioListenerTop.enabled = true;

            _camBrief.enabled = false;
            _camBrief.tag = "Untagged";
            if (_audioListenerBrief != null) _audioListenerBrief.enabled = false;

            // Hand over UI Canvas to cam_top immediately
            UpdateCanvasCameras(_camTop);

            // Record start pose from cam_top's position
            startLocalPos = transform.InverseTransformPoint(_camTop.transform.position);
            startLocalRot = Quaternion.Inverse(transform.rotation) * _camTop.transform.rotation;
            startFov = _camTop.fieldOfView;
            startNearClip = _camTop.nearClipPlane;

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / transitionDuration);
                float t = transitionCurve != null ? transitionCurve.Evaluate(progress) : Mathf.SmoothStep(0f, 1f, progress);

                // Origin evaluated dynamically from ship root
                Vector3 currentStartWorldPos = transform.TransformPoint(startLocalPos);
                Quaternion currentStartWorldRot = transform.rotation * startLocalRot;

                // Destination evaluated dynamically from ship root home pose
                Vector3 targetWorldPos = transform.TransformPoint(_homeLocalPos);
                Quaternion targetWorldRot = transform.rotation * _homeLocalRot;

                _camTop.transform.position = Vector3.Lerp(currentStartWorldPos, targetWorldPos, t);
                _camTop.transform.rotation = Quaternion.Slerp(currentStartWorldRot, targetWorldRot, t);
                _camTop.fieldOfView = Mathf.Lerp(startFov, _homeFov, t);
                _camTop.nearClipPlane = Mathf.Lerp(startNearClip, _homeNearClip, t);

                // Smoothly interpolate fog density and fade in HUD
                if (manageFog)
                {
                    RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);
                }
                UpdateHUDTransition(t, showing: true);

                yield return null;
            }

            // Restore exact home local transform
            _camTop.transform.localPosition = _homeLocalPos;
            _camTop.transform.localRotation = _homeLocalRot;
            _camTop.fieldOfView = _homeFov;
            _camTop.nearClipPlane = _homeNearClip;

            // Ensure Canvas worldCamera remains cam_top
            UpdateCanvasCameras(_camTop);

            // Finalize HUD shown state and fog density
            SetHUDVisibilityImmediate(true);
            if (manageFog)
            {
                SetFogDensityImmediate(targetFogDensity);
            }

            // Deactivate briefing animator to conserve CPU cycles during combat
            if (_briefingAnimator != null)
            {
                _briefingAnimator.SetActive(false);
            }
        }

        if (_player != null)
        {
            _player._player_cam = _camTop;
        }

        _currentMode = targetMode;
        _isTransitioning = false;
        _activeTransitionRoutine = null;

        OnTransitionCompleted?.Invoke(_currentMode);
        OnCameraModeChanged?.Invoke(_currentMode);
    }

    private void PlayBriefingAnimation()
    {
        if (_briefingDotweenAnim != null)
        {
            _briefingDotweenAnim.isIndependentUpdate = true;
            _briefingDotweenAnim.autoKill = false;
            _briefingDotweenAnim.DOPlay();
            if (_briefingDotweenAnim.tween != null)
            {
                _briefingDotweenAnim.tween.SetUpdate(true);
            }
        }
    }

    #endregion
}

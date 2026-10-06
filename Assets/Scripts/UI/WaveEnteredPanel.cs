using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Displays the WaveEnteredPanel card and countdown during the Countdown phase of a wave.
/// Displays wave title, number, target objective, and real-time countdown seconds.
/// Automatically fades out when combat begins.
/// </summary>
public class WaveEnteredPanel : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The main container GameObject for the wave entered notification.")]
    public GameObject panel;

    [Tooltip("Text field displaying the wave title/number.")]
    public TMP_Text titleText;

    [Tooltip("Text field displaying wave subtitle and countdown timer.")]
    public TMP_Text subtitleText;

    [Tooltip("CanvasGroup controlling fade-in and fade-out transitions.")]
    public CanvasGroup canvasGroup;

    [Header("Wave Manager Connection")]
    [Tooltip("Optional explicit reference to WaveManager. If null, resolves via WaveManager.Instance.")]
    public WaveManager waveManager;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.25f;
    public float fadeOutDuration = 0.2f;

    /// <summary>
    /// Resolves active WaveManager reference.
    /// </summary>
    public WaveManager ActiveWaveManager => waveManager != null ? waveManager : WaveManager.Instance;

    private int _currentWaveNumber = 1;
    private WaveDefinition _currentConfig;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void ResolveReferences()
    {
        if (panel == null)
        {
            Transform p = transform.Find("WaveEnteredPanel");
            if (p != null) panel = p.gameObject;
            else panel = gameObject;
        }

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
        }

        if (titleText == null && panel != null)
        {
            Transform t = panel.transform.Find("Card/WaveTitle");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }

        if (subtitleText == null && panel != null)
        {
            Transform s = panel.transform.Find("Card/Subtitle");
            if (s != null) subtitleText = s.GetComponent<TMP_Text>();
        }
    }

    public void Subscribe()
    {
        WaveManager wm = ActiveWaveManager;
        if (wm != null)
        {
            wm.OnStateChanged -= HandleStateChanged;
            wm.OnStateChanged += HandleStateChanged;
            wm.OnCountdownTick -= HandleCountdownTick;
            wm.OnCountdownTick += HandleCountdownTick;

            if (wm.State == WaveManager.WaveState.Countdown)
            {
                ShowPanel(wm.CurrentWaveIndex, wm.CurrentWaveConfig, wm.StateTimer);
            }
        }
    }

    public void Unsubscribe()
    {
        WaveManager wm = ActiveWaveManager;
        if (wm != null)
        {
            wm.OnStateChanged -= HandleStateChanged;
            wm.OnCountdownTick -= HandleCountdownTick;
        }
    }

    private void HandleStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
    {
        if (newState == WaveManager.WaveState.Countdown)
        {
            WaveManager wm = ActiveWaveManager;
            int waveNum = wm != null ? wm.CurrentWaveIndex : 1;
            WaveDefinition config = wm != null ? wm.CurrentWaveConfig : null;
            float countdownTime = wm != null ? wm.StateTimer : 3f;
            ShowPanel(waveNum, config, countdownTime);
        }
        else if (prevState == WaveManager.WaveState.Countdown && newState != WaveManager.WaveState.Countdown)
        {
            HidePanel();
        }
    }

    private void HandleCountdownTick(float remainingSeconds)
    {
        if (panel != null && panel.activeSelf)
        {
            UpdateCountdownDisplay(remainingSeconds);
        }
    }

    public void ShowPanel(int waveNumber, WaveDefinition config, float remainingSeconds)
    {
        if (panel == null) return;

        _currentWaveNumber = waveNumber;
        _currentConfig = config;

        // 1. Populate Title
        if (titleText != null)
        {
            string title = (config != null && !string.IsNullOrEmpty(config.waveTitle))
                ? config.waveTitle
                : $"WAVE {waveNumber}";
            titleText.SetText(title);
        }

        // 2. Populate Countdown
        UpdateCountdownDisplay(remainingSeconds);

        // 3. Show and Fade in
        panel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            if (fadeInDuration > 0f && Application.isPlaying)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
        }
    }

    public void UpdateCountdownDisplay(float remainingSeconds)
    {
        if (subtitleText == null) return;

        int seconds = Mathf.Max(1, Mathf.CeilToInt(remainingSeconds));
        string targetText = (_currentConfig != null && _currentConfig.targetXPGoal > 0)
            ? $"OBJECTIVE: {_currentConfig.targetXPGoal} XP\n"
            : "";

        subtitleText.SetText($"{targetText}STARTING IN {seconds}...");
    }

    public void HidePanel()
    {
        if (panel == null || !panel.activeSelf) return;

        if (canvasGroup != null && fadeOutDuration > 0f && Application.isPlaying)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true).OnComplete(() =>
            {
                if (panel != null) panel.SetActive(false);
            });
        }
        else
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 0f;
            }
            panel.SetActive(false);
        }
    }
}

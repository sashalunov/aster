using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private player _player;
    [SerializeField] private PlayerProgression _progression;

    [Header("Progression UI")]
    public TMP_Text playerNameText;
    public TMP_Text xpText;
    public TMP_Text xpNextText;
    public TMP_Text waveText;
    public TMP_Text timerText;
    public Image xpProgress;
    public Image xpProgressFill;

    [SerializeField] private DOTweenAnimation shieldBarAnim;
    [SerializeField] private DOTweenAnimation healthBarAnim;

    [Header("Weapon UI")]
    public TMP_Text wpnPowerText;
    public TMP_Text wpnRateText;
    public TMP_Text wpnDamageText;

    [Header("Shield UI")]
    public TMP_Text shieldText;
    public Image shieldProgress;

    [Header("Health UI")]
    public TMP_Text healthText;
    public Image healthProgress;

    [Header("Upgrade Points UI")]
    public TMP_Text upgradePointsText;

    [Header("Wave Status UI")]
    [Tooltip("The main container GameObject for the wave status notification.")]
    public GameObject waveStatusPanel;

    [Tooltip("Text field displaying the wave title/number or cleared title.")]
    public TMP_Text waveStatusTitleText;

    [Tooltip("Text field displaying wave subtitle, objective, and countdown or cleared status.")]
    public TMP_Text waveStatusSubtitleText;

    [Tooltip("CanvasGroup controlling fade-in and fade-out transitions.")]
    public CanvasGroup waveStatusCanvasGroup;

    [Header("Wave Status Animation Settings")]
    public float waveStatusFadeInDuration = 0.25f;
    public float waveStatusFadeOutDuration = 0.2f;
    public float waveStatusClearedDisplayDuration = 2.5f;

    [Header("Wave Manager Connection")]
    [SerializeField] private WaveManager _waveManager;
    public WaveManager ActiveWaveManager => _waveManager != null ? _waveManager : WaveManager.Instance;

    private bool _isSubscribed = false;
    private bool _isWaveManagerSubscribed = false;
    private int _currentWaveNumber = 1;
    private WaveDefinition _currentWaveConfig;
    private Coroutine _waveClearedRoutine;
    private float _lastShield = -1f;
    private float _lastHealth = -1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        if (waveStatusPanel != null && (ActiveWaveManager == null || (ActiveWaveManager.State != WaveManager.WaveState.Countdown && ActiveWaveManager.State != WaveManager.WaveState.WaveCleared)))
        {
            waveStatusPanel.SetActive(false);
        }
        Subscribe();
        RefreshAllDisplays();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshAllDisplays();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (waveStatusCanvasGroup != null)
        {
            waveStatusCanvasGroup.DOKill();
        }
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            ResolveReferences();
            if (_player != null)
            {
                Subscribe();
                RefreshAllDisplays();
            }
        }
        else if (!_isWaveManagerSubscribed && WaveManager.Instance != null)
        {
            SubscribeWaveManager();
        }
    }

    public void ResolveReferences()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
        }
        if (_player != null)
        {
            PlayerProgression activeProg = _player.Progression ?? _player.GetComponent<PlayerProgression>();
            if (_progression != activeProg)
            {
                if (_isSubscribed && _progression != null)
                {
                    _progression.OnXPChanged -= UpdateXPDisplay;
                    _progression.OnWaveCompleted -= HandleProgressionWaveCompleted;
                }
                _progression = activeProg;
                if (_isSubscribed && _progression != null)
                {
                    _progression.OnXPChanged += UpdateXPDisplay;
                    _progression.OnWaveCompleted += HandleProgressionWaveCompleted;
                }
            }
        }

        if (waveStatusPanel == null)
        {
            Transform ws = transform.Find("HUD/WaveStatus");
            if (ws == null) ws = transform.Find("WaveStatus");
            if (ws != null)
            {
                Transform p = ws.Find("WaveStatusPanel");
                if (p == null) p = ws.Find("WaveStatusdPanel");
                if (p == null) p = ws.Find("WaveEnteredPanel");
                if (p == null && ws.childCount > 0) p = ws.GetChild(0);

                if (p != null) waveStatusPanel = p.gameObject;
                else waveStatusPanel = ws.gameObject;
            }
        }

        if (waveStatusCanvasGroup == null && waveStatusPanel != null)
        {
            waveStatusCanvasGroup = waveStatusPanel.GetComponent<CanvasGroup>();
        }

        if (waveStatusTitleText == null && waveStatusPanel != null)
        {
            Transform t = waveStatusPanel.transform.Find("Card/WaveTitle");
            if (t == null) t = waveStatusPanel.transform.Find("WaveTitle");
            if (t != null) waveStatusTitleText = t.GetComponent<TMP_Text>();
        }

        if (waveStatusSubtitleText == null && waveStatusPanel != null)
        {
            Transform s = waveStatusPanel.transform.Find("Card/Subtitle");
            if (s == null) s = waveStatusPanel.transform.Find("Subtitle");
            if (s != null) waveStatusSubtitleText = s.GetComponent<TMP_Text>();
        }

        Transform weaponStats = transform.Find("HUD/weapon_stats");
        if (weaponStats == null) weaponStats = transform.Find("weapon_stats");
        if (weaponStats == null)
        {
            GameObject wsGo = GameObject.Find("weapon_stats");
            if (wsGo != null) weaponStats = wsGo.transform;
        }

        if (weaponStats != null)
        {
            if (upgradePointsText == null)
            {
                Transform t = weaponStats.Find("upgrade_points");
                if (t != null) upgradePointsText = t.GetComponent<TMP_Text>();
            }
            if (wpnPowerText == null)
            {
                Transform t = weaponStats.Find("gun_force");
                if (t != null) wpnPowerText = t.GetComponent<TMP_Text>();
            }
            if (wpnRateText == null)
            {
                Transform t = weaponStats.Find("gun_rate");
                if (t != null) wpnRateText = t.GetComponent<TMP_Text>();
            }
            if (wpnDamageText == null)
            {
                Transform t = weaponStats.Find("gun_damage");
                if (t != null) wpnDamageText = t.GetComponent<TMP_Text>();
            }
        }

        if (_waveManager == null)
        {
            _waveManager = WaveManager.Instance;
        }
    }

    public void Subscribe()
    {
        if (_isSubscribed) return;

        PlayerProfile.OnPlayerNameChanged += UpdatePlayerNameDisplay;

        if (_progression != null)
        {
            _progression.OnXPChanged += UpdateXPDisplay;
            _progression.OnWaveCompleted += HandleProgressionWaveCompleted;
        }
        if (_player != null)
        {
            _player.OnWeaponStatsChanged += UpdateWeaponDisplay;
            _player.OnShieldChanged += UpdateShieldDisplay;
            _player.OnHealthChanged += UpdateHealthDisplay;
            _player.OnUpgradePointsChanged += UpdateUpgradePointsDisplay;
            _isSubscribed = true;
        }

        SubscribeWaveManager();
    }

    public void SubscribeWaveManager()
    {
        WaveManager wm = ActiveWaveManager;
        if (wm != null && !_isWaveManagerSubscribed)
        {
            wm.OnStateChanged -= HandleWaveStateChanged;
            wm.OnStateChanged += HandleWaveStateChanged;
            wm.OnCountdownTick -= HandleWaveCountdownTick;
            wm.OnCountdownTick += HandleWaveCountdownTick;
            wm.OnWaveCompleted -= HandleWaveCompleted;
            wm.OnWaveCompleted += HandleWaveCompleted;
            _isWaveManagerSubscribed = true;

            if (wm.State == WaveManager.WaveState.Countdown)
            {
                ShowWaveEnteredStatus(wm.CurrentWaveIndex, wm.CurrentWaveConfig, wm.StateTimer);
            }
            else if (wm.State == WaveManager.WaveState.WaveCleared)
            {
                ShowWaveClearedStatus(wm.CurrentWaveIndex, wm.CurrentWaveConfig);
            }
        }
    }

    public void Unsubscribe()
    {
        if (_isSubscribed)
        {
            PlayerProfile.OnPlayerNameChanged -= UpdatePlayerNameDisplay;

            if (_progression != null)
            {
                _progression.OnXPChanged -= UpdateXPDisplay;
                _progression.OnWaveCompleted -= HandleProgressionWaveCompleted;
            }
            if (_player != null)
            {
                _player.OnWeaponStatsChanged -= UpdateWeaponDisplay;
                _player.OnShieldChanged -= UpdateShieldDisplay;
                _player.OnHealthChanged -= UpdateHealthDisplay;
                _player.OnUpgradePointsChanged -= UpdateUpgradePointsDisplay;
            }
            _isSubscribed = false;
        }

        WaveManager wm = ActiveWaveManager;
        if (wm != null && _isWaveManagerSubscribed)
        {
            wm.OnStateChanged -= HandleWaveStateChanged;
            wm.OnCountdownTick -= HandleWaveCountdownTick;
            wm.OnWaveCompleted -= HandleWaveCompleted;
            _isWaveManagerSubscribed = false;
        }

        if (_waveClearedRoutine != null)
        {
            StopCoroutine(_waveClearedRoutine);
            _waveClearedRoutine = null;
        }
    }

    private void HandleWaveStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
    {
        if (newState == WaveManager.WaveState.Countdown)
        {
            WaveManager wm = ActiveWaveManager;
            int waveNum = wm != null ? wm.CurrentWaveIndex : 1;
            WaveDefinition config = wm != null ? wm.CurrentWaveConfig : null;
            float countdownTime = wm != null ? wm.StateTimer : 3f;
            ShowWaveEnteredStatus(waveNum, config, countdownTime);
        }
        else if (prevState == WaveManager.WaveState.Countdown && newState != WaveManager.WaveState.Countdown)
        {
            HideWaveStatusPanel();
        }
        else if (newState == WaveManager.WaveState.WaveCleared)
        {
            WaveManager wm = ActiveWaveManager;
            int waveNum = wm != null ? wm.CurrentWaveIndex : 1;
            WaveDefinition config = wm != null ? wm.CurrentWaveConfig : null;
            ShowWaveClearedStatus(waveNum, config);
        }
        else if (newState == WaveManager.WaveState.GameOver)
        {
            HideWaveStatusPanel();
        }
    }

    private void HandleWaveCountdownTick(float remainingSeconds)
    {
        if (waveStatusPanel != null && waveStatusPanel.activeSelf)
        {
            UpdateWaveCountdownDisplay(remainingSeconds);
        }
    }

    private void HandleWaveCompleted(int waveNumber, WaveDefinition config)
    {
        ShowWaveClearedStatus(waveNumber, config);
    }

    public void ShowWaveEnteredStatus(int waveNumber, WaveDefinition config, float remainingSeconds)
    {
        if (waveStatusPanel == null) return;

        if (_waveClearedRoutine != null)
        {
            StopCoroutine(_waveClearedRoutine);
            _waveClearedRoutine = null;
        }

        _currentWaveNumber = waveNumber;
        _currentWaveConfig = config;

        // 1. Populate Title
        if (waveStatusTitleText != null)
        {
            string title = (config != null && !string.IsNullOrEmpty(config.waveTitle))
                ? config.waveTitle
                : $"WAVE {waveNumber}";
            waveStatusTitleText.SetText(title);
        }

        // 2. Populate Countdown
        UpdateWaveCountdownDisplay(remainingSeconds);

        // 3. Show and Fade in
        waveStatusPanel.SetActive(true);
        if (waveStatusCanvasGroup != null)
        {
            waveStatusCanvasGroup.DOKill();
            if (waveStatusFadeInDuration > 0f && Application.isPlaying)
            {
                waveStatusCanvasGroup.alpha = 0f;
                waveStatusCanvasGroup.DOFade(1f, waveStatusFadeInDuration);
            }
            else
            {
                waveStatusCanvasGroup.alpha = 1f;
            }
        }
    }

    public void UpdateWaveCountdownDisplay(float remainingSeconds)
    {
        if (waveStatusSubtitleText == null) return;

        int seconds = Mathf.Max(1, Mathf.CeilToInt(remainingSeconds));
        string targetText = (_currentWaveConfig != null && _currentWaveConfig.targetXPGoal > 0)
            ? $"OBJECTIVE: {_currentWaveConfig.targetXPGoal} XP\n"
            : "";

        waveStatusSubtitleText.SetText($"{targetText}STARTING IN {seconds}...");
    }

    public void ShowWaveClearedStatus(int waveNumber, WaveDefinition config)
    {
        if (waveStatusPanel == null) return;

        if (_waveClearedRoutine != null)
        {
            StopCoroutine(_waveClearedRoutine);
            _waveClearedRoutine = null;
        }

        _currentWaveNumber = waveNumber;
        _currentWaveConfig = config;

        // 1. Populate Title
        if (waveStatusTitleText != null)
        {
            string title = (config != null && !string.IsNullOrEmpty(config.waveTitle))
                ? $"{config.waveTitle} CLEARED!"
                : $"WAVE {waveNumber} CLEARED!";
            waveStatusTitleText.SetText(title);
        }

        // 2. Populate Subtitle as Wave Finished
        if (waveStatusSubtitleText != null)
        {
            waveStatusSubtitleText.SetText("WAVE FINISHED");
        }

        // 3. Show and Fade in
        waveStatusPanel.SetActive(true);
        if (waveStatusCanvasGroup != null)
        {
            waveStatusCanvasGroup.DOKill();
            if (waveStatusFadeInDuration > 0f && Application.isPlaying)
            {
                waveStatusCanvasGroup.alpha = 0f;
                waveStatusCanvasGroup.DOFade(1f, waveStatusFadeInDuration);
            }
            else
            {
                waveStatusCanvasGroup.alpha = 1f;
            }
        }

        // 4. Auto-hide after duration if playing
        if (gameObject.activeInHierarchy && waveStatusClearedDisplayDuration > 0f && Application.isPlaying)
        {
            _waveClearedRoutine = StartCoroutine(DismissWaveClearedStatusAfterDelay(waveStatusClearedDisplayDuration));
        }
    }

    private IEnumerator DismissWaveClearedStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideWaveStatusPanel();
        _waveClearedRoutine = null;
    }

    public void HideWaveStatusPanel()
    {
        if (waveStatusPanel == null || !waveStatusPanel.activeSelf) return;

        if (_waveClearedRoutine != null)
        {
            StopCoroutine(_waveClearedRoutine);
            _waveClearedRoutine = null;
        }

        if (waveStatusCanvasGroup != null && waveStatusFadeOutDuration > 0f && Application.isPlaying)
        {
            waveStatusCanvasGroup.DOKill();
            waveStatusCanvasGroup.DOFade(0f, waveStatusFadeOutDuration).OnComplete(() =>
            {
                if (waveStatusPanel != null) waveStatusPanel.SetActive(false);
            });
        }
        else
        {
            if (waveStatusCanvasGroup != null)
            {
                waveStatusCanvasGroup.DOKill();
                waveStatusCanvasGroup.alpha = 0f;
            }
            if (waveStatusPanel != null) waveStatusPanel.SetActive(false);
        }
    }

    public void RefreshAllDisplays()
    {
        UpdatePlayerNameDisplay(PlayerProfile.PlayerName);

        if (_progression != null)
        {
            UpdateXPDisplay(_progression.CurrentXP, _progression.GetNextXPGoal());
            UpdateWaveDisplay(_progression.WaveLevel);
        }
        if (_player != null)
        {
            UpdateWeaponDisplay(_player._bullet_force, _player._fire_rate, _player._bullet_dmg);
            _lastShield = _player.shield_value;
            _lastHealth = _player.health_value;

            UpdateShieldDisplay(_player.shield_value, _player.shield_max_value);
            UpdateHealthDisplay(_player.health_value, _player.health_max_value);
            UpdateUpgradePointsDisplay(_player.UpgradePoints);
        }
    }

    public void UpdateUpgradePointsDisplay(int points)
    {
        if (upgradePointsText != null)
        {
            upgradePointsText.SetText("Points: " + points.ToString());
            upgradePointsText.gameObject.SetActive(true);
        }
    }

    public void UpdatePlayerNameDisplay(string name)
    {
        if (playerNameText != null)
        {
            playerNameText.SetText(name);
        }
    }

    public void UpdateXPDisplay(ulong currentXP, ulong nextGoalXP)
    {
        if (xpText != null) xpText.SetText("XP: " + currentXP);
        if (xpNextText != null) xpNextText.SetText("Next: " + nextGoalXP);
        if (waveText != null && _progression != null) waveText.SetText("Wave: " + _progression.WaveLevel);

        if (xpProgressFill != null)
        {
            xpProgressFill.fillAmount = _progression != null && _progression.GetNextXPGoal() > 0 ? Mathf.Clamp01((float)_progression.CurrentXP / (float)_progression.GetNextXPGoal()) : 0f;
            xpProgressFill.gameObject.SetActive(_progression != null && _progression.GetNextXPGoal() > 0);
        }
    }

    private void HandleProgressionWaveCompleted(int completedWaveLevel)
    {
        UpdateWaveDisplay(completedWaveLevel);

        WaveManager wm = ActiveWaveManager;
        // If WaveManager has not already entered WaveCleared or Intermission, ensure wave status displays the cleared state
        if (wm == null || (wm.State != WaveManager.WaveState.WaveCleared && wm.State != WaveManager.WaveState.Intermission))
        {
            WaveDefinition config = wm != null ? wm.CurrentWaveConfig : null;
            ShowWaveClearedStatus(completedWaveLevel, config);
        }
    }

    public void UpdateWaveDisplay(int wave)
    {
        if (waveText != null) waveText.SetText("Wave: " + wave);
    }

    public void UpdateWeaponDisplay(float power, float rate, float damage)
    {
        if (wpnPowerText != null) wpnPowerText.SetText("kinetic: " + power.ToString());
        if (wpnRateText != null) wpnRateText.SetText("rate: " + (rate > 0 ? (1f / rate).ToString("0.#") + "Hz" : "0Hz"));
        if (wpnDamageText != null) wpnDamageText.SetText("damage: " + damage.ToString());
    }

    public void UpdateShieldDisplay(float currentShield, float maxShield)
    {
        if (shieldText != null)
        {
            shieldText.SetText("SHIELD: " + Mathf.Max(0, (int)currentShield) + " / " + (int)maxShield);
        }
        if (shieldProgress != null && maxShield > 0)
        {
            shieldProgress.fillAmount = Mathf.Clamp01(currentShield / maxShield);
        }
        if (currentShield != _lastShield)
        {
            if (shieldBarAnim != null) shieldBarAnim.DORestart();
        }
    }

    public void UpdateHealthDisplay(float currentHealth, float maxHealth)
    {
        if (healthText != null)
        {
            healthText.SetText("HULL: " + Mathf.Max(0, (int)currentHealth) + " / " + (int)maxHealth);
        }
        if (healthProgress != null && maxHealth > 0)
        {
            float fill = Mathf.Clamp01(currentHealth / maxHealth);
            healthProgress.fillAmount = fill;

            // Dynamically tint color based on health level: green -> yellow -> red
            if (fill > 0.5f)
            {
                healthProgress.color = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.9f, 0.3f), (fill - 0.5f) * 2f);
            }
            else
            {
                healthProgress.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.8f, 0.2f), fill * 2f);
            }
        }
        if (currentHealth != _lastHealth)
        {
            if (healthBarAnim != null) healthBarAnim.DORestart();
        }
    }
}

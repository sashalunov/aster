using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Controls the Intermission popup interface shown during WaveManager.WaveState.Intermission.
/// Allows spending player upgrade points on weapon damage, impulse force, and fire rate,
/// and advances to the next wave on player confirmation.
/// </summary>
public class WaveIntermissionPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root panel GameObject of the intermission popup.")]
    public GameObject panel;

    [Tooltip("Main title text field displaying the wave/intermission header.")]
    public TMP_Text titleText;

    [Tooltip("Subtitle text field for additional wave or flavor info.")]
    public TMP_Text subtitleText;

    [Tooltip("Optional text field for bonus/stats debrief.")]
    public TMP_Text statsText;

    [Tooltip("Text field displaying player's available upgrade points.")]
    public TMP_Text pointsText;

    [Tooltip("CanvasGroup controlling fade animations.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Button used by the player to confirm and start the next wave.")]
    public Button startButton;

    [Header("Upgrade Buttons & Labels")]
    public Button damageUpgradeButton;
    public TMP_Text damageLabel;
    public Button forceUpgradeButton;
    public TMP_Text forceLabel;
    public Button rateUpgradeButton;
    public TMP_Text rateLabel;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.25f;
    public float fadeOutDuration = 0.2f;

    [Header("Target References")]
    [SerializeField] private player _player;
    [SerializeField] private WaveManager _waveManager;

    private bool _isSubscribed = false;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void Start()
    {
        ResolveReferences();
        BindButtons();

        if (panel != null && (WaveManager.Instance == null || WaveManager.Instance.State != WaveManager.WaveState.Intermission))
        {
            panel.SetActive(false);
        }

        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtons();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnbindButtons();
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
        }
    }

    private void Update()
    {
        if (_waveManager == null && WaveManager.Instance != null)
        {
            _waveManager = WaveManager.Instance;
            Subscribe();
        }
        if (_player == null)
        {
            ResolvePlayer();
        }
    }

    public void ResolveReferences()
    {
        if (panel == null)
        {
            Transform p = transform.Find("WaveIntermissionPanel");
            if (p != null) panel = p.gameObject;
            else panel = gameObject;
        }

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
        }

        if (titleText == null && panel != null)
        {
            Transform t = panel.transform.Find("Card/Title");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }

        if (subtitleText == null && panel != null)
        {
            Transform s = panel.transform.Find("Card/Subtitle");
            if (s != null) subtitleText = s.GetComponent<TMP_Text>();
        }

        if (pointsText == null && panel != null)
        {
            Transform pt = panel.transform.Find("Card/PointsText");
            if (pt != null) pointsText = pt.GetComponent<TMP_Text>();
        }

        if (startButton == null && panel != null)
        {
            Transform sb = panel.transform.Find("Card/StartButton");
            if (sb != null) startButton = sb.GetComponent<Button>();
            else startButton = panel.GetComponentInChildren<Button>(true);
        }

        if (damageUpgradeButton == null && panel != null)
        {
            Transform b = panel.transform.Find("Card/UpgradesContainer/Row_Damage/Button");
            if (b != null) damageUpgradeButton = b.GetComponent<Button>();
        }

        if (damageLabel == null && panel != null)
        {
            Transform l = panel.transform.Find("Card/UpgradesContainer/Row_Damage/Label");
            if (l != null) damageLabel = l.GetComponent<TMP_Text>();
        }

        if (forceUpgradeButton == null && panel != null)
        {
            Transform b = panel.transform.Find("Card/UpgradesContainer/Row_Force/Button");
            if (b != null) forceUpgradeButton = b.GetComponent<Button>();
        }

        if (forceLabel == null && panel != null)
        {
            Transform l = panel.transform.Find("Card/UpgradesContainer/Row_Force/Label");
            if (l != null) forceLabel = l.GetComponent<TMP_Text>();
        }

        if (rateUpgradeButton == null && panel != null)
        {
            Transform b = panel.transform.Find("Card/UpgradesContainer/Row_Rate/Button");
            if (b != null) rateUpgradeButton = b.GetComponent<Button>();
        }

        if (rateLabel == null && panel != null)
        {
            Transform l = panel.transform.Find("Card/UpgradesContainer/Row_Rate/Label");
            if (l != null) rateLabel = l.GetComponent<TMP_Text>();
        }

        ResolvePlayer();

        if (_waveManager == null)
        {
            _waveManager = WaveManager.Instance;
        }
    }

    private void ResolvePlayer()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
            if (_player != null && _isSubscribed)
            {
                _player.OnUpgradePointsChanged -= HandleUpgradePointsChanged;
                _player.OnUpgradePointsChanged += HandleUpgradePointsChanged;
                _player.OnWeaponStatsChanged -= HandleWeaponStatsChanged;
                _player.OnWeaponStatsChanged += HandleWeaponStatsChanged;
            }
        }
    }

    private void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
            startButton.onClick.AddListener(HandleStartButtonClicked);
        }

        if (damageUpgradeButton != null)
        {
            damageUpgradeButton.onClick.RemoveListener(HandleDamageUpgradeClicked);
            damageUpgradeButton.onClick.AddListener(HandleDamageUpgradeClicked);
        }

        if (forceUpgradeButton != null)
        {
            forceUpgradeButton.onClick.RemoveListener(HandleForceUpgradeClicked);
            forceUpgradeButton.onClick.AddListener(HandleForceUpgradeClicked);
        }

        if (rateUpgradeButton != null)
        {
            rateUpgradeButton.onClick.RemoveListener(HandleRateUpgradeClicked);
            rateUpgradeButton.onClick.AddListener(HandleRateUpgradeClicked);
        }
    }

    private void UnbindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonClicked);
        }
        if (damageUpgradeButton != null)
        {
            damageUpgradeButton.onClick.RemoveListener(HandleDamageUpgradeClicked);
        }
        if (forceUpgradeButton != null)
        {
            forceUpgradeButton.onClick.RemoveListener(HandleForceUpgradeClicked);
        }
        if (rateUpgradeButton != null)
        {
            rateUpgradeButton.onClick.RemoveListener(HandleRateUpgradeClicked);
        }
    }

    public void Subscribe()
    {
        if (_isSubscribed) return;

        WaveManager wm = _waveManager != null ? _waveManager : WaveManager.Instance;
        if (wm != null)
        {
            wm.OnStateChanged -= HandleStateChanged;
            wm.OnStateChanged += HandleStateChanged;

            if (wm.State == WaveManager.WaveState.Intermission)
            {
                ShowPopup(wm.CurrentWaveIndex, wm.CurrentWaveConfig);
            }
        }

        ResolvePlayer();
        if (_player != null)
        {
            _player.OnUpgradePointsChanged -= HandleUpgradePointsChanged;
            _player.OnUpgradePointsChanged += HandleUpgradePointsChanged;
            _player.OnWeaponStatsChanged -= HandleWeaponStatsChanged;
            _player.OnWeaponStatsChanged += HandleWeaponStatsChanged;
        }

        _isSubscribed = true;
    }

    public void Unsubscribe()
    {
        if (!_isSubscribed) return;

        WaveManager wm = _waveManager != null ? _waveManager : WaveManager.Instance;
        if (wm != null)
        {
            wm.OnStateChanged -= HandleStateChanged;
        }

        if (_player != null)
        {
            _player.OnUpgradePointsChanged -= HandleUpgradePointsChanged;
            _player.OnWeaponStatsChanged -= HandleWeaponStatsChanged;
        }

        _isSubscribed = false;
    }

    private void HandleStateChanged(WaveManager.WaveState prevState, WaveManager.WaveState newState)
    {
        // Wire to WaveManager.WaveState.Intermission
        if (newState == WaveManager.WaveState.Intermission)
        {
            WaveManager wm = _waveManager != null ? _waveManager : WaveManager.Instance;
            int waveNum = wm != null ? wm.CurrentWaveIndex : 1;
            WaveDefinition config = wm != null ? wm.CurrentWaveConfig : null;
            ShowPopup(waveNum, config);
        }
        else if (prevState == WaveManager.WaveState.Intermission && newState != WaveManager.WaveState.Intermission)
        {
            ClosePopup();
        }
    }

    private void HandleUpgradePointsChanged(int points)
    {
        RefreshUpgradeUI();
    }

    private void HandleWeaponStatsChanged(float power, float rate, float damage)
    {
        RefreshUpgradeUI();
    }

    private void HandleStartButtonClicked()
    {
        WaveManager wm = _waveManager != null ? _waveManager : WaveManager.Instance;
        if (wm != null)
        {
            wm.StartNextWave();
        }
    }

    private void HandleDamageUpgradeClicked()
    {
        ResolvePlayer();
        if (_player != null && _player.UpgradeDamageWithPoints(1))
        {
            RefreshUpgradeUI();
        }
    }

    private void HandleForceUpgradeClicked()
    {
        ResolvePlayer();
        if (_player != null && _player.UpgradeForceWithPoints(1))
        {
            RefreshUpgradeUI();
        }
    }

    private void HandleRateUpgradeClicked()
    {
        ResolvePlayer();
        if (_player != null && _player.UpgradeFireRateWithPoints(1))
        {
            RefreshUpgradeUI();
        }
    }

    public void RefreshUpgradeUI()
    {
        ResolvePlayer();
        int pts = _player != null ? _player.UpgradePoints : 0;
        bool hasPoints = pts > 0;

        if (pointsText != null)
        {
            if (hasPoints)
            {
                pointsText.SetText($"<color=#00FFAA>UPGRADE POINTS: {pts}</color>");
            }
            else
            {
                pointsText.SetText("<color=#888888>UPGRADE POINTS: 0</color>");
            }
        }

        if (damageUpgradeButton != null) damageUpgradeButton.interactable = hasPoints;
        if (forceUpgradeButton != null) forceUpgradeButton.interactable = hasPoints;
        if (rateUpgradeButton != null) rateUpgradeButton.interactable = hasPoints;

        if (_player != null)
        {
            if (damageLabel != null)
            {
                damageLabel.SetText($"DAMAGE: {_player._bullet_dmg:0.#} <color=#00FFAA>(+1.0)</color>");
            }
            if (forceLabel != null)
            {
                forceLabel.SetText($"FORCE: {_player._bullet_force:0.#} <color=#00FFAA>(+2.0)</color>");
            }
            if (rateLabel != null)
            {
                rateLabel.SetText($"RATE: {_player._fire_hz:0.#} Hz <color=#00FFAA>(+0.5 Hz)</color>");
            }
        }
    }

    public void ShowPopup(int waveNumber, WaveDefinition config)
    {
        if (panel == null) return;

        ResolveReferences();
        BindButtons();
        RefreshUpgradeUI();

        // 1. Populate Text
        if (titleText != null)
        {
            titleText.SetText($"WAVE {waveNumber} INTERMISSION");
        }

        if (subtitleText != null)
        {
            string sub = (config != null && !string.IsNullOrEmpty(config.waveTitle))
                ? config.waveTitle
                : "TACTICAL ASTEROID DEFENSE";
            subtitleText.SetText(sub);
        }

        if (statsText != null)
        {
            int xpReward = config != null ? config.rewardXP : 0;
            int credReward = config != null ? config.rewardCredits : 0;
            statsText.SetText($"+{xpReward} XP   +{credReward} Credits");
        }

        // 2. Open Panel with Fade Animation
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

    public void ClosePopup()
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
            if (panel != null) panel.SetActive(false);
        }
    }
}

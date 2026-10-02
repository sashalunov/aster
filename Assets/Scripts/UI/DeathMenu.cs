using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays post-death mission debrief and provides roguelite meta-progression
/// hangar upgrades (Hull, Shield, Thrusters, Weapons) before redeploying.
/// </summary>
public class DeathMenu : MonoBehaviour
{
    [Header("Menu Panels")]
    [Tooltip("Root panel of the death menu.")]
    public GameObject deathMenuPanel;

    [Header("Dependencies")]
    public player _player;
    public MainMenu _mainMenu;

    [Header("Debrief Stats UI")]
    public TMP_Text titleText;
    public TMP_Text runTimeText;
    public TMP_Text wavesClearedText;
    public TMP_Text scrapEarnedText;
    public TMP_Text totalScrapText;

    [Header("Meta Upgrade Elements")]
    public TMP_Text hullLevelText;
    public TMP_Text hullCostText;
    public Button hullUpgradeButton;

    public TMP_Text shieldLevelText;
    public TMP_Text shieldCostText;
    public Button shieldUpgradeButton;

    public TMP_Text thrusterLevelText;
    public TMP_Text thrusterCostText;
    public Button thrusterUpgradeButton;

    public TMP_Text weaponLevelText;
    public TMP_Text weaponCostText;
    public Button weaponUpgradeButton;

    [Header("Navigation Buttons")]
    public Button deployAgainButton;
    public Button mainMenuButton;
    public Button exitButton;

    [Header("Timing")]
    [Tooltip("Delay in real seconds after player death before showing the death menu.")]
    public float delayBeforeShow = 1.0f;

    public bool IsOpen { get; private set; }
    public int LastRunScrapEarned { get; private set; }
    public float LastRunDuration { get; private set; }

    public event Action OnDeathMenuOpened;
    public event Action OnDeathMenuClosed;

    private Coroutine _showRoutine;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToPlayer();

        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayer();
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartRun();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                ReturnToMainMenu();
            }
        }
    }

    public void ResolveReferences()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
        }
        if (_mainMenu == null)
        {
            _mainMenu = FindAnyObjectByType<MainMenu>();
        }
    }

    public void SubscribeToPlayer()
    {
        if (_player != null)
        {
            _player.OnDeath -= HandlePlayerDeath;
            _player.OnDeath += HandlePlayerDeath;
        }
    }

    public void UnsubscribeFromPlayer()
    {
        if (_player != null)
        {
            _player.OnDeath -= HandlePlayerDeath;
        }
    }

    public void BindButtons()
    {
        if (deployAgainButton != null)
        {
            deployAgainButton.onClick.RemoveListener(RestartRun);
            deployAgainButton.onClick.AddListener(RestartRun);
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
            exitButton.onClick.AddListener(ExitGame);
        }

        if (hullUpgradeButton != null)
        {
            hullUpgradeButton.onClick.RemoveListener(BuyHullUpgrade);
            hullUpgradeButton.onClick.AddListener(BuyHullUpgrade);
        }
        if (shieldUpgradeButton != null)
        {
            shieldUpgradeButton.onClick.RemoveListener(BuyShieldUpgrade);
            shieldUpgradeButton.onClick.AddListener(BuyShieldUpgrade);
        }
        if (thrusterUpgradeButton != null)
        {
            thrusterUpgradeButton.onClick.RemoveListener(BuyThrusterUpgrade);
            thrusterUpgradeButton.onClick.AddListener(BuyThrusterUpgrade);
        }
        if (weaponUpgradeButton != null)
        {
            weaponUpgradeButton.onClick.RemoveListener(BuyWeaponUpgrade);
            weaponUpgradeButton.onClick.AddListener(BuyWeaponUpgrade);
        }
    }

    public void HandlePlayerDeath()
    {
        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowDeathMenuRoutine());
    }

    private IEnumerator ShowDeathMenuRoutine()
    {
        if (delayBeforeShow > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeShow);
        }

        OpenDeathMenu();
    }

    public void OpenDeathMenu()
    {
        IsOpen = true;

        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(true);
        }

        // Calculate run metrics
        LastRunDuration = Time.timeSinceLevelLoad;
        int waves = _player != null ? _player._wavelvl : 0;
        ulong runXP = _player != null ? _player._xp_value : 0;
        ulong runCreds = _player != null ? _player._cred_value : 0;

        // Base reward formula: XP + 5*Creds + 25*Waves + flat 20 participation scrap
        LastRunScrapEarned = (int)(runXP + (runCreds * 5) + (ulong)(waves * 25) + 20);
        PlayerMetaProgression.AddScrap(LastRunScrapEarned);

        UpdateDebriefDisplay(LastRunDuration, waves, LastRunScrapEarned);
        RefreshAllUpgrades();

        Time.timeScale = 0f;
        OnDeathMenuOpened?.Invoke();
    }

    public void UpdateDebriefDisplay(float duration, int waves, int earnedScrap)
    {
        int minutes = Mathf.FloorToInt(duration / 60f);
        int seconds = Mathf.FloorToInt(duration % 60f);

        if (runTimeText != null)
        {
            runTimeText.SetText(string.Format("SURVIVAL TIME: {0:00}:{1:00}", minutes, seconds));
        }
        if (wavesClearedText != null)
        {
            wavesClearedText.SetText("WAVE REACHED: " + waves);
        }
        if (scrapEarnedText != null)
        {
            scrapEarnedText.SetText(string.Format("SCRAP SALVAGED: +{0}", earnedScrap));
        }
        if (totalScrapText != null)
        {
            totalScrapText.SetText("BANKED SCRAP: " + PlayerMetaProgression.BankedScrap);
        }
    }

    public void RefreshAllUpgrades()
    {
        RefreshUpgradeRow(MetaUpgradeType.HullArmor, hullLevelText, hullCostText, hullUpgradeButton, "+2 HULL");
        RefreshUpgradeRow(MetaUpgradeType.ShieldCapacitor, shieldLevelText, shieldCostText, shieldUpgradeButton, "+2 SHIELD");
        RefreshUpgradeRow(MetaUpgradeType.IonThrusters, thrusterLevelText, thrusterCostText, thrusterUpgradeButton, "+15% THRUST");
        RefreshUpgradeRow(MetaUpgradeType.PlasmaCannons, weaponLevelText, weaponCostText, weaponUpgradeButton, "+0.5 DAMAGE");

        if (totalScrapText != null)
        {
            totalScrapText.SetText("BANKED SCRAP: " + PlayerMetaProgression.BankedScrap);
        }
    }

    private void RefreshUpgradeRow(MetaUpgradeType type, TMP_Text levelText, TMP_Text costText, Button btn, string bonusDesc)
    {
        int level = PlayerMetaProgression.GetUpgradeLevel(type);
        int cost = PlayerMetaProgression.GetUpgradeCost(type);
        bool isMax = level >= PlayerMetaProgression.MAX_UPGRADE_LEVEL;

        if (levelText != null)
        {
            levelText.SetText(isMax ? string.Format("LVL {0} (MAX) [{1}]", level, bonusDesc) : string.Format("LVL {0} [{1}]", level, bonusDesc));
        }

        if (costText != null)
        {
            costText.SetText(isMax ? "MAXED" : string.Format("COST: {0} SCRAP", cost));
        }

        if (btn != null)
        {
            btn.interactable = !isMax && PlayerMetaProgression.CanAffordUpgrade(type);
        }
    }

    public void BuyHullUpgrade()
    {
        if (PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.HullArmor))
        {
            PlayUpgradeFeedback();
            RefreshAllUpgrades();
        }
    }

    public void BuyShieldUpgrade()
    {
        if (PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.ShieldCapacitor))
        {
            PlayUpgradeFeedback();
            RefreshAllUpgrades();
        }
    }

    public void BuyThrusterUpgrade()
    {
        if (PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.IonThrusters))
        {
            PlayUpgradeFeedback();
            RefreshAllUpgrades();
        }
    }

    public void BuyWeaponUpgrade()
    {
        if (PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.PlasmaCannons))
        {
            PlayUpgradeFeedback();
            RefreshAllUpgrades();
        }
    }

    private void PlayUpgradeFeedback()
    {
        if (_player != null && _player._clip_lvlup != null)
        {
            _player._clip_lvlup.Play();
        }
    }

    public void RestartRun()
    {
        Time.timeScale = 1f;
        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(activeScene))
        {
            SceneManager.LoadScene(activeScene);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (deathMenuPanel != null)
        {
            deathMenuPanel.SetActive(false);
        }
        IsOpen = false;
        OnDeathMenuClosed?.Invoke();

        if (_mainMenu != null)
        {
            _mainMenu.OpenMainMenu();
        }
        else
        {
            RestartRun();
        }
    }

    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

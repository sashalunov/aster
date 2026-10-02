using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeathMenuTests
{
    private GameObject _holder;
    private DeathMenu _deathMenu;
    private GameObject _panel;
    private GameObject _playerObj;
    private player _playerComp;
    private GameObject _mainMenuObj;
    private MainMenu _mainMenuComp;

    [SetUp]
    public void SetUp()
    {
        PlayerMetaProgression.ResetAllProgress();

        _holder = new GameObject("DeathMenuHolder");
        _deathMenu = _holder.AddComponent<DeathMenu>();

        _panel = new GameObject("DeathPanel");
        _panel.transform.SetParent(_holder.transform);
        _panel.SetActive(false);
        _deathMenu.deathMenuPanel = _panel;

        _playerObj = new GameObject("PlayerShip");
        _playerComp = _playerObj.AddComponent<player>();
        _playerComp.health_value = 10f;
        _playerComp.health_max_value = 10f;
        _playerComp.shield_value = 10f;
        _playerComp.shield_max_value = 10f;
        _deathMenu._player = _playerComp;

        _mainMenuObj = new GameObject("MainMenuObj");
        _mainMenuComp = _mainMenuObj.AddComponent<MainMenu>();
        _deathMenu._mainMenu = _mainMenuComp;

        // UI text elements
        _deathMenu.runTimeText = new GameObject("RunTime").AddComponent<TextMeshProUGUI>();
        _deathMenu.wavesClearedText = new GameObject("Waves").AddComponent<TextMeshProUGUI>();
        _deathMenu.scrapEarnedText = new GameObject("Scrap").AddComponent<TextMeshProUGUI>();
        _deathMenu.totalScrapText = new GameObject("TotalScrap").AddComponent<TextMeshProUGUI>();

        // Upgrade UI elements
        _deathMenu.hullLevelText = new GameObject("HullLvl").AddComponent<TextMeshProUGUI>();
        _deathMenu.hullCostText = new GameObject("HullCost").AddComponent<TextMeshProUGUI>();
        _deathMenu.hullUpgradeButton = new GameObject("HullBtn").AddComponent<Button>();

        _deathMenu.shieldLevelText = new GameObject("ShieldLvl").AddComponent<TextMeshProUGUI>();
        _deathMenu.shieldCostText = new GameObject("ShieldCost").AddComponent<TextMeshProUGUI>();
        _deathMenu.shieldUpgradeButton = new GameObject("ShieldBtn").AddComponent<Button>();

        _deathMenu.thrusterLevelText = new GameObject("ThrustLvl").AddComponent<TextMeshProUGUI>();
        _deathMenu.thrusterCostText = new GameObject("ThrustCost").AddComponent<TextMeshProUGUI>();
        _deathMenu.thrusterUpgradeButton = new GameObject("ThrustBtn").AddComponent<Button>();

        _deathMenu.weaponLevelText = new GameObject("WpnLvl").AddComponent<TextMeshProUGUI>();
        _deathMenu.weaponCostText = new GameObject("WpnCost").AddComponent<TextMeshProUGUI>();
        _deathMenu.weaponUpgradeButton = new GameObject("WpnBtn").AddComponent<Button>();

        _deathMenu.delayBeforeShow = 0f;
        _deathMenu.BindButtons();

        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerMetaProgression.ResetAllProgress();

        if (_holder != null) Object.DestroyImmediate(_holder);
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_mainMenuObj != null) Object.DestroyImmediate(_mainMenuObj);
    }

    [Test]
    public void MetaProgression_AddScrapAndPurchaseUpgrade_IncreasesLevelAndDeductsScrap()
    {
        PlayerMetaProgression.AddScrap(200);
        Assert.AreEqual(200, PlayerMetaProgression.BankedScrap);

        int initialCost = PlayerMetaProgression.GetUpgradeCost(MetaUpgradeType.HullArmor);
        Assert.AreEqual(50, initialCost);
        Assert.IsTrue(PlayerMetaProgression.CanAffordUpgrade(MetaUpgradeType.HullArmor));

        bool bought = PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.HullArmor);
        Assert.IsTrue(bought);
        Assert.AreEqual(1, PlayerMetaProgression.GetUpgradeLevel(MetaUpgradeType.HullArmor));
        Assert.AreEqual(150, PlayerMetaProgression.BankedScrap);

        // Next level cost increases
        Assert.AreEqual(100, PlayerMetaProgression.GetUpgradeCost(MetaUpgradeType.HullArmor));
    }

    [Test]
    public void MetaProgression_ApplyToPlayer_AppliesUpgradedStats()
    {
        PlayerMetaProgression.AddScrap(500);
        PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.HullArmor); // level 1 -> +2 hull
        PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.ShieldCapacitor); // level 1 -> +2 shield
        PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.IonThrusters); // level 1 -> +0.8 thrust
        PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.PlasmaCannons); // level 1 -> +0.5 dmg

        PlayerMetaProgression.ApplyTo(_playerComp);

        Assert.AreEqual(12f, _playerComp.health_max_value, 0.001f);
        Assert.AreEqual(12f, _playerComp.health_value, 0.001f);
        Assert.AreEqual(12f, _playerComp.shield_max_value, 0.001f);
        Assert.AreEqual(12f, _playerComp.shield_value, 0.001f);
        Assert.AreEqual(6.1f, _playerComp._thrust_force, 0.001f);
        Assert.AreEqual(1.5f, _playerComp._bullet_dmg, 0.001f);
    }

    [Test]
    public void DeathMenu_OpenDeathMenu_CalculatesScrapAndActivatesPanel()
    {
        _playerComp.Progression.AddXP(80);
        _deathMenu.OpenDeathMenu();

        Assert.IsTrue(_deathMenu.IsOpen);
        Assert.IsTrue(_panel.activeSelf);
        Assert.Greater(_deathMenu.LastRunScrapEarned, 80);
        Assert.AreEqual(_deathMenu.LastRunScrapEarned, PlayerMetaProgression.BankedScrap);
        Assert.AreEqual(0f, Time.timeScale, 0.001f, "Time should freeze when death menu opens");
    }

    [Test]
    public void DeathMenu_BuyUpgrade_UpdatesUIAndCurrency()
    {
        PlayerMetaProgression.AddScrap(100);
        _deathMenu.RefreshAllUpgrades();

        Assert.IsTrue(_deathMenu.hullUpgradeButton.interactable);

        _deathMenu.BuyHullUpgrade();

        Assert.AreEqual(1, PlayerMetaProgression.GetUpgradeLevel(MetaUpgradeType.HullArmor));
        Assert.AreEqual(50, PlayerMetaProgression.BankedScrap);
        StringAssert.Contains("LVL 1", _deathMenu.hullLevelText.text);
    }

    [Test]
    public void DeathMenu_ReturnToMainMenu_ClosesDeathMenuAndOpensMainMenu()
    {
        _deathMenu.OpenDeathMenu();
        Assert.IsTrue(_deathMenu.IsOpen);

        _deathMenu.ReturnToMainMenu();

        Assert.IsFalse(_deathMenu.IsOpen);
        Assert.IsFalse(_panel.activeSelf);
        Assert.IsTrue(_mainMenuComp.IsOpen);
    }
}

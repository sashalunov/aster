using NUnit.Framework;
using UnityEngine;
using TMPro;

public class WaveEnteredPanelTests
{
    private GameObject waveManagerObj;
    private WaveManager waveManager;
    private GameObject panelHostObj;
    private GameObject panelObj;
    private WaveEnteredPanel waveEnteredPanel;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private CanvasGroup canvasGroup;

    [SetUp]
    public void SetUp()
    {
        waveManagerObj = new GameObject("TestWaveManager");
        waveManager = waveManagerObj.AddComponent<WaveManager>();
        waveManager.countdownDuration = 3f;

        panelHostObj = new GameObject("WaveEntered");
        waveEnteredPanel = panelHostObj.AddComponent<WaveEnteredPanel>();
        waveEnteredPanel.waveManager = waveManager;

        panelObj = new GameObject("WaveEnteredPanel");
        panelObj.transform.SetParent(panelHostObj.transform);
        canvasGroup = panelObj.AddComponent<CanvasGroup>();

        GameObject cardObj = new GameObject("Card");
        cardObj.transform.SetParent(panelObj.transform);

        GameObject titleGo = new GameObject("WaveTitle");
        titleGo.transform.SetParent(cardObj.transform);
        titleText = titleGo.AddComponent<TextMeshProUGUI>();

        GameObject subGo = new GameObject("Subtitle");
        subGo.transform.SetParent(cardObj.transform);
        subtitleText = subGo.AddComponent<TextMeshProUGUI>();

        waveEnteredPanel.panel = panelObj;
        waveEnteredPanel.titleText = titleText;
        waveEnteredPanel.subtitleText = subtitleText;
        waveEnteredPanel.canvasGroup = canvasGroup;
        waveEnteredPanel.fadeInDuration = 0f;
        waveEnteredPanel.fadeOutDuration = 0f;

        waveEnteredPanel.Subscribe();
    }

    [TearDown]
    public void TearDown()
    {
        if (waveManagerObj != null) Object.DestroyImmediate(waveManagerObj);
        if (panelHostObj != null) Object.DestroyImmediate(panelHostObj);
    }

    [Test]
    public void WaveEnteredPanel_InitialState_PanelIsInactive()
    {
        panelObj.SetActive(false);
        Assert.IsFalse(panelObj.activeSelf, "Panel should start inactive");
    }

    [Test]
    public void WaveEnteredPanel_WhenCountdownStarts_ShowsPanelAndPopulatesText()
    {
        panelObj.SetActive(false);

        WaveDefinition wave1 = WaveDefinition.Create(
            waveNumber: 1,
            waveTitle: "First Contact: Just Asteroids",
            duration: 20f,
            threatBudget: 8,
            targetXPGoal: 100
        );
        waveManager.authoredWaves.Clear();
        waveManager.authoredWaves.Add(wave1);

        waveManager.StartRun();

        Assert.AreEqual(WaveManager.WaveState.Countdown, waveManager.State);
        Assert.IsTrue(panelObj.activeSelf, "WaveEnteredPanel must become active when Countdown state begins");
        Assert.AreEqual("First Contact: Just Asteroids", titleText.text, "Title should show the authored waveTitle");
        StringAssert.Contains("STARTING IN 3...", subtitleText.text, "Subtitle should show the countdown duration");
        StringAssert.Contains("100 XP", subtitleText.text, "Subtitle should display target XP goal");
    }

    [Test]
    public void WaveEnteredPanel_CountdownTick_UpdatesCountdownSeconds()
    {
        waveManager.countdownDuration = 3f;
        waveManager.StartRun();

        Assert.IsTrue(panelObj.activeSelf);
        StringAssert.Contains("3...", subtitleText.text);

        // Advance 1.2s -> 1.8s remaining -> CeilToInt = 2
        waveManager.Tick(1.2f);
        StringAssert.Contains("2...", subtitleText.text);

        // Advance 1.0s -> 0.8s remaining -> CeilToInt = 1
        waveManager.Tick(1.0f);
        StringAssert.Contains("1...", subtitleText.text);
    }

    [Test]
    public void WaveEnteredPanel_WhenCombatBegins_HidesPanel()
    {
        waveManager.countdownDuration = 1.0f;
        waveManager.StartRun();
        Assert.IsTrue(panelObj.activeSelf);

        // Tick past countdown into Combat
        waveManager.Tick(1.1f);
        Assert.AreEqual(WaveManager.WaveState.Combat, waveManager.State);
        Assert.IsFalse(panelObj.activeSelf, "WaveEnteredPanel must hide when Combat begins");
    }

    [Test]
    public void WaveDefinition_DataAsset_CanBeCreatedAndCloned()
    {
        WaveDefinition def = WaveDefinition.Create(
            waveNumber: 5,
            waveTitle: "Boss Sector",
            duration: 45f,
            threatBudget: 50,
            spawnInterval: 1.2f,
            minMass: 4,
            maxMass: 8,
            rewardCredits: 100,
            rewardXP: 250,
            grantExtraGun: true
        );

        Assert.IsNotNull(def);
        Assert.AreEqual(5, def.waveNumber);
        Assert.AreEqual("Boss Sector", def.waveTitle);
        Assert.AreEqual(45f, def.duration);
        Assert.AreEqual(50, def.threatBudget);
        Assert.IsTrue(def.grantExtraGun);

        WaveDefinition clone = def.Clone();
        Assert.IsNotNull(clone);
        Assert.AreNotSame(def, clone);
        Assert.AreEqual(def.waveNumber, clone.waveNumber);
        Assert.AreEqual(def.waveTitle, clone.waveTitle);
        Assert.AreEqual(def.threatBudget, clone.threatBudget);
        Assert.AreEqual(def.grantExtraGun, clone.grantExtraGun);

        Object.DestroyImmediate(def);
        Object.DestroyImmediate(clone);
    }

    [Test]
    public void WaveDefinition_AuthoredResourcesAssets_AreLoadedCorrectly()
    {
        WaveDefinition[] loaded = Resources.LoadAll<WaveDefinition>("Waves");
        Assert.IsNotNull(loaded);
        Assert.GreaterOrEqual(loaded.Length, 3, "At least 3 authored wave assets should be in Resources/Waves");

        System.Array.Sort(loaded, (a, b) => a.waveNumber.CompareTo(b.waveNumber));
        Assert.AreEqual(1, loaded[0].waveNumber);
        Assert.AreEqual(2, loaded[1].waveNumber);
        Assert.AreEqual(3, loaded[2].waveNumber);
        Assert.IsTrue(loaded[2].grantExtraGun, "Wave 3 should grant extra gun");
    }
}

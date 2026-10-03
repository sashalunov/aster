using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuTests
{
    private GameObject _menuHolder;
    private MainMenu _mainMenu;
    private GameObject _startPanel;
    private GameObject _pausePanel;
    private GameObject _playerObj;
    private player _playerComp;
    private GameObject _inputObj;
    private TMP_InputField _inputField;
    private GameObject _resetBtnObj;
    private Button _resetButton;

    [SetUp]
    public void SetUp()
    {
        PlayerMetaProgression.ResetAllProgress();
        PlayerProfile.ResetAllProfileData();

        _menuHolder = new GameObject("MainMenuHolder");
        _mainMenu = _menuHolder.AddComponent<MainMenu>();

        _startPanel = new GameObject("StartPanel");
        _startPanel.transform.SetParent(_menuHolder.transform);
        _mainMenu._menu_ = _startPanel;

        _pausePanel = new GameObject("PausePanel");
        _pausePanel.transform.SetParent(_menuHolder.transform);
        _pausePanel.SetActive(false);
        _mainMenu._menuRe_ = _pausePanel;

        _inputObj = new GameObject("InputName");
        _inputObj.transform.SetParent(_startPanel.transform);
        _inputField = _inputObj.AddComponent<TMP_InputField>();
        _mainMenu.nameInputField = _inputField;
        _mainMenu.BindNameInput();

        _resetBtnObj = new GameObject("ResetProgress");
        _resetBtnObj.transform.SetParent(_startPanel.transform);
        _resetButton = _resetBtnObj.AddComponent<Button>();
        _mainMenu.resetProgressButton = _resetButton;
        _mainMenu.BindButtons();

        _playerObj = new GameObject("PlayerShip");
        _playerComp = _playerObj.AddComponent<player>();
        _playerComp._can_play = true;
        _mainMenu._player = _playerComp;

        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerMetaProgression.ResetAllProgress();
        PlayerProfile.ResetAllProfileData();

        if (_menuHolder != null) Object.DestroyImmediate(_menuHolder);
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
    }

    [Test]
    public void MainMenu_OpenMainMenu_ShowsStartPanelAndPauses()
    {
        _mainMenu.pauseTimeWhenOpen = true;
        _mainMenu.OpenMainMenu();

        Assert.IsTrue(_mainMenu.IsOpen, "Menu should be marked as open");
        Assert.IsTrue(_startPanel.activeSelf, "Start panel should be active");
        Assert.IsFalse(_pausePanel.activeSelf, "Pause panel should be inactive");
        Assert.AreEqual(0f, Time.timeScale, 0.001f, "Time scale should be 0 when menu is open");
        Assert.IsFalse(_playerComp._can_play, "Player _can_play should be false when menu is open");
    }

    [Test]
    public void MainMenu_StartGame_ClosesPanelsAndResumes()
    {
        _mainMenu.OpenMainMenu();
        Assert.IsTrue(_mainMenu.IsOpen);

        _mainMenu.StartGame();

        Assert.IsFalse(_mainMenu.IsOpen, "Menu should be closed after StartGame");
        Assert.IsTrue(_mainMenu.HasGameStarted, "HasGameStarted should be true");
        Assert.IsFalse(_startPanel.activeSelf, "Start panel should be deactivated");
        Assert.IsFalse(_pausePanel.activeSelf, "Pause panel should be deactivated");
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time scale should be restored to 1");
        Assert.IsTrue(_playerComp._can_play, "Player _can_play should be true after start");
    }

    [Test]
    public void MainMenu_OpenPauseMenu_ShowsPausePanelAndPauses()
    {
        _mainMenu.StartGame();
        Assert.IsFalse(_mainMenu.IsOpen);

        _mainMenu.OpenPauseMenu();

        Assert.IsTrue(_mainMenu.IsOpen, "Menu should be marked as open");
        Assert.IsFalse(_startPanel.activeSelf, "Start panel should be inactive in pause menu");
        Assert.IsTrue(_pausePanel.activeSelf, "Pause panel should be active");
        Assert.AreEqual(0f, Time.timeScale, 0.001f, "Time scale should be 0 when paused");
        Assert.IsFalse(_playerComp._can_play, "Player _can_play should be false when paused");
    }

    [Test]
    public void MainMenu_ResumeGame_RestoresPlayState()
    {
        _mainMenu.StartGame();
        _mainMenu.OpenPauseMenu();
        Assert.IsTrue(_mainMenu.IsOpen);

        _mainMenu.ResumeGame();

        Assert.IsFalse(_mainMenu.IsOpen, "Menu should be closed after resume");
        Assert.IsFalse(_pausePanel.activeSelf, "Pause panel should be inactive");
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time scale should be restored");
        Assert.IsTrue(_playerComp._can_play, "Player _can_play should be true");
    }

    [Test]
    public void MainMenu_ToggleMenu_TogglesStateCorrectly()
    {
        // 1. Initial toggle when game has not started opens MainMenu
        _mainMenu.ToggleMenu();
        Assert.IsTrue(_mainMenu.IsOpen);
        Assert.IsTrue(_startPanel.activeSelf);

        // 2. Toggle again starts game
        _mainMenu.ToggleMenu();
        Assert.IsFalse(_mainMenu.IsOpen);
        Assert.IsTrue(_mainMenu.HasGameStarted);

        // 3. Toggle during gameplay opens pause menu
        _mainMenu.ToggleMenu();
        Assert.IsTrue(_mainMenu.IsOpen);
        Assert.IsTrue(_pausePanel.activeSelf);

        // 4. Toggle during pause resumes game
        _mainMenu.ToggleMenu();
        Assert.IsFalse(_mainMenu.IsOpen);
        Assert.IsTrue(_playerComp._can_play);
    }

    [Test]
    public void MainMenu_ToggleMenu_IgnoredWhenPlayerIsDead()
    {
        _mainMenu.StartGame();
        _playerComp.isDead = true;
        _playerComp._can_play = false;

        // Attempt to toggle
        _mainMenu.ToggleMenu();

        Assert.IsFalse(_mainMenu.IsOpen, "Toggle should be ignored when player is dead");
        Assert.IsFalse(_playerComp._can_play, "Player should remain unable to play when dead");
    }

    [Test]
    public void MainMenu_Events_FireOnStateTransitions()
    {
        bool openedFired = false;
        bool closedFired = false;
        bool startedFired = false;
        bool resumedFired = false;

        _mainMenu.OnMenuOpened += () => openedFired = true;
        _mainMenu.OnMenuClosed += () => closedFired = true;
        _mainMenu.OnGameStarted += () => startedFired = true;
        _mainMenu.OnGameResumed += () => resumedFired = true;

        _mainMenu.OpenMainMenu();
        Assert.IsTrue(openedFired, "OnMenuOpened should fire");

        _mainMenu.StartGame();
        Assert.IsTrue(startedFired, "OnGameStarted should fire");

        _mainMenu.OpenPauseMenu();
        _mainMenu.ResumeGame();
        Assert.IsTrue(resumedFired, "OnGameResumed should fire");
        Assert.IsTrue(closedFired, "OnMenuClosed should fire");
    }

    [Test]
    public void MainMenu_StartGame_SavesPlayerNameFromInputField()
    {
        _inputField.text = "Valkyrie_7";
        _mainMenu.StartGame();

        Assert.AreEqual("Valkyrie_7", PlayerProfile.PlayerName);
    }

    [Test]
    public void MainMenu_StartGame_WithEmptyInputField_UsesDefaultName()
    {
        _inputField.text = "   ";
        _mainMenu.StartGame();

        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.PlayerName);
    }

    [Test]
    public void MainMenu_OpenMainMenu_LoadsCurrentPlayerNameIntoInputField()
    {
        PlayerProfile.SetPlayerName("Falcon_09");

        _mainMenu.OpenMainMenu();

        Assert.AreEqual("Falcon_09", _inputField.text);
    }

    [Test]
    public void MainMenu_ResolveNameInput_FindsInputFieldInChildren()
    {
        _mainMenu.nameInputField = null;
        _mainMenu.ResolveNameInput();

        Assert.IsNotNull(_mainMenu.nameInputField, "ResolveNameInput should find input field in children");
        Assert.AreEqual(_inputField, _mainMenu.nameInputField);
    }

    [Test]
    public void MainMenu_ResetProgressButton_HiddenWhenNoSaveData()
    {
        _mainMenu.OpenMainMenu();

        Assert.IsFalse(_resetButton.gameObject.activeSelf, "Reset button should be hidden when no save data exists");
    }

    [Test]
    public void MainMenu_ResetProgressButton_VisibleWhenSaveDataExists()
    {
        PlayerProfile.SetPlayerName("SavedPilot");

        _mainMenu.OpenMainMenu();

        Assert.IsTrue(_resetButton.gameObject.activeSelf, "Reset button should be visible when save data exists");
    }

    [Test]
    public void MainMenu_ResetProgress_ResetsUpgradesScrapNameAndHidesButton()
    {
        PlayerProfile.SetPlayerName("VeteranPilot");
        PlayerMetaProgression.AddScrap(500);
        PlayerMetaProgression.TryPurchaseUpgrade(MetaUpgradeType.HullArmor);

        _mainMenu.OpenMainMenu();
        Assert.IsTrue(_resetButton.gameObject.activeSelf);

        bool resetFired = false;
        _mainMenu.OnProgressReset += () => resetFired = true;

        _mainMenu.ResetProgress();

        Assert.IsTrue(resetFired, "OnProgressReset event should fire");
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, PlayerProfile.PlayerName);
        Assert.AreEqual(PlayerProfile.DEFAULT_NAME, _inputField.text);
        Assert.AreEqual(0, PlayerMetaProgression.BankedScrap);
        Assert.AreEqual(0, PlayerMetaProgression.GetUpgradeLevel(MetaUpgradeType.HullArmor));
        Assert.IsFalse(_resetButton.gameObject.activeSelf, "Reset button should be hidden after reset");
    }

    [Test]
    public void MainMenu_ResolveResetProgressButton_FindsButtonInChildren()
    {
        _mainMenu.resetProgressButton = null;
        _mainMenu.ResolveResetProgressButton();

        Assert.IsNotNull(_mainMenu.resetProgressButton, "ResolveResetProgressButton should find reset button in children");
        Assert.AreEqual(_resetButton, _mainMenu.resetProgressButton);
    }
}

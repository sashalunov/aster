using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WaveIntermissionFreezeTests
{
    private GameObject _playerObj;
    private player _player;
    private Rigidbody _playerRb;
    private PlayerProgression _progression;
    private GameObject _waveManagerObj;
    private WaveManager _waveManager;
    private GameObject _mainMenuObj;
    private MainMenu _mainMenu;

    [SetUp]
    public void SetUp()
    {
        // Cleanup any existing singletons
        foreach (var wm in Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(wm.gameObject);
        }
        foreach (var p in Object.FindObjectsByType<player>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(p.gameObject);
        }
        foreach (var mm in Object.FindObjectsByType<MainMenu>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(mm.gameObject);
        }

        // Setup Player
        _playerObj = new GameObject("TestPlayer");
        _player = _playerObj.AddComponent<player>();
        _playerRb = _playerObj.AddComponent<Rigidbody>();
        _progression = _playerObj.AddComponent<PlayerProgression>();
        _player._can_play = true;

        // Setup WaveManager
        _waveManagerObj = new GameObject("TestWaveManager");
        _waveManager = _waveManagerObj.AddComponent<WaveManager>();
        _waveManager.authoredWaves = new List<WaveDefinition>();
        _waveManager.ActivePlayer = _player;
        WaveManager.Instance = _waveManager;

        // Setup MainMenu
        _mainMenuObj = new GameObject("TestMainMenu");
        _mainMenu = _mainMenuObj.AddComponent<MainMenu>();
        _mainMenu._player = _player;
        _mainMenu._menu_ = new GameObject("TestMenuPanel");
        _mainMenu._menu_.transform.SetParent(_mainMenuObj.transform);
        _mainMenu._menuRe_ = new GameObject("TestPausePanel");
        _mainMenu._menuRe_.transform.SetParent(_mainMenuObj.transform);
        _mainMenu.showOnStart = false;

        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        if (_mainMenuObj != null) Object.DestroyImmediate(_mainMenuObj);
        if (_waveManagerObj != null) Object.DestroyImmediate(_waveManagerObj);
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);

        WaveManager.Instance = null;
    }

    [Test]
    public void EnteringIntermission_FreezesTimeScale_AndDisablesPlayerCanPlay()
    {
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(_player._can_play);

        _playerRb.linearVelocity = new Vector3(5f, 0f, 5f);
        _playerRb.angularVelocity = new Vector3(0f, 2f, 0f);

        _waveManager.SetState(WaveManager.WaveState.Intermission);

        Assert.AreEqual(WaveManager.WaveState.Intermission, _waveManager.State);
        Assert.AreEqual(0f, Time.timeScale, "Time.timeScale must be 0f during intermission");
        Assert.IsFalse(_player._can_play, "player._can_play must be false during intermission");
        Assert.AreEqual(Vector3.zero, _playerRb.linearVelocity, "Ship velocity should be zeroed in intermission");
        Assert.AreEqual(Vector3.zero, _playerRb.angularVelocity, "Ship angular velocity should be zeroed in intermission");
    }

    [Test]
    public void TickingDuringIntermission_DoesNotAutoAdvance()
    {
        _waveManager.SetState(WaveManager.WaveState.Intermission);

        // Advance simulated time repeatedly
        for (int i = 0; i < 20; i++)
        {
            _waveManager.Tick(1f);
        }

        Assert.AreEqual(WaveManager.WaveState.Intermission, _waveManager.State,
            "Intermission must never auto-advance; it must wait indefinitely for player confirmation");
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsFalse(_player._can_play);
    }

    [Test]
    public void StartingNextWaveFromIntermission_RestoresTimeScale_AndEnablesPlayerCanPlay()
    {
        _waveManager.SetState(WaveManager.WaveState.Intermission);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsFalse(_player._can_play);

        _waveManager.StartNextWave();

        Assert.AreEqual(WaveManager.WaveState.Countdown, _waveManager.State);
        Assert.AreEqual(1f, Time.timeScale, "Time.timeScale must be restored to 1f upon exiting intermission");
        Assert.IsTrue(_player._can_play, "player._can_play must be restored to true upon exiting intermission");
    }

    [Test]
    public void MainMenu_PauseAndResumeDuringIntermission_PreservesIntermissionFreeze()
    {
        _waveManager.SetState(WaveManager.WaveState.Intermission);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsFalse(_player._can_play);

        // Open Pause Menu
        _mainMenu.OpenPauseMenu();
        Assert.IsTrue(_mainMenu.IsOpen);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsFalse(_player._can_play);

        // Resume Game back to Intermission
        _mainMenu.ResumeGame();
        Assert.IsFalse(_mainMenu.IsOpen);
        Assert.AreEqual(0f, Time.timeScale, "Resuming from pause menu while in intermission must keep Time.timeScale at 0f");
        Assert.IsFalse(_player._can_play, "Resuming from pause menu while in intermission must keep _can_play false");

        // Now advance wave
        _waveManager.StartNextWave();
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(_player._can_play);
    }

    [Test]
    public void DestroyingWaveManagerInIntermission_RestoresTimeScale()
    {
        _waveManager.SetState(WaveManager.WaveState.Intermission);
        Assert.AreEqual(0f, Time.timeScale);

        _waveManager.OnDestroy();

        Assert.AreEqual(1f, Time.timeScale, "Calling OnDestroy on WaveManager must restore Time.timeScale to 1f");
    }

    [Test]
    public void DisablingWaveManagerInIntermission_RestoresTimeScale()
    {
        _waveManager.SetState(WaveManager.WaveState.Intermission);
        Assert.AreEqual(0f, Time.timeScale);

        _waveManager.OnDisable();

        Assert.AreEqual(1f, Time.timeScale, "Calling OnDisable on WaveManager must restore Time.timeScale to 1f");
    }
}

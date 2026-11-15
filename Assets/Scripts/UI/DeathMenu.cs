using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays post-death mission debrief (survival time, wave reached, high score records)
/// and provides action buttons to redeploy or return to main menu.
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
    public TMP_Text pilotNameText;
    public TMP_Text highScoreText;

    [Header("Navigation Buttons")]
    public Button deployAgainButton;
    public Button mainMenuButton;
    public Button exitButton;

    [Header("Timing")]
    [Tooltip("Delay in real seconds after player death before showing the death menu.")]
    public float delayBeforeShow = 1.0f;

    public bool IsOpen { get; private set; }
    public float LastRunDuration { get; private set; }
    public bool IsNewHighScore { get; private set; }
    public bool IsNewBestWave { get; private set; }
    public LeaderboardPayload LastLeaderboardPayload { get; private set; }

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

        // Record run and prepare leaderboard payload
        IsNewHighScore = PlayerProfile.RecordRun(runXP, waves, LastRunDuration, out bool isNewWave);
        IsNewBestWave = isNewWave;
        LastLeaderboardPayload = PlayerProfile.SubmitLeaderboardEntry(runXP, waves, LastRunDuration);

        UpdateDebriefDisplay(LastRunDuration, waves);

        Time.timeScale = 0f;
        OnDeathMenuOpened?.Invoke();
    }

    public void UpdateDebriefDisplay(float duration, int waves)
    {
        int minutes = Mathf.FloorToInt(duration / 60f);
        int seconds = Mathf.FloorToInt(duration % 60f);

        if (titleText != null)
        {
            titleText.SetText(IsNewHighScore ? "NEW HIGH SCORE!" : "HULL DESTROYED");
        }
        if (pilotNameText != null)
        {
            pilotNameText.SetText("PILOT: " + PlayerProfile.PlayerName);
        }
        if (highScoreText != null)
        {
            highScoreText.SetText("BEST XP: " + PlayerProfile.HighScore);
        }
        if (runTimeText != null)
        {
            runTimeText.SetText(string.Format("SURVIVAL TIME: {0:00}:{1:00}", minutes, seconds));
        }
        if (wavesClearedText != null)
        {
            wavesClearedText.SetText("WAVE REACHED: " + waves);
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

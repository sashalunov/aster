using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls Main Menu and Pause Menu states, keybindings (M, P),
/// game time pausing, and transitions into combat/waves.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Start/Title menu panel GameObject.")]
    public GameObject _menu_;

    [Tooltip("In-game Pause/Resume menu panel GameObject.")]
    public GameObject _menuRe_;

    [Header("Legacy / External References")]
    public GameObject _ultradeath_on_start;
    public GameObject _wave_menu;

    [Header("Player Reference")]
    public player _player;

    [Header("Buttons")]
    public UnityEngine.UI.Button startButton;
    public UnityEngine.UI.Button resumeButton;
    public UnityEngine.UI.Button restartButton;
    public UnityEngine.UI.Button exitButton;

    [Header("Menu Configuration")]
    [Tooltip("Whether to display the main menu automatically on start.")]
    public bool showOnStart = true;

    [Tooltip("Whether to freeze Time.timeScale when menu is open.")]
    public bool pauseTimeWhenOpen = true;

    [Tooltip("Primary key to toggle/open menu.")]
    public KeyCode menuKeyPrimary = KeyCode.M;

    [Tooltip("Secondary key to toggle/open menu.")]
    public KeyCode menuKeySecondary = KeyCode.P;

    [Tooltip("Optional escape key support.")]
    public bool allowEscapeKey = true;

    // Runtime state
    public bool IsOpen { get; private set; }
    public bool HasGameStarted { get; private set; }

    // Events
    public event Action OnMenuOpened;
    public event Action OnMenuClosed;
    public event Action OnGameStarted;
    public event Action OnGameResumed;

    private void Awake()
    {
        ResolvePlayer();
        BindButtons();
    }

    public void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
            exitButton.onClick.AddListener(ExitGame);
        }
    }

    private void Start()
    {
        ResolvePlayer();

        if (showOnStart)
        {
            OpenMainMenu();
        }
        else
        {
            StartGame();
        }
    }

    private void Update()
    {
        HandleInput();
    }

    public void ResolvePlayer()
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<player>();
        }
    }

    public void HandleInput()
    {
        bool keyHit = Input.GetKeyDown(menuKeyPrimary) || Input.GetKeyDown(menuKeySecondary);
        if (!keyHit && allowEscapeKey)
        {
            keyHit = Input.GetKeyDown(KeyCode.Escape);
        }

        if (keyHit)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        // Never allow unpausing or toggling menu into play mode if player is dead
        if (_player != null && _player.isDead)
        {
            return;
        }

        if (IsOpen)
        {
            if (HasGameStarted)
            {
                ResumeGame();
            }
            else
            {
                StartGame();
            }
        }
        else
        {
            if (HasGameStarted)
            {
                OpenPauseMenu();
            }
            else
            {
                OpenMainMenu();
            }
        }
    }

    public void OpenMainMenu()
    {
        IsOpen = true;

        if (_menu_ != null) _menu_.SetActive(true);
        if (_menuRe_ != null) _menuRe_.SetActive(false);

        ApplyPauseState(true);
        OnMenuOpened?.Invoke();
    }

    public void OpenPauseMenu()
    {
        IsOpen = true;

        if (_menuRe_ != null)
        {
            _menuRe_.SetActive(true);
            if (_menu_ != null) _menu_.SetActive(false);
        }
        else if (_menu_ != null)
        {
            _menu_.SetActive(true);
        }

        ApplyPauseState(true);
        OnMenuOpened?.Invoke();
    }

    public void StartGame()
    {
        HasGameStarted = true;
        IsOpen = false;

        if (_menu_ != null) _menu_.SetActive(false);
        if (_menuRe_ != null) _menuRe_.SetActive(false);

        ApplyPauseState(false);

        if (_wave_menu != null)
        {
            _wave_menu.SetActive(true);
            WaveMenu wm = _wave_menu.GetComponent<WaveMenu>();
            if (wm != null)
            {
                wm.StartWaves();
            }
        }

        OnGameStarted?.Invoke();
    }

    public void ResumeGame()
    {
        if (_player != null && _player.isDead) return;

        IsOpen = false;

        if (_menu_ != null) _menu_.SetActive(false);
        if (_menuRe_ != null) _menuRe_.SetActive(false);

        ApplyPauseState(false);
        OnGameResumed?.Invoke();
        OnMenuClosed?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        string activeScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(activeScene))
        {
            SceneManager.LoadScene(activeScene);
        }
    }

    public void ExitGame()
    {
        if (_menu_ != null) _menu_.SetActive(false);
        if (_menuRe_ != null) _menuRe_.SetActive(false);

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ApplyPauseState(bool paused)
    {
        if (pauseTimeWhenOpen)
        {
            Time.timeScale = paused ? 0f : 1f;
        }

        if (_player != null)
        {
            _player._can_play = !paused && !_player.isDead;
        }
    }
}

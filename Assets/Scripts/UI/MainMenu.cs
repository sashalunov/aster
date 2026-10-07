using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls Main Menu and Pause Menu states, keybindings (M, P),
/// game time pausing, player name entry, progress reset, and transitions into combat/waves.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Start/Title menu panel GameObject.")]
    public GameObject _menu_;

    [Tooltip("In-game Pause/Resume menu panel GameObject.")]
    public GameObject _menuRe_;

    [Header("Player Identity / Input")]
    [Tooltip("TMP input field for entering player name.")]
    public TMP_InputField nameInputField;

    [Header("Player Reference")]
    public player _player;

    [Header("Spawn Reference")]
    [Tooltip("Optional reference to player spawn point.")]
    public SpawnPoint spawnPoint;

    [Header("Buttons")]
    public UnityEngine.UI.Button startButton;
    public UnityEngine.UI.Button resumeButton;
    public UnityEngine.UI.Button restartButton;
    public UnityEngine.UI.Button exitButton;

    [Tooltip("Button to wipe player progress and restore clean defaults.")]
    public UnityEngine.UI.Button resetProgressButton;

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
    public static MainMenu Instance { get; private set; }
    public bool IsOpen { get; private set; }
    public bool HasGameStarted { get; private set; }

    // Events
    public event Action OnMenuOpened;
    public event Action OnMenuClosed;
    public event Action OnGameStarted;
    public event Action OnGameResumed;
    public event Action OnProgressReset;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResolvePlayer();
        ResolveNameInput();
        ResolveResetProgressButton();
        BindButtons();
        BindNameInput();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        PlayerProfile.OnPlayerNameChanged += HandleProfileChanged;
        PlayerMetaProgression.OnProgressionChanged += HandleProgressionChanged;
        UpdateResetProgressButtonVisibility();
    }

    private void OnDisable()
    {
        PlayerProfile.OnPlayerNameChanged -= HandleProfileChanged;
        PlayerMetaProgression.OnProgressionChanged -= HandleProgressionChanged;
    }

    private void HandleProfileChanged(string _)
    {
        UpdateResetProgressButtonVisibility();
    }

    private void HandleProgressionChanged()
    {
        UpdateResetProgressButtonVisibility();
    }

    public void ResolveNameInput()
    {
        if (nameInputField == null && _menu_ != null)
        {
            nameInputField = _menu_.GetComponentInChildren<TMP_InputField>(true);
        }
        if (nameInputField == null)
        {
            nameInputField = GetComponentInChildren<TMP_InputField>(true);
        }
    }

    public void ResolveResetProgressButton()
    {
        if (resetProgressButton == null && _menu_ != null)
        {
            Transform t = _menu_.transform.Find("Card/ResetProgress") ?? _menu_.transform.Find("ResetProgress");
            if (t != null)
            {
                resetProgressButton = t.GetComponent<UnityEngine.UI.Button>();
            }
            if (resetProgressButton == null)
            {
                var allButtons = _menu_.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                foreach (var btn in allButtons)
                {
                    if (btn.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        resetProgressButton = btn;
                        break;
                    }
                }
            }
        }
    }

    public void BindNameInput()
    {
        if (nameInputField != null)
        {
            nameInputField.characterLimit = PlayerProfile.MAX_NAME_LENGTH;
            nameInputField.text = PlayerProfile.PlayerName;

            nameInputField.onEndEdit.RemoveListener(OnNameInputEndEdit);
            nameInputField.onEndEdit.AddListener(OnNameInputEndEdit);
            nameInputField.onSubmit.RemoveListener(OnNameInputSubmit);
            nameInputField.onSubmit.AddListener(OnNameInputSubmit);
        }
    }

    private void OnNameInputEndEdit(string text)
    {
        PlayerProfile.SetPlayerName(text);
        if (nameInputField != null)
        {
            nameInputField.text = PlayerProfile.PlayerName;
        }
        UpdateResetProgressButtonVisibility();
    }

    private void OnNameInputSubmit(string text)
    {
        PlayerProfile.SetPlayerName(text);
        if (nameInputField != null)
        {
            nameInputField.text = PlayerProfile.PlayerName;
        }

        if (IsOpen && !HasGameStarted)
        {
            StartGame();
        }
        else
        {
            UpdateResetProgressButtonVisibility();
        }
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
        if (resetProgressButton != null)
        {
            resetProgressButton.onClick.RemoveListener(ResetProgress);
            resetProgressButton.onClick.AddListener(ResetProgress);
        }
    }

    private void Start()
    {
        ResolvePlayer();
        ResolveNameInput();
        ResolveResetProgressButton();
        BindButtons();
        BindNameInput();
        UpdateResetProgressButtonVisibility();

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

    public void ResolveSpawnPoint()
    {
        if (spawnPoint == null)
        {
            spawnPoint = SpawnPoint.Instance != null ? SpawnPoint.Instance : FindAnyObjectByType<SpawnPoint>();
        }
    }

    public void HandleInput()
    {
        // Don't intercept menu shortcut keys while actively typing in the name input field
        if (nameInputField != null && nameInputField.isFocused)
        {
            return;
        }

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

        if (nameInputField != null)
        {
            nameInputField.text = PlayerProfile.PlayerName;
        }

        UpdateResetProgressButtonVisibility();

        ApplyPauseState(true);
        OnMenuOpened?.Invoke();
    }

    public void OpenPauseMenu()
    {
        IsOpen = true;

        transform.SetAsLastSibling();

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
        // Persist player name entered in UI
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
        {
            PlayerProfile.SetPlayerName(nameInputField.text);
        }
        else if (string.IsNullOrWhiteSpace(PlayerProfile.PlayerName))
        {
            PlayerProfile.SetPlayerName(PlayerProfile.DEFAULT_NAME);
        }

        HasGameStarted = true;
        IsOpen = false;

        if (_menu_ != null) _menu_.SetActive(false);
        if (_menuRe_ != null) _menuRe_.SetActive(false);

        // Respawn player at SpawnPoint
        ResolvePlayer();
        ResolveSpawnPoint();

        if (spawnPoint != null)
        {
            spawnPoint.RespawnPlayer(_player);
        }
        else if (_player != null)
        {
            _player.Respawn(Vector3.zero, Quaternion.identity);
        }

        ApplyPauseState(false);

        // Play  background music
        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.PlayBGM();
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

    /// <summary>
    /// Updates the active/visible state of the reset progress button based on whether saved data exists.
    /// </summary>
    public void UpdateResetProgressButtonVisibility()
    {
        if (resetProgressButton != null)
        {
            bool hasSave = PlayerProfile.HasSaveData();
            resetProgressButton.gameObject.SetActive(hasSave);
        }
    }

    /// <summary>
    /// Resets all player progression (upgrades, scrap, records, custom name) to clean default state.
    /// </summary>
    public void ResetProgress()
    {
        PlayerMetaProgression.ResetAllProgress();
        PlayerProfile.ResetAllProfileData();

        if (nameInputField != null)
        {
            nameInputField.text = PlayerProfile.DEFAULT_NAME;
        }

        if (_player != null)
        {
            PlayerMetaProgression.ApplyTo(_player);
        }

        UpdateResetProgressButtonVisibility();
        OnProgressReset?.Invoke();
    }

    private void ApplyPauseState(bool paused)
    {
        bool inIntermission = WaveManager.Instance != null && WaveManager.Instance.State == WaveManager.WaveState.Intermission;

        if (pauseTimeWhenOpen)
        {
            Time.timeScale = (paused || inIntermission) ? 0f : 1f;
        }

        if (_player != null)
        {
            _player._can_play = !paused && !_player.isDead && !inIntermission;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls wave lifecycle, game pacing, countdowns, threat quotas, and state transitions.
/// Designed as an extensible foundation for Aster's combat director and spawning pipelines.
/// </summary>
public class WaveManager : MonoBehaviour
{
    private static WaveManager _instance;
    public static WaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WaveManager>();
            }
            return _instance;
        }
        set => _instance = value;
    }

    public enum WaveState
    {
        Idle,           // Before game starts or stopped
        Intermission,   // Rest / shop / perk selection
        Countdown,      // "Wave starting in 3, 2, 1..."
        Combat,         // Active combat and threat spawning
        WaveCleared,    // Objectives complete, rewards distributed
        GameOver        // Player died or session ended
    }

    [Header("Current Status")]
    [SerializeField] private WaveState currentState = WaveState.Idle;
    [SerializeField] private int currentWaveIndex = 0;
    [SerializeField] private float stateTimer = 0f;
    [SerializeField] private float waveTimer = 0f;
    [SerializeField] private int remainingThreatBudget = 0;

    [Header("Objectives & Pacing")]
    [Tooltip("If true, wave completes automatically when the player's XP reaches the wave's goal.")]
    public bool completeOnXPGoal = true;

    [Tooltip("If true, wave clears when combat duration timer expires. Disabled by default (XP-goal driven).")]
    public bool useWaveTimer = false;

    [Tooltip("If true, threats continue spawning continuously until the wave objective is reached.")]
    public bool continuousSpawning = true;

    [Header("Timers Configuration")]
    [Tooltip("Countdown duration before combat begins")]
    public float countdownDuration = 3f;

    [Tooltip("Delay in seconds during WaveCleared state before transitioning to Intermission.")]
    public float waveClearedDelay = 3f;

    [Header("Wave Progression Config")]
    [Tooltip("Pre-configured waves. If current wave exceeds this list, procedural waves are generated.")]
    public List<WaveDefinition> authoredWaves = new List<WaveDefinition>();

    [Header("Player & Progression Links")]
    [Tooltip("Optional explicit reference to the player. Resolves automatically if null.")]
    [SerializeField] private player playerRef;

    [Tooltip("Optional explicit reference to the player's progression. Resolves automatically if null.")]
    [SerializeField] private PlayerProgression playerProgression;

    public player ActivePlayer
    {
        get
        {
            if (playerRef == null)
            {
                playerRef = FindAnyObjectByType<player>();
            }
            return playerRef;
        }
        set => playerRef = value;
    }

    [Header("Runtime Active Threats")]
    private readonly HashSet<GameObject> activeThreats = new HashSet<GameObject>();

    // Events
    public event Action<WaveState, WaveState> OnStateChanged;
    public event Action<int, WaveDefinition> OnWaveStarted;
    public event Action<float> OnCountdownTick;
    public event Action<float, float> OnWaveTimerTick; // currentTimer, totalDuration
    public event Action<int, int> OnThreatsChanged;    // activeThreatCount, remainingBudget
    public event Action<int, WaveDefinition> OnWaveCompleted;
    public event Action OnGameOver;

    // Public Getters
    public WaveState State => currentState;
    public int CurrentWaveIndex => currentWaveIndex;
    public float StateTimer => stateTimer;
    public float WaveTimer => waveTimer;
    public int RemainingThreatBudget => remainingThreatBudget;
    public int ActiveThreatCount => activeThreats.Count;
    public WaveDefinition CurrentWaveConfig { get; private set; }

    [Header("Progression Tracking")]
    private ulong waveStartXP = 0;
    public ulong WaveStartXP => waveStartXP;
    public ulong WaveEarnedXP
    {
        get
        {
            PlayerProgression prog = ActiveProgression;
            if (prog == null) return 0;
            return prog.CurrentXP >= waveStartXP ? prog.CurrentXP - waveStartXP : 0;
        }
    }

    public PlayerProgression ActiveProgression
    {
        get
        {
            if (playerProgression == null)
            {
                player p = ActivePlayer;
                if (p != null)
                {
                    playerProgression = p.Progression ?? p.GetComponent<PlayerProgression>();
                }
            }
            return playerProgression;
        }
        set
        {
            if (playerProgression != value)
            {
                UnsubscribeProgression();
                playerProgression = value;
                SubscribeProgression();
            }
        }
    }

    /// <summary>
    /// Whether spawning systems should actively spawn new threats right now.
    /// </summary>
    public virtual bool CanSpawn => currentState == WaveState.Combat && (continuousSpawning || remainingThreatBudget > 0);

    protected virtual void Awake()
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

        if (authoredWaves == null || authoredWaves.Count == 0)
        {
            PopulateDefaultWaves();
        }
    }

    protected virtual void Start()
    {
        SubscribeProgression();
    }

    protected virtual void OnEnable()
    {
        SubscribeProgression();
    }

    public virtual void OnDisable()
    {
        UnsubscribeProgression();
        if (currentState == WaveState.Intermission)
        {
            Time.timeScale = 1f;
        }
    }

    public virtual void OnDestroy()
    {
        UnsubscribeProgression();
        if (currentState == WaveState.Intermission)
        {
            Time.timeScale = 1f;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeProgression()
    {
        PlayerProgression prog = ActiveProgression;
        if (prog != null)
        {
            prog.OnXPChanged -= HandleXPChanged;
            prog.OnXPChanged += HandleXPChanged;
            prog.OnWaveCompleted -= HandleProgressionWaveCompleted;
            prog.OnWaveCompleted += HandleProgressionWaveCompleted;
        }
    }

    private void UnsubscribeProgression()
    {
        if (playerProgression != null)
        {
            playerProgression.OnXPChanged -= HandleXPChanged;
            playerProgression.OnWaveCompleted -= HandleProgressionWaveCompleted;
        }
    }

    private void HandleXPChanged(ulong currentXP, ulong nextGoal)
    {
        if (currentState == WaveState.Combat && completeOnXPGoal)
        {
            ulong targetGoal = GetTargetXPGoal(currentWaveIndex);
            if (currentXP >= targetGoal && targetGoal > 0)
            {
                CompleteWave();
            }
        }
    }

    private void HandleProgressionWaveCompleted(int completedWaveLevel)
    {
        if (currentState == WaveState.Combat)
        {
            CompleteWave();
        }
        else if (currentState == WaveState.Idle)
        {
            // If progression completed a wave while WaveManager was Idle (e.g. direct play in scene without MainMenu),
            // initialize wave state and transition to cleared / intermission
            currentWaveIndex = Mathf.Max(1, completedWaveLevel);
            CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
            CompleteWaveInternal();
        }
    }

    public virtual ulong GetTargetXPGoal(int waveNumber)
    {
        if (CurrentWaveConfig != null && CurrentWaveConfig.targetXPGoal > 0)
        {
            return CurrentWaveConfig.targetXPGoal;
        }

        PlayerProgression prog = ActiveProgression;
        if (prog != null && prog.waveGoals != null && prog.waveGoals.Length > 0)
        {
            int index = Mathf.Clamp(waveNumber - 1, 0, prog.waveGoals.Length - 1);
            return prog.waveGoals[index];
        }

        return (ulong)(10 * Mathf.Max(1, waveNumber));
    }

    public virtual bool IsXPGoalReached()
    {
        PlayerProgression prog = ActiveProgression;
        if (prog == null) return false;

        ulong goal = GetTargetXPGoal(currentWaveIndex);
        return prog.CurrentXP >= goal;
    }

    protected virtual void Update()
    {
        if (playerProgression == null && ActiveProgression != null)
        {
            SubscribeProgression();
        }
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Advances timers and state logic. Exposed for manual ticking in tests.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        switch (currentState)
        {
            case WaveState.Countdown:
                ProcessCountdown(deltaTime);
                break;

            case WaveState.Combat:
                ProcessCombat(deltaTime);
                break;

            case WaveState.WaveCleared:
                ProcessWaveCleared(deltaTime);
                break;

            case WaveState.Intermission:
                ProcessIntermission(deltaTime);
                break;
        }
    }

    #region State Machine Management

    public void SetState(WaveState newState)
    {
        if (currentState == newState) return;

        WaveState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;

        OnStateExit(previousState);
        OnStateEnter(newState);

        OnStateChanged?.Invoke(previousState, newState);
    }

    protected virtual void OnStateEnter(WaveState state)
    {
        switch (state)
        {
            case WaveState.Intermission:
                stateTimer = 0f;
                Time.timeScale = 0f;
                SetPlayerCanPlay(false);
                if (ActivePlayer != null)
                {
                    Rigidbody rb = ActivePlayer.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
                break;

            case WaveState.Countdown:
                stateTimer = countdownDuration;
                OnCountdownTick?.Invoke(stateTimer);
                break;

            case WaveState.Combat:
                SetupCombatPhase();
                break;

            case WaveState.WaveCleared:
                stateTimer = waveClearedDelay;
                DistributeWaveRewards();
                break;

            case WaveState.GameOver:
                OnGameOver?.Invoke();
                break;
        }
    }

    protected virtual void OnStateExit(WaveState state)
    {
        switch (state)
        {
            case WaveState.Intermission:
                Time.timeScale = 1f;
                SetPlayerCanPlay(true);
                break;
        }
    }

    /// <summary>
    /// Updates player's playable state, taking dead status and pause menu into account.
    /// </summary>
    public virtual void SetPlayerCanPlay(bool canPlay)
    {
        player p = ActivePlayer;
        if (p != null)
        {
            if (!canPlay)
            {
                p._can_play = false;
            }
            else
            {
                bool isPaused = MainMenu.Instance != null && MainMenu.Instance.IsOpen;
                p._can_play = !p.isDead && !isPaused;
            }
        }
    }

    #endregion

    #region Wave Control Flow

    /// <summary>
    /// Starts the game run from wave 1 (or restarts if already running).
    /// </summary>
    public virtual void StartRun()
    {
        SubscribeProgression();
        currentWaveIndex = 0;
        activeThreats.Clear();
        StartNextWave();
    }

    /// <summary>
    /// Advances to the next wave and begins countdown.
    /// </summary>
    public virtual void StartNextWave()
    {
        currentWaveIndex++;
        CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
        SetState(WaveState.Countdown);

         if (AudioManager.HasInstance)
            {
               AudioManager.Instance.PlayWaveStart();
           }
    
    }

    /// <summary>
    /// Manually triggers combat immediately, skipping countdown.
    /// </summary>
    public virtual void TriggerCombatImmediately()
    {
        if (currentWaveIndex < 1)
        {
            currentWaveIndex = 1;
        }
        if (CurrentWaveConfig == null)
        {
            CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
        }
        SetState(WaveState.Combat);
    }

    /// <summary>
    /// Ends the current wave successfully, moving to WaveCleared.
    /// </summary>
    public virtual void CompleteWave()
    {
        if (currentState != WaveState.Combat && currentState != WaveState.Idle) return;

        if (currentState == WaveState.Idle && currentWaveIndex < 1)
        {
            currentWaveIndex = 1;
            CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
        }

        CompleteWaveInternal();
    }

    private void CompleteWaveInternal()
    {
        SetState(WaveState.WaveCleared);
        OnWaveCompleted?.Invoke(currentWaveIndex, CurrentWaveConfig);

        if (waveClearedDelay <= 0f)
        {
            SetState(WaveState.Intermission);
        }
    }

    /// <summary>
    /// Marks the run as Game Over.
    /// </summary>
    public virtual void TriggerGameOver()
    {
        SetState(WaveState.GameOver);
    }

    #endregion

    #region Pacing & Timers

    protected virtual void ProcessWaveCleared(float deltaTime)
    {
        if (stateTimer > 0f)
        {
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                SetState(WaveState.Intermission);
            }
        }
        else
        {
            SetState(WaveState.Intermission);
        }
    }

    protected virtual void ProcessCountdown(float deltaTime)
    {
        stateTimer -= deltaTime;
        OnCountdownTick?.Invoke(Mathf.Max(0f, stateTimer));

        if (stateTimer <= 0f)
        {
            SetState(WaveState.Combat);
        }
    }

    protected virtual void ProcessIntermission(float deltaTime)
    {
        // Intermission always waits for player input via the intermission popup confirmation
    }

    protected virtual void ProcessCombat(float deltaTime)
    {
        stateTimer += deltaTime;

        if (useWaveTimer && CurrentWaveConfig != null && CurrentWaveConfig.duration > 0f)
        {
            waveTimer = Mathf.Max(0f, CurrentWaveConfig.duration - stateTimer);
            OnWaveTimerTick?.Invoke(waveTimer, CurrentWaveConfig.duration);

            // If time expired, wave is cleared (only when useWaveTimer is explicitly true)
            if (waveTimer <= 0f)
            {
                CompleteWave();
                return;
            }
        }
        else
        {
            waveTimer = stateTimer;
            OnWaveTimerTick?.Invoke(stateTimer, 0f);
        }

        // Objective check: if threat budget spent and no active threats remain, or wave XP goal met
        EvaluateWaveClearConditions();
    }

    protected virtual void SetupCombatPhase()
    {
        stateTimer = 0f;
        if (CurrentWaveConfig == null)
        {
            CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
        }

        CurrentWaveConfig?.ResetRuntimeDrops();
        waveStartXP = ActiveProgression != null ? ActiveProgression.CurrentXP : 0;

        remainingThreatBudget = CurrentWaveConfig.threatBudget;
        waveTimer = CurrentWaveConfig.duration;
        
        OnWaveStarted?.Invoke(currentWaveIndex, CurrentWaveConfig);
        OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
    }

    protected virtual void EvaluateWaveClearConditions()
    {
        // 1. Primary Objective: Check if Wave Goal XP is reached
        if (completeOnXPGoal && IsXPGoalReached())
        {
            CompleteWave();
            return;
        }

        // 2. Secondary Objective: if not continuous spawning, check threat quota
        if (!continuousSpawning && remainingThreatBudget <= 0 && activeThreats.Count == 0)
        {
            CompleteWave();
        }
    }

    protected virtual void DistributeWaveRewards()
    {
        if (CurrentWaveConfig == null) return;

        player p = FindAnyObjectByType<player>();
        if (p != null)
        {
            if (CurrentWaveConfig.rewardXP > 0)
            {
                p.AddXP(CurrentWaveConfig.rewardXP, p.transform);
            }
            if (PowerupManager.Instance != null)
            {
                PowerupManager.Instance.CollectAllRemaining(p);
            }
        }
    }

    #endregion

    #region Threat Management

    /// <summary>
    /// Consumes points from the remaining threat budget when a spawner creates an entity.
    /// </summary>
    public virtual bool ConsumeThreatBudget(int cost = 1)
    {
        if (continuousSpawning)
        {
            remainingThreatBudget = Mathf.Max(0, remainingThreatBudget - cost);
            return true;
        }

        if (remainingThreatBudget < cost) return false;

        remainingThreatBudget -= cost;
        OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
        return true;
    }

    /// <summary>
    /// Registers an active threat (asteroid, tank, turret) to track completion.
    /// </summary>
    public virtual void RegisterThreat(GameObject threat)
    {
        if (threat == null) return;
        if (activeThreats.Add(threat))
        {
            OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
        }
    }

    /// <summary>
    /// Unregisters a threat upon destruction.
    /// </summary>
    public virtual void UnregisterThreat(GameObject threat)
    {
        if (threat == null) return;
        if (activeThreats.Remove(threat))
        {
            OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
        }
    }

    /// <summary>
    /// Cleans up any null or destroyed threats in the active list.
    /// </summary>
    public virtual void PurgeDestroyedThreats()
    {
        int initialCount = activeThreats.Count;
        activeThreats.RemoveWhere(t => t == null);
        if (activeThreats.Count != initialCount)
        {
            OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
        }
    }

    #endregion

    #region Wave Definitions & Fallback Procedural Scaling

    public virtual WaveDefinition GetWaveDefinition(int waveNumber)
    {
        if (authoredWaves != null && waveNumber >= 1 && waveNumber <= authoredWaves.Count)
        {
            return authoredWaves[waveNumber - 1].Clone();
        }

        return GenerateProceduralWave(waveNumber);
    }

    /// <summary>
    /// Algorithmic wave generation for infinite scaling beyond authored waves.
    /// </summary>
    public virtual WaveDefinition GenerateProceduralWave(int waveNumber)
    {
        float duration = Mathf.Min(60f, 25f + waveNumber * 3f);
        int threatBudget = Mathf.RoundToInt(10 + Mathf.Pow(waveNumber, 1.25f) * 3f);
        float spawnInterval = Mathf.Max(0.6f, 2.2f - (waveNumber * 0.08f));
        int minMass = Mathf.Min(8, 2 + (waveNumber / 3));
        int maxMass = Mathf.Min(16, 4 + (waveNumber / 2));
        int shellLevel = 1 + (waveNumber / 4);
        int blockLevel = 1 + (waveNumber / 5);

        int rewardXP = 50 + waveNumber * 25;

        return WaveDefinition.Create(
            waveNumber: waveNumber,
            waveTitle: $"Sector Zone {waveNumber}",
            duration: duration,
            threatBudget: threatBudget,
            spawnInterval: spawnInterval,
            minMass: minMass,
            maxMass: maxMass,

            rewardXP: rewardXP,
            shellLevel: shellLevel,
            blockLevel: blockLevel
        );
    }

    private void PopulateDefaultWaves()
    {
        WaveDefinition[] loaded = Resources.LoadAll<WaveDefinition>("Waves");
#if UNITY_EDITOR
        if (loaded == null || loaded.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:WaveDefinition");
            if (guids != null && guids.Length > 0)
            {
                var list = new List<WaveDefinition>();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    var wave = UnityEditor.AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
                    if (wave != null) list.Add(wave);
                }
                loaded = list.ToArray();
            }
        }
#endif
        if (loaded != null && loaded.Length > 0)
        {
            var sorted = new List<WaveDefinition>(loaded);
            sorted.Sort((a, b) => a.waveNumber.CompareTo(b.waveNumber));
            authoredWaves = sorted;
            return;
        }

        authoredWaves = new List<WaveDefinition>
        {
            WaveDefinition.Create(
                waveNumber: 1,
                waveTitle: "First Contact: Just Asteroids",
                duration: 20f,
                threatBudget: 8,
                spawnInterval: 2.5f,
                minMass: 2,
                maxMass: 3,

                rewardXP: 40,
                blockDropChance: 0.12f,
                dropTable: new List<WaveDropEntry>
                {
                    new WaveDropEntry(StandardPowerup.StandardType.UpgradePoint, 60f),
                    new WaveDropEntry(StandardPowerup.StandardType.AmmoRefill, 40f)
                },
                xpThresholdDrops: new List<WaveXPDropEntry>
                {
                    new WaveXPDropEntry(5, StandardPowerup.StandardType.UpgradePoint)
                }
            ),
            WaveDefinition.Create(
                waveNumber: 2,
                waveTitle: "Second wave: Asteroid Clusters",
                duration: 25f,
                threatBudget: 14,
                spawnInterval: 2.0f,
                minMass: 2,
                maxMass: 5,

                rewardXP: 75,
                blockDropChance: 0.15f,
                dropTable: new List<WaveDropEntry>
                {
                    new WaveDropEntry(StandardPowerup.StandardType.UpgradePoint, 50f),
                    new WaveDropEntry(StandardPowerup.StandardType.AmmoRefill, 30f),
                    new WaveDropEntry(StandardPowerup.StandardType.GunKinetic, 20f)
                },
                xpThresholdDrops: new List<WaveXPDropEntry>
                {
                    new WaveXPDropEntry(25, StandardPowerup.StandardType.UpgradePoint),
                    new WaveXPDropEntry(50, StandardPowerup.StandardType.GunKinetic)
                }
            ),
            WaveDefinition.Create(
                waveNumber: 3,
                waveTitle: "Third wave: Dense Debris Field, Can you hold it?",
                duration: 30f,
                threatBudget: 20,
                spawnInterval: 1.7f,
                minMass: 3,
                maxMass: 6,
  
                rewardXP: 120,
                grantExtraGun: true,
                blockDropChance: 0.18f,
                dropTable: new List<WaveDropEntry>
                {
                    new WaveDropEntry(StandardPowerup.StandardType.UpgradePoint, 40f),
                    new WaveDropEntry(StandardPowerup.StandardType.AmmoRefill, 25f),
                    new WaveDropEntry(StandardPowerup.StandardType.GunKinetic, 20f),
                    new WaveDropEntry(StandardPowerup.StandardType.GunPlasma, 15f)
                },
                xpThresholdDrops: new List<WaveXPDropEntry>
                {
                    new WaveXPDropEntry(50, StandardPowerup.StandardType.UpgradePoint),
                    new WaveXPDropEntry(150, StandardPowerup.StandardType.GunPlasma)
                }
            )
        };
    }

    #endregion
}

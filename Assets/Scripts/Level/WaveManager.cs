using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls wave lifecycle, game pacing, countdowns, threat quotas, and state transitions.
/// Designed as an extensible foundation for Aster's combat director and spawning pipelines.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; set; }

    public enum WaveState
    {
        Idle,           // Before game starts or stopped
        Intermission,   // Rest / shop / perk selection
        Countdown,      // "Wave starting in 3, 2, 1..."
        Combat,         // Active combat and threat spawning
        WaveCleared,    // Objectives complete, rewards distributed
        GameOver        // Player died or session ended
    }

    [System.Serializable]
    public class WaveDefinition
    {
        public int waveNumber = 1;
        public string waveTitle = "Wave 1";
        
        [Header("Duration & Objectives")]
        [Tooltip("Max combat duration in seconds. If 0 or negative, wave lasts until threat budget is cleared.")]
        public float duration = 30f;
        
        [Tooltip("Total threat points/budget to spawn during this wave.")]
        public int threatBudget = 10;

        [Header("Pacing")]
        [Tooltip("Target interval in seconds between spawning threats.")]
        public float spawnInterval = 2.0f;

        [Header("Asteroid Scaling")]
        public int minMass = 2;
        public int maxMass = 4;
        public int shellLevel = 1;
        public int blockLevel = 1;

        [Header("Rewards")]
        public int rewardCredits = 25;
        public int rewardXP = 50;

        public WaveDefinition Clone()
        {
            return (WaveDefinition)MemberwiseClone();
        }
    }

    [Header("Current Status")]
    [SerializeField] private WaveState currentState = WaveState.Idle;
    [SerializeField] private int currentWaveIndex = 0;
    [SerializeField] private float stateTimer = 0f;
    [SerializeField] private float waveTimer = 0f;
    [SerializeField] private int remainingThreatBudget = 0;

    [Header("Timers Configuration")]
    [Tooltip("Countdown duration before combat begins")]
    public float countdownDuration = 3f;

    [Tooltip("Default intermission time if auto-advancing (0 = waits for player input)")]
    public float intermissionDuration = 0f;

    [Header("Wave Progression Config")]
    [Tooltip("Pre-configured waves. If current wave exceeds this list, procedural waves are generated.")]
    public List<WaveDefinition> authoredWaves = new List<WaveDefinition>();

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

    /// <summary>
    /// Whether spawning systems should actively spawn new threats right now.
    /// </summary>
    public virtual bool CanSpawn => currentState == WaveState.Combat && remainingThreatBudget > 0;

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

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected virtual void Update()
    {
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
                stateTimer = intermissionDuration;
                break;

            case WaveState.Countdown:
                stateTimer = countdownDuration;
                OnCountdownTick?.Invoke(stateTimer);
                break;

            case WaveState.Combat:
                SetupCombatPhase();
                break;

            case WaveState.WaveCleared:
                DistributeWaveRewards();
                break;

            case WaveState.GameOver:
                OnGameOver?.Invoke();
                break;
        }
    }

    protected virtual void OnStateExit(WaveState state)
    {
    }

    #endregion

    #region Wave Control Flow

    /// <summary>
    /// Starts the game run from wave 1 (or restarts if already running).
    /// </summary>
    public virtual void StartRun()
    {
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
    }

    /// <summary>
    /// Manually triggers combat immediately, skipping countdown.
    /// </summary>
    public virtual void TriggerCombatImmediately()
    {
        if (CurrentWaveConfig == null)
        {
            CurrentWaveConfig = GetWaveDefinition(Mathf.Max(1, currentWaveIndex));
        }
        SetState(WaveState.Combat);
    }

    /// <summary>
    /// Ends the current wave successfully, moving to WaveCleared.
    /// </summary>
    public virtual void CompleteWave()
    {
        if (currentState != WaveState.Combat) return;

        SetState(WaveState.WaveCleared);
        OnWaveCompleted?.Invoke(currentWaveIndex, CurrentWaveConfig);

        // Move to intermission for player upgrades / shop
        SetState(WaveState.Intermission);
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
        // If intermissionDuration > 0, auto-advance after time expires
        if (intermissionDuration > 0f)
        {
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                StartNextWave();
            }
        }
    }

    protected virtual void ProcessCombat(float deltaTime)
    {
        stateTimer += deltaTime;

        if (CurrentWaveConfig != null && CurrentWaveConfig.duration > 0f)
        {
            waveTimer = Mathf.Max(0f, CurrentWaveConfig.duration - stateTimer);
            OnWaveTimerTick?.Invoke(waveTimer, CurrentWaveConfig.duration);

            // If time expired, wave is cleared
            if (waveTimer <= 0f)
            {
                CompleteWave();
                return;
            }
        }
        else
        {
            waveTimer = 0f;
            OnWaveTimerTick?.Invoke(stateTimer, 0f);
        }

        // Objective check: if threat budget spent and no active threats remain
        EvaluateWaveClearConditions();
    }

    protected virtual void SetupCombatPhase()
    {
        stateTimer = 0f;
        if (CurrentWaveConfig == null)
        {
            CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
        }

        remainingThreatBudget = CurrentWaveConfig.threatBudget;
        waveTimer = CurrentWaveConfig.duration;

        OnWaveStarted?.Invoke(currentWaveIndex, CurrentWaveConfig);
        OnThreatsChanged?.Invoke(ActiveThreatCount, remainingThreatBudget);
    }

    protected virtual void EvaluateWaveClearConditions()
    {
        // If the wave is objective-based (all threats spawned and destroyed)
        if (remainingThreatBudget <= 0 && activeThreats.Count == 0)
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
            if (CurrentWaveConfig.rewardCredits > 0)
            {
                p._cred_value += (ulong)CurrentWaveConfig.rewardCredits;
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
        WaveDefinition def = new WaveDefinition();
        def.waveNumber = waveNumber;
        def.waveTitle = $"Sector Zone {waveNumber}";

        // Duration scales gradually, capped at 60s
        def.duration = Mathf.Min(60f, 25f + waveNumber * 3f);

        // Exponential-linear threat budget curve
        def.threatBudget = Mathf.RoundToInt(10 + Mathf.Pow(waveNumber, 1.25f) * 3f);

        // Spawns speed up slightly as waves progress, min 0.6s
        def.spawnInterval = Mathf.Max(0.6f, 2.2f - (waveNumber * 0.08f));

        // Asteroid hardness scales with waves
        def.minMass = Mathf.Min(8, 2 + (waveNumber / 3));
        def.maxMass = Mathf.Min(16, 4 + (waveNumber / 2));
        def.shellLevel = 1 + (waveNumber / 4);
        def.blockLevel = 1 + (waveNumber / 5);

        // Reward progression
        def.rewardCredits = 25 + waveNumber * 10;
        def.rewardXP = 50 + waveNumber * 25;

        return def;
    }

    private void PopulateDefaultWaves()
    {
        authoredWaves = new List<WaveDefinition>
        {
            new WaveDefinition
            {
                waveNumber = 1,
                waveTitle = "First Contact: Scout Asteroids",
                duration = 20f,
                threatBudget = 8,
                spawnInterval = 2.5f,
                minMass = 2,
                maxMass = 3,
                rewardCredits = 20,
                rewardXP = 40
            },
            new WaveDefinition
            {
                waveNumber = 2,
                waveTitle = "Asteroid Cluster",
                duration = 25f,
                threatBudget = 14,
                spawnInterval = 2.0f,
                minMass = 2,
                maxMass = 5,
                rewardCredits = 35,
                rewardXP = 75
            },
            new WaveDefinition
            {
                waveNumber = 3,
                waveTitle = "Dense Debris Field",
                duration = 30f,
                threatBudget = 20,
                spawnInterval = 1.7f,
                minMass = 3,
                maxMass = 6,
                rewardCredits = 50,
                rewardXP = 120
            }
        };
    }

    #endregion
}

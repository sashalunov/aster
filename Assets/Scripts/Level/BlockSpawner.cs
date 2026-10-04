using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns block0 prefabs and empty single-core AsteroidGrid prefabs within a specified radius around a target center.
/// Controls spawn pacing (rate), maximum active limits, block hit scaling,
/// and full integration with WaveManager (threat budget, threat registration, wave pacing).
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    [Header("Prefabs & Hierarchy")]
    [Tooltip("The block0 prefab to spawn. If not assigned, loads from Resources/block0.")]
    public GameObject blockPrefab;

    [Tooltip("The AsteroidGrid prefab to spawn. If not assigned, loads from Resources/AsteroidGrid.")]
    public GameObject asterPrefab;

    [Tooltip("Parent transform to hold spawned blocks and asteroids. Defaults to this transform.")]
    public Transform spawnContainer;

    [Header("Center & Radius")]
    [Tooltip("Center position for spawning. If null, automatically targets player or this transform.")]
    public Transform centerTarget;

    [Tooltip("Maximum radius around the center where blocks and asteroids can spawn.")]
    [Min(0.1f)]
    public float radius = 15f;

    [Tooltip("Minimum radius (donut hole) around center to avoid spawning directly on the target.")]
    [Min(0f)]
    public float minRadius = 3f;

    [Header("Block Spawn Limits & Pacing")]
    [Tooltip("Maximum number of concurrently active blocks allowed.")]
    [Min(1)]
    public int max_limit = 20;

    [Tooltip("Time in seconds between block spawn attempts.")]
    [Min(0.01f)]
    public float rate = 1.0f;

    [Tooltip("Number of blocks to spawn per interval tick.")]
    [Range(1, 10)]
    public int spawnBatchSize = 1;

    [Tooltip("Initial number of blocks to spawn immediately on start.")]
    public int initialSpawnCount = 0;

    [Header("Asteroid Spawn Limits & Pacing")]
    [Tooltip("Whether to spawn empty single-core AsteroidGrid instances.")]
    public bool spawnAsteroids = true;

    [Tooltip("Maximum number of concurrently active asteroids allowed.")]
    [Min(1)]
    public int max_asteroids = 5;

    [Tooltip("Time in seconds between asteroid spawn attempts.")]
    [Min(0.01f)]
    public float asteroidRate = 3.0f;

    [Tooltip("Number of asteroids to spawn per interval tick.")]
    [Range(1, 10)]
    public int asteroidBatchSize = 1;

    [Tooltip("Initial number of asteroids to spawn immediately on start.")]
    public int initialAsteroidCount = 0;

    [Tooltip("Hit points assigned to the core of spawned asteroids.")]
    [Min(1)]
    public int asteroidCoreHits = 3;

    [Tooltip("Threat budget points consumed when spawning an asteroid.")]
    [Min(1)]
    public int asteroidThreatCost = 2;

    [Header("Asteroid Field Integration")]
    [Tooltip("Optional reference to an AsteroidFieldSpawner managing pregenerated fields.")]
    public AsteroidFieldSpawner fieldSpawner;

    [Header("General Spawner Settings")]
    [Tooltip("Whether the spawner is actively running.")]
    public bool isSpawning = true;

    [Tooltip("Initial random drift velocity applied to spawned entities.")]
    public float initialDrift = 1.0f;

    [Header("Block Attributes")]
    [Tooltip("Minimum hit points assigned to spawned blocks.")]
    public int minHits = 1;

    [Tooltip("Maximum hit points assigned to spawned blocks.")]
    public int maxHits = 3;

    [Header("Wave Integration")]
    [Tooltip("Optional explicit reference to WaveManager. If null, resolves via WaveManager.Instance.")]
    [SerializeField] private WaveManager _waveManager;
    public WaveManager waveManager
    {
        get => _waveManager;
        set
        {
            if (_waveManager != value)
            {
                UnsubscribeWaveManager();
                _waveManager = value;
                EnsureWaveManagerSubscribed();
            }
        }
    }

    [Tooltip("If true, coordinates with WaveManager (respects CanSpawn and registers threats).")]
    [SerializeField] private bool _syncWithWaveManager = true;
    public bool syncWithWaveManager
    {
        get => _syncWithWaveManager;
        set
        {
            if (_syncWithWaveManager != value)
            {
                _syncWithWaveManager = value;
                EnsureWaveManagerSubscribed();
            }
        }
    }

    [Tooltip("If true, adapts block hit points and asteroid core hits based on current wave config.")]
    public bool adaptWaveDifficulty = true;

    [Tooltip("If true, adapts block and asteroid spawn rates from WaveDefinition.spawnInterval.")]
    public bool adaptWavePacing = false;

    [Tooltip("If true, clears all active blocks and asteroids when a wave completes.")]
    public bool clearOnWaveComplete = false;

    /// <summary>
    /// Resolves the active WaveManager reference.
    /// </summary>
    public WaveManager ActiveWaveManager => waveManager != null ? waveManager : WaveManager.Instance;

    // Runtime state
    private float spawnTimer = 0f;
    private float asteroidTimer = 0f;
    private readonly List<GameObject> activeBlocks = new List<GameObject>();
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();
    private WaveManager subscribedWm;

    // Events
    public event Action<GameObject> OnBlockSpawned;
    public event Action<int> OnActiveCountChanged;
    public event Action<GameObject> OnAsteroidSpawned;
    public event Action<int> OnActiveAsteroidCountChanged;

    /// <summary>
    /// Current count of alive spawned blocks.
    /// </summary>
    public int ActiveCount
    {
        get
        {
            CleanDeadReferences();
            return activeBlocks.Count;
        }
    }

    /// <summary>
    /// Explicit alias for ActiveCount (blocks).
    /// </summary>
    public int ActiveBlockCount => ActiveCount;

    /// <summary>
    /// Current count of alive spawned asteroids.
    /// </summary>
    public int ActiveAsteroidCount
    {
        get
        {
            CleanDeadReferences();
            return activeAsteroids.Count;
        }
    }

    /// <summary>
    /// Combined count of all alive spawned blocks and asteroids.
    /// </summary>
    public int TotalActiveCount => ActiveBlockCount + ActiveAsteroidCount;

    /// <summary>
    /// True if the active block count has reached or exceeded max_limit.
    /// </summary>
    public bool IsAtCapacity => ActiveCount >= max_limit;

    /// <summary>
    /// True if the active asteroid count has reached or exceeded max_asteroids.
    /// </summary>
    public bool IsAsteroidAtCapacity => ActiveAsteroidCount >= max_asteroids;

    /// <summary>
    /// Read-only collection of active block GameObjects.
    /// </summary>
    public IReadOnlyList<GameObject> ActiveBlocks => activeBlocks;

    /// <summary>
    /// Read-only collection of active asteroid GameObjects.
    /// </summary>
    public IReadOnlyList<GameObject> ActiveAsteroids => activeAsteroids;

    protected virtual void Awake()
    {
        if (blockPrefab == null)
        {
            blockPrefab = Resources.Load<GameObject>("block0");
        }

        if (asterPrefab == null)
        {
            asterPrefab = Resources.Load<GameObject>("AsteroidGrid");
            if (asterPrefab == null)
            {
                asterPrefab = Resources.Load<GameObject>("AsteroidBase");
            }
        }

        if (spawnContainer == null)
        {
            spawnContainer = transform;
        }
    }

    protected virtual void OnEnable()
    {
        EnsureWaveManagerSubscribed();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeWaveManager();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeWaveManager();
    }

    protected virtual void Start()
    {
        ResolveCenterTarget();
        EnsureWaveManagerSubscribed();

        if (initialSpawnCount > 0)
        {
            SpawnBurst(initialSpawnCount);
        }

        if (initialAsteroidCount > 0 && spawnAsteroids)
        {
            SpawnAsteroidBurst(initialAsteroidCount);
        }
    }

    protected virtual void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Updates spawner timers and triggers spawns. Exposed for testability.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        if (!isSpawning || deltaTime <= 0f) return;

        EnsureWaveManagerSubscribed();

        // Check WaveManager state if synced
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null && !wm.CanSpawn)
        {
            return;
        }

        CleanDeadReferences();

        // 1. Block Spawning Pacing
        if (activeBlocks.Count < max_limit)
        {
            spawnTimer += deltaTime;
            if (spawnTimer >= rate)
            {
                spawnTimer = 0f;
                SpawnBurst(spawnBatchSize);
            }
        }

        // 2. Asteroid Spawning Pacing
        if (spawnAsteroids && activeAsteroids.Count < max_asteroids)
        {
            asteroidTimer += deltaTime;
            if (asteroidTimer >= asteroidRate)
            {
                asteroidTimer = 0f;
                SpawnAsteroidBurst(asteroidBatchSize);
            }
        }
    }

    /// <summary>
    /// Spawns a burst of blocks up to max_limit, coordinating with WaveManager threat budget.
    /// </summary>
    public virtual void SpawnBurst(int count)
    {
        WaveManager wm = ActiveWaveManager;
        for (int i = 0; i < count; i++)
        {
            if (ActiveCount >= max_limit) break;

            if (syncWithWaveManager && wm != null)
            {
                if (!wm.ConsumeThreatBudget(1))
                {
                    break;
                }
            }

            SpawnSingleBlock();
        }
    }

    /// <summary>
    /// Spawns a burst of empty single-core asteroid grids up to max_asteroids, coordinating with WaveManager threat budget.
    /// </summary>
    public virtual void SpawnAsteroidBurst(int count)
    {
        WaveManager wm = ActiveWaveManager;
        for (int i = 0; i < count; i++)
        {
            if (ActiveAsteroidCount >= max_asteroids) break;

            if (syncWithWaveManager && wm != null)
            {
                if (!wm.ConsumeThreatBudget(asteroidThreatCost))
                {
                    break;
                }
            }

            SpawnSingleAsteroid();
        }
    }

    /// <summary>
    /// Spawns one block within the defined radial bounds.
    /// </summary>
    public virtual GameObject SpawnSingleBlock()
    {
        if (IsAtCapacity) return null;

        if (blockPrefab == null)
        {
            blockPrefab = Resources.Load<GameObject>("block0");
            if (blockPrefab == null)
            {
                Debug.LogWarning("BlockSpawner: Cannot spawn block, blockPrefab is null and Resources/block0 could not be loaded.");
                return null;
            }
        }

        Vector3 spawnPosition = CalculateRandomSpawnPosition();
        GameObject newBlock = Instantiate(blockPrefab, spawnPosition, Quaternion.identity, spawnContainer);

        // Configure block attributes
        block0 b0 = newBlock.GetComponent<block0>();
        if (b0 != null)
        {
            b0._detached = true;
            b0.SetHits(UnityEngine.Random.Range(minHits, maxHits + 1));
        }

        // Ensure Rigidbody exists immediately (block0.prefab has no Rigidbody by default)
        Rigidbody rb = newBlock.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newBlock.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.mass = b0 != null && b0._hits > 0 ? b0._hits : 1f;
        }

        // Apply slight random 2D drift
        if (rb != null && initialDrift > 0f)
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.001f) randomDir = Vector2.up;
            float speed = UnityEngine.Random.Range(initialDrift * 0.5f, initialDrift);
            rb.linearVelocity = new Vector3(randomDir.x, randomDir.y, 0f) * speed;
        }

        activeBlocks.Add(newBlock);

        // Register with WaveManager if synced
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null)
        {
            wm.RegisterThreat(newBlock);
        }

        OnBlockSpawned?.Invoke(newBlock);
        OnActiveCountChanged?.Invoke(activeBlocks.Count);

        return newBlock;
    }

    /// <summary>
    /// Spawns one empty AsteroidGrid with only one core within the defined radial bounds.
    /// </summary>
    public virtual GameObject SpawnSingleAsteroid()
    {
        if (IsAsteroidAtCapacity) return null;

        if (asterPrefab == null)
        {
            asterPrefab = Resources.Load<GameObject>("AsteroidGrid");
            if (asterPrefab == null)
            {
                asterPrefab = Resources.Load<GameObject>("AsteroidBase");
            }
        }

        Vector3 spawnPosition = CalculateRandomSpawnPosition();
        GameObject newAsteroid = null;

        if (asterPrefab != null)
        {
            newAsteroid = Instantiate(asterPrefab, spawnPosition, Quaternion.identity, spawnContainer);
        }
        else
        {
            newAsteroid = new GameObject("AsteroidGrid");
            newAsteroid.transform.position = spawnPosition;
            newAsteroid.transform.SetParent(spawnContainer);
        }

        AsteroidGrid grid = newAsteroid.GetComponent<AsteroidGrid>();
        if (grid == null)
        {
            grid = newAsteroid.AddComponent<AsteroidGrid>();
        }

        // Configure AsteroidGrid: must be empty with only one core
        grid.targetBlockCount = 0;

        // Clean up any extra attached child blocks to guarantee only the core remains
        if (grid.coreBlock == null)
        {
            Transform coreT = newAsteroid.transform.Find("core_block");
            if (coreT != null)
            {
                grid.coreBlock = coreT.GetComponent<block0>();
            }
        }

        // If no core exists yet, generate authoritative core at (0, 0)
        if (grid.coreBlock == null)
        {
            GameObject cPrefab = blockPrefab != null ? blockPrefab : Resources.Load<GameObject>("block0");
            grid._block = cPrefab;
            grid.generate_asteroid(coremass: asteroidCoreHits, massmin: 0, massmax: 0);
        }
        else
        {
            grid.SetHits(asteroidCoreHits);
            grid.coreBlock.SetHits(asteroidCoreHits);
            grid.coreBlock.isCore = true;
            grid.coreBlock.gameObject.tag = "core";
        }

        // Remove any non-core block0 children to strictly guarantee single-core empty grid
        List<GameObject> extraChildren = new List<GameObject>();
        foreach (Transform child in newAsteroid.transform)
        {
            if (child == null) continue;
            if (grid.coreBlock != null && child.gameObject == grid.coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;
            if (child.name == "pfx_core" || child.GetComponent<ParticleSystem>() != null) continue;

            if (child.GetComponent<block0>() != null || child.name.StartsWith("b_"))
            {
                extraChildren.Add(child.gameObject);
            }
        }
        for (int i = 0; i < extraChildren.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(extraChildren[i]);
            else
                Destroy(extraChildren[i]);
#else
            Destroy(extraChildren[i]);
#endif
        }

        grid.SyncGrid();
        grid.UpdateMass();

        // Ensure Rigidbody exists for 2D physics simulation
        Rigidbody rb = newAsteroid.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newAsteroid.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.mass = asteroidCoreHits;
        }

        // Apply slight random 2D drift
        if (rb != null && initialDrift > 0f)
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.001f) randomDir = Vector2.up;
            float speed = UnityEngine.Random.Range(initialDrift * 0.5f, initialDrift);
            rb.linearVelocity = new Vector3(randomDir.x, randomDir.y, 0f) * speed;
        }

        activeAsteroids.Add(newAsteroid);

        // Register threat with WaveManager
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null)
        {
            wm.RegisterThreat(newAsteroid);
        }

        OnAsteroidSpawned?.Invoke(newAsteroid);
        OnActiveAsteroidCountChanged?.Invoke(activeAsteroids.Count);

        return newAsteroid;
    }

    /// <summary>
    /// Calculates a random position within the minRadius to radius donut on the XY plane.
    /// </summary>
    public virtual Vector3 CalculateRandomSpawnPosition()
    {
        ResolveCenterTarget();
        Vector3 center = centerTarget != null ? centerTarget.position : transform.position;

        // Random angle and distance between minRadius and radius
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distance = UnityEngine.Random.Range(Mathf.Min(minRadius, radius), Mathf.Max(minRadius, radius));

        float x = center.x + Mathf.Cos(angle) * distance;
        float y = center.y + Mathf.Sin(angle) * distance;
        float z = center.z; // Maintain 2D plane depth

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Removes null/destroyed blocks and asteroids from the active tracking lists.
    /// </summary>
    public void CleanDeadReferences()
    {
        int initialBlockCount = activeBlocks.Count;
        activeBlocks.RemoveAll(b => b == null);
        if (activeBlocks.Count != initialBlockCount)
        {
            OnActiveCountChanged?.Invoke(activeBlocks.Count);
        }

        int initialAsteroidCount = activeAsteroids.Count;
        activeAsteroids.RemoveAll(a => a == null);
        if (activeAsteroids.Count != initialAsteroidCount)
        {
            OnActiveAsteroidCountChanged?.Invoke(activeAsteroids.Count);
        }
    }

    /// <summary>
    /// Destroys all currently active spawned blocks and asteroids and resets the spawner tracking.
    /// </summary>
    public void ClearAllSpawned()
    {
        WaveManager wm = ActiveWaveManager;
        for (int i = 0; i < activeBlocks.Count; i++)
        {
            if (activeBlocks[i] != null)
            {
                if (syncWithWaveManager && wm != null)
                {
                    wm.UnregisterThreat(activeBlocks[i]);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(activeBlocks[i]);
                else
                    Destroy(activeBlocks[i]);
#else
                Destroy(activeBlocks[i]);
#endif
            }
        }
        activeBlocks.Clear();
        OnActiveCountChanged?.Invoke(0);

        for (int i = 0; i < activeAsteroids.Count; i++)
        {
            if (activeAsteroids[i] != null)
            {
                if (syncWithWaveManager && wm != null)
                {
                    wm.UnregisterThreat(activeAsteroids[i]);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(activeAsteroids[i]);
                else
                    Destroy(activeAsteroids[i]);
#else
                Destroy(activeAsteroids[i]);
#endif
            }
        }
        activeAsteroids.Clear();
        OnActiveAsteroidCountChanged?.Invoke(0);
    }

    private void EnsureWaveManagerSubscribed()
    {
        WaveManager current = syncWithWaveManager ? ActiveWaveManager : null;
        if (subscribedWm != current)
        {
            UnsubscribeWaveManager();
            subscribedWm = current;
            SubscribeWaveManager();
        }
    }

    private void SubscribeWaveManager()
    {
        if (subscribedWm != null)
        {
            subscribedWm.OnWaveStarted -= HandleWaveStarted;
            subscribedWm.OnWaveStarted += HandleWaveStarted;
            subscribedWm.OnWaveCompleted -= HandleWaveCompleted;
            subscribedWm.OnWaveCompleted += HandleWaveCompleted;
            subscribedWm.OnStateChanged -= HandleWaveStateChanged;
            subscribedWm.OnStateChanged += HandleWaveStateChanged;

            if (subscribedWm.CurrentWaveConfig != null)
            {
                HandleWaveStarted(subscribedWm.CurrentWaveIndex, subscribedWm.CurrentWaveConfig);
            }
        }
    }

    private void UnsubscribeWaveManager()
    {
        if (subscribedWm != null)
        {
            subscribedWm.OnWaveStarted -= HandleWaveStarted;
            subscribedWm.OnWaveCompleted -= HandleWaveCompleted;
            subscribedWm.OnStateChanged -= HandleWaveStateChanged;
            subscribedWm = null;
        }
    }

    private void HandleWaveStarted(int waveIndex, WaveDefinition config)
    {
        if (!syncWithWaveManager || config == null) return;

        if (adaptWavePacing && config.spawnInterval > 0f)
        {
            rate = Mathf.Max(0.1f, config.spawnInterval);
            asteroidRate = Mathf.Max(0.5f, config.spawnInterval * 2f);
        }

        if (adaptWaveDifficulty)
        {
            minHits = Mathf.Max(1, config.blockLevel);
            maxHits = Mathf.Max(minHits, config.shellLevel);
            asteroidCoreHits = Mathf.Max(1, config.minMass);
        }
    }

    private void HandleWaveCompleted(int waveIndex, WaveDefinition config)
    {
        if (clearOnWaveComplete)
        {
            ClearAllSpawned();
        }
    }

    private void HandleWaveStateChanged(WaveManager.WaveState oldState, WaveManager.WaveState newState)
    {
        if (newState == WaveManager.WaveState.Combat)
        {
            spawnTimer = rate;
            asteroidTimer = asteroidRate;
        }
    }

    private void ResolveCenterTarget()
    {
        if (centerTarget == null)
        {
            player p = FindAnyObjectByType<player>();
            if (p != null)
            {
                centerTarget = p.transform;
            }
            else
            {
                centerTarget = transform;
            }
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 center = centerTarget != null ? centerTarget.position : transform.position;

        // Draw max radius
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        DrawWireCircle(center, radius);

        // Draw min radius
        if (minRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            DrawWireCircle(center, minRadius);
        }
    }

    private void DrawWireCircle(Vector3 center, float r)
    {
        int segments = 32;
        float step = (Mathf.PI * 2f) / segments;
        Vector3 prev = center + new Vector3(r, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float theta = i * step;
            Vector3 next = center + new Vector3(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}

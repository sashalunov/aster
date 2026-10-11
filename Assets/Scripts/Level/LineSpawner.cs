using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns blocks and asteroids along a line segment in 2D gameplay space (XY plane, Z=0)
/// and imparts an initial directional impulse to launched entities.
/// Supports configurable pacing, limits, spread angles, wave integration, and visual editor gizmos.
/// </summary>
[DisallowMultipleComponent]
public class LineSpawner : MonoBehaviour
{
    [Header("Line Geometry")]
    [Tooltip("Start point of the spawn line in local space (or world space if useWorldSpace is true).")]
    public Vector3 lineStart = new Vector3(-15f, 15f, 0f);

    [Tooltip("End point of the spawn line in local space (or world space if useWorldSpace is true).")]
    public Vector3 lineEnd = new Vector3(15f, 15f, 0f);

    [Tooltip("If true, lineStart and lineEnd coordinates are treated as world-space coordinates.")]
    public bool useWorldSpace = false;

    [Tooltip("Optional transform defining the start position. Overrides lineStart when assigned.")]
    public Transform startAnchor;

    [Tooltip("Optional transform defining the end position. Overrides lineEnd when assigned.")]
    public Transform endAnchor;

    [Header("Impulse & Dynamics")]
    [Tooltip("Base direction vector of the launch impulse.")]
    public Vector3 impulseDirection = Vector3.down;

    [Tooltip("If true, impulseDirection is transformed by this GameObject's local rotation.")]
    public bool useLocalDirection = false;

    [Tooltip("Random angular dispersion cone in degrees (+/- half on the XY plane).")]
    [Range(0f, 180f)]
    public float spreadAngle = 15f;

    [Tooltip("Optional random rotational torque applied to spawned entities around the Z axis.")]
    [Min(0f)]
    public float torqueVariance = 2f;

    [Tooltip("Physics force mode used when applying launch impulse.")]
    public ForceMode impulseMode = ForceMode.Impulse;

    [Header("Block Impulse Settings")]
    [Tooltip("Magnitude of the initial launch impulse force applied to spawned blocks.")]
    [Min(0f)]
    public float blockImpulseForce = 5f;

    [Tooltip("Random variance added or subtracted from blockImpulseForce.")]
    [Min(0f)]
    public float blockImpulseVariance = 1f;

    [Header("Asteroid Impulse Settings")]
    [Tooltip("Magnitude of the initial launch impulse force applied to spawned asteroids.")]
    [Min(0f)]
    public float asteroidImpulseForce = 15f;

    [Tooltip("Random variance added or subtracted from asteroidImpulseForce.")]
    [Min(0f)]
    public float asteroidImpulseVariance = 2f;

    [Header("Prefabs & Hierarchy")]
    [Tooltip("Block prefab instantiated. Defaults to PrefabId.Block0 from PrefabManager.")]
    public GameObject blockPrefab;

    [Tooltip("Asteroid prefab instantiated. Defaults to PrefabId.AsteroidGrid or AsteroidBase.")]
    public GameObject asterPrefab;

    [Tooltip("Parent container transform for spawned objects. Defaults to this transform.")]
    public Transform spawnContainer;

    [Header("Block Spawning Settings")]
    [Tooltip("Whether to spawn blocks periodically.")]
    public bool spawnBlocks = true;

    [Tooltip("Maximum concurrent active blocks allowed from this spawner.")]
    [Min(1)]
    public int maxBlocks = 20;

    [Tooltip("Interval in seconds between block spawn attempts.")]
    [Min(0.01f)]
    public float blockRate = 1.2f;

    [Tooltip("Number of blocks spawned per interval tick.")]
    [Range(1, 10)]
    public int blockBatchSize = 1;

    [Tooltip("Initial number of blocks spawned immediately on start.")]
    public int initialBlockCount = 0;

    [Tooltip("Minimum hit points assigned to spawned blocks.")]
    [Min(1)]
    public int minBlockHits = 1;

    [Tooltip("Maximum hit points assigned to spawned blocks.")]
    [Min(1)]
    public int maxBlockHits = 3;

    [Header("Asteroid Spawning Settings")]
    [Tooltip("Whether to spawn asteroids periodically.")]
    public bool spawnAsteroids = true;

    [Tooltip("Maximum concurrent active asteroids allowed from this spawner.")]
    [Min(1)]
    public int maxAsteroids = 5;

    [Tooltip("Interval in seconds between asteroid spawn attempts.")]
    [Min(0.01f)]
    public float asteroidRate = 3.5f;

    [Tooltip("Number of asteroids spawned per interval tick.")]
    [Range(1, 10)]
    public int asteroidBatchSize = 1;

    [Tooltip("Initial number of asteroids spawned immediately on start.")]
    public int initialAsteroidCount = 0;

    [Tooltip("Hit points assigned to the core of spawned asteroids.")]
    [Min(1)]
    public int asteroidCoreHits = 3;

    [Tooltip("Threat budget points consumed when spawning an asteroid.")]
    [Min(1)]
    public int asteroidThreatCost = 2;

    [Tooltip("If true, procedurally generates extra blocks around the core.")]
    public bool generateAsteroidCluster = false;

    [Tooltip("Minimum extra blocks generated if generateAsteroidCluster is enabled.")]
    public int minClusterBlocks = 0;

    [Tooltip("Maximum extra blocks generated if generateAsteroidCluster is enabled.")]
    public int maxClusterBlocks = 4;

    [Header("Asteroid Shell Configuration")]
    [Tooltip("Whether spawned asteroids generate a protective shell of blocks around their core.")]
    public bool spawnAsteroidShell = false;

    [Tooltip("Thickness or layer count of the protective shell around the core (0 = 4-block diamond/cross shell, 1 = 8-block full ring, >1 = additional concentric layers).")]
    [Range(0, 5)]
    public int shellThickness = 1;

    [Tooltip("Minimum hit points assigned to shell blocks.")]
    [Min(1)]
    public int minShellHits = 2;

    [Tooltip("Maximum hit points assigned to shell blocks.")]
    [Min(1)]
    public int maxShellHits = 2;

    [Tooltip("Optional custom prefab used for shell blocks. If unassigned, defaults to blockPrefab.")]
    public GameObject shellBlockPrefab;

    [Header("General Spawner Control")]
    [Tooltip("Whether the spawner is actively running.")]
    public bool isSpawning = true;

    [Tooltip("Linear damping applied to the Rigidbody of spawned objects.")]
    public float entityLinearDamping = 0.0f;

    [Tooltip("Angular damping applied to the Rigidbody of spawned objects.")]
    public float entityAngularDamping = 0.05f;

    [Header("Clearance & Overlap Detection")]
    [Tooltip("Whether to verify that the candidate spawn location is clear of existing blocks and solid obstacles before spawning.")]
    public bool checkOverlapBeforeSpawn = true;

    [Tooltip("Radius around the candidate spawn position checked for collisions when spawning a block.")]
    [Min(0.1f)]
    public float blockClearanceRadius = 1.0f;

    [Tooltip("Radius around the candidate spawn position checked for collisions when spawning an asteroid.")]
    [Min(0.1f)]
    public float asteroidClearanceRadius = 2.0f;

    [Tooltip("Maximum retry attempts to find an unblocked position along the line before aborting the spawn attempt.")]
    [Range(1, 30)]
    public int maxSpawnPlacementAttempts = 10;

    [Tooltip("LayerMask of solid obstacles tested during spawn clearance checks.")]
    public LayerMask spawnObstacleMask = ~0;

    [Header("Wave Manager Integration")]
    [Tooltip("Optional explicit reference to WaveManager. Auto-resolves if null.")]
    [SerializeField] private WaveManager _waveManager;

    [Tooltip("Whether to register spawned objects as threats and synchronize with WaveManager.")]
    public bool syncWithWaveManager = true;

    [Tooltip("If true, only spawns when WaveManager is actively in Combat state.")]
    public bool spawnDuringCombatOnly = false;

    [Tooltip("If true, adapts block hit points, asteroid core hits, and shell configuration from active WaveDefinition.")]
    public bool adaptWaveDifficulty = false;

    // Events
    public event Action<GameObject> OnBlockSpawned;
    public event Action<GameObject> OnAsteroidSpawned;
    public event Action<GameObject, List<GameObject>> OnAsteroidShellGenerated;
    public event Action<int> OnActiveCountChanged;

    // Active object tracking
    protected readonly List<GameObject> activeBlocks = new List<GameObject>();
    protected readonly List<GameObject> activeAsteroids = new List<GameObject>();

    // Internal timing
    private float blockTimer = 0f;
    private float asteroidTimer = 0f;

    // Public accessors
    public Vector3 WorldStart => startAnchor != null ? startAnchor.position : (useWorldSpace ? lineStart : transform.TransformPoint(lineStart));
    public Vector3 WorldEnd => endAnchor != null ? endAnchor.position : (useWorldSpace ? lineEnd : transform.TransformPoint(lineEnd));
    public int ActiveBlockCount => activeBlocks.Count;
    public int ActiveAsteroidCount => activeAsteroids.Count;
    public int TotalActiveCount => activeBlocks.Count + activeAsteroids.Count;
    public bool IsBlockAtCapacity => activeBlocks.Count >= maxBlocks;
    public bool IsAsteroidAtCapacity => activeAsteroids.Count >= maxAsteroids;

    public bool generateAsteroidShell
    {
        get => spawnAsteroidShell;
        set => spawnAsteroidShell = value;
    }

    public bool spawnShell
    {
        get => spawnAsteroidShell;
        set => spawnAsteroidShell = value;
    }

    public int asteroidShellThickness
    {
        get => shellThickness;
        set => shellThickness = value;
    }

    public int shellHits
    {
        get => maxShellHits;
        set
        {
            minShellHits = value;
            maxShellHits = value;
        }
    }

    public int asteroidShellHits
    {
        get => maxShellHits;
        set
        {
            minShellHits = value;
            maxShellHits = value;
        }
    }

    public GameObject shellPrefab
    {
        get => shellBlockPrefab;
        set => shellBlockPrefab = value;
    }

    private WaveManager subscribedWm;

    public WaveManager ActiveWaveManager
    {
        get
        {
            if (_waveManager == null)
            {
                _waveManager = WaveManager.Instance != null ? WaveManager.Instance : FindAnyObjectByType<WaveManager>();
            }
            return _waveManager;
        }
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

    protected virtual void Awake()
    {
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

    public void EnsureWaveManagerSubscribed()
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
            subscribedWm = null;
        }
    }

    public virtual void HandleWaveStarted(int waveIndex, WaveDefinition config)
    {
        if (!syncWithWaveManager || config == null) return;

        if (adaptWaveDifficulty)
        {
            minBlockHits = Mathf.Max(1, config.blockLevel);
            maxBlockHits = Mathf.Max(minBlockHits, config.shellLevel);
            asteroidCoreHits = Mathf.Max(1, config.minMass);
            minShellHits = Mathf.Max(1, config.shellLevel);
            maxShellHits = Mathf.Max(minShellHits, config.shellLevel);
        }
    }

    protected virtual void Start()
    {
        EnsureWaveManagerSubscribed();
        ResolvePrefabs();

        if (initialBlockCount > 0 && spawnBlocks)
        {
            for (int i = 0; i < initialBlockCount; i++)
            {
                if (IsBlockAtCapacity) break;
                SpawnSingleBlock();
            }
        }

        if (initialAsteroidCount > 0 && spawnAsteroids)
        {
            for (int i = 0; i < initialAsteroidCount; i++)
            {
                if (IsAsteroidAtCapacity) break;
                SpawnSingleAsteroid();
            }
        }
    }

    protected virtual void Update()
    {
        CleanupDestroyedEntities();
        EnsureWaveManagerSubscribed();

        if (!isSpawning) return;

        // Check combat-only constraint
        if (spawnDuringCombatOnly && syncWithWaveManager)
        {
            WaveManager wm = ActiveWaveManager;
            if (wm != null && wm.State != WaveManager.WaveState.Combat)
            {
                return;
            }
        }

        float dt = Time.deltaTime;

        // Block spawning loop
        if (spawnBlocks && !IsBlockAtCapacity)
        {
            blockTimer += dt;
            if (blockTimer >= Mathf.Max(0.01f, blockRate))
            {
                blockTimer = 0f;
                int batch = Mathf.Min(blockBatchSize, maxBlocks - activeBlocks.Count);
                for (int i = 0; i < batch; i++)
                {
                    if (IsBlockAtCapacity) break;
                    SpawnSingleBlock();
                }
            }
        }

        // Asteroid spawning loop
        if (spawnAsteroids && !IsAsteroidAtCapacity)
        {
            asteroidTimer += dt;
            if (asteroidTimer >= Mathf.Max(0.01f, asteroidRate))
            {
                asteroidTimer = 0f;
                int batch = Mathf.Min(asteroidBatchSize, maxAsteroids - activeAsteroids.Count);
                for (int i = 0; i < batch; i++)
                {
                    if (IsAsteroidAtCapacity) break;
                    SpawnSingleAsteroid();
                }
            }
        }
    }

    /// <summary>
    /// Resolves default block and asteroid prefabs from PrefabManager if unassigned.
    /// </summary>
    public virtual void ResolvePrefabs()
    {
        if (blockPrefab == null)
        {
            blockPrefab = PrefabManager.Get(PrefabId.BlockAsteroid);
            if (blockPrefab == null)
            {
                blockPrefab = PrefabManager.Get(PrefabId.Block0);
            }
        }

        if (asterPrefab == null)
        {
            asterPrefab = PrefabManager.Get(PrefabId.AsteroidGrid);
            if (asterPrefab == null)
            {
                asterPrefab = PrefabManager.Get(PrefabId.AsteroidBase);
            }
        }

        if (shellBlockPrefab == null)
        {
            shellBlockPrefab = blockPrefab;
        }
    }

    /// <summary>
    /// Computes a random point along the configured line segment on the XY gameplay plane (Z=0).
    /// </summary>
    public virtual Vector3 CalculateRandomSpawnPosition()
    {
        float t = UnityEngine.Random.value;
        Vector3 pos = Vector3.Lerp(WorldStart, WorldEnd, t);
        pos.z = 0f; // Constrain to 2D play plane
        return pos;
    }

    /// <summary>
    /// Calculates the normalized impulse direction incorporating optional local rotation and angular spread.
    /// </summary>
    public virtual Vector3 GetCalculatedImpulseDirection()
    {
        Vector3 baseDir = GetBaseImpulseDirection();

        if (spreadAngle > 0f)
        {
            float halfAngle = spreadAngle * 0.5f;
            float randomAngle = UnityEngine.Random.Range(-halfAngle, halfAngle);
            baseDir = Quaternion.Euler(0f, 0f, randomAngle) * baseDir;
        }

        baseDir.z = 0f;
        return baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector3.down;
    }

    /// <summary>
    /// Resolves the base unspread impulse direction vector.
    /// </summary>
    public virtual Vector3 GetBaseImpulseDirection()
    {
        Vector3 dir = impulseDirection.sqrMagnitude > 0.0001f ? impulseDirection.normalized : Vector3.down;

        if (useLocalDirection)
        {
            dir = transform.TransformDirection(dir);
        }

        dir.z = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.down;
    }

    /// <summary>
    /// Checks if a candidate position is free of solid colliders (blocks, asteroids, terrain, vessels).
    /// Ignores triggers (such as sensor zones or trigger zones).
    /// </summary>
    public virtual bool IsPositionClear(Vector3 position, float radius)
    {
        if (!checkOverlapBeforeSpawn) return true;

        Collider[] hits = Physics.OverlapSphere(position, radius, spawnObstacleMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return true;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.isTrigger) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to find a clear, non-overlapping spawn position along the line segment.
    /// Returns true if a clear position was found, or false if all attempts were obstructed.
    /// </summary>
    public virtual bool TryGetClearSpawnPosition(float clearanceRadius, out Vector3 clearPosition)
    {
        int attempts = Mathf.Max(1, maxSpawnPlacementAttempts);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector3 candidate = CalculateRandomSpawnPosition();
            if (IsPositionClear(candidate, clearanceRadius))
            {
                clearPosition = candidate;
                return true;
            }
        }

        clearPosition = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Spawns a single block at a random point along the line and applies the configured initial impulse.
    /// </summary>
    public virtual GameObject SpawnSingleBlock()
    {
        if (IsBlockAtCapacity) return null;

        if (!TryGetClearSpawnPosition(blockClearanceRadius, out Vector3 spawnPos))
        {
            // Candidate line segments obstructed; defer spawn to avoid overlapping other blocks
            return null;
        }

        return SpawnBlockAt(spawnPos);
    }

    /// <summary>
    /// Spawns a block at an explicit world position and applies the directional impulse.
    /// </summary>
    public virtual GameObject SpawnBlockAt(Vector3 position, Vector3? customDirection = null, float? customForce = null)
    {
        ResolvePrefabs();
        if (blockPrefab == null)
        {
            Debug.LogWarning("[LineSpawner] Cannot spawn block: blockPrefab is null.");
            return null;
        }

        position.z = 0f;
        GameObject newBlock = Instantiate(blockPrefab, position, Quaternion.identity, spawnContainer);
        newBlock.transform.localScale = Vector3.one;

        // Configure BlockBase attributes
        BlockBase b0 = newBlock.GetComponent<BlockBase>();
        if (b0 != null)
        {
            b0._detached = true;
            b0.SetHits(UnityEngine.Random.Range(minBlockHits, maxBlockHits + 1));
            b0._level = Mathf.Max(1, b0._level);
        }

        // Configure 2D physics Rigidbody
        Rigidbody rb = newBlock.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newBlock.AddComponent<Rigidbody>();
        }

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.useGravity = false;
        rb.linearDamping = entityLinearDamping;
        rb.angularDamping = entityAngularDamping;
        rb.mass = b0 != null && b0._hits > 0 ? b0._hits : 1f;

        // Apply directional launch impulse
        ApplyInitialImpulse(rb, customDirection, customForce, blockImpulseForce, blockImpulseVariance);

        activeBlocks.Add(newBlock);

        // WaveManager threat registration
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null)
        {
            wm.RegisterThreat(newBlock);
        }

        OnBlockSpawned?.Invoke(newBlock);
        OnActiveCountChanged?.Invoke(TotalActiveCount);

        return newBlock;
    }

    /// <summary>
    /// Computes clearance radius required for spawning an asteroid, accounting for shell thickness when enabled.
    /// </summary>
    public virtual float GetAsteroidClearanceRadius() =>
        spawnAsteroidShell ? Mathf.Max(asteroidClearanceRadius, 1.2f + shellThickness) : asteroidClearanceRadius;

    /// <summary>
    /// Spawns a single asteroid at a random point along the line and applies the configured initial impulse.
    /// </summary>
    public virtual GameObject SpawnSingleAsteroid()
    {
        if (IsAsteroidAtCapacity) return null;

        float clearance = GetAsteroidClearanceRadius();
        if (!TryGetClearSpawnPosition(clearance, out Vector3 spawnPos))
        {
            // Candidate line segments obstructed; defer spawn to avoid overlapping other entities
            return null;
        }

        return SpawnAsteroidAt(spawnPos);
    }

    /// <summary>
    /// Spawns an asteroid at an explicit world position and applies the directional impulse.
    /// Supports optional overrides for impulse direction/force, as well as shell generation parameters.
    /// </summary>
    public virtual GameObject SpawnAsteroidAt(
        Vector3 position,
        Vector3? customDirection = null,
        float? customForce = null,
        bool? overrideSpawnShell = null,
        int? overrideShellThickness = null,
        int? overrideShellHits = null)
    {
        ResolvePrefabs();

        position.z = 0f;
        GameObject newAsteroid = null;

        if (asterPrefab != null)
        {
            newAsteroid = Instantiate(asterPrefab, position, Quaternion.identity, spawnContainer);
        }
        else
        {
            newAsteroid = new GameObject("AsteroidGrid");
            newAsteroid.transform.position = position;
            newAsteroid.transform.SetParent(spawnContainer);
        }

        AsteroidGrid grid = newAsteroid.GetComponent<AsteroidGrid>();
        if (grid == null)
        {
            grid = newAsteroid.AddComponent<AsteroidGrid>();
        }

        bool shouldSpawnShell = overrideSpawnShell.HasValue ? overrideSpawnShell.Value : spawnAsteroidShell;
        int activeThickness = overrideShellThickness.HasValue ? overrideShellThickness.Value : shellThickness;
        int minHits = overrideShellHits.HasValue ? overrideShellHits.Value : minShellHits;
        int maxHits = overrideShellHits.HasValue ? overrideShellHits.Value : maxShellHits;

        // Configure AsteroidGrid structure
        if (generateAsteroidCluster)
        {
            grid.targetBlockCount = UnityEngine.Random.Range(minClusterBlocks, maxClusterBlocks + 1);
            GameObject cPrefab = blockPrefab != null ? blockPrefab : PrefabManager.Get(PrefabId.Block0);
            grid._blockPrefab = cPrefab;
            grid.generate_asteroid(coremass: asteroidCoreHits, massmin: minClusterBlocks, massmax: maxClusterBlocks);

            if (shouldSpawnShell)
            {
                SpawnAsteroidShell(grid, activeThickness, minHits, maxHits, shellBlockPrefab);
            }
        }
        else
        {
            grid.targetBlockCount = 0;

            EnsureAsteroidCore(grid, asteroidCoreHits);
            CleanUpNonCoreChildren(newAsteroid, grid.coreBlock);

            if (shouldSpawnShell)
            {
                SpawnAsteroidShell(grid, activeThickness, minHits, maxHits, shellBlockPrefab);
            }

            grid.SyncGrid();
            grid.UpdateMass();
        }

        // Configure 2D physics Rigidbody
        Rigidbody rb = newAsteroid.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newAsteroid.AddComponent<Rigidbody>();
        }

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.useGravity = false;
        rb.linearDamping = entityLinearDamping;
        rb.angularDamping = entityAngularDamping;
        rb.mass = Mathf.Max(1f, rb.mass > 0 ? rb.mass : asteroidCoreHits);

        // Apply directional launch impulse
        ApplyInitialImpulse(rb, customDirection, customForce, asteroidImpulseForce, asteroidImpulseVariance);

        activeAsteroids.Add(newAsteroid);

        // WaveManager threat registration
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null)
        {
            wm.RegisterThreat(newAsteroid);
        }

        OnAsteroidSpawned?.Invoke(newAsteroid);
        OnActiveCountChanged?.Invoke(TotalActiveCount);

        return newAsteroid;
    }

    /// <summary>
    /// Computes relative 2D grid coordinates for a concentric shell around the origin (0, 0).
    /// Thickness 0 returns 4 cardinal/diamond points. Thickness >= 1 returns concentric rings up to thickness.
    /// </summary>
    public static List<Vector2Int> GetShellCoordinates(int thickness)
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        if (thickness < 0) return coords;

        if (thickness == 0)
        {
            coords.Add(new Vector2Int(0, 1));
            coords.Add(new Vector2Int(1, 0));
            coords.Add(new Vector2Int(0, -1));
            coords.Add(new Vector2Int(-1, 0));
            return coords;
        }

        for (int r = 1; r <= thickness; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == r)
                    {
                        coords.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        return coords;
    }

    /// <summary>
    /// Spawns a concentric protective shell around the core of the specified AsteroidGrid.
    /// </summary>
    public virtual List<GameObject> SpawnAsteroidShell(
        AsteroidGrid grid,
        int thickness,
        int minHits,
        int maxHits,
        GameObject customPrefab = null)
    {
        List<GameObject> shellBlocks = new List<GameObject>();
        if (grid == null) return shellBlocks;

        EnsureAsteroidCore(grid, asteroidCoreHits);

        GameObject pFab = customPrefab != null
            ? customPrefab
            : (shellBlockPrefab != null
                ? shellBlockPrefab
                : (blockPrefab != null ? blockPrefab : PrefabManager.Get(PrefabId.Block0)));
        if (pFab == null) return shellBlocks;

        GameObject prevBlockPrefab = grid._blockPrefab;
        grid._blockPrefab = pFab;

        List<Vector2Int> coords = GetShellCoordinates(thickness);
        for (int i = 0; i < coords.Count; i++)
        {
            Vector2Int coord = coords[i];
            if (coord == Vector2Int.zero) continue;

            int shellHitPoints = UnityEngine.Random.Range(minHits, maxHits + 1);

            // If a block already exists at this coordinate, update its hit points
            if (grid.HasBlockAt(coord))
            {
                GameObject existingBlock = grid.GetBlockAt(coord);
                if (existingBlock == null)
                {
                    Transform t = grid.transform.Find($"b_{coord.x}_{coord.y}");
                    if (t != null) existingBlock = t.gameObject;
                }

                if (existingBlock != null)
                {
                    BlockBase b = existingBlock.GetComponent<BlockBase>();
                    if (b != null && !b.isCore)
                    {
                        b.SetHits(shellHitPoints);
                        shellBlocks.Add(existingBlock);
                    }
                }
                continue;
            }

            GameObject newShellBlock = grid.SpawnBlockAtCoord(coord, shellHitPoints);
            if (newShellBlock != null)
            {
                shellBlocks.Add(newShellBlock);
            }
        }

        grid._blockPrefab = prevBlockPrefab;
        grid.SyncGrid();
        grid.UpdateMass();

        OnAsteroidShellGenerated?.Invoke(grid.gameObject, shellBlocks);
        return shellBlocks;
    }

    /// <summary>
    /// Ensures that an authoritative BlockAsteroidCore component exists on the grid at (0, 0) with specified hit points.
    /// </summary>
    public virtual void EnsureAsteroidCore(AsteroidGrid grid, int hits)
    {
        if (grid == null) return;

        if (grid.coreBlock == null)
        {
            Transform coreT = grid.transform.Find("core_block");
            if (coreT != null)
            {
                grid.coreBlock = coreT.GetComponent<BlockAsteroidCore>();
            }
            if (grid.coreBlock == null)
            {
                grid.EnsureCoreBlock();
            }
        }

        if (grid.coreBlock == null)
        {
            GameObject cPrefab = blockPrefab != null ? blockPrefab : PrefabManager.Get(PrefabId.Block0);
            grid._blockPrefab = cPrefab;
            grid.generate_asteroid(coremass: hits, massmin: 0, massmax: 0);
        }
        else
        {
            grid.SetHits(hits);
            grid.coreBlock.SetHits(hits);
            grid.coreBlock.isCore = true;
            grid.coreBlock.gameObject.tag = "core";
        }
    }

    /// <summary>
    /// Removes non-core child blocks from an asteroid to ensure a clean base before generating shells.
    /// </summary>
    public virtual void CleanUpNonCoreChildren(GameObject asteroidObj, BlockAsteroidCore coreBlock)
    {
        if (asteroidObj == null) return;

        List<GameObject> extraChildren = new List<GameObject>();
        foreach (Transform child in asteroidObj.transform)
        {
            if (child == null) continue;
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block" || child.name == "pfx_core" || child.GetComponent<ParticleSystem>() != null) continue;

            if (child.GetComponent<BlockBase>() != null || child.name.StartsWith("b_"))
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
    }

    /// <summary>
    /// Applies directional launch impulse and optional torque to a Rigidbody.
    /// </summary>
    protected virtual void ApplyInitialImpulse(
        Rigidbody rb,
        Vector3? customDirection = null,
        float? customForce = null,
        float defaultForce = 5f,
        float forceVariance = 0f)
    {
        if (rb == null) return;

        Vector3 dir = customDirection.HasValue ? customDirection.Value.normalized : GetCalculatedImpulseDirection();
        float force = customForce.HasValue ? customForce.Value : defaultForce;

        if (!customForce.HasValue && forceVariance > 0f)
        {
            force += UnityEngine.Random.Range(-forceVariance, forceVariance);
            force = Mathf.Max(0.01f, force);
        }

        rb.AddForce(dir * force, impulseMode);

        if (torqueVariance > 0f)
        {
            float torque = UnityEngine.Random.Range(-torqueVariance, torqueVariance);
            rb.AddTorque(Vector3.forward * torque, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Cleans up null or destroyed references from the tracking lists.
    /// </summary>
    protected virtual void CleanupDestroyedEntities()
    {
        int initialTotal = TotalActiveCount;

        activeBlocks.RemoveAll(b => b == null);
        activeAsteroids.RemoveAll(a => a == null);

        if (TotalActiveCount != initialTotal)
        {
            OnActiveCountChanged?.Invoke(TotalActiveCount);
        }
    }

    /// <summary>
    /// Immediately destroys all active entities spawned by this spawner and clears tracking.
    /// </summary>
    public virtual void ClearAll()
    {
        foreach (var b in activeBlocks)
        {
            if (b != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(b);
                else
                    Destroy(b);
#else
                Destroy(b);
#endif
            }
        }
        activeBlocks.Clear();

        foreach (var a in activeAsteroids)
        {
            if (a != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(a);
                else
                    Destroy(a);
#else
                Destroy(a);
#endif
            }
        }
        activeAsteroids.Clear();

        OnActiveCountChanged?.Invoke(0);
    }

    public void StartSpawning() => isSpawning = true;
    public void StopSpawning() => isSpawning = false;
    public void ToggleSpawning() => isSpawning = !isSpawning;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawGizmoLine(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmoLine(true);
    }

    private void DrawGizmoLine(bool selected)
    {
        Vector3 start = WorldStart;
        Vector3 end = WorldEnd;

        // Draw line segment
        Gizmos.color = selected
            ? (isSpawning ? Color.cyan : Color.yellow)
            : (isSpawning ? new Color(0.2f, 0.8f, 1f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.5f));

        Gizmos.DrawLine(start, end);

        // Draw endpoint anchors
        Gizmos.DrawWireSphere(start, selected ? 0.6f : 0.4f);
        Gizmos.DrawWireSphere(end, selected ? 0.6f : 0.4f);

        // Draw impulse direction rays along the line
        Vector3 baseDir = GetBaseImpulseDirection();
        float representativeForce = spawnAsteroids ? asteroidImpulseForce : blockImpulseForce;
        float rayLength = Mathf.Clamp(representativeForce * 0.4f, 1.5f, 8f);

        Gizmos.color = selected ? Color.yellow : new Color(1f, 0.9f, 0.2f, 0.6f);
        int sampleCount = 5;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);
            Vector3 pt = Vector3.Lerp(start, end, t);
            Gizmos.DrawRay(pt, baseDir * rayLength);

            // Draw small arrowhead
            if (selected)
            {
                Vector3 tip = pt + baseDir * rayLength;
                Vector3 right = Quaternion.Euler(0f, 0f, 150f) * baseDir * 0.5f;
                Vector3 left = Quaternion.Euler(0f, 0f, -150f) * baseDir * 0.5f;
                Gizmos.DrawRay(tip, right);
                Gizmos.DrawRay(tip, left);
            }
        }

        // Draw spread cone boundaries from center of line if selected
        if (selected && spreadAngle > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Vector3 mid = Vector3.Lerp(start, end, 0.5f);
            Vector3 coneLeft = Quaternion.Euler(0f, 0f, spreadAngle * 0.5f) * baseDir * rayLength;
            Vector3 coneRight = Quaternion.Euler(0f, 0f, -spreadAngle * 0.5f) * baseDir * rayLength;
            Gizmos.DrawRay(mid, coneLeft);
            Gizmos.DrawRay(mid, coneRight);
        }
    }
#endif
}

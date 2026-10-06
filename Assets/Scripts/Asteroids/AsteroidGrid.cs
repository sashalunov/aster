using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// High-performance compound asteroid managing multiple block components via an in-memory 2D grid.
/// Eliminates physics raycast bottlenecks with BFS connectivity checks, supports procedural fractal/math
/// generation algorithms, and provides dynamic block detachment and re-attachment around an authoritative <see cref="BlockAsteroidCore"/>.
/// </summary>
public class AsteroidGrid : AsteroidBase
{
    public enum GenerationAlgorithm
    {
        JuliaFractal,           // Fractal boundary crags using complex polynomial iterations
        HarmonicRose,           // Polar harmonic curve (superformula / multi-lobed space crystal)
        DiffusionAggregation,   // Accretion model: Brownian walkers freeze onto adjacent neighbors
        ClassicShell            // In-memory version of legacy concentric shell generation
    }

    [Header("Procedural Math & Fractal Settings")]
    [Tooltip("Algorithm used to procedurally sculpt the asteroid block cluster.")]
    public GenerationAlgorithm algorithm = GenerationAlgorithm.HarmonicRose;

    [Tooltip("Target block count for procedural generation.")]
    [Range(1, 48)]
    public int targetBlockCount = 12;

    [Tooltip("Grid bounding radius (max distance in blocks from core).")]
    [Range(2, 8)]
    public int gridRadius = 4;

    [Header("Re-attachment & Accretion Settings")]
    [Tooltip("Allow floating detached blocks to re-attach upon collision.")]
    public bool allowReattachmentOnCollision = true;

    [Tooltip("Max relative impact velocity allowed to re-attach (too fast will bounce/damage instead).")]
    public float maxReattachImpactVelocity = 5.0f;

    [Tooltip("If true, magnetically pulls standalone blocks toward empty adjacent grid sockets to grow.")]
    public bool magneticAccretion = false;

    [Tooltip("Radius around asteroid to magnetically detect and pull standalone blocks.")]
    public float accretionRadius = 6.0f;

    [Tooltip("Force magnitude applied to pull standalone blocks inward.")]
    public float accretionPullForce = 12.0f;

    [Tooltip("Distance threshold at which a pulled block snaps into the vacant grid socket.")]
    public float attachDistance = 1.4f;

    /// <summary>
    /// Legacy alias for _max_blocks capacity limit.
    /// </summary>
    public int maxAccretionBlocks
    {
        get => _max_blocks;
        set => _max_blocks = value;
    }

    /// <summary>
    /// Current count of blocks in the grid.
    /// </summary>
    public override int CurrentBlockCount => gridBlocks.Count;

    /// <summary>
    /// True if the asteroid has reached its maximum block attachment limit.
    /// </summary>
    public override bool IsAtCapacity => _max_blocks > 0 && gridBlocks.Count >= _max_blocks;

    /// <summary>
    /// Property controlling whether the asteroid magnetically attracts standalone blocks and grows.
    /// </summary>
    public bool MagneticAccretion
    {
        get => magneticAccretion;
        set => magneticAccretion = value;
    }

    [Header("Simulation LOD")]
    [Tooltip("Puts Rigidbody to sleep when far from the player to conserve PhysX cycles for thousands of asteroids.")]
    public bool enableSimulationLOD = true;
    public float sleepDistance = 50f;

    // In-memory 2D Grid representations
    private readonly Dictionary<Vector2Int, GameObject> gridBlocks = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<GameObject, Vector2Int> blockToCoord = new Dictionary<GameObject, Vector2Int>();

    // Slot reservations for in-flight accretion (prevents multiple blocks targeting the same socket)
    private readonly Dictionary<Vector2Int, GameObject> reservedSlots = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<GameObject, Vector2Int> blockReservations = new Dictionary<GameObject, Vector2Int>();
    private readonly HashSet<GameObject> processedAccretionInFrame = new HashSet<GameObject>();

    // Reusable BFS structures to prevent GC allocations
    private readonly Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>(64);
    private readonly HashSet<Vector2Int> bfsReachable = new HashSet<Vector2Int>(64);

    // Adjacent 8-way and 4-way offsets in grid coordinates
    private static readonly Vector2Int[] NeighborOffsets8 = {
        new Vector2Int(0, 1),   new Vector2Int(1, 1),   new Vector2Int(1, 0),   new Vector2Int(1, -1),
        new Vector2Int(0, -1),  new Vector2Int(-1, -1), new Vector2Int(-1, 0),  new Vector2Int(-1, 1)
    };

    private static readonly Vector2Int[] NeighborOffsets4 = {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };

    // Cached resource prefabs
    private static GameObject cachedCoreHitFx;
    private static GameObject cachedBlockDestroyFx;
    private static GameObject cachedShieldFx;
    private static GameObject cachedPowerupFx;
    private static GameObject cachedAdditiveBonusFx;
    private static bool resourcesCached = false;

    // Events
    public event Action<GameObject, Vector2Int> OnBlockDetached;
    public event Action<GameObject, Vector2Int> OnBlockReattached;

    // Public Grid Accessors
    public int ActiveBlockCount => gridBlocks.Count;
    public bool HasBlockAt(Vector2Int coord) => gridBlocks.ContainsKey(coord);

    protected override void Awake()
    {
        base.Awake();
        EnsureCachedResources();
        if (_blockPrefab == null)
        {
            _blockPrefab = GetBlockPrefab();
        }
        RegisterExistingChildrenIntoGrid();
    }

    private void EnsureCachedResources()
    {
        if (resourcesCached) return;

        cachedCoreHitFx = PrefabManager.Get(PrefabId.CoreHitFx);
        cachedBlockDestroyFx = PrefabManager.Get(PrefabId.BlockDestroyFx);
        cachedShieldFx = PrefabManager.Get(PrefabId.PowerupShield);
        cachedPowerupFx = PrefabManager.Get(PrefabId.PowerupDefault);
        cachedAdditiveBonusFx = PrefabManager.Get(PrefabId.AdditiveBonusFx);
        resourcesCached = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BindAllBlockEvents();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnbindAllBlockEvents();
    }

    protected override void Start()
    {
        base.Start();

        // If no blocks registered yet, build from initial configuration
        if (gridBlocks.Count == 0 && transform.childCount > 0)
        {
            RegisterExistingChildrenIntoGrid();
        }
        UpdateMass();
        BindAllBlockEvents();
    }

    /// <summary>
    /// Checks if the core has been destroyed, marked dead, or depleted of hits.
    /// </summary>
    public override bool IsCoreDead()
    {
        if (coreBlock != null)
        {
            return coreBlock._dead || coreBlock._hits <= 0;
        }

        // If we previously had blocks registered in the grid, check if core at (0,0) was killed
        if (gridBlocks.TryGetValue(Vector2Int.zero, out GameObject coreGo))
        {
            if (coreGo == null || !coreGo) return true;
            BlockBase b0 = coreGo.GetComponent<BlockBase>();
            if (b0 == null || b0._dead || b0._hits <= 0) return true;
        }

        // If blocks are attached to the grid but core hits depleted
        if (gridBlocks.Count > 0 && _core_hits <= 0)
        {
            return true;
        }

        return base.IsCoreDead();
    }

    protected override void Update()
    {
        base.Update();

        if (enableSimulationLOD && Application.isPlaying && Time.frameCount % 30 == 0)
        {
            UpdateSimulationLOD();
        }
    }

    void FixedUpdate()
    {
        if (!isDestructing && IsCoreDead())
        {
            core_destruct(null);
            return;
        }

        if (magneticAccretion && !isDestructing && !isSleeping && Application.isPlaying)
        {
            ProcessMagneticAccretion(Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Sets the dormant sleep state of the AsteroidGrid, pausing or playing child particle systems.
    /// </summary>
    public override void SetSleeping(bool sleep)
    {
        base.SetSleeping(sleep);

        ParticleSystem ps = GetComponentInChildren<ParticleSystem>();
        if (ps != null && (coreBlock == null || ps != coreBlock.CoreParticleEffect))
        {
            if (sleep)
            {
                ps.Pause();
            }
            else
            {
                ps.Play();
            }
        }
    }

    protected virtual void OnDestroy()
    {
        isDestructing = true;
        magneticAccretion = false;
        allowReattachmentOnCollision = false;
        reservedSlots.Clear();
        blockReservations.Clear();
        UnbindCoreBlockEvents();
        UnbindAllBlockEvents();
    }

    #region Procedural Block Placement (3 Math & Fractal Functions)

    [ContextMenu("Regenerate With AsteroidGrid")]
    public void RegenerateGrid()
    {
        Clear();
        generate_asteroid(_core_hits, 4, targetBlockCount, _core_shell, _shell_hits, _block_hits);
    }

    public override int generate_asteroid(int coremass = 1, int massmin = 1, int massmax = 16, int shell = 0, int shlvl = 1, int blocklvl = 1)
    {
        Clear();

        _core_hits = coremass;
        _block_hits = blocklvl;
        _shell_hits = shlvl;
        if (massmax > _max_blocks)
        {
            _max_blocks = massmax;
        }
        targetBlockCount = (massmax > 0) ? Mathf.Clamp(UnityEngine.Random.Range(massmin, massmax + 1), 0, _max_blocks) : 0;

        EnsureCachedResources();

        // Spawn authoritative central BlockAsteroidCore at (0, 0)
        SpawnBlockAtCoord(Vector2Int.zero, _core_hits);

        List<Vector2Int> coordinatesToSpawn;

        switch (algorithm)
        {
            case GenerationAlgorithm.JuliaFractal:
                coordinatesToSpawn = GenerateJuliaFractalCoords(targetBlockCount, gridRadius);
                break;

            case GenerationAlgorithm.HarmonicRose:
                coordinatesToSpawn = GenerateHarmonicRoseCoords(targetBlockCount, gridRadius);
                break;

            case GenerationAlgorithm.DiffusionAggregation:
                coordinatesToSpawn = GenerateDiffusionAggregationCoords(targetBlockCount, gridRadius);
                break;

            case GenerationAlgorithm.ClassicShell:
            default:
                coordinatesToSpawn = GenerateShellGridCoords(targetBlockCount, gridRadius);
                break;
        }

        // Spawn perimeter blocks at generated in-memory coordinates
        foreach (Vector2Int coord in coordinatesToSpawn)
        {
            if (coord != Vector2Int.zero)
            {
                SpawnBlockAtCoord(coord, blocklvl);
            }
        }

        SetHits(_core_hits);
        return UpdateMass();
    }

    private void BindBlockEvents(BlockBase b0)
    {
        if (b0 == null || b0.isCore) return;
        b0.OnDestroyed -= HandleBlockDestroyed;
        b0.OnDestroyed += HandleBlockDestroyed;
        b0.OnHit -= HandleBlockHit;
        b0.OnHit += HandleBlockHit;
    }

    private void UnbindBlockEvents(BlockBase b0)
    {
        if (b0 == null) return;
        b0.OnDestroyed -= HandleBlockDestroyed;
        b0.OnHit -= HandleBlockHit;
    }

    private void BindAllBlockEvents()
    {
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null && pair.Key != Vector2Int.zero)
            {
                BlockBase b0 = pair.Value.GetComponent<BlockBase>();
                BindBlockEvents(b0);
            }
        }
    }

    private void UnbindAllBlockEvents()
    {
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null && pair.Key != Vector2Int.zero)
            {
                BlockBase b0 = pair.Value.GetComponent<BlockBase>();
                UnbindBlockEvents(b0);
            }
        }
    }

    private void HandleBlockHit(BlockBase block, Transform source, ProjectileBase b1)
    {
        if (isSleeping)
        {
            SetSleeping(false);
        }
        UpdateMass();
    }

    private void HandleBlockDestroyed(BlockBase block, Transform source, ProjectileBase b1)
    {
        if (isDestructing || this == null || !gameObject) return;

        UnbindBlockEvents(block);

        if (block != null && blockToCoord.TryGetValue(block.gameObject, out Vector2Int coord))
        {
            gridBlocks.Remove(coord);
            blockToCoord.Remove(block.gameObject);
        }

        Vector3 impactImpulse = Vector3.zero;
        if (b1 != null)
        {
            Vector3 bulletDir = b1.transform.up;
            Rigidbody b1Rb = b1.GetComponent<Rigidbody>();
            if (b1Rb != null && b1Rb.linearVelocity.sqrMagnitude > 0.001f)
            {
                bulletDir = b1Rb.linearVelocity.normalized;
            }
            bulletDir.z = 0f;
            impactImpulse = bulletDir * Mathf.Max(b1.CalculateBounceImpulse(), 1f);
        }
        else if (source != null && block != null)
        {
            Vector3 pushDir = (block.transform.position - source.position).normalized;
            pushDir.z = 0f;
            impactImpulse = pushDir * 2.0f;
        }

        check_for_unconnected(impactImpulse);
    }

    public override int core_receive_hit(Transform source, ProjectileBase b1)
    {
        if (isSleeping)
        {
            SetSleeping(false);
        }
        return base.core_receive_hit(source, b1);
    }

    public override int core_destruct(ProjectileBase b1)
    {
        if (isDestructing) return 0;

        // 1. Immediately disable accretion and reservations on the killed core
        magneticAccretion = false;
        allowReattachmentOnCollision = false;

        // Immediately cancel any in-flight pull velocities on blocks being attracted
        foreach (var pair in blockReservations)
        {
            if (pair.Key != null)
            {
                Rigidbody inFlightRb = pair.Key.GetComponent<Rigidbody>();
                if (inFlightRb != null)
                {
                    inFlightRb.linearVelocity = Vector3.zero;
                    inFlightRb.angularVelocity = Vector3.zero;
                }
            }
        }
        reservedSlots.Clear();
        blockReservations.Clear();

        return base.core_destruct(b1);
    }

    public override int DetachChildrenOnDestruction()
    {
        return DetachAllChildrenOnDestruction();
    }

    private int DetachAllChildrenOnDestruction()
    {
        int reward = 0;
        List<GameObject> childrenToDetach = new List<GameObject>();
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null && pair.Key != Vector2Int.zero)
            {
                if (!childrenToDetach.Contains(pair.Value))
                {
                    childrenToDetach.Add(pair.Value);
                }
            }
        }

        // Also check any transform children with BlockBase not indexed in grid
        foreach (Transform child in transform)
        {
            if (child != null)
            {
                if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
                if (child.name == "core_block") continue;

                BlockBase b0 = child.GetComponent<BlockBase>();
                if (b0 != null && !b0.isCore && !childrenToDetach.Contains(child.gameObject))
                {
                    childrenToDetach.Add(child.gameObject);
                }
            }
        }

        gridBlocks.Clear();
        blockToCoord.Clear();

        Transform newParent = transform.parent;
        if (newParent != null && Application.isPlaying && (!newParent.gameObject.scene.isLoaded || !newParent.gameObject.activeInHierarchy))
        {
            newParent = null;
        }

        foreach (GameObject blockObj in childrenToDetach)
        {
            if (blockObj == null) continue;
            BlockBase b0 = blockObj.GetComponent<BlockBase>();
            if (b0 != null && !b0._dead)
            {
                UnbindBlockEvents(b0);
                reward += (int)b0._hits;

                b0.EndLife(newParent);
                b0.gameObject.tag = "block";

                Collider c = blockObj.GetComponent<Collider>();
                if (c != null)
                {
                    c.enabled = true;
                    c.isTrigger = false;
                }

                Rigidbody rb = blockObj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = blockObj.AddComponent<Rigidbody>();
                }
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.mass = b0._hits > 0 ? b0._hits : 1f;

                Vector3 ejectDir = (blockObj.transform.position - transform.position).normalized;
                if (ejectDir.sqrMagnitude < 0.001f) ejectDir = UnityEngine.Random.insideUnitSphere;
                ejectDir.z = 0f;
                ejectDir.Normalize();
                rb.linearVelocity = ejectDir * UnityEngine.Random.Range(2.5f, 6.0f);

                OnBlockDetached?.Invoke(blockObj, Vector2Int.zero);
            }
        }

        return reward;
    }

    /// <summary>
    /// Math Function 1: Julia Fractal Boundary Sampling.
    /// Maps 2D grid coordinates into the complex plane z = x + iy and evaluates quadratic iterator z = z^2 + c.
    /// Points near the fractal boundary create rocky crevices and asymmetric crags.
    /// </summary>
    public List<Vector2Int> GenerateJuliaFractalCoords(int maxCount, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int> { Vector2Int.zero };

        // Interesting Julia parameter seeds
        float cx = UnityEngine.Random.Range(-0.75f, -0.4f);
        float cy = UnityEngine.Random.Range(0.15f, 0.65f);
        float scale = 2.0f / Mathf.Max(2, radius);

        List<KeyValuePair<Vector2Int, float>> candidates = new List<KeyValuePair<Vector2Int, float>>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0) continue;

                float zx = x * scale;
                float zy = y * scale;
                int iter = 0;
                int maxIter = 16;

                while (zx * zx + zy * zy < 4f && iter < maxIter)
                {
                    float xtemp = zx * zx - zy * zy + cx;
                    zy = 2f * zx * zy + cy;
                    zx = xtemp;
                    iter++;
                }

                // Score based on distance from escape threshold (boundary detail)
                float score = Mathf.Abs(iter - 6);
                candidates.Add(new KeyValuePair<Vector2Int, float>(new Vector2Int(x, y), score));
            }
        }

        // Sort by score and pick connected items
        candidates.Sort((a, b) => a.Value.CompareTo(b.Value));

        foreach (var pair in candidates)
        {
            if (result.Count >= maxCount) break;
            if (IsAdjacentToSet(pair.Key, occupied))
            {
                occupied.Add(pair.Key);
                result.Add(pair.Key);
            }
        }

        return result;
    }

    /// <summary>
    /// Math Function 2: Harmonic Rose Curve / Polar Superformula.
    /// Evaluates polar equation r(theta) = a + b * cos(k * theta + phi) for lobed crystal/star geometries.
    /// </summary>
    public List<Vector2Int> GenerateHarmonicRoseCoords(int maxCount, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int> { Vector2Int.zero };

        // Harmonic curve parameters: k lobes (3, 4, or 5-leaf space crystal)
        int k = UnityEngine.Random.Range(3, 6);
        float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float a = radius * 0.6f;
        float b = radius * 0.35f;

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0) continue;

                float r = Mathf.Sqrt(x * x + y * y);
                float theta = Mathf.Atan2(y, x);

                float curveLimit = a + b * Mathf.Cos(k * theta + phase);
                if (r <= curveLimit)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        ShuffleList(candidates);

        foreach (Vector2Int coord in candidates)
        {
            if (result.Count >= maxCount) break;
            if (IsAdjacentToSet(coord, occupied))
            {
                occupied.Add(coord);
                result.Add(coord);
            }
        }

        return result;
    }

    /// <summary>
    /// Math Function 3: Diffusion-Limited Aggregation (DLA).
    /// Simulates dust/debris accretion via 2D Brownian walks freezing upon contact with cluster.
    /// </summary>
    public List<Vector2Int> GenerateDiffusionAggregationCoords(int maxCount, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> cluster = new HashSet<Vector2Int> { Vector2Int.zero };

        int spawnRadius = radius + 1;
        int maxAttempts = maxCount * 25;
        int attempts = 0;

        while (result.Count < maxCount && attempts < maxAttempts)
        {
            attempts++;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 walker = new Vector2(Mathf.Cos(angle) * spawnRadius, Mathf.Sin(angle) * spawnRadius);
            Vector2Int currentCoord = new Vector2Int(Mathf.RoundToInt(walker.x), Mathf.RoundToInt(walker.y));

            for (int step = 0; step < 40; step++)
            {
                Vector2Int stepDir = NeighborOffsets4[UnityEngine.Random.Range(0, NeighborOffsets4.Length)];
                Vector2Int nextCoord = currentCoord + stepDir;

                if (cluster.Contains(nextCoord))
                {
                    if (!cluster.Contains(currentCoord) && currentCoord != Vector2Int.zero &&
                        Mathf.Abs(currentCoord.x) <= radius && Mathf.Abs(currentCoord.y) <= radius)
                    {
                        cluster.Add(currentCoord);
                        result.Add(currentCoord);
                    }
                    break;
                }

                currentCoord = nextCoord;
                if (currentCoord.magnitude > spawnRadius + 2) break;
            }
        }

        return result;
    }

    /// <summary>
    /// In-memory replacement for concentric shell coordinate generation.
    /// </summary>
    public List<Vector2Int> GenerateShellGridCoords(int maxCount, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int> { Vector2Int.zero };

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int r = 1; r <= radius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == r)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        ShuffleList(candidates);

        foreach (Vector2Int coord in candidates)
        {
            if (result.Count >= maxCount) break;
            if (IsAdjacentToSet(coord, occupied))
            {
                occupied.Add(coord);
                result.Add(coord);
            }
        }

        return result;
    }

    #endregion

    #region In-Memory Grid Connectivity & Detachment (BFS - 0 Raycasts)

    /// <summary>
    /// Replaces legacy scan_connected recursive raycasts with an in-memory BFS connectivity check.
    /// Any blocks disconnected from Core (0,0) are cleanly detached.
    /// </summary>
    public override void check_for_unconnected()
    {
        check_for_unconnected(Vector3.zero);
    }

    /// <summary>
    /// Runs in-memory BFS connectivity check and detaches any orphan blocks, imparting impact impulse.
    /// </summary>
    public override void check_for_unconnected(Vector3 impactImpulse)
    {
        if (isDestructing || this == null || !gameObject) return;
        if (IsCoreDead())
        {
            core_destruct(null);
            return;
        }

        PruneNullGridEntries();

        bfsQueue.Clear();
        bfsReachable.Clear();

        // Core is always at (0, 0)
        bfsQueue.Enqueue(Vector2Int.zero);
        bfsReachable.Add(Vector2Int.zero);

        // Run BFS across adjacent grid blocks in memory
        while (bfsQueue.Count > 0)
        {
            Vector2Int current = bfsQueue.Dequeue();

            foreach (Vector2Int offset in NeighborOffsets8)
            {
                Vector2Int neighbor = current + offset;

                if (gridBlocks.ContainsKey(neighbor) && bfsReachable.Add(neighbor))
                {
                    bfsQueue.Enqueue(neighbor);
                }
            }
        }

        // Find all unreachable orphan blocks in gridBlocks
        List<Vector2Int> disconnectedCoords = new List<Vector2Int>();
        foreach (var pair in gridBlocks)
        {
            if (!bfsReachable.Contains(pair.Key))
            {
                disconnectedCoords.Add(pair.Key);
            }
        }

        // Also sweep any transform children with BlockBase not registered in grid or unreachable
        List<GameObject> orphanChildren = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0.isCore && !b0._dead)
            {
                if (!blockToCoord.TryGetValue(child.gameObject, out Vector2Int c) || !bfsReachable.Contains(c))
                {
                    if (!orphanChildren.Contains(child.gameObject))
                    {
                        orphanChildren.Add(child.gameObject);
                    }
                }
            }
        }

        // Detach orphans and impart player impact force
        foreach (Vector2Int orphanCoord in disconnectedCoords)
        {
            DetachBlock(orphanCoord, impactImpulse);
        }

        foreach (GameObject orphanGo in orphanChildren)
        {
            if (orphanGo != null && orphanGo.transform.parent == transform)
            {
                DetachChildGameObject(orphanGo, impactImpulse, Vector2Int.zero);
            }
        }

        UpdateMass();
    }

    /// <summary>
    /// Detaches a block at coordinate, converting it into independent floating debris.
    /// </summary>
    public virtual GameObject DetachBlock(Vector2Int coord)
    {
        return DetachBlock(coord, Vector3.zero);
    }

    /// <summary>
    /// Detaches a block at coordinate, converting it into independent floating debris with impact impulse.
    /// </summary>
    public virtual GameObject DetachBlock(Vector2Int coord, Vector3 impactImpulse)
    {
        if (!gridBlocks.TryGetValue(coord, out GameObject blockObj) || blockObj == null)
        {
            gridBlocks.Remove(coord);
            return null;
        }

        gridBlocks.Remove(coord);
        blockToCoord.Remove(blockObj);

        return DetachChildGameObject(blockObj, impactImpulse, coord);
    }

    /// <summary>
    /// Detaches a child block GameObject, ensures physical debris properties and unbinds events.
    /// </summary>
    private GameObject DetachChildGameObject(GameObject blockObj, Vector3 impactImpulse, Vector2Int coord)
    {
        if (blockObj == null) return null;

        BlockBase b0 = blockObj.GetComponent<BlockBase>();
        if (b0 != null)
        {
            UnbindBlockEvents(b0);
            b0._detached = true;

            Transform targetParent = transform.parent;
            if (targetParent != null && (!targetParent.gameObject.scene.isLoaded || !targetParent.gameObject.activeInHierarchy))
            {
                targetParent = null;
            }

            b0.EndLife(targetParent);
            b0.gameObject.tag = "block";

            Collider c = blockObj.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = true;
                c.isTrigger = false;
            }

            // Ensure independent physics on detached piece
            Rigidbody rb = blockObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = blockObj.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.mass = b0._hits > 0 ? b0._hits : 1f;

            // Give outward ejection impulse relative to asteroid core
            Vector3 ejectDir = (blockObj.transform.position - transform.position).normalized;
            if (ejectDir.sqrMagnitude < 0.001f) ejectDir = Vector3.up;

            float baseSpeed = UnityEngine.Random.Range(1.0f, 2.5f);
            Vector3 velocity = ejectDir * baseSpeed;

            // Add player impact force proportional to impactImpulse
            if (impactImpulse.sqrMagnitude > 0.001f)
            {
                velocity += impactImpulse / rb.mass;
            }

            rb.linearVelocity = velocity;
        }

        OnBlockDetached?.Invoke(blockObj, coord);
        return blockObj;
    }

    #endregion

    #region Re-attachment, Accretion & Slot Reservations

    /// <summary>
    /// Checks whether a given grid slot is within bounds, adjacent, and unreserved.
    /// </summary>
    public bool IsSlotAvailable(Vector2Int coord, GameObject requestingBlock = null)
    {
        if (coord == Vector2Int.zero) return false;
        if (Mathf.Abs(coord.x) > gridRadius || Mathf.Abs(coord.y) > gridRadius) return false;
        if (gridBlocks.ContainsKey(coord)) return false;

        if (reservedSlots.TryGetValue(coord, out GameObject owner))
        {
            return owner == null || owner == requestingBlock;
        }
        return true;
    }

    /// <summary>
    /// Attempts to reserve a vacant slot for an approaching block to prevent other blocks from competing for it.
    /// </summary>
    public bool TryReserveSlot(Vector2Int coord, GameObject block)
    {
        if (block == null || coord == Vector2Int.zero) return false;
        if (gridBlocks.ContainsKey(coord)) return false;

        if (reservedSlots.TryGetValue(coord, out GameObject currentOwner) && currentOwner != null)
        {
            if (currentOwner != block) return false;
        }

        // Release prior reservation if moving to a new socket
        if (blockReservations.TryGetValue(block, out Vector2Int oldCoord) && oldCoord != coord)
        {
            reservedSlots.Remove(oldCoord);
        }

        reservedSlots[coord] = block;
        blockReservations[block] = coord;
        return true;
    }

    /// <summary>
    /// Releases a previously held slot reservation.
    /// </summary>
    public void ReleaseReservation(GameObject block)
    {
        if (block == null) return;
        if (blockReservations.TryGetValue(block, out Vector2Int coord))
        {
            blockReservations.Remove(block);
            reservedSlots.Remove(coord);
        }
    }

    /// <summary>
    /// Cleans up reservations for blocks that have attached, died, or moved away.
    /// </summary>
    public void PruneDeadReservations()
    {
        List<GameObject> toRemove = null;
        foreach (var pair in blockReservations)
        {
            if (pair.Key == null || pair.Key.transform.parent == transform)
            {
                if (toRemove == null) toRemove = new List<GameObject>();
                toRemove.Add(pair.Key);
            }
            else
            {
                float distSq = (pair.Key.transform.position - transform.position).sqrMagnitude;
                if (distSq > (accretionRadius + 3f) * (accretionRadius + 3f))
                {
                    if (toRemove == null) toRemove = new List<GameObject>();
                    toRemove.Add(pair.Key);
                }
            }
        }

        if (toRemove != null)
        {
            foreach (var b in toRemove)
            {
                ReleaseReservation(b);
            }
        }
    }

    /// <summary>
    /// Finds the closest available, adjacent grid socket within bounds for a requesting block.
    /// </summary>
    public Vector2Int FindBestAttachmentSlot(Vector2Int desiredCoord, GameObject requestingBlock = null)
    {
        PruneNullGridEntries();

        // 1. If requesting block already holds a valid reserved slot adjacent to the cluster, maintain it
        if (requestingBlock != null && blockReservations.TryGetValue(requestingBlock, out Vector2Int currentReserved))
        {
            if (IsSlotAvailable(currentReserved, requestingBlock) && IsAdjacentToCluster(currentReserved))
            {
                return currentReserved;
            }
        }

        // 2. If the desired coordinate itself is valid, available, and adjacent:
        if (IsSlotAvailable(desiredCoord, requestingBlock) && IsAdjacentToCluster(desiredCoord))
        {
            return desiredCoord;
        }

        // 3. Search closest available candidate adjacent to any currently attached block
        float bestDist = float.MaxValue;
        Vector2Int bestSlot = Vector2Int.zero;
        bool found = false;

        List<Vector2Int> anchorPoints = new List<Vector2Int>(gridBlocks.Keys);
        if (!anchorPoints.Contains(Vector2Int.zero))
        {
            anchorPoints.Add(Vector2Int.zero);
        }

        foreach (Vector2Int anchor in anchorPoints)
        {
            foreach (Vector2Int offset in NeighborOffsets8)
            {
                Vector2Int candidate = anchor + offset;
                if (!IsSlotAvailable(candidate, requestingBlock)) continue;

                float dist = (candidate - desiredCoord).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSlot = candidate;
                    found = true;
                }
            }
        }

        return found ? bestSlot : Vector2Int.zero;
    }

    /// <summary>
    /// Attempts to re-attach a floating block into the asteroid's grid structure.
    /// Enforces multi-block safety so blocks never overwrite or stack on top of each other.
    /// </summary>
    public virtual bool TryReattachBlock(GameObject blockObj)
    {
        if (blockObj == null || IsAtCapacity) return false;

        PruneNullGridEntries();

        BlockBase b0 = blockObj.GetComponent<BlockBase>();
        if (b0 == null || b0._dead || b0.IsCore || b0 is BlockAsteroidCore) return false;

        // Calculate local position relative to asteroid
        Vector3 localPos = transform.InverseTransformPoint(blockObj.transform.position);
        Vector2Int idealCoord = new Vector2Int(Mathf.RoundToInt(localPos.x), Mathf.RoundToInt(localPos.y));

        Vector2Int targetCoord = FindBestAttachmentSlot(idealCoord, blockObj);
        if (targetCoord == Vector2Int.zero)
        {
            return false;
        }

        // Multi-block safety check: if another block claimed targetCoord in the same frame, re-route
        if (gridBlocks.ContainsKey(targetCoord))
        {
            targetCoord = FindBestAttachmentSlot(targetCoord, blockObj);
            if (targetCoord == Vector2Int.zero || gridBlocks.ContainsKey(targetCoord))
            {
                return false;
            }
        }

        ReleaseReservation(blockObj);

        // Strip Rigidbody so PhysX compound hierarchy moves as one asteroid
        Rigidbody rb = blockObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(rb);
            else
                Destroy(rb);
#else
            Destroy(rb);
#endif
        }

        // Re-parent to asteroid with explicit non-scaled parent assignment
        blockObj.transform.SetParent(transform, false);
        blockObj.transform.localPosition = new Vector3(targetCoord.x, targetCoord.y, 0f);
        blockObj.transform.localRotation = Quaternion.identity;
        blockObj.transform.localScale = Vector3.one;

        b0._detached = false;
        b0.isCore = false;

        gridBlocks[targetCoord] = blockObj;
        blockToCoord[blockObj] = targetCoord;

        BindBlockEvents(b0);

        UpdateMass();

        // Growth scale punch feedback
        if (Application.isPlaying)
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.08f, 0.2f);
        }

        OnBlockReattached?.Invoke(blockObj, targetCoord);
        return true;
    }

    public virtual void ProcessMagneticAccretion(float deltaTime = 0.02f)
    {
        if (!magneticAccretion || isDestructing || deltaTime <= 0f) return;
        if (IsCoreDead())
        {
            magneticAccretion = false;
            return;
        }

        if (IsAtCapacity)
        {
            reservedSlots.Clear();
            blockReservations.Clear();
            return;
        }

        PruneDeadReservations();
        PruneNullGridEntries();
        processedAccretionInFrame.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, accretionRadius);
        if (colliders == null || colliders.Length == 0) return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

            GameObject targetGo = col.gameObject;
            if (!processedAccretionInFrame.Add(targetGo)) continue;

            BlockBase b0 = col.GetComponent<BlockBase>();
            if (b0 == null || b0._dead || b0.IsCore || b0 is BlockAsteroidCore) continue;

            bool isStandalone = b0._detached || col.transform.parent == null || col.transform.GetComponentInParent<AsteroidBase>() == null;
            if (!isStandalone) continue;

            Vector3 localPos = transform.InverseTransformPoint(col.transform.position);
            Vector2Int idealCoord = new Vector2Int(Mathf.RoundToInt(localPos.x), Mathf.RoundToInt(localPos.y));
            Vector2Int targetCoord = FindBestAttachmentSlot(idealCoord, targetGo);

            if (targetCoord == Vector2Int.zero) continue;

            TryReserveSlot(targetCoord, targetGo);

            Vector3 targetWorldPos = transform.TransformPoint(new Vector3(targetCoord.x, targetCoord.y, 0f));
            float dist = Vector3.Distance(col.transform.position, targetWorldPos);

            if (dist <= attachDistance)
            {
                TryReattachBlock(targetGo);
                if (IsAtCapacity) break;
            }
            else
            {
                Rigidbody rb = col.attachedRigidbody ?? col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 pullDir = (targetWorldPos - col.transform.position).normalized;
                    rb.AddForce(pullDir * accretionPullForce, ForceMode.Acceleration);
                    rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, 5f * deltaTime);
                }
            }
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (!allowReattachmentOnCollision) return;

        BlockBase looseBlock = col.collider.GetComponent<BlockBase>();
        if (looseBlock != null && looseBlock._detached && !looseBlock._dead && !looseBlock.IsCore)
        {
            if (col.relativeVelocity.magnitude <= maxReattachImpactVelocity)
            {
                TryReattachBlock(looseBlock.gameObject);
            }
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (magneticAccretion && accretionRadius > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, accretionRadius);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            foreach (var pair in reservedSlots)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(pair.Key.x, pair.Key.y, 0f));
                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.85f);
            }
        }
    }

    #endregion

    #region Grid Helpers & Lifecycle

    /// <summary>
    /// Spawns a block at the specified grid coordinate.
    /// Spawns authoritative BlockAsteroidCore at (0, 0), and standard BlockAsteroid for perimeter sockets.
    /// </summary>
    public virtual GameObject SpawnBlockAtCoord(Vector2Int coord, int hits)
    {
        if (coord == Vector2Int.zero && coreBlock != null)
        {
            return coreBlock.gameObject;
        }

        if (gridBlocks.ContainsKey(coord)) return null;

        if (coord == Vector2Int.zero)
        {
            BlockAsteroidCore core = SpawnCoreBlock(hits, Vector3.zero);
            if (core != null)
            {
                gridBlocks[Vector2Int.zero] = core.gameObject;
                blockToCoord[core.gameObject] = Vector2Int.zero;
                _num_boxes_generated++;
                return core.gameObject;
            }
            return null;
        }

        GameObject blockPrefab = GetBlockPrefab();
        if (blockPrefab == null) return null;

        Vector3 localPos = new Vector3(coord.x, coord.y, 0f);
        Vector3 worldPos = transform.TransformPoint(localPos);

        GameObject newBox = Instantiate(blockPrefab, worldPos, transform.rotation, transform);
        newBox.transform.localPosition = localPos;
        newBox.transform.name = $"b_{coord.x}_{coord.y}";
        newBox.transform.localScale = Vector3.one;

        Rigidbody childRb = newBox.GetComponent<Rigidbody>();
        if (childRb != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(childRb);
            else
                Destroy(childRb);
#else
            Destroy(childRb);
#endif
        }

        BlockBase b0 = newBox.GetComponent<BlockBase>();
        if (b0 != null)
        {
            b0._detached = false;
            b0.isCore = false;
            b0.SetHits(hits);
            b0._level = Mathf.Max(1, _block_hits);
            BindBlockEvents(b0);
        }

        gridBlocks[coord] = newBox;
        blockToCoord[newBox] = coord;
        _num_boxes_generated++;

        return newBox;
    }

    public override void Clear()
    {
        UnbindAllBlockEvents();
        base.Clear();

        gridBlocks.Clear();
        blockToCoord.Clear();
        reservedSlots.Clear();
        blockReservations.Clear();
    }

    /// <summary>
    /// Synchronizes in-memory grid tracking with child GameObjects attached to this transform.
    /// </summary>
    public void SyncGrid()
    {
        RegisterExistingChildrenIntoGrid();
    }

    public override int UpdateMass()
    {
        if (gridBlocks.Count == 0 && transform.childCount > 0)
        {
            RegisterExistingChildrenIntoGrid();
        }

        PruneNullGridEntries();

        int childMass = 0;
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null)
            {
                if (pair.Key == Vector2Int.zero) continue;

                BlockBase b0 = pair.Value.GetComponent<BlockBase>();
                if (b0 != null && !b0.isCore)
                {
                    childMass += (int)b0._hits;
                }
            }
        }

        int totalMass = childMass + _core_hits;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = totalMass > 0 ? totalMass : 1f;
        }

        return totalMass;
    }

    private void RegisterExistingChildrenIntoGrid()
    {
        gridBlocks.Clear();
        blockToCoord.Clear();

        // 1. Locate and register authoritative BlockAsteroidCore
        EnsureCoreBlock();

        if (coreBlock != null)
        {
            gridBlocks[Vector2Int.zero] = coreBlock.gameObject;
            blockToCoord[coreBlock.gameObject] = Vector2Int.zero;
        }

        // 2. Register all other attached children
        foreach (Transform child in transform)
        {
            if (coreBlock != null && child.gameObject == coreBlock.gameObject) continue;
            if (child.name == "core_block") continue;

            BlockBase b0 = child.GetComponent<BlockBase>();
            if (b0 != null && !b0._dead)
            {
                b0.isCore = false;
                Vector3 local = child.localPosition;
                Vector2Int coord = new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
                if (coord == Vector2Int.zero)
                {
                    coord = FindBestAttachmentSlot(Vector2Int.zero, child.gameObject);
                }

                if (coord != Vector2Int.zero && !gridBlocks.ContainsKey(coord))
                {
                    gridBlocks[coord] = child.gameObject;
                    blockToCoord[child.gameObject] = coord;
                    BindBlockEvents(b0);
                }
            }
        }
    }

    private void PruneNullGridEntries()
    {
        List<Vector2Int> dead = null;
        List<GameObject> deadObjs = null;
        foreach (var pair in gridBlocks)
        {
            if (pair.Value == null)
            {
                if (dead == null) dead = new List<Vector2Int>();
                dead.Add(pair.Key);
            }
            else
            {
                BlockBase b0 = pair.Value.GetComponent<BlockBase>();
                if (b0 == null || b0._dead)
                {
                    if (dead == null) dead = new List<Vector2Int>();
                    dead.Add(pair.Key);
                    if (deadObjs == null) deadObjs = new List<GameObject>();
                    deadObjs.Add(pair.Value);
                }
            }
        }

        if (dead != null)
        {
            foreach (var d in dead)
            {
                gridBlocks.Remove(d);
            }
        }

        if (deadObjs != null)
        {
            foreach (var obj in deadObjs)
            {
                blockToCoord.Remove(obj);
            }
        }
    }

    private bool IsAdjacentToCluster(Vector2Int coord)
    {
        if (coord == Vector2Int.zero) return true;

        foreach (Vector2Int offset in NeighborOffsets8)
        {
            Vector2Int check = coord + offset;
            if (check == Vector2Int.zero || gridBlocks.ContainsKey(check))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsAdjacentToSet(Vector2Int coord, HashSet<Vector2Int> set)
    {
        foreach (Vector2Int offset in NeighborOffsets8)
        {
            if (set.Contains(coord + offset))
            {
                return true;
            }
        }
        return false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }

    private void UpdateSimulationLOD()
    {
        player p = FindAnyObjectByType<player>();
        if (p == null) return;

        float distSq = (transform.position - p.transform.position).sqrMagnitude;
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            if (distSq > sleepDistance * sleepDistance)
            {
                if (!rb.IsSleeping()) rb.Sleep();
            }
            else
            {
                if (rb.IsSleeping()) rb.WakeUp();
            }
        }
    }

    #endregion
}

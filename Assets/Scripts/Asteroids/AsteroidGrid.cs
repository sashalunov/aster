using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// High-performance compound asteroid managing multiple block0 components via an in-memory 2D grid.
/// Eliminates physics raycast bottlenecks with BFS connectivity checks, supports procedural fractal/math
/// generation algorithms, and provides dynamic block detachment and re-attachment.
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

    [Tooltip("Maximum total blocks this asteroid can grow to via accretion (0 = unlimited).")]
    public int maxAccretionBlocks = 36;

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

    // Cached resource prefabs (Zero Resources.Load during runtime hot paths)
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
    public bool HasBlockAt(Vector2Int coord) => coord == Vector2Int.zero || gridBlocks.ContainsKey(coord);

    protected virtual void Awake()
    {
        EnsureCachedResources();
        if (_block == null)
        {
            _block = Resources.Load<GameObject>("block0");
        }
    }

    private void EnsureCachedResources()
    {
        if (resourcesCached) return;

        cachedCoreHitFx = Resources.Load<GameObject>("show_corehit");
        cachedBlockDestroyFx = Resources.Load<GameObject>("blockdestroy");
        cachedShieldFx = Resources.Load<GameObject>("powerup_shield");
        cachedPowerupFx = Resources.Load<GameObject>("powerup");
        cachedAdditiveBonusFx = Resources.Load<GameObject>("additive_bonus");
        resourcesCached = true;
    }

    void Start()
    {
        SetHits(_core_hits);

        // If no blocks generated yet, build from initial configuration
        if (gridBlocks.Count == 0 && transform.childCount > 0)
        {
            RegisterExistingChildrenIntoGrid();
        }
    }

    void Update()
    {
        if (enableSimulationLOD && Application.isPlaying && Time.frameCount % 30 == 0)
        {
            UpdateSimulationLOD();
        }
    }

    void FixedUpdate()
    {
        if (magneticAccretion && Application.isPlaying)
        {
            ProcessMagneticAccretion(Time.fixedDeltaTime);
        }
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
        targetBlockCount = UnityEngine.Random.Range(massmin, massmax + 1);

        EnsureCachedResources();
        if (_block == null)
        {
            _block = Resources.Load<GameObject>("block0");
        }

        List<Vector2Int> coordinatesToSpawn = new List<Vector2Int>();

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

        // Spawn blocks at generated in-memory coordinates
        foreach (Vector2Int coord in coordinatesToSpawn)
        {
            SpawnBlockAtCoord(coord, blocklvl);
        }

        SetHits(_core_hits);
        return UpdateMass();
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

        // Shuffle candidates for organic distribution
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

            // Pick random perimeter start point
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 walker = new Vector2(Mathf.Cos(angle) * spawnRadius, Mathf.Sin(angle) * spawnRadius);
            Vector2Int currentCoord = new Vector2Int(Mathf.RoundToInt(walker.x), Mathf.RoundToInt(walker.y));

            // Walk up to 40 steps
            for (int step = 0; step < 40; step++)
            {
                // Random 4-way step
                Vector2Int stepDir = NeighborOffsets4[UnityEngine.Random.Range(0, NeighborOffsets4.Length)];
                Vector2Int nextCoord = currentCoord + stepDir;

                if (cluster.Contains(nextCoord))
                {
                    // Touched cluster! Freeze at currentCoord
                    if (!cluster.Contains(currentCoord) && currentCoord != Vector2Int.zero &&
                        Mathf.Abs(currentCoord.x) <= radius && Mathf.Abs(currentCoord.y) <= radius)
                    {
                        cluster.Add(currentCoord);
                        result.Add(currentCoord);
                    }
                    break;
                }

                currentCoord = nextCoord;

                // Wander out of bounds check
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
    public override void check_for_unconected()
    {
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

        // Find all unreachable orphan blocks
        List<Vector2Int> disconnectedCoords = new List<Vector2Int>();
        foreach (var pair in gridBlocks)
        {
            if (!bfsReachable.Contains(pair.Key))
            {
                disconnectedCoords.Add(pair.Key);
            }
        }

        // Detach orphans
        foreach (Vector2Int orphanCoord in disconnectedCoords)
        {
            DetachBlock(orphanCoord);
        }

        UpdateMass();
    }

    /// <summary>
    /// Detaches a block at coordinate, converting it into independent floating debris.
    /// </summary>
    public virtual GameObject DetachBlock(Vector2Int coord)
    {
        if (!gridBlocks.TryGetValue(coord, out GameObject blockObj) || blockObj == null)
        {
            gridBlocks.Remove(coord);
            return null;
        }

        gridBlocks.Remove(coord);
        blockToCoord.Remove(blockObj);

        // Deparent from asteroid transform
        blockObj.transform.parent = transform.parent;

        block0 b0 = blockObj.GetComponent<block0>();
        if (b0 != null)
        {
            b0._detached = true;

            // Ensure independent physics on detached piece
            Rigidbody rb = blockObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = blockObj.AddComponent<Rigidbody>();
            }
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.mass = b0._hits > 0 ? b0._hits : 1f;

            // Give outward ejection impulse relative to asteroid core
            Vector3 ejectDir = (blockObj.transform.position - transform.position).normalized;
            if (ejectDir.sqrMagnitude < 0.001f) ejectDir = Vector3.up;
            rb.linearVelocity = ejectDir * UnityEngine.Random.Range(1.0f, 2.5f);
        }

        OnBlockDetached?.Invoke(blockObj, coord);
        return blockObj;
    }

    #endregion

    #region Re-attachment & Accretion

    /// <summary>
    /// Attempts to re-attach a floating block into the asteroid's grid structure.
    /// </summary>
    public virtual bool TryReattachBlock(GameObject blockObj)
    {
        if (blockObj == null) return false;

        block0 b0 = blockObj.GetComponent<block0>();
        if (b0 == null || b0._dead) return false;

        // Calculate local position relative to asteroid
        Vector3 localPos = transform.InverseTransformPoint(blockObj.transform.position);
        Vector2Int idealCoord = new Vector2Int(Mathf.RoundToInt(localPos.x), Mathf.RoundToInt(localPos.y));

        Vector2Int targetCoord = FindBestAttachmentSlot(idealCoord);
        if (targetCoord == Vector2Int.zero && HasBlockAt(Vector2Int.zero))
        {
            return false; // No valid adjacent slot found
        }

        // Re-parent to asteroid
        blockObj.transform.parent = transform;
        blockObj.transform.localPosition = new Vector3(targetCoord.x, targetCoord.y, 0f);
        blockObj.transform.localRotation = Quaternion.identity;

        // Disable loose Rigidbody so physical simulation is driven by parent asteroid
        Rigidbody rb = blockObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(rb);
            else
                Destroy(rb);
#else
            Destroy(rb);
#endif
        }

        b0._detached = false;

        gridBlocks[targetCoord] = blockObj;
        blockToCoord[blockObj] = targetCoord;

        UpdateMass();

        // Growth scale punch feedback
        if (Application.isPlaying)
        {
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.08f, 0.2f);
        }

        OnBlockReattached?.Invoke(blockObj, targetCoord);

        return true;
    }

    private Vector2Int FindBestAttachmentSlot(Vector2Int desiredCoord)
    {
        // If ideal coordinate is valid, empty, and adjacent to cluster:
        if (desiredCoord != Vector2Int.zero && !gridBlocks.ContainsKey(desiredCoord) && IsAdjacentToCluster(desiredCoord))
        {
            return desiredCoord;
        }

        // Otherwise find closest open neighbor to desired coordinate
        float bestDist = float.MaxValue;
        Vector2Int bestSlot = Vector2Int.zero;
        bool found = false;

        // Search adjacent slots to existing blocks
        List<Vector2Int> anchorPoints = new List<Vector2Int>(gridBlocks.Keys) { Vector2Int.zero };

        foreach (Vector2Int anchor in anchorPoints)
        {
            foreach (Vector2Int offset in NeighborOffsets8)
            {
                Vector2Int candidate = anchor + offset;
                if (candidate == Vector2Int.zero || gridBlocks.ContainsKey(candidate)) continue;

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

    public virtual void ProcessMagneticAccretion(float deltaTime = 0.02f)
    {
        if (!magneticAccretion || deltaTime <= 0f) return;
        if (maxAccretionBlocks > 0 && ActiveBlockCount >= maxAccretionBlocks) return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, accretionRadius);
        if (colliders == null || colliders.Length == 0) return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

            block0 b0 = col.GetComponent<block0>();
            if (b0 == null || b0._dead) continue;

            // Check if standalone: marked detached, or has no AsteroidBase parent
            bool isStandalone = b0._detached || col.transform.parent == null || col.transform.GetComponentInParent<AsteroidBase>() == null;
            if (!isStandalone) continue;

            // Calculate ideal slot and target world position
            Vector3 localPos = transform.InverseTransformPoint(col.transform.position);
            Vector2Int idealCoord = new Vector2Int(Mathf.RoundToInt(localPos.x), Mathf.RoundToInt(localPos.y));
            Vector2Int targetCoord = FindBestAttachmentSlot(idealCoord);

            if (targetCoord == Vector2Int.zero && HasBlockAt(Vector2Int.zero)) continue;

            Vector3 targetWorldPos = transform.TransformPoint(new Vector3(targetCoord.x, targetCoord.y, 0f));
            float dist = Vector3.Distance(col.transform.position, targetWorldPos);

            // Snap & Accrete if close enough
            if (dist <= attachDistance)
            {
                TryReattachBlock(col.gameObject);
                if (maxAccretionBlocks > 0 && ActiveBlockCount >= maxAccretionBlocks) break;
            }
            else
            {
                // Pull toward the specific empty socket
                Rigidbody rb = col.attachedRigidbody ?? col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 pullDir = (targetWorldPos - col.transform.position).normalized;
                    rb.AddForce(pullDir * accretionPullForce, ForceMode.Acceleration);

                    // Smooth angular drift
                    rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, 5f * deltaTime);
                }
            }
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (!allowReattachmentOnCollision) return;

        block0 looseBlock = col.collider.GetComponent<block0>();
        if (looseBlock != null && looseBlock._detached && !looseBlock._dead)
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
        }
    }

    #endregion

    #region Grid Helpers & Lifecycle

    public virtual GameObject SpawnBlockAtCoord(Vector2Int coord, int hits)
    {
        if (coord == Vector2Int.zero || gridBlocks.ContainsKey(coord)) return null;

        if (_block == null)
        {
            _block = Resources.Load<GameObject>("block0");
            if (_block == null) return null;
        }

        Vector3 localPos = new Vector3(coord.x, coord.y, 0f);
        Vector3 worldPos = transform.TransformPoint(localPos);

        GameObject newBox = Instantiate(_block, worldPos, transform.rotation, transform);
        newBox.transform.localPosition = localPos;
        newBox.transform.name = $"b_{coord.x}_{coord.y}";

        block0 b0 = newBox.GetComponent<block0>();
        if (b0 != null)
        {
            b0._detached = false;
            b0.SetHits(hits);
        }

        gridBlocks[coord] = newBox;
        blockToCoord[newBox] = coord;
        _num_boxes_generated++;

        return newBox;
    }

    public override void Clear()
    {
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(pair.Value);
                else
                    Destroy(pair.Value);
#else
                Destroy(pair.Value);
#endif
            }
        }

        gridBlocks.Clear();
        blockToCoord.Clear();
        _num_boxes_generated = 0;
    }

    public override int UpdateMass()
    {
        PruneNullGridEntries();

        int childMass = 0;
        foreach (var pair in gridBlocks)
        {
            if (pair.Value != null)
            {
                block0 b0 = pair.Value.GetComponent<block0>();
                if (b0 != null)
                {
                    childMass += b0._hits;
                }
            }
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = childMass + _core_hits;
        }

        return childMass + _core_hits;
    }

    private void RegisterExistingChildrenIntoGrid()
    {
        gridBlocks.Clear();
        blockToCoord.Clear();

        foreach (Transform child in transform)
        {
            block0 b0 = child.GetComponent<block0>();
            if (b0 != null && !b0._dead)
            {
                Vector3 local = child.localPosition;
                Vector2Int coord = new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
                if (coord != Vector2Int.zero && !gridBlocks.ContainsKey(coord))
                {
                    gridBlocks[coord] = child.gameObject;
                    blockToCoord[child.gameObject] = coord;
                }
            }
        }
    }

    private void PruneNullGridEntries()
    {
        List<Vector2Int> dead = null;
        foreach (var pair in gridBlocks)
        {
            if (pair.Value == null)
            {
                if (dead == null) dead = new List<Vector2Int>();
                dead.Add(pair.Key);
            }
        }

        if (dead != null)
        {
            foreach (var d in dead)
            {
                gridBlocks.Remove(d);
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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns block0 prefabs within a specified radius around a target center.
/// Controls spawn pacing (rate), maximum active limits, block hit scaling,
/// and optional integration with WaveManager.
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    [Header("Prefab & Hierarchy")]
    [Tooltip("The block0 prefab to spawn. If not assigned, loads from Resources/block0.")]
    public GameObject blockPrefab;
    public GameObject asterPrefab;


    [Tooltip("Parent transform to hold spawned blocks. Defaults to this transform.")]
    public Transform spawnContainer;

    [Header("Center & Radius")]
    [Tooltip("Center position for spawning. If null, automatically targets player or this transform.")]
    public Transform centerTarget;

    [Tooltip("Maximum radius around the center where blocks can spawn.")]
    [Min(0.1f)]
    public float radius = 15f;

    [Tooltip("Minimum radius (donut hole) around center to avoid spawning directly on the target.")]
    [Min(0f)]
    public float minRadius = 3f;

    [Header("Spawn Limits & Pacing")]
    [Tooltip("Maximum number of concurrently active blocks allowed.")]
    [Min(1)]
    public int max_limit = 20;

    [Tooltip("Time in seconds between spawn attempts.")]
    [Min(0.01f)]
    public float rate = 1.0f;

    [Tooltip("Number of blocks to spawn per interval tick.")]
    [Range(1, 10)]
    public int spawnBatchSize = 1;

    [Tooltip("Initial number of blocks to spawn immediately on start.")]
    public int initialSpawnCount = 0;

    [Tooltip("Whether the spawner is actively running.")]
    public bool isSpawning = true;

    [Header("Block Attributes")]
    [Tooltip("Minimum hit points assigned to spawned blocks.")]
    public int minHits = 1;

    [Tooltip("Maximum hit points assigned to spawned blocks.")]
    public int maxHits = 3;

    [Tooltip("Initial random drift velocity applied to spawned blocks.")]
    public float initialDrift = 1.0f;

    [Header("Wave Integration")]
    [Tooltip("Optional explicit reference to WaveManager. If null, resolves via WaveManager.Instance.")]
    public WaveManager waveManager;

    [Tooltip("If true, coordinates with WaveManager (respects CanSpawn and registers threats).")]
    public bool syncWithWaveManager = true;

    /// <summary>
    /// Resolves the active WaveManager reference.
    /// </summary>
    public WaveManager ActiveWaveManager => waveManager != null ? waveManager : WaveManager.Instance;

    // Runtime state
    private float spawnTimer = 0f;
    private readonly List<GameObject> activeBlocks = new List<GameObject>();
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();
    // Events
    public event Action<GameObject> OnBlockSpawned;
    public event Action<int> OnActiveCountChanged;

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
    /// True if the active count has reached or exceeded max_limit.
    /// </summary>
    public bool IsAtCapacity => ActiveCount >= max_limit;

    protected virtual void Awake()
    {
        if (blockPrefab == null)
        {
            blockPrefab = Resources.Load<GameObject>("block0");
        }

        if (spawnContainer == null)
        {
            spawnContainer = transform;
        }
    }

    protected virtual void Start()
    {
        ResolveCenterTarget();

        if (initialSpawnCount > 0)
        {
            SpawnBurst(initialSpawnCount);
        }
    }

    protected virtual void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Updates spawner timer and triggers spawns. Exposed for testability.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        if (!isSpawning || deltaTime <= 0f) return;

        // Check WaveManager state if synced
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null && !wm.CanSpawn)
        {
            return;
        }

        CleanDeadReferences();

        if (activeBlocks.Count >= max_limit)
        {
            return;
        }

        spawnTimer += deltaTime;
        if (spawnTimer >= rate)
        {
            spawnTimer = 0f;
            SpawnBurst(spawnBatchSize);
        }
    }

    /// <summary>
    /// Spawns a burst of blocks up to max_limit.
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

    /// Spawns one block within the defined radial bounds.
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

    /// Spawns one asteroidgrid within the defined radial bounds.

    public virtual GameObject SpawnSingleAsteroid()
    {
        if (asterPrefab == null)
        {
            asterPrefab = Resources.Load<GameObject>("AsteroidGrid");
            if (asterPrefab == null)
            {
                Debug.LogWarning("BlockSpawner: Cannot spawn asteroid, asterPrefab is null and Resources/AsteroidGrid could not be loaded.");
                return null;
            }
        }
        Vector3 spawnPosition = CalculateRandomSpawnPosition();
        GameObject newBlock = Instantiate(asterPrefab, spawnPosition, Quaternion.identity, spawnContainer);

         Rigidbody rb = newBlock.GetComponent<Rigidbody>();
         // Apply slight random 2D drift
        if (rb != null && initialDrift > 0f)
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.001f) randomDir = Vector2.up;
            float speed = UnityEngine.Random.Range(initialDrift * 0.5f, initialDrift);
            rb.linearVelocity = new Vector3(randomDir.x, randomDir.y, 0f) * speed;
        }

         activeAsteroids.Add(newBlock);

        // Register with WaveManager if synced
        WaveManager wm = ActiveWaveManager;
        if (syncWithWaveManager && wm != null)
        {
            wm.RegisterThreat(newBlock);
        }

       // OnBlockSpawned?.Invoke(newBlock);
        //OnActiveCountChanged?.Invoke(activeBlocks.Count);

        return newBlock;
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
    /// Removes null/destroyed blocks from the active tracking list.
    /// </summary>
    public void CleanDeadReferences()
    {
        int initialCount = activeBlocks.Count;
        activeBlocks.RemoveAll(b => b == null);

        if (activeBlocks.Count != initialCount)
        {
            OnActiveCountChanged?.Invoke(activeBlocks.Count);
        }
    }

    /// <summary>
    /// Destroys all currently active spawned blocks and resets the spawner tracking.
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

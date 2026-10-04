using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable entry configuring an authored or pregenerated asteroid template
/// with an assigned budget cost and selection weight.
/// </summary>
[System.Serializable]
public class AsteroidFieldEntry
{
    [Tooltip("Pregenerated asteroid prefab or template GameObject.")]
    public GameObject prefab;

    [Tooltip("Budget / threat cost consumed when this asteroid is spawned.")]
    [Min(1)]
    public int budgetCost = 3;

    [Tooltip("Relative weighted likelihood of selecting this asteroid from the catalog.")]
    [Range(0.01f, 100f)]
    public float weight = 1f;

    [Tooltip("Descriptive label for this variant (e.g. Light Fractal, Heavy Crystal).")]
    public string label = "Asteroid";

    public AsteroidFieldEntry() { }

    public AsteroidFieldEntry(GameObject prefab, int budgetCost = 3, float weight = 1f, string label = "Asteroid")
    {
        this.prefab = prefab;
        this.budgetCost = budgetCost;
        this.weight = weight;
        this.label = label;
    }
}

/// <summary>
/// Spawns a structured asteroid field composed of pregenerated multi-block asteroid clusters
/// constrained by a defined threat / mass budget.
/// Supports pre-authored prefabs, procedurally pregenerated template catalogs,
/// non-overlapping spatial distribution, drift dynamics, and WaveManager synchronization.
/// </summary>
public class AsteroidFieldSpawner : MonoBehaviour
{
    [Header("Budget Configuration")]
    [Tooltip("Total threat / mass budget allocated for spawning the asteroid field.")]
    [Min(1)]
    public int fieldBudget = 25;

    [Tooltip("If true, synchronizes budget and events with WaveManager (uses WaveManager.RemainingThreatBudget).")]
    [SerializeField] private bool _syncWithWaveManager = true;
    public bool syncWithWaveManager
    {
        get => _syncWithWaveManager;
        set
        {
            _syncWithWaveManager = value;
            EnsureWaveManagerSubscribed();
        }
    }

    [Tooltip("If true, automatically regenerates the field whenever a new wave starts.")]
    public bool generateOnWaveStart = true;

    [Tooltip("If true, clears all active field asteroids when a wave completes.")]
    public bool clearOnWaveComplete = true;

    [Tooltip("If true, automatically generates the asteroid field on Start().")]
    public bool generateOnStart = true;

    [Header("Pregenerated Asteroid Catalog")]
    [Tooltip("List of pre-configured or authored asteroid templates with individual budget costs.")]
    public List<AsteroidFieldEntry> asteroidCatalog = new List<AsteroidFieldEntry>();

    [Tooltip("If asteroidCatalog is empty, automatically pregenerates a diverse catalog of templates on Awake.")]
    public bool autoPregenerateCatalog = true;

    [Header("Field Geometry & Spatial Bounds")]
    [Tooltip("Center transform of the asteroid field (defaults to player or this transform).")]
    public Transform centerTarget;

    [Tooltip("Minimum radius (safe zone) around center where asteroids will not spawn.")]
    [Min(0f)]
    public float minRadius = 4f;

    [Tooltip("Maximum radius defining the outer boundary of the asteroid field.")]
    [Min(1f)]
    public float maxRadius = 22f;

    [Tooltip("Minimum distance required between asteroid centers to prevent overlapping.")]
    [Min(0.5f)]
    public float minAsteroidSpacing = 3.5f;

    [Tooltip("Maximum attempts to find a non-overlapping spawn location per asteroid.")]
    [Range(5, 50)]
    public int maxPlacementAttempts = 25;

    [Header("Physics & Motion Dynamics")]
    [Tooltip("Minimum linear drift speed applied to spawned asteroids.")]
    public float minDriftSpeed = 0.2f;

    [Tooltip("Maximum linear drift speed applied to spawned asteroids.")]
    public float maxDriftSpeed = 1.2f;

    [Tooltip("Maximum angular tumble speed in degrees per second.")]
    public float maxTumbleSpeed = 25f;

    [Header("Container & Hierarchy")]
    [Tooltip("Parent transform to hold spawned field asteroids. Defaults to this transform.")]
    public Transform spawnContainer;

    [Header("Field Maintenance & Replenishment")]
    [Tooltip("If true, periodically spawns replacement asteroids to maintain the defined budget.")]
    public bool replenishField = false;

    [Tooltip("Time in seconds between replenishment checks.")]
    [Min(0.1f)]
    public float replenishInterval = 2.0f;

    // Runtime state
    private float replenishTimer = 0f;
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();
    private readonly Dictionary<GameObject, int> asteroidCostMap = new Dictionary<GameObject, int>();
    private WaveManager subscribedWm;

    // Events
    public event Action<GameObject, int> OnAsteroidSpawned;
    public event Action<int, int> OnBudgetChanged; // spent, remaining
    public event Action OnFieldGenerated;
    public event Action OnFieldCleared;

    // Public Getters
    public IReadOnlyList<GameObject> ActiveAsteroids => activeAsteroids;
    public int ActiveCount
    {
        get
        {
            CleanDeadReferences();
            return activeAsteroids.Count;
        }
    }

    public int CurrentBudgetSpent
    {
        get
        {
            CleanDeadReferences();
            int total = 0;
            for (int i = 0; i < activeAsteroids.Count; i++)
            {
                if (activeAsteroids[i] != null && asteroidCostMap.TryGetValue(activeAsteroids[i], out int cost))
                {
                    total += cost;
                }
            }
            return total;
        }
    }

    public int RemainingBudget => Mathf.Max(0, fieldBudget - CurrentBudgetSpent);

    /// <summary>
    /// Resolves the active WaveManager reference.
    /// </summary>
    public WaveManager ActiveWaveManager => WaveManager.Instance;

    protected virtual void Awake()
    {
        if (spawnContainer == null)
        {
            spawnContainer = transform;
        }

        EnsurePregeneratedCatalog();
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
        ClearField();
    }

    protected virtual void Start()
    {
        //ResolveCenterTarget();
        EnsureWaveManagerSubscribed();

        if (generateOnStart && activeAsteroids.Count == 0)
        {
            GenerateField();
        }
    }

    protected virtual void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Updates field maintenance timer and handles periodic replenishment. Exposed for testability.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        EnsureWaveManagerSubscribed();

        if (replenishField)
        {
            replenishTimer += deltaTime;
            if (replenishTimer >= replenishInterval)
            {
                replenishTimer = 0f;
                ReplenishField();
            }
        }
    }

    #region WaveManager Event Synchronization

    private void EnsureWaveManagerSubscribed()
    {
        WaveManager wm = ActiveWaveManager;
        if (wm != subscribedWm)
        {
            UnsubscribeWaveManager();
            if (syncWithWaveManager && wm != null)
            {
                subscribedWm = wm;
                subscribedWm.OnWaveStarted += HandleWaveStarted;
                subscribedWm.OnWaveCompleted += HandleWaveCompleted;
            }
        }
    }

    private void UnsubscribeWaveManager()
    {
        if (subscribedWm != null)
        {
            subscribedWm.OnWaveStarted -= HandleWaveStarted;
            subscribedWm.OnWaveCompleted -= HandleWaveCompleted;
            subscribedWm = null;
        }
    }

    private void HandleWaveStarted(int waveIndex, WaveDefinition waveDef)
    {
        if (generateOnWaveStart)
        {
            int budget = waveDef != null && waveDef.threatBudget > 0 ? waveDef.threatBudget : fieldBudget;
            GenerateField(budget);
        }
    }

    private void HandleWaveCompleted(int waveIndex, WaveDefinition waveDef)
    {
        if (clearOnWaveComplete)
        {
            ClearField();
        }
    }

    #endregion

    #region Pregenerated Template Catalog Management

    /// <summary>
    /// Ensures that the catalog contains pregenerated asteroid templates.
    /// If none are configured in the Inspector, procedurally pregenerates a diverse catalog.
    /// </summary>
    public virtual void EnsurePregeneratedCatalog()
    {
        if (asteroidCatalog != null && asteroidCatalog.Count > 0)
        {
            return;
        }

        if (asteroidCatalog == null)
        {
            asteroidCatalog = new List<AsteroidFieldEntry>();
        }

        if (!autoPregenerateCatalog) return;

        // Create or find hidden template container
        Transform templateContainer = transform.Find("_PregeneratedAsteroidTemplates");
        if (templateContainer == null)
        {
            GameObject containerGo = new GameObject("_PregeneratedAsteroidTemplates");
            containerGo.transform.SetParent(transform);
            containerGo.SetActive(false);
            templateContainer = containerGo.transform;
        }

        GameObject asterBasePrefab = Resources.Load<GameObject>("AsteroidGrid");
#if UNITY_EDITOR
        if (asterBasePrefab == null)
        {
            asterBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/AsteroidGrid.prefab");
        }
#endif

        // Define a set of diverse pregenerated variants with varying mass and threat budgets
        var variantConfigs = new[]
        {
            new { Name = "Light_Harmonic", Algo = AsteroidGrid.GenerationAlgorithm.HarmonicRose, CoreHits = 2, MinMass = 2, MaxMass = 3, Cost = 3, Weight = 40f },
            new { Name = "Medium_Julia", Algo = AsteroidGrid.GenerationAlgorithm.JuliaFractal, CoreHits = 3, MinMass = 4, MaxMass = 6, Cost = 6, Weight = 35f },
            new { Name = "Heavy_DLA", Algo = AsteroidGrid.GenerationAlgorithm.DiffusionAggregation, CoreHits = 4, MinMass = 6, MaxMass = 9, Cost = 9, Weight = 20f },
            new { Name = "Bastion_Shell", Algo = AsteroidGrid.GenerationAlgorithm.ClassicShell, CoreHits = 5, MinMass = 8, MaxMass = 12, Cost = 12, Weight = 10f }
        };

        for (int i = 0; i < variantConfigs.Length; i++)
        {
            var cfg = variantConfigs[i];
            GameObject templateObj = null;

            if (asterBasePrefab != null)
            {
                templateObj = Instantiate(asterBasePrefab, Vector3.zero, Quaternion.identity, templateContainer);
            }
            else
            {
                templateObj = new GameObject(cfg.Name);
                templateObj.transform.SetParent(templateContainer);
                templateObj.AddComponent<AsteroidGrid>();
            }

            templateObj.name = $"Template_{cfg.Name}";
            AsteroidGrid grid = templateObj.GetComponent<AsteroidGrid>();
            if (grid != null)
            {
                grid.algorithm = cfg.Algo;
                grid.generate_asteroid(cfg.CoreHits, cfg.MinMass, cfg.MaxMass, shell: 1, shlvl: 1, blocklvl: 1);
            }

            templateObj.SetActive(false);
            asteroidCatalog.Add(new AsteroidFieldEntry(templateObj, cfg.Cost, cfg.Weight, cfg.Name));
        }
    }

    #endregion

    #region Field Generation & Spawning Logic

    /// <summary>
    /// Generates the asteroid field using the default fieldBudget.
    /// </summary>
    public virtual int GenerateField()
    {
        int targetBudget = fieldBudget;
        if (syncWithWaveManager && ActiveWaveManager != null && ActiveWaveManager.CurrentWaveConfig != null)
        {
            targetBudget = ActiveWaveManager.CurrentWaveConfig.threatBudget;
        }
        return GenerateField(targetBudget);
    }

    /// <summary>
    /// Generates the asteroid field up to the specified budget.
    /// Returns total budget spent.
    /// </summary>
    public virtual int GenerateField(int targetBudget)
    {
        ClearField();
        EnsurePregeneratedCatalog();
        //ResolveCenterTarget();

        if (asteroidCatalog == null || asteroidCatalog.Count == 0)
        {
            Debug.LogWarning("AsteroidFieldSpawner: Cannot generate field, catalog is empty.");
            return 0;
        }

        int remainingBudget = targetBudget;
        WaveManager wm = syncWithWaveManager ? ActiveWaveManager : null;

        // If synced with WaveManager, also clamp to WaveManager's remaining threat budget
        if (wm != null && wm.RemainingThreatBudget > 0 && remainingBudget > wm.RemainingThreatBudget)
        {
            remainingBudget = wm.RemainingThreatBudget;
        }

        List<Vector3> placedPositions = new List<Vector3>();
        int totalSpent = 0;

        // Find the cheapest entry in catalog to know when we can't afford anything else
        int minEntryCost = int.MaxValue;
        for (int i = 0; i < asteroidCatalog.Count; i++)
        {
            if (asteroidCatalog[i] != null && asteroidCatalog[i].budgetCost < minEntryCost)
            {
                minEntryCost = asteroidCatalog[i].budgetCost;
            }
        }
        if (minEntryCost == int.MaxValue) minEntryCost = 1;

        int safetyLoop = 150;
        while (remainingBudget >= minEntryCost && safetyLoop-- > 0)
        {
            // Pick an affordable entry from the catalog based on weights
            AsteroidFieldEntry entry = SelectAffordableEntry(remainingBudget);
            if (entry == null || entry.prefab == null) break;

            // Try to find a non-overlapping position
            Vector3 spawnPos;
            if (!TryFindSpawnPosition(placedPositions, out spawnPos))
            {
                // Field is full or congested
                break;
            }

            // If synced with WaveManager, consume threat budget
            if (wm != null)
            {
                if (!wm.ConsumeThreatBudget(entry.budgetCost))
                {
                    break;
                }
            }

            // Spawn pregenerated asteroid
            GameObject asteroid = SpawnAsteroidInstance(entry, spawnPos);
            if (asteroid != null)
            {
                activeAsteroids.Add(asteroid);
                asteroidCostMap[asteroid] = entry.budgetCost;
                placedPositions.Add(spawnPos);
                remainingBudget -= entry.budgetCost;
                totalSpent += entry.budgetCost;

                if (wm != null)
                {
                    wm.RegisterThreat(asteroid);
                }

                OnAsteroidSpawned?.Invoke(asteroid, entry.budgetCost);
            }
        }

        OnBudgetChanged?.Invoke(totalSpent, remainingBudget);
        OnFieldGenerated?.Invoke();

        return totalSpent;
    }

    /// <summary>
    /// Replenishes the field if current active budget is below target budget.
    /// </summary>
    public virtual int ReplenishField()
    {
        CleanDeadReferences();
        EnsurePregeneratedCatalog();
        //ResolveCenterTarget();

        int targetBudget = fieldBudget;
        WaveManager wm = syncWithWaveManager ? ActiveWaveManager : null;
        if (wm != null && wm.CurrentWaveConfig != null)
        {
            targetBudget = wm.CurrentWaveConfig.threatBudget;
        }

        int remainingBudget = targetBudget - CurrentBudgetSpent;
        if (remainingBudget <= 0) return 0;

        if (wm != null && wm.RemainingThreatBudget < remainingBudget)
        {
            remainingBudget = wm.RemainingThreatBudget;
        }

        List<Vector3> placedPositions = new List<Vector3>();
        for (int i = 0; i < activeAsteroids.Count; i++)
        {
            if (activeAsteroids[i] != null) placedPositions.Add(activeAsteroids[i].transform.position);
        }

        int totalSpawnedCost = 0;
        int minEntryCost = int.MaxValue;
        for (int i = 0; i < asteroidCatalog.Count; i++)
        {
            if (asteroidCatalog[i] != null && asteroidCatalog[i].budgetCost < minEntryCost)
            {
                minEntryCost = asteroidCatalog[i].budgetCost;
            }
        }
        if (minEntryCost == int.MaxValue) minEntryCost = 1;

        int safetyLoop = 50;
        while (remainingBudget >= minEntryCost && safetyLoop-- > 0)
        {
            AsteroidFieldEntry entry = SelectAffordableEntry(remainingBudget);
            if (entry == null || entry.prefab == null) break;

            Vector3 spawnPos;
            if (!TryFindSpawnPosition(placedPositions, out spawnPos)) break;

            if (wm != null)
            {
                if (!wm.ConsumeThreatBudget(entry.budgetCost)) break;
            }

            GameObject asteroid = SpawnAsteroidInstance(entry, spawnPos);
            if (asteroid != null)
            {
                activeAsteroids.Add(asteroid);
                asteroidCostMap[asteroid] = entry.budgetCost;
                placedPositions.Add(spawnPos);
                remainingBudget -= entry.budgetCost;
                totalSpawnedCost += entry.budgetCost;

                if (wm != null)
                {
                    wm.RegisterThreat(asteroid);
                }

                OnAsteroidSpawned?.Invoke(asteroid, entry.budgetCost);
            }
        }

        if (totalSpawnedCost > 0)
        {
            OnBudgetChanged?.Invoke(CurrentBudgetSpent, RemainingBudget);
        }

        return totalSpawnedCost;
    }

    /// <summary>
    /// Clears all currently spawned asteroids in the field.
    /// </summary>
    public virtual void ClearField()
    {
        CleanDeadReferences();

        for (int i = 0; i < activeAsteroids.Count; i++)
        {
            GameObject asteroid = activeAsteroids[i];
            if (asteroid != null)
            {
                if (syncWithWaveManager && ActiveWaveManager != null)
                {
                    ActiveWaveManager.UnregisterThreat(asteroid);
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(asteroid);
                else
                    Destroy(asteroid);
#else
                Destroy(asteroid);
#endif
            }
        }

        activeAsteroids.Clear();
        asteroidCostMap.Clear();
        OnBudgetChanged?.Invoke(0, fieldBudget);
        OnFieldCleared?.Invoke();
    }

    /// <summary>
    /// Selects an asteroid entry from the catalog whose cost does not exceed remainingBudget.
    /// </summary>
    public virtual AsteroidFieldEntry SelectAffordableEntry(int remainingBudget)
    {
        if (asteroidCatalog == null || asteroidCatalog.Count == 0) return null;

        List<AsteroidFieldEntry> affordable = new List<AsteroidFieldEntry>();
        float totalWeight = 0f;

        for (int i = 0; i < asteroidCatalog.Count; i++)
        {
            var entry = asteroidCatalog[i];
            if (entry != null && entry.prefab != null && entry.budgetCost <= remainingBudget && entry.weight > 0f)
            {
                affordable.Add(entry);
                totalWeight += entry.weight;
            }
        }

        if (affordable.Count == 0 || totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < affordable.Count; i++)
        {
            cumulative += affordable[i].weight;
            if (roll <= cumulative)
            {
                return affordable[i];
            }
        }

        return affordable[affordable.Count - 1];
    }

    /// <summary>
    /// Finds a non-overlapping spawn position within the radial field bounds.
    /// </summary>
    protected virtual bool TryFindSpawnPosition(List<Vector3> placedPositions, out Vector3 spawnPosition)
    {
        Vector3 center = centerTarget != null ? centerTarget.position : transform.position;
        center.z = 0f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float randomRadius = UnityEngine.Random.Range(minRadius, maxRadius);
            float randomAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            Vector3 candidate = center + new Vector3(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius,
                0f
            );

            // Check distance against already placed asteroids in this batch
            bool tooClose = false;
            for (int i = 0; i < placedPositions.Count; i++)
            {
                if (Vector3.Distance(candidate, placedPositions[i]) < minAsteroidSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            // Check physics overlap in PlayMode
            if (Application.isPlaying)
            {
                Collider[] hits = Physics.OverlapSphere(candidate, minAsteroidSpacing * 0.5f);
                if (hits != null && hits.Length > 0)
                {
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (hits[h] != null && !hits[h].isTrigger)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
            }

            if (!tooClose)
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Instantiates a pregenerated asteroid template, synchronizes its grid, and applies drift dynamics.
    /// </summary>
    protected virtual GameObject SpawnAsteroidInstance(AsteroidFieldEntry entry, Vector3 spawnPosition)
    {
        if (entry == null || entry.prefab == null) return null;

        Quaternion rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        GameObject asteroid = Instantiate(entry.prefab, spawnPosition, rotation, spawnContainer);
        asteroid.name = $"{entry.label}_{activeAsteroids.Count + 1}";
        asteroid.SetActive(true);

        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        if (grid != null)
        {
            grid.magneticAccretion = false;
            grid.SyncGrid();
            grid.UpdateMass();
        }

        Rigidbody rb = asteroid.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector2 driftDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (driftDir.sqrMagnitude < 0.001f) driftDir = Vector2.up;
            float driftSpeed = UnityEngine.Random.Range(minDriftSpeed, maxDriftSpeed);
            rb.linearVelocity = new Vector3(driftDir.x, driftDir.y, 0f) * driftSpeed;

            float tumble = UnityEngine.Random.Range(-maxTumbleSpeed, maxTumbleSpeed);
            rb.angularVelocity = new Vector3(0f, 0f, tumble * Mathf.Deg2Rad);
        }

        return asteroid;
    }

    /// <summary>
    /// Cleans dead or null asteroid references from the active tracking list.
    /// </summary>
    public virtual void CleanDeadReferences()
    {
        activeAsteroids.RemoveAll(go => go == null);
    }

    /// <summary>
    /// Resolves the center target transform for the field boundaries.
    /// </summary>
    public virtual Transform ResolveCenterTarget()
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
        return centerTarget;
    }

    #endregion

    #region Scene View Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 center = centerTarget != null ? centerTarget.position : transform.position;
        center.z = 0f;

        // Outer field boundary
        Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.45f);
        Gizmos.DrawWireSphere(center, maxRadius);

        // Inner safe zone
        Gizmos.color = new Color(1.0f, 0.3f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(center, minRadius);

        // Draw line from spawner to center if different
        if (centerTarget != null && centerTarget != transform)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(transform.position, centerTarget.position);
        }
    }

    #endregion
}

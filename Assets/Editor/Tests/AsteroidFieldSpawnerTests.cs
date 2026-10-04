using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class AsteroidFieldSpawnerTests
{
    private GameObject _spawnerObj;
    private AsteroidFieldSpawner _spawner;
    private GameObject _centerObj;
    private GameObject _waveManagerObj;
    private WaveManager _waveManager;

    [SetUp]
    public void SetUp()
    {
        // Clean up any stray singletons
        foreach (var wm in Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(wm.gameObject);
        }
        foreach (var s in Object.FindObjectsByType<AsteroidFieldSpawner>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(s.gameObject);
        }

        _centerObj = new GameObject("TestCenter");
        _centerObj.transform.position = Vector3.zero;

        _spawnerObj = new GameObject("TestFieldSpawner");
        _spawner = _spawnerObj.AddComponent<AsteroidFieldSpawner>();
        _spawner.centerTarget = _centerObj.transform;
        _spawner.generateOnStart = false;
        _spawner.syncWithWaveManager = false;
    }

    [TearDown]
    public void TearDown()
    {
        if (_spawner != null)
        {
            _spawner.ClearField();
        }

        if (_spawnerObj != null) Object.DestroyImmediate(_spawnerObj);
        if (_centerObj != null) Object.DestroyImmediate(_centerObj);
        if (_waveManagerObj != null) Object.DestroyImmediate(_waveManagerObj);

        WaveManager.Instance = null;
    }

    [Test]
    public void EnsurePregeneratedCatalog_BuildsDiverseCatalogOfTemplates()
    {
        _spawner.asteroidCatalog.Clear();
        _spawner.EnsurePregeneratedCatalog();

        Assert.GreaterOrEqual(_spawner.asteroidCatalog.Count, 4);

        foreach (var entry in _spawner.asteroidCatalog)
        {
            Assert.IsNotNull(entry.prefab);
            Assert.Greater(entry.budgetCost, 0);
            Assert.Greater(entry.weight, 0f);

            AsteroidGrid grid = entry.prefab.GetComponent<AsteroidGrid>();
            Assert.IsNotNull(grid);
            Assert.Greater(grid.UpdateMass(), 0);
        }
    }

    [Test]
    public void GenerateField_RespectsDefinedBudget()
    {
        int targetBudget = 18;
        _spawner.fieldBudget = targetBudget;
        _spawner.minRadius = 2f;
        _spawner.maxRadius = 20f;
        _spawner.minAsteroidSpacing = 2f;

        int spent = _spawner.GenerateField(targetBudget);

        Assert.LessOrEqual(spent, targetBudget, "Total spent budget must not exceed target budget");
        Assert.Greater(spent, 0, "Expected some budget to be spent");
        Assert.AreEqual(spent, _spawner.CurrentBudgetSpent);
        Assert.Greater(_spawner.ActiveCount, 0);
    }

    [Test]
    public void SelectAffordableEntry_NeverOverspendsBudget()
    {
        _spawner.asteroidCatalog.Clear();

        GameObject gSmall = new GameObject("Small");
        GameObject gMed = new GameObject("Med");
        GameObject gBig = new GameObject("Big");

        _spawner.asteroidCatalog.Add(new AsteroidFieldEntry(gSmall, budgetCost: 3, weight: 1f));
        _spawner.asteroidCatalog.Add(new AsteroidFieldEntry(gMed, budgetCost: 6, weight: 1f));
        _spawner.asteroidCatalog.Add(new AsteroidFieldEntry(gBig, budgetCost: 12, weight: 1f));

        // When budget is 15: can afford all
        var entry1 = _spawner.SelectAffordableEntry(15);
        Assert.IsNotNull(entry1);
        Assert.LessOrEqual(entry1.budgetCost, 15);

        // When budget is 5: can ONLY afford small (cost 3)
        for (int i = 0; i < 10; i++)
        {
            var entry2 = _spawner.SelectAffordableEntry(5);
            Assert.IsNotNull(entry2);
            Assert.AreEqual(3, entry2.budgetCost);
        }

        // When budget is 2: cannot afford any (cheapest is 3)
        var entry3 = _spawner.SelectAffordableEntry(2);
        Assert.IsNull(entry3);

        Object.DestroyImmediate(gSmall);
        Object.DestroyImmediate(gMed);
        Object.DestroyImmediate(gBig);
    }

    [Test]
    public void GenerateField_EnforcesMinRadiusAndSpacing()
    {
        _spawner.fieldBudget = 25;
        _spawner.minRadius = 5f;
        _spawner.maxRadius = 25f;
        _spawner.minAsteroidSpacing = 3.5f;

        _spawner.GenerateField(25);

        var asteroids = _spawner.ActiveAsteroids;
        Assert.Greater(asteroids.Count, 1);

        for (int i = 0; i < asteroids.Count; i++)
        {
            GameObject a = asteroids[i];
            float distFromCenter = Vector3.Distance(a.transform.position, _centerObj.transform.position);
            Assert.GreaterOrEqual(distFromCenter, _spawner.minRadius - 0.05f, "Asteroid must spawn outside minRadius safe zone");

            for (int j = i + 1; j < asteroids.Count; j++)
            {
                GameObject b = asteroids[j];
                float distBetween = Vector3.Distance(a.transform.position, b.transform.position);
                Assert.GreaterOrEqual(distBetween, _spawner.minAsteroidSpacing - 0.05f, "Asteroids must maintain minAsteroidSpacing");
            }
        }
    }

    [Test]
    public void ClearField_DestroysAllSpawnedAsteroidsAndResetsBudget()
    {
        _spawner.fieldBudget = 20;
        _spawner.GenerateField();

        Assert.Greater(_spawner.ActiveCount, 0);
        Assert.Greater(_spawner.CurrentBudgetSpent, 0);

        _spawner.ClearField();

        Assert.AreEqual(0, _spawner.ActiveCount);
        Assert.AreEqual(0, _spawner.CurrentBudgetSpent);
        Assert.AreEqual(_spawner.fieldBudget, _spawner.RemainingBudget);
    }

    [Test]
    public void WaveManager_Integration_ConsumesThreatBudgetAndRegistersThreats()
    {
        _waveManagerObj = new GameObject("TestWaveManager");
        _waveManager = _waveManagerObj.AddComponent<WaveManager>();
        WaveManager.Instance = _waveManager;

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.threatBudget = 20;
        _waveManager.authoredWaves = new List<WaveDefinition> { wave };
        _waveManager.TriggerCombatImmediately();

        int initialWmBudget = _waveManager.RemainingThreatBudget;
        Assert.AreEqual(20, initialWmBudget);

        _spawner.syncWithWaveManager = true;
        _spawner.fieldBudget = 20;
        int spent = _spawner.GenerateField(20);

        Assert.Greater(spent, 0);
        Assert.AreEqual(initialWmBudget - spent, _waveManager.RemainingThreatBudget);
        Assert.AreEqual(_spawner.ActiveCount, _waveManager.ActiveThreatCount);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void WaveManager_Integration_ClearsFieldOnWaveComplete()
    {
        _waveManagerObj = new GameObject("TestWaveManager");
        _waveManager = _waveManagerObj.AddComponent<WaveManager>();
        WaveManager.Instance = _waveManager;

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.threatBudget = 15;
        _waveManager.authoredWaves = new List<WaveDefinition> { wave };
        _waveManager.TriggerCombatImmediately();

        _spawner.syncWithWaveManager = true;
        _spawner.clearOnWaveComplete = true;
        _spawner.GenerateField(15);

        Assert.Greater(_spawner.ActiveCount, 0);

        _waveManager.CompleteWave();

        Assert.AreEqual(0, _spawner.ActiveCount);
        Assert.AreEqual(0, _spawner.CurrentBudgetSpent);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void ReplenishField_SpawnsNewAsteroidsToRestoreBudget()
    {
        _spawner.fieldBudget = 20;
        _spawner.GenerateField(20);

        int initialCount = _spawner.ActiveCount;
        int initialSpent = _spawner.CurrentBudgetSpent;
        Assert.Greater(initialCount, 1);

        // Destroy the first spawned asteroid
        GameObject firstAsteroid = _spawner.ActiveAsteroids[0];
        Object.DestroyImmediate(firstAsteroid);

        Assert.AreEqual(initialCount - 1, _spawner.ActiveCount);
        Assert.Less(_spawner.CurrentBudgetSpent, initialSpent);

        // Replenish field
        int replenishedCost = _spawner.ReplenishField();

        Assert.Greater(replenishedCost, 0);
        Assert.GreaterOrEqual(_spawner.ActiveCount, initialCount);
    }
}

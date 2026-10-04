using NUnit.Framework;
using UnityEngine;

public class BlockSpawnerTests
{
    private GameObject spawnerObj;
    private BlockSpawner spawner;
    private GameObject dummyBlockPrefab;

    [SetUp]
    public void SetUp()
    {
        // Create dummy prefab for clean unit testing without depending on external asset loading
        dummyBlockPrefab = new GameObject("DummyBlock");
        dummyBlockPrefab.AddComponent<block0>();

        spawnerObj = new GameObject("TestBlockSpawner");
        spawner = spawnerObj.AddComponent<BlockSpawner>();
        spawner.blockPrefab = dummyBlockPrefab;
        spawner.syncWithWaveManager = false; // Isolated unit tests
        spawner.radius = 10f;
        spawner.minRadius = 2f;
        spawner.rate = 1.0f;
        spawner.max_limit = 5;
    }

    [TearDown]
    public void TearDown()
    {
        if (spawner != null)
        {
            spawner.ClearAllSpawned();
        }

        if (spawnerObj != null)
        {
            Object.DestroyImmediate(spawnerObj);
        }

        if (dummyBlockPrefab != null)
        {
            Object.DestroyImmediate(dummyBlockPrefab);
        }
    }

    [Test]
    public void BlockSpawner_CalculateRandomSpawnPosition_WithinRadiusBounds()
    {
        spawnerObj.transform.position = new Vector3(10f, 20f, 0f);
        spawner.centerTarget = spawnerObj.transform;

        for (int i = 0; i < 50; i++)
        {
            Vector3 pos = spawner.CalculateRandomSpawnPosition();
            float distance = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(spawnerObj.transform.position.x, spawnerObj.transform.position.y));

            Assert.GreaterOrEqual(distance, spawner.minRadius - 0.01f, "Distance should be at or above minRadius");
            Assert.LessOrEqual(distance, spawner.radius + 0.01f, "Distance should be at or below radius");
            Assert.AreEqual(spawnerObj.transform.position.z, pos.z, 0.001f, "Z depth should remain aligned with center");
        }
    }

    [Test]
    public void BlockSpawner_Tick_SpawnsAtConfiguredRate()
    {
        Assert.AreEqual(0, spawner.ActiveCount);

        // Advance half the rate interval -> should not spawn yet
        spawner.Tick(0.5f);
        Assert.AreEqual(0, spawner.ActiveCount);

        // Advance past rate interval -> spawns 1 block
        spawner.Tick(0.6f);
        Assert.AreEqual(1, spawner.ActiveCount);

        // Advance past rate again -> spawns 2nd block
        spawner.Tick(1.0f);
        Assert.AreEqual(2, spawner.ActiveCount);
    }

    [Test]
    public void BlockSpawner_MaxLimit_EnforcesCapacityCap()
    {
        spawner.max_limit = 3;

        // Try spawning burst larger than max_limit
        spawner.SpawnBurst(10);

        Assert.AreEqual(3, spawner.ActiveCount);
        Assert.IsTrue(spawner.IsAtCapacity);

        // Further tick attempts should not spawn additional blocks
        spawner.Tick(5.0f);
        Assert.AreEqual(3, spawner.ActiveCount);
    }

    [Test]
    public void BlockSpawner_DestroyedBlocks_FreeCapacityForNewSpawns()
    {
        spawner.max_limit = 2;
        GameObject spawned1 = null;
        GameObject spawned2 = null;
        spawner.OnBlockSpawned += b =>
        {
            if (spawned1 == null) spawned1 = b;
            else spawned2 = b;
        };

        spawner.SpawnBurst(2);
        Assert.AreEqual(2, spawner.ActiveCount);
        Assert.IsTrue(spawner.IsAtCapacity);
        Assert.IsNotNull(spawned1);
        Assert.IsNotNull(spawned2);

        // Attempting to spawn another block returns null because at capacity
        GameObject extraBlock = spawner.SpawnSingleBlock();
        Assert.IsNull(extraBlock);

        // Destroy one spawned block
        Object.DestroyImmediate(spawned1);

        // Clean dead references and check count
        spawner.CleanDeadReferences();
        Assert.AreEqual(1, spawner.ActiveCount);
        Assert.IsFalse(spawner.IsAtCapacity);

        // Spawning is now available again
        spawner.Tick(1.1f);
        Assert.AreEqual(2, spawner.ActiveCount);
    }

    [Test]
    public void BlockSpawner_WaveManagerSync_RespectsCanSpawn()
    {
        GameObject wmObj = new GameObject("TestWaveManager");
        WaveManager wm = wmObj.AddComponent<WaveManager>();
        spawner.waveManager = wm;
        spawner.syncWithWaveManager = true;

        // WaveManager is initially Idle (CanSpawn == false)
        Assert.IsFalse(wm.CanSpawn);

        spawner.Tick(2.0f);
        Assert.AreEqual(0, spawner.ActiveCount, "Should not spawn when WaveManager cannot spawn");

        // Start run and trigger combat
        wm.StartRun();
        wm.TriggerCombatImmediately();
        Assert.IsTrue(wm.CanSpawn);

        spawner.Tick(2.0f);
        Assert.AreEqual(1, spawner.ActiveCount, "Should spawn when WaveManager CanSpawn is true");

        Object.DestroyImmediate(wmObj);
    }

    [Test]
    public void BlockSpawner_SpawnSingleBlock_AppliesInitialDriftVelocity()
    {
        spawner.initialDrift = 4.0f;
        GameObject block = spawner.SpawnSingleBlock();

        Assert.IsNotNull(block);
        Rigidbody rb = block.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb, "Spawned block must have Rigidbody immediately");
        Assert.Greater(rb.linearVelocity.magnitude, 0.5f, "Spawned block should have drift velocity applied");
    }

    [Test]
    public void BlockSpawner_SpawnSingleAsteroid_SpawnsEmptyAsteroidGridWithOnlyOneCore()
    {
        spawner.asteroidCoreHits = 5;
        GameObject astObj = spawner.SpawnSingleAsteroid();

        Assert.IsNotNull(astObj, "SpawnSingleAsteroid should return a valid GameObject");
        Assert.AreEqual(1, spawner.ActiveAsteroidCount);

        AsteroidGrid grid = astObj.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid, "Spawned asteroid must contain AsteroidGrid component");

        // Verify it contains only the core block
        Assert.IsNotNull(grid.coreBlock, "AsteroidGrid must have a coreBlock assigned");
        Assert.IsTrue(grid.coreBlock.isCore, "Core block must have isCore flag true");
        Assert.AreEqual("core", grid.coreBlock.tag, "Core block must have 'core' tag");
        Assert.AreEqual(5, grid.coreBlock._hits, "Core block should have configured asteroidCoreHits");

        // Authoritative grid block count: only 1 (the core at (0, 0))
        Assert.AreEqual(1, grid.CurrentBlockCount, "Empty AsteroidGrid must contain exactly 1 block (the core)");

        // Verify Rigidbody exists
        Rigidbody rb = astObj.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb, "Spawned asteroid must have a Rigidbody");
    }

    [Test]
    public void BlockSpawner_AsteroidCapacity_EnforcesMaxAsteroidsLimit()
    {
        spawner.max_asteroids = 2;

        GameObject ast1 = spawner.SpawnSingleAsteroid();
        GameObject ast2 = spawner.SpawnSingleAsteroid();
        Assert.IsNotNull(ast1);
        Assert.IsNotNull(ast2);
        Assert.AreEqual(2, spawner.ActiveAsteroidCount);
        Assert.IsTrue(spawner.IsAsteroidAtCapacity);

        // 3rd attempt returns null
        GameObject ast3 = spawner.SpawnSingleAsteroid();
        Assert.IsNull(ast3, "Spawning past capacity should return null");

        // Destroy one and clean
        Object.DestroyImmediate(ast1);
        spawner.CleanDeadReferences();

        Assert.AreEqual(1, spawner.ActiveAsteroidCount);
        Assert.IsFalse(spawner.IsAsteroidAtCapacity);

        GameObject ast4 = spawner.SpawnSingleAsteroid();
        Assert.IsNotNull(ast4);
        Assert.AreEqual(2, spawner.ActiveAsteroidCount);
    }

    [Test]
    public void BlockSpawner_WaveManager_SyncsAsteroidThreatsAndBudget()
    {
        GameObject wmObj = new GameObject("TestWaveManager");
        WaveManager wm = wmObj.AddComponent<WaveManager>();
        spawner.waveManager = wm;
        spawner.syncWithWaveManager = true;
        spawner.asteroidThreatCost = 3;

        wm.StartRun();
        wm.TriggerCombatImmediately();

        int initialBudget = wm.RemainingThreatBudget;
        Assert.GreaterOrEqual(initialBudget, 3);
        int initialThreats = wm.ActiveThreatCount;

        // Spawn asteroid burst of 1
        spawner.SpawnAsteroidBurst(1);

        Assert.AreEqual(1, spawner.ActiveAsteroidCount);
        Assert.AreEqual(initialThreats + 1, wm.ActiveThreatCount, "WaveManager should have 1 additional active threat");
        Assert.AreEqual(initialBudget - 3, wm.RemainingThreatBudget, "WaveManager should consume threat budget for asteroid");

        // Clearing spawner unregisters threats
        spawner.ClearAllSpawned();
        Assert.AreEqual(0, spawner.ActiveAsteroidCount);
        Assert.AreEqual(initialThreats, wm.ActiveThreatCount, "Threat should be unregistered after ClearAllSpawned");

        Object.DestroyImmediate(wmObj);
    }

    [Test]
    public void BlockSpawner_WaveManager_AdaptsDifficultyOnWaveStarted()
    {
        GameObject wmObj = new GameObject("TestWaveManager");
        WaveManager wm = wmObj.AddComponent<WaveManager>();
        spawner.waveManager = wm;
        spawner.syncWithWaveManager = true;
        spawner.adaptWaveDifficulty = true;

        WaveDefinition def = WaveDefinition.Create(minMass: 7, blockLevel: 4, shellLevel: 6);
        wm.authoredWaves.Clear();
        wm.authoredWaves.Add(def);

        wm.StartRun();
        wm.TriggerCombatImmediately();

        Assert.AreEqual(7, spawner.asteroidCoreHits, "Asteroid core hits should adapt from WaveDefinition.minMass");
        Assert.AreEqual(4, spawner.minHits, "Min hits should adapt from WaveDefinition.blockLevel");
        Assert.AreEqual(6, spawner.maxHits, "Max hits should adapt from WaveDefinition.shellLevel");

        Object.DestroyImmediate(wmObj);
    }
}


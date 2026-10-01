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
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class LineSpawnerTests
{
    private List<GameObject> _spawnedObjects;

    [SetUp]
    public void SetUp()
    {
        _spawnedObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i] != null)
            {
                Object.DestroyImmediate(_spawnedObjects[i]);
            }
        }
        _spawnedObjects.Clear();
    }

    private LineSpawner CreateLineSpawner()
    {
        var spawnerGo = new GameObject("TestLineSpawner");
        _spawnedObjects.Add(spawnerGo);
        var spawner = spawnerGo.AddComponent<LineSpawner>();
        spawner.syncWithWaveManager = false;
        return spawner;
    }

    [Test]
    public void LineSpawner_Default_ShellConfiguration_Defaults()
    {
        var spawner = CreateLineSpawner();

        Assert.IsFalse(spawner.spawnAsteroidShell);
        Assert.AreEqual(1, spawner.shellThickness);
        Assert.AreEqual(2, spawner.minShellHits);
        Assert.AreEqual(2, spawner.maxShellHits);
        Assert.IsNull(spawner.shellBlockPrefab);
        Assert.IsFalse(spawner.adaptWaveDifficulty);
    }

    [Test]
    public void LineSpawner_GetShellCoordinates_CalculatesCorrectCoordinates()
    {
        // Negative thickness
        var neg = LineSpawner.GetShellCoordinates(-1);
        Assert.AreEqual(0, neg.Count);

        // Thickness 0 -> 4 cardinal/diamond points
        var ring0 = LineSpawner.GetShellCoordinates(0);
        Assert.AreEqual(4, ring0.Count);
        Assert.IsTrue(ring0.Contains(new Vector2Int(0, 1)));
        Assert.IsTrue(ring0.Contains(new Vector2Int(1, 0)));
        Assert.IsTrue(ring0.Contains(new Vector2Int(0, -1)));
        Assert.IsTrue(ring0.Contains(new Vector2Int(-1, 0)));
        Assert.IsFalse(ring0.Contains(Vector2Int.zero));

        // Thickness 1 -> 8 points (Chebyshev distance 1)
        var ring1 = LineSpawner.GetShellCoordinates(1);
        Assert.AreEqual(8, ring1.Count);
        Assert.IsFalse(ring1.Contains(Vector2Int.zero));
        foreach (var c in ring1)
        {
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)));
        }

        // Thickness 2 -> 8 (r=1) + 16 (r=2) = 24 points
        var ring2 = LineSpawner.GetShellCoordinates(2);
        Assert.AreEqual(24, ring2.Count);
        Assert.IsFalse(ring2.Contains(Vector2Int.zero));
        foreach (var c in ring2)
        {
            int r = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y));
            Assert.IsTrue(r == 1 || r == 2);
        }
    }

    [Test]
    public void LineSpawner_PropertyAliases_SyncProperly()
    {
        var spawner = CreateLineSpawner();

        // generateAsteroidShell alias
        spawner.generateAsteroidShell = true;
        Assert.IsTrue(spawner.spawnAsteroidShell);
        Assert.IsTrue(spawner.spawnShell);

        // spawnShell alias
        spawner.spawnShell = false;
        Assert.IsFalse(spawner.spawnAsteroidShell);
        Assert.IsFalse(spawner.generateAsteroidShell);

        // asteroidShellThickness alias
        spawner.asteroidShellThickness = 3;
        Assert.AreEqual(3, spawner.shellThickness);

        // shellHits and asteroidShellHits setters
        spawner.shellHits = 6;
        Assert.AreEqual(6, spawner.minShellHits);
        Assert.AreEqual(6, spawner.maxShellHits);
        Assert.AreEqual(6, spawner.shellHits);

        spawner.asteroidShellHits = 4;
        Assert.AreEqual(4, spawner.minShellHits);
        Assert.AreEqual(4, spawner.maxShellHits);
        Assert.AreEqual(4, spawner.asteroidShellHits);

        // shellPrefab alias
        var dummyPrefab = new GameObject("DummyPrefab");
        _spawnedObjects.Add(dummyPrefab);
        spawner.shellPrefab = dummyPrefab;
        Assert.AreSame(dummyPrefab, spawner.shellBlockPrefab);
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_WithoutShell_SpawnsSingleCoreAsteroid()
    {
        var spawner = CreateLineSpawner();
        spawner.spawnAsteroidShell = false;
        spawner.generateAsteroidCluster = false;
        spawner.asteroidCoreHits = 5;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.IsNotNull(asteroid);
        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid);

        // Single core asteroid only has 1 block
        Assert.AreEqual(1, grid.CurrentBlockCount);
        Assert.IsNotNull(grid.coreBlock);
        Assert.AreEqual(5f, grid.coreBlock._hits);
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_WithShellThickness1_SpawnsCoreAnd8ShellBlocks()
    {
        var spawner = CreateLineSpawner();
        spawner.spawnAsteroidShell = true;
        spawner.shellThickness = 1;
        spawner.minShellHits = 3;
        spawner.maxShellHits = 3;
        spawner.asteroidCoreHits = 4;
        spawner.generateAsteroidCluster = false;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.IsNotNull(asteroid);
        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid);

        // 1 core + 8 shell blocks = 9 total blocks
        Assert.AreEqual(9, grid.CurrentBlockCount);
        Assert.IsNotNull(grid.coreBlock);
        Assert.AreEqual(4f, grid.coreBlock._hits);

        // Verify all 8 surrounding sockets exist and have hits = 3
        var shellCoords = LineSpawner.GetShellCoordinates(1);
        Assert.AreEqual(8, shellCoords.Count);
        foreach (var coord in shellCoords)
        {
            Assert.IsTrue(grid.HasBlockAt(coord), $"Grid should have shell block at {coord}");
            GameObject blockGo = grid.GetBlockAt(coord);
            Assert.IsNotNull(blockGo, $"Block at {coord} should not be null");
            BlockBase b = blockGo.GetComponent<BlockBase>();
            Assert.IsNotNull(b);
            Assert.AreEqual(3f, b._hits);
            Assert.IsFalse(b.isCore);
        }
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_WithShellThickness0_SpawnsCoreAnd4ShellBlocks()
    {
        var spawner = CreateLineSpawner();
        spawner.spawnAsteroidShell = true;
        spawner.shellThickness = 0;
        spawner.minShellHits = 2;
        spawner.maxShellHits = 2;
        spawner.asteroidCoreHits = 3;
        spawner.generateAsteroidCluster = false;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.IsNotNull(asteroid);
        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid);

        // 1 core + 4 diamond shell blocks = 5 total blocks
        Assert.AreEqual(5, grid.CurrentBlockCount);
        Assert.IsTrue(grid.HasBlockAt(new Vector2Int(0, 1)));
        Assert.IsTrue(grid.HasBlockAt(new Vector2Int(1, 0)));
        Assert.IsTrue(grid.HasBlockAt(new Vector2Int(0, -1)));
        Assert.IsTrue(grid.HasBlockAt(new Vector2Int(-1, 0)));
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_WithShellThickness2_SpawnsCoreAnd24ShellBlocks()
    {
        var spawner = CreateLineSpawner();
        spawner.spawnAsteroidShell = true;
        spawner.shellThickness = 2;
        spawner.minShellHits = 1;
        spawner.maxShellHits = 1;
        spawner.asteroidCoreHits = 6;
        spawner.generateAsteroidCluster = false;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.IsNotNull(asteroid);
        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid);

        // 1 core + 24 shell blocks = 25 total blocks
        Assert.AreEqual(25, grid.CurrentBlockCount);
        Assert.AreEqual(6f, grid.coreBlock._hits);
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_Fires_OnAsteroidShellGenerated_Event()
    {
        var spawner = CreateLineSpawner();
        spawner.spawnAsteroidShell = true;
        spawner.shellThickness = 1;
        spawner.minShellHits = 2;
        spawner.maxShellHits = 2;

        GameObject eventAsteroid = null;
        List<GameObject> eventShellBlocks = null;
        spawner.OnAsteroidShellGenerated += (ast, blocks) =>
        {
            eventAsteroid = ast;
            eventShellBlocks = blocks;
        };

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.AreSame(asteroid, eventAsteroid);
        Assert.IsNotNull(eventShellBlocks);
        Assert.AreEqual(8, eventShellBlocks.Count);
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_WithCustomShellPrefab_InstantiatesShellPrefab()
    {
        var spawner = CreateLineSpawner();

        // Create a custom block prefab with a distinct tag or child
        var customShellPrefab = new GameObject("CustomShellBlock");
        _spawnedObjects.Add(customShellPrefab);
        customShellPrefab.AddComponent<BoxCollider>();
        customShellPrefab.AddComponent<BlockAsteroid>();

        spawner.shellBlockPrefab = customShellPrefab;
        spawner.spawnAsteroidShell = true;
        spawner.shellThickness = 1;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        AsteroidGrid grid = asteroid.GetComponent<AsteroidGrid>();
        Assert.IsNotNull(grid);

        GameObject shellBlock = grid.GetBlockAt(new Vector2Int(0, 1));
        Assert.IsNotNull(shellBlock);
        Assert.IsNotNull(shellBlock.GetComponent<BoxCollider>());
    }

    [Test]
    public void LineSpawner_HandleWaveStarted_WithAdaptWaveDifficulty_UpdatesShellAndCoreStats()
    {
        var spawner = CreateLineSpawner();
        spawner.syncWithWaveManager = true;
        spawner.adaptWaveDifficulty = true;

        var waveDef = ScriptableObject.CreateInstance<WaveDefinition>();
        waveDef.shellLevel = 4;
        waveDef.blockLevel = 2;
        waveDef.minMass = 6;

        spawner.HandleWaveStarted(1, waveDef);

        Assert.AreEqual(4, spawner.minShellHits);
        Assert.AreEqual(4, spawner.maxShellHits);
        Assert.AreEqual(2, spawner.minBlockHits);
        Assert.AreEqual(4, spawner.maxBlockHits);
        Assert.AreEqual(6, spawner.asteroidCoreHits);

        Object.DestroyImmediate(waveDef);
    }

    [Test]
    public void LineSpawner_GetAsteroidClearanceRadius_ExpandsWithShellThickness()
    {
        var spawner = CreateLineSpawner();
        spawner.asteroidClearanceRadius = 2.0f;
        spawner.spawnAsteroidShell = false;
        spawner.shellThickness = 3;

        // When shell is disabled, returns base clearance radius
        Assert.AreEqual(2.0f, spawner.GetAsteroidClearanceRadius());

        // When shell is enabled, accounts for shell thickness (1.2 + 3 = 4.2)
        spawner.spawnAsteroidShell = true;
        Assert.AreEqual(4.2f, spawner.GetAsteroidClearanceRadius(), 0.001f);
    }

    [Test]
    public void LineSpawner_Default_SeparateImpulseForces_Defaults()
    {
        var spawner = CreateLineSpawner();

        Assert.AreEqual(5f, spawner.blockImpulseForce);
        Assert.AreEqual(1f, spawner.blockImpulseVariance);
        Assert.AreEqual(15f, spawner.asteroidImpulseForce);
        Assert.AreEqual(2f, spawner.asteroidImpulseVariance);
    }

    [Test]
    public void LineSpawner_SpawnBlockAt_AppliesBlockImpulseForce()
    {
        var spawner = CreateLineSpawner();
        spawner.blockImpulseForce = 10f;
        spawner.blockImpulseVariance = 0f;
        spawner.impulseDirection = Vector3.up;
        spawner.spreadAngle = 0f;

        GameObject block = spawner.SpawnBlockAt(Vector3.zero);
        _spawnedObjects.Add(block);

        Assert.IsNotNull(block);
        Rigidbody rb = block.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb);

        Physics.simulationMode = SimulationMode.Script;
        Physics.Simulate(0.02f);
        Physics.simulationMode = SimulationMode.FixedUpdate;

        Assert.IsTrue(rb.linearVelocity.y > 0f, $"Block linearVelocity.y should be positive but was {rb.linearVelocity.y}");
    }

    [Test]
    public void LineSpawner_SpawnAsteroidAt_AppliesAsteroidImpulseForce()
    {
        var spawner = CreateLineSpawner();
        spawner.asteroidImpulseForce = 30f;
        spawner.asteroidImpulseVariance = 0f;
        spawner.impulseDirection = Vector3.right;
        spawner.spreadAngle = 0f;

        GameObject asteroid = spawner.SpawnAsteroidAt(Vector3.zero);
        _spawnedObjects.Add(asteroid);

        Assert.IsNotNull(asteroid);
        Rigidbody rb = asteroid.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb);

        Physics.simulationMode = SimulationMode.Script;
        Physics.Simulate(0.02f);
        Physics.simulationMode = SimulationMode.FixedUpdate;

        Assert.IsTrue(rb.linearVelocity.x > 0f, $"Asteroid linearVelocity.x should be positive but was {rb.linearVelocity.x}");
    }
}

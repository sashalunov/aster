using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AsteroidGridTests
{
    private GameObject asteroidObj;
    private AsteroidGrid asteroidGrid;
    private GameObject dummyBlockPrefab;

    [SetUp]
    public void SetUp()
    {
        dummyBlockPrefab = new GameObject("DummyBlock");
        dummyBlockPrefab.AddComponent<block0>();

        asteroidObj = new GameObject("TestAsteroidGrid");
        asteroidObj.AddComponent<Rigidbody>();
        asteroidGrid = asteroidObj.AddComponent<AsteroidGrid>();
        asteroidGrid._block = dummyBlockPrefab;
        asteroidGrid._core_hits = 3;
    }

    [TearDown]
    public void TearDown()
    {
        if (asteroidGrid != null)
        {
            asteroidGrid.Clear();
        }

        if (asteroidObj != null)
        {
            Object.DestroyImmediate(asteroidObj);
        }

        if (dummyBlockPrefab != null)
        {
            Object.DestroyImmediate(dummyBlockPrefab);
        }
    }

    [Test]
    public void AsteroidGrid_GenerateHarmonicRose_PopulatesGridBlocks()
    {
        asteroidGrid.algorithm = AsteroidGrid.GenerationAlgorithm.HarmonicRose;
        asteroidGrid.targetBlockCount = 8;
        asteroidGrid.gridRadius = 3;

        int totalMass = asteroidGrid.generate_asteroid(3, 6, 8, 0, 1, 1);

        Assert.GreaterOrEqual(asteroidGrid.ActiveBlockCount, 4);
        Assert.Greater(totalMass, 3);
    }

    [Test]
    public void AsteroidGrid_GenerateJuliaFractal_CreatesValidCoordinates()
    {
        List<Vector2Int> coords = asteroidGrid.GenerateJuliaFractalCoords(10, 4);

        Assert.IsNotNull(coords);
        Assert.Greater(coords.Count, 0);

        // Core (0,0) should not be duplicated in result
        Assert.IsFalse(coords.Contains(Vector2Int.zero));

        // Coordinates should be within radius
        foreach (Vector2Int c in coords)
        {
            Assert.LessOrEqual(Mathf.Abs(c.x), 4);
            Assert.LessOrEqual(Mathf.Abs(c.y), 4);
        }
    }

    [Test]
    public void AsteroidGrid_GenerateDiffusionAggregation_CreatesConnectedTree()
    {
        List<Vector2Int> coords = asteroidGrid.GenerateDiffusionAggregationCoords(8, 3);

        Assert.IsNotNull(coords);
        Assert.Greater(coords.Count, 0);
        Assert.IsFalse(coords.Contains(Vector2Int.zero));
    }

    [Test]
    public void AsteroidGrid_CheckForUnconnected_DetachesOrphanBlocksViaBFS()
    {
        // Setup a chain: Core(0,0) -> Bridge(1,0) -> Outer(2,0)
        GameObject bridgeBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 2);
        GameObject outerBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(2, 0), 2);

        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(1, 0)));
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)));

        bool detachedEventFired = false;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            detachedEventFired = true;
            Assert.AreEqual(new Vector2Int(2, 0), coord);
        };

        // Destroy the bridge block (1, 0)
        Object.DestroyImmediate(bridgeBlock);

        // Run BFS connectivity check (zero raycasts)
        asteroidGrid.check_for_unconected();

        // Outer block (2,0) is now an orphan and must be detached
        Assert.IsTrue(detachedEventFired);
        Assert.AreEqual(0, asteroidGrid.ActiveBlockCount);
        Assert.IsFalse(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)));

        if (outerBlock != null)
        {
            Object.DestroyImmediate(outerBlock);
        }
    }

    [Test]
    public void AsteroidGrid_TryReattachBlock_BindsLooseBlockToAdjacentSlot()
    {
        // Core is at (0,0). Place a loose block at (0, 1) world position
        GameObject looseBlock = new GameObject("LooseBlock");
        looseBlock.transform.position = asteroidObj.transform.position + new Vector3(0f, 1f, 0f);
        block0 b0 = looseBlock.AddComponent<block0>();
        b0._detached = true;
        b0.SetHits(2);

        bool reattachedEventFired = false;
        asteroidGrid.OnBlockReattached += (b, coord) =>
        {
            reattachedEventFired = true;
            Assert.AreEqual(new Vector2Int(0, 1), coord);
        };

        bool success = asteroidGrid.TryReattachBlock(looseBlock);

        Assert.IsTrue(success);
        Assert.IsTrue(reattachedEventFired);
        Assert.AreEqual(1, asteroidGrid.ActiveBlockCount);
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(0, 1)));
        Assert.AreEqual(asteroidObj.transform, looseBlock.transform.parent);
        Assert.IsFalse(b0._detached);

        Object.DestroyImmediate(looseBlock);
    }

    [Test]
    public void AsteroidGrid_MagneticAccretion_PullsAndSnapsStandaloneBlock()
    {
        asteroidGrid.magneticAccretion = true;
        asteroidGrid.accretionRadius = 10f;
        asteroidGrid.attachDistance = 2.0f;

        // Create standalone block within accretion radius and attach distance
        GameObject standaloneBlock = new GameObject("StandaloneBlock");
        standaloneBlock.transform.position = asteroidObj.transform.position + new Vector3(0f, 1.2f, 0f);
        standaloneBlock.AddComponent<BoxCollider>();
        standaloneBlock.AddComponent<Rigidbody>();
        block0 b0 = standaloneBlock.AddComponent<block0>();
        b0._detached = true;
        b0.SetHits(2);

        int initialCount = asteroidGrid.ActiveBlockCount;
        Assert.AreEqual(0, initialCount);

        // Process accretion step
        asteroidGrid.ProcessMagneticAccretion(0.02f);

        Assert.AreEqual(1, asteroidGrid.ActiveBlockCount, "Block should be accreted into grid");
        Assert.AreEqual(asteroidObj.transform, standaloneBlock.transform.parent, "Block should be parented to asteroid");
        Assert.IsFalse(b0._detached, "Block should no longer be marked detached");

        Object.DestroyImmediate(standaloneBlock);
    }
}

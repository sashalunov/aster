using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BlockScaleAttachmentTests
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

    [Test]
    public void BlockBase_EndLife_EnforcesVector3OneScale_WhenDetachedToNull()
    {
        var parentGo = new GameObject("ScaledParent");
        parentGo.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        _spawnedObjects.Add(parentGo);

        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        blockGo.transform.SetParent(parentGo.transform);
        var block = blockGo.AddComponent<BlockAsteroid>();

        // Intentionally corrupt block localScale
        blockGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

        // Detach to null
        block.EndLife(null);

        Assert.AreEqual(Vector3.one, blockGo.transform.localScale, "Detached block must have localScale == Vector3.one");
        Assert.IsNull(blockGo.transform.parent, "Detached block should have no parent");
    }

    [Test]
    public void BlockBase_EndLife_EnforcesVector3OneScale_WhenDetachedToScaledContainer()
    {
        var containerGo = new GameObject("Container");
        containerGo.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _spawnedObjects.Add(containerGo);

        var astGo = new GameObject("Asteroid");
        astGo.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        _spawnedObjects.Add(astGo);

        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        blockGo.transform.SetParent(astGo.transform);
        var block = blockGo.AddComponent<BlockAsteroid>();

        // Detach to container
        block.EndLife(containerGo.transform);

        Assert.AreEqual(Vector3.one, blockGo.transform.localScale, "Block localScale must be Vector3.one even when parented to a scaled container.");
        Assert.AreEqual(containerGo.transform, blockGo.transform.parent);
    }

    [Test]
    public void AsteroidGrid_TryReattachBlock_EnforcesVector3OneScale_WhenAsteroidPunched()
    {
        var astGo = new GameObject("Asteroid");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 1, massmin: 0, massmax: 0);

        // Simulate DOTween punch scale on asteroid (e.g. +8% punch scale)
        astGo.transform.localScale = new Vector3(1.08f, 1.08f, 1.08f);

        var blockGo = new GameObject("LooseBlock");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        blockGo.transform.localScale = Vector3.one;

        bool reattached = grid.TryReattachBlock(blockGo);
        Assert.IsTrue(reattached, "Block should successfully re-attach to the asteroid grid.");

        Assert.AreEqual(Vector3.one, blockGo.transform.localScale, "Reattached block localScale must be strictly Vector3.one even during punch scale.");
    }

    [Test]
    public void AsteroidGrid_DetachBlock_EnforcesVector3OneScale()
    {
        var astGo = new GameObject("Asteroid");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 1, massmin: 0, massmax: 0);

        // Add a block to the grid at (1, 0)
        var blockGo = grid.SpawnBlockAtCoord(new Vector2Int(1, 0), hits: 2);
        Assert.IsNotNull(blockGo);
        Assert.AreEqual(Vector3.one, blockGo.transform.localScale, "Spawned block must start with Vector3.one scale.");

        // Simulate punch scale on asteroid
        astGo.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);

        // Detach the block
        GameObject detachedGo = grid.DetachBlock(new Vector2Int(1, 0));
        Assert.IsNotNull(detachedGo);
        Assert.AreEqual(Vector3.one, detachedGo.transform.localScale, "Detached block localScale must be strictly Vector3.one.");
    }

    [Test]
    public void AsteroidGrid_DetachChildrenOnDestruction_EnforcesVector3OneScaleOnAllDebris()
    {
        var astGo = new GameObject("Asteroid");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 1, massmin: 0, massmax: 0);

        var block1 = grid.SpawnBlockAtCoord(new Vector2Int(1, 0), hits: 2);
        var block2 = grid.SpawnBlockAtCoord(new Vector2Int(-1, 0), hits: 2);
        var block3 = grid.SpawnBlockAtCoord(new Vector2Int(0, 1), hits: 2);

        // Distort asteroid scale
        astGo.transform.localScale = new Vector3(1.15f, 1.15f, 1.15f);

        grid.DetachChildrenOnDestruction();

        Assert.AreEqual(Vector3.one, block1.transform.localScale, "Block 1 must have Vector3.one scale after destruction detachment.");
        Assert.AreEqual(Vector3.one, block2.transform.localScale, "Block 2 must have Vector3.one scale after destruction detachment.");
        Assert.AreEqual(Vector3.one, block3.transform.localScale, "Block 3 must have Vector3.one scale after destruction detachment.");
    }

    [Test]
    public void AsteroidGrid_RepeatedAttachDetachCycles_DoNotAccumulateScaleDistortion()
    {
        var astGo = new GameObject("Asteroid");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 1, massmin: 0, massmax: 0);

        var blockGo = new GameObject("CycleBlock");
        _spawnedObjects.Add(blockGo);
        blockGo.AddComponent<BlockAsteroid>();

        Vector2Int lastAttachedCoord = Vector2Int.zero;
        grid.OnBlockReattached += (go, coord) => lastAttachedCoord = coord;

        for (int i = 0; i < 10; i++)
        {
            // Position near socket (1, 0)
            blockGo.transform.position = astGo.transform.TransformPoint(new Vector3(1f, 0f, 0f));

            // Simulate punch scale variation
            astGo.transform.localScale = (i % 2 == 0) ? new Vector3(1.08f, 1.08f, 1.08f) : new Vector3(0.92f, 0.92f, 0.92f);

            bool attached = grid.TryReattachBlock(blockGo);
            Assert.IsTrue(attached, $"Cycle {i}: block should reattach.");
            Assert.AreEqual(Vector3.one, blockGo.transform.localScale, $"Cycle {i}: attached localScale must be Vector3.one.");

            GameObject detached = grid.DetachBlock(lastAttachedCoord);
            Assert.IsNotNull(detached, $"Cycle {i}: block should detach from {lastAttachedCoord}.");
            Assert.AreEqual(Vector3.one, blockGo.transform.localScale, $"Cycle {i}: detached localScale must be Vector3.one.");
        }
    }

    [Test]
    public void AsteroidGrid_SpawnBlockAtCoord_EnforcesVector3OneScale()
    {
        var astGo = new GameObject("Asteroid");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        astGo.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);

        var coreBlock = grid.SpawnBlockAtCoord(Vector2Int.zero, hits: 3);
        Assert.IsNotNull(coreBlock);
        Assert.AreEqual(Vector3.one, coreBlock.transform.localScale, "Core block must have Vector3.one localScale.");

        var perimeterBlock = grid.SpawnBlockAtCoord(new Vector2Int(1, 0), hits: 2);
        Assert.IsNotNull(perimeterBlock);
        Assert.AreEqual(Vector3.one, perimeterBlock.transform.localScale, "Perimeter block must have Vector3.one localScale.");
    }

    [Test]
    public void AsteroidBase_SpawnCoreBlock_EnforcesVector3OneScale()
    {
        var astGo = new GameObject("AsteroidBase");
        _spawnedObjects.Add(astGo);
        var ast = astGo.AddComponent<AsteroidBase>();
        astGo.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

        var core = ast.SpawnCoreBlock(hits: 5, Vector3.zero);
        Assert.IsNotNull(core);
        Assert.AreEqual(Vector3.one, core.transform.localScale, "Spawned core must have Vector3.one localScale.");
    }
}

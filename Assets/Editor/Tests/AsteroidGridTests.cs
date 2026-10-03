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
        if (asteroidGrid != null && (bool)asteroidGrid)
        {
            asteroidGrid.Clear();
        }

        if (asteroidObj != null && (bool)asteroidObj)
        {
            Object.DestroyImmediate(asteroidObj);
        }

        if (dummyBlockPrefab != null && (bool)dummyBlockPrefab)
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
        asteroidGrid.check_for_unconnected();

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
    public void AsteroidGrid_BlockDeath_EventDriven_AutomaticallyDetachesOrphanBlocks()
    {
        // Core at (0, 0), bridge at (1, 0), outer at (2, 0)
        GameObject bridgeBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 1);
        GameObject outerBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(2, 0), 1);

        block0 bridgeB0 = bridgeBlock.GetComponent<block0>();
        block0 outerB0 = outerBlock.GetComponent<block0>();

        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(1, 0)));
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)));

        bool detachedEventFired = false;
        GameObject detachedObj = null;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            detachedEventFired = true;
            detachedObj = b;
        };

        // Deal lethal hit to bridgeBlock via block_receive_hit. DO NOT call check_for_unconnected manually!
        bridgeB0.block_receive_hit(null, null, 10);

        // Event-driven check should automatically detach the orphaned outerBlock
        Assert.IsTrue(detachedEventFired, "OnBlockDetached event should fire automatically upon bridge destruction");
        Assert.AreEqual(outerBlock, detachedObj, "Detached object should be the outer block");
        Assert.AreEqual(0, asteroidGrid.ActiveBlockCount, "Active block count should be 0");
        Assert.IsFalse(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)), "Outer block coordinate should no longer be registered in grid");
        Assert.AreNotEqual(asteroidObj.transform, outerBlock.transform.parent, "Outer block should be deparented from asteroid");
        Assert.IsTrue(outerB0._detached, "Outer block should be marked as detached");

        if (outerBlock != null)
        {
            Object.DestroyImmediate(outerBlock);
        }
    }

    [Test]
    public void AsteroidGrid_BlockDeath_ProjectileHit_TransfersImpactImpulseAndDetachesOrphans()
    {
        // Core at (0,0), Bridge at (1,0), Outer at (2,0)
        GameObject bridgeBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 1);
        GameObject outerBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(2, 0), 2);

        block0 bridgeB0 = bridgeBlock.GetComponent<block0>();
        block0 outerB0 = outerBlock.GetComponent<block0>();

        // Create a bullet1 flying rightward (+X)
        GameObject bulletObj = new GameObject("TestBullet");
        bulletObj.transform.position = bridgeBlock.transform.position - new Vector3(1f, 0f, 0f);
        bulletObj.transform.up = Vector3.right;
        Rigidbody bulletRb = bulletObj.AddComponent<Rigidbody>();
        bulletRb.linearVelocity = new Vector3(20f, 0f, 0f);
        bullet1 b1 = bulletObj.AddComponent<bullet1>();
        b1.Damage = 5;

        bool detachedFired = false;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            detachedFired = true;
        };

        // Bullet delivers lethal blow to bridgeBlock
        bridgeB0.block_receive_hit(bulletObj.transform, b1);

        // Verification:
        Assert.IsTrue(detachedFired, "Outer block should be detached upon projectile killing bridge");
        Assert.AreEqual(0, asteroidGrid.ActiveBlockCount);
        Assert.IsFalse(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)));
        Assert.IsTrue(outerB0._detached);

        Rigidbody outerRb = outerBlock.GetComponent<Rigidbody>();
        Assert.IsNotNull(outerRb);
        Assert.Greater(outerRb.linearVelocity.x, 0f, "Impact velocity should have positive X direction from bullet flight vector");

        Object.DestroyImmediate(bulletObj);
        if (outerBlock != null) Object.DestroyImmediate(outerBlock);
    }

    [Test]
    public void AsteroidGrid_BlockDeath_LeafBlockDestroyed_KeepsConnectedBlocksAttached()
    {
        // Core at (0, 0), bridge at (1, 0), outer leaf at (2, 0)
        GameObject bridgeBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 2);
        GameObject outerBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(2, 0), 1);

        block0 bridgeB0 = bridgeBlock.GetComponent<block0>();
        block0 outerB0 = outerBlock.GetComponent<block0>();

        bool detachedEventFired = false;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            detachedEventFired = true;
        };

        // Destroy the outer leaf block only
        outerB0.block_receive_hit(null, null, 10);

        // No orphan blocks should be detached because bridgeBlock is still connected to Core (0,0)
        Assert.IsFalse(detachedEventFired, "No blocks should be detached when destroying a leaf block");
        Assert.AreEqual(1, asteroidGrid.ActiveBlockCount, "Bridge block should remain active");
        Assert.IsTrue(asteroidGrid.HasBlockAt(new Vector2Int(1, 0)), "Bridge block should still be in grid");
        Assert.AreEqual(asteroidObj.transform, bridgeBlock.transform.parent, "Bridge block should stay parented to asteroid");
        Assert.IsFalse(bridgeB0._detached, "Bridge block must not be marked detached");
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

    [Test]
    public void AsteroidGrid_CoreBlock_IsSpawnedAtOriginWithCoreTag()
    {
        asteroidGrid.generate_asteroid(5, 4, 8, 0, 1, 1);

        Assert.IsNotNull(asteroidGrid.coreBlock);
        Assert.IsTrue(asteroidGrid.coreBlock.isCore);
        Assert.AreEqual("core", asteroidGrid.coreBlock.gameObject.tag);
        Assert.AreEqual(5, asteroidGrid.coreBlock._hits);
        Assert.IsTrue(asteroidGrid.HasBlockAt(Vector2Int.zero));
    }

    [Test]
    public void AsteroidGrid_MultipleBlocks_ReserveDistinctSlots()
    {
        asteroidGrid.generate_asteroid(3, 0, 0, 0, 1, 1); // Generates only core at (0,0)

        GameObject blockA = new GameObject("BlockA");
        GameObject blockB = new GameObject("BlockB");

        Vector2Int desiredSlot = new Vector2Int(1, 0);

        // Block A finds and reserves desiredSlot
        Vector2Int slotA = asteroidGrid.FindBestAttachmentSlot(desiredSlot, blockA);
        Assert.AreEqual(desiredSlot, slotA);
        bool reservedA = asteroidGrid.TryReserveSlot(slotA, blockA);
        Assert.IsTrue(reservedA);

        // Block B requesting same desiredSlot should be redirected to a different distinct slot
        Vector2Int slotB = asteroidGrid.FindBestAttachmentSlot(desiredSlot, blockB);
        Assert.AreNotEqual(slotA, slotB, "Block B must receive a different slot than Block A");
        Assert.IsTrue(asteroidGrid.IsSlotAvailable(slotB, blockB));

        bool reservedB = asteroidGrid.TryReserveSlot(slotB, blockB);
        Assert.IsTrue(reservedB);

        Object.DestroyImmediate(blockA);
        Object.DestroyImmediate(blockB);
    }

    [Test]
    public void AsteroidGrid_TryReattachBlock_NeverOverwritesExistingGridBlock()
    {
        asteroidGrid.generate_asteroid(3, 0, 0, 0, 1, 1); // Generates only core at (0,0)

        GameObject block1 = new GameObject("Block1");
        block1.transform.position = asteroidObj.transform.position + new Vector3(1f, 0f, 0f);
        block0 b1 = block1.AddComponent<block0>();
        b1._detached = true;

        GameObject block2 = new GameObject("Block2");
        block2.transform.position = asteroidObj.transform.position + new Vector3(1f, 0f, 0f);
        block0 b2 = block2.AddComponent<block0>();
        b2._detached = true;

        // Reattach both
        bool reattach1 = asteroidGrid.TryReattachBlock(block1);
        bool reattach2 = asteroidGrid.TryReattachBlock(block2);

        Assert.IsTrue(reattach1);
        Assert.IsTrue(reattach2);

        // Both blocks must exist at different local coordinates
        Assert.AreNotEqual(block1.transform.localPosition, block2.transform.localPosition, "Blocks must not overlap at identical local positions");
        Assert.AreEqual(3, asteroidGrid.ActiveBlockCount, "Must have Core + Block1 + Block2");

        Object.DestroyImmediate(block1);
        Object.DestroyImmediate(block2);
    }

    [Test]
    public void AsteroidGrid_FindBestAttachmentSlot_RespectsGridRadius()
    {
        asteroidGrid.gridRadius = 1;
        asteroidGrid.generate_asteroid(3, 1, 1, 0, 1, 1);

        // Slot (5, 5) is far outside gridRadius = 1
        Vector2Int slot = asteroidGrid.FindBestAttachmentSlot(new Vector2Int(5, 5), null);

        Assert.LessOrEqual(Mathf.Abs(slot.x), asteroidGrid.gridRadius);
        Assert.LessOrEqual(Mathf.Abs(slot.y), asteroidGrid.gridRadius);
    }

    [Test]
    public void AsteroidGrid_IsAtCapacity_PreventsAccretionBeyondMaxBlocks()
    {
        asteroidGrid._max_blocks = 2; // Capacity of 2 total blocks (including core)
        asteroidGrid.generate_asteroid(3, 0, 0, 0, 1, 1); // Spawns core at (0,0) -> 1 block

        Assert.IsFalse(asteroidGrid.IsAtCapacity);
        Assert.AreEqual(1, asteroidGrid.ActiveBlockCount);

        GameObject block1 = new GameObject("Block1");
        block1.transform.position = asteroidObj.transform.position + new Vector3(1f, 0f, 0f);
        block0 b1 = block1.AddComponent<block0>();
        b1._detached = true;

        bool reattach1 = asteroidGrid.TryReattachBlock(block1);
        Assert.IsTrue(reattach1, "First block should reattach successfully");
        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);
        Assert.IsTrue(asteroidGrid.IsAtCapacity, "Asteroid should now report IsAtCapacity = true");

        // Attempting to attach another block beyond _max_blocks must fail
        GameObject block2 = new GameObject("Block2");
        block2.transform.position = asteroidObj.transform.position + new Vector3(-1f, 0f, 0f);
        block0 b2 = block2.AddComponent<block0>();
        b2._detached = true;

        bool reattach2 = asteroidGrid.TryReattachBlock(block2);
        Assert.IsFalse(reattach2, "Attachment beyond _max_blocks capacity limit must be rejected");
        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount, "ActiveBlockCount must not exceed _max_blocks");

        Object.DestroyImmediate(block1);
        Object.DestroyImmediate(block2);
    }

    [Test]
    public void AsteroidGrid_AttachedBlocks_DoNotRetainLocalRigidbodies()
    {
        asteroidGrid.generate_asteroid(3, 4, 8, 0, 1, 1);

        // Core and all perimeter blocks attached to AsteroidGrid must NOT have local Rigidbodies
        Rigidbody[] childRbs = asteroidGrid.GetComponentsInChildren<Rigidbody>();

        Assert.AreEqual(1, childRbs.Length, "Only the root AsteroidGrid GameObject should have a Rigidbody");
        Assert.AreEqual(asteroidObj, childRbs[0].gameObject, "Rigidbody must reside on the root asteroid");

        foreach (Transform child in asteroidGrid.transform)
        {
            block0 b = child.GetComponent<block0>();
            if (b != null)
            {
                Assert.IsNull(child.GetComponent<Rigidbody>(), $"Child block {child.name} must not hold an independent Rigidbody");
            }
        }
    }

    [Test]
    public void AsteroidGrid_CheckForUnconnected_WithImpactImpulse_DetachesAndAppliesImpactVelocity()
    {
        // Setup a chain: Core(0,0) -> Bridge(1,0) -> Outer(2,0)
        GameObject bridgeBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 2);
        GameObject outerBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(2, 0), 2);

        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);

        // Bridge block gets destroyed (e.g. from impact damage)
        Object.DestroyImmediate(bridgeBlock);

        // Impact vector from player pushing rightwards
        Vector3 impactImpulse = new Vector3(8f, 0f, 0f);

        // Check for unconnected blocks and impart player impact force
        asteroidGrid.check_for_unconnected(impactImpulse);

        // Outer block (2,0) is detached
        Assert.AreEqual(0, asteroidGrid.ActiveBlockCount);
        Assert.IsFalse(asteroidGrid.HasBlockAt(new Vector2Int(2, 0)));

        block0 outerB0 = outerBlock.GetComponent<block0>();
        Assert.IsNotNull(outerB0);
        Assert.IsTrue(outerB0._detached);

        Rigidbody outerRb = outerBlock.GetComponent<Rigidbody>();
        Assert.IsNotNull(outerRb);
        // Linear velocity should include the rightward impulse contribution
        Assert.Greater(outerRb.linearVelocity.x, 3f, "Outer block velocity must reflect the applied impact impulse");

        Object.DestroyImmediate(outerBlock);
    }

    [Test]
    public void AsteroidGrid_CoreDestruct_DisablesAccretion_AndDetachesChildrenAsReattachableDebris()
    {
        // 1. Setup Asteroid A with Core and perimeter block at (1, 0)
        asteroidGrid.generate_asteroid(3, 0, 0, 0, 1, 1);
        GameObject childBlock = asteroidGrid.SpawnBlockAtCoord(new Vector2Int(1, 0), 2);
        asteroidGrid.magneticAccretion = true;

        Assert.IsTrue(asteroidGrid.magneticAccretion);
        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);

        bool accretionDisabledOnDetach = false;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            if (!asteroidGrid.magneticAccretion)
            {
                accretionDisabledOnDetach = true;
            }
        };

        // 2. Kill / Destroy Asteroid A's core
        asteroidGrid.core_destruct(null);

        // Accretion on killed core must have been disabled immediately when core destructed
        Assert.IsTrue(accretionDisabledOnDetach, "Accretion must be disabled immediately when core is destructed");

        // The child block must NOT be destroyed, must be detached debris
        Assert.IsTrue((bool)childBlock, "Child block must survive core destruction as debris");
        Assert.IsNull(childBlock.transform.parent, "Child block must be deparented from killed asteroid");

        block0 b0 = childBlock.GetComponent<block0>();
        Assert.IsNotNull(b0);
        Assert.IsTrue(b0._detached, "Block must be marked detached");
        Assert.IsFalse(b0._dead, "Block must not be marked dead");
        Assert.AreEqual("block", childBlock.tag, "Block tag must remain 'block'");

        Rigidbody debrisRb = childBlock.GetComponent<Rigidbody>();
        Assert.IsNotNull(debrisRb, "Debris block must have independent Rigidbody");
        Assert.IsFalse(debrisRb.isKinematic);

        // 3. Create a second nearby Asteroid B
        GameObject asteroidBObj = new GameObject("NearbyAsteroidB");
        asteroidBObj.AddComponent<Rigidbody>();
        AsteroidGrid asteroidGridB = asteroidBObj.AddComponent<AsteroidGrid>();
        asteroidGridB._block = dummyBlockPrefab;
        asteroidGridB.generate_asteroid(3, 0, 0, 0, 1, 1); // Only core at (0, 0)

        // 4. Verify the debris block from the killed core can re-attach to the new core
        bool reattached = asteroidGridB.TryReattachBlock(childBlock);
        Assert.IsTrue(reattached, "Debris from killed core must be reattachable to another nearby core");
        Assert.AreEqual(2, asteroidGridB.ActiveBlockCount, "Nearby asteroid must now contain core + accreted debris block");
        Assert.AreEqual(asteroidBObj.transform, childBlock.transform.parent, "Debris block must be parented to new asteroid");
        Assert.IsFalse(b0._detached, "Block should no longer be marked detached after reattaching");

        asteroidGridB.Clear();
        Object.DestroyImmediate(asteroidBObj);
        Object.DestroyImmediate(childBlock);
    }

    [Test]
    public void AsteroidGrid_CoreBlock_TakesLethalDamage_AutomaticallyTriggersCoreDestructAndDetachesChildren()
    {
        // Setup hierarchy manually as in a scene or prefab:
        // asteroidObj -> core_block (with block0)
        //             -> child_block (with block0)
        GameObject coreChild = new GameObject("core_block");
        coreChild.transform.parent = asteroidObj.transform;
        coreChild.transform.localPosition = Vector3.zero;
        block0 coreB0 = coreChild.AddComponent<block0>();
        coreB0.SetHits(2);

        GameObject childObj = new GameObject("b_1_0");
        childObj.transform.parent = asteroidObj.transform;
        childObj.transform.localPosition = new Vector3(1f, 0f, 0f);
        block0 childB0 = childObj.AddComponent<block0>();
        childB0.SetHits(1);

        asteroidGrid.magneticAccretion = true;

        // Force grid registration from existing children (as in Awake/Start)
        System.Reflection.MethodInfo regMethod = typeof(AsteroidGrid).GetMethod("RegisterExistingChildrenIntoGrid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        regMethod.Invoke(asteroidGrid, null);

        Assert.AreEqual(coreB0, asteroidGrid.coreBlock);
        Assert.IsTrue(coreB0.isCore);
        Assert.AreEqual(2, asteroidGrid.ActiveBlockCount);
        Assert.IsTrue(asteroidGrid.magneticAccretion);

        bool accretionDisabledOnDetach = false;
        asteroidGrid.OnBlockDetached += (b, coord) =>
        {
            if (!asteroidGrid.magneticAccretion)
            {
                accretionDisabledOnDetach = true;
            }
        };

        // Core block receives lethal hit
        coreB0.block_receive_hit(null, null, 10);

        // Accretion must have been disabled
        Assert.IsTrue(accretionDisabledOnDetach, "Accretion must be disabled on core death");

        // The asteroid root was destroyed
        Assert.IsFalse((bool)asteroidObj, "Asteroid object must be destroyed on core death");

        // child_block must be detached and survive as debris
        Assert.IsTrue((bool)childObj, "Child block must survive as separate debris");
        Assert.IsNull(childObj.transform.parent, "Child block must be deparented from killed asteroid");
        Assert.IsTrue(childB0._detached, "Child block must be marked detached");
        Assert.IsFalse(childB0._dead, "Child block must not be marked dead");

        Object.DestroyImmediate(childObj);
    }
}

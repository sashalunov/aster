using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AsteroidCoreIntegrationTests
{
    private readonly List<GameObject> cleanupList = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < cleanupList.Count; i++)
        {
            if (cleanupList[i] != null)
            {
                Object.DestroyImmediate(cleanupList[i]);
            }
        }
        cleanupList.Clear();
    }

    [Test]
    public void AsteroidBase_SpawnsBlockAsteroidCore_OnGenerate()
    {
        var go = new GameObject("TestAsteroidBase");
        cleanupList.Add(go);
        var ast = go.AddComponent<AsteroidBase>();

        ast.generate_asteroid(coremass: 4, massmin: 2, massmax: 5);

        Assert.IsNotNull(ast.coreBlock, "AsteroidBase must have an assigned coreBlock.");
        Assert.IsInstanceOf<BlockAsteroidCore>(ast.coreBlock, "coreBlock must be an instance of BlockAsteroidCore.");
        Assert.IsTrue(ast.coreBlock.IsCore, "coreBlock.IsCore must be true.");
        Assert.AreEqual(4, (int)ast.coreBlock._hits, "coreBlock hits must match requested coremass.");
        Assert.AreEqual(4, ast._core_hits, "_core_hits must match coremass.");
        Assert.IsTrue(ast.coreBlock.CompareTag("core"), "coreBlock must have tag 'core'.");
    }

    [Test]
    public void AsteroidGrid_SpawnsBlockAsteroidCore_OnGenerate()
    {
        var go = new GameObject("TestAsteroidGrid");
        cleanupList.Add(go);
        var grid = go.AddComponent<AsteroidGrid>();

        grid.generate_asteroid(coremass: 5, massmin: 3, massmax: 6);

        Assert.IsNotNull(grid.coreBlock, "AsteroidGrid must have an assigned coreBlock.");
        Assert.IsInstanceOf<BlockAsteroidCore>(grid.coreBlock, "coreBlock must be an instance of BlockAsteroidCore.");
        Assert.IsTrue(grid.coreBlock.IsCore, "coreBlock.IsCore must be true.");
        Assert.AreEqual(5, (int)grid.coreBlock._hits, "coreBlock hits must match requested coremass.");
        Assert.AreEqual(5, grid._core_hits, "_core_hits must match coremass.");
        Assert.IsTrue(grid.HasBlockAt(Vector2Int.zero), "Grid must have block at (0, 0).");
        Assert.AreEqual(grid.coreBlock.gameObject, grid.SpawnBlockAtCoord(Vector2Int.zero, 5), "Spawning at (0,0) returns existing core.");
    }

    [Test]
    public void AsteroidBase_CoreReceiveHit_DelegatesToBlockAsteroidCore()
    {
        var go = new GameObject("TestAsteroidBaseHit");
        cleanupList.Add(go);
        var ast = go.AddComponent<AsteroidBase>();
        ast.generate_asteroid(coremass: 3, massmin: 1, massmax: 2);

        // Deal 1 damage via core_receive_hit
        ast.core_receive_hit(null, null);

        Assert.AreEqual(2, ast._core_hits, "AsteroidBase._core_hits should be decremented to 2.");
        Assert.AreEqual(2f, ast.coreBlock._hits, "coreBlock._hits should be decremented to 2.");
    }

    [Test]
    public void AsteroidGrid_CoreReceiveHit_DelegatesToBlockAsteroidCore()
    {
        var go = new GameObject("TestAsteroidGridHit");
        cleanupList.Add(go);
        var grid = go.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 3, massmin: 1, massmax: 2);

        // Deal 1 damage via core_receive_hit
        grid.core_receive_hit(null, null);

        Assert.AreEqual(2, grid._core_hits, "AsteroidGrid._core_hits should be decremented to 2.");
        Assert.AreEqual(2f, grid.coreBlock._hits, "coreBlock._hits should be decremented to 2.");
    }

    [Test]
    public void AsteroidGrid_DirectBlockReceiveHit_SynchronizesHitsAndDestroysOnDepleted()
    {
        var go = new GameObject("TestDirectHitGrid");
        cleanupList.Add(go);
        var grid = go.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 2, massmin: 0, massmax: 0);

        var core = grid.coreBlock;
        Assert.IsNotNull(core);

        // Direct hit on core BlockBase component
        core.block_receive_hit(null, null, 1f);
        Assert.AreEqual(1, grid._core_hits);
        Assert.AreEqual(1f, core._hits);

        // Killing blow on core
        core.block_receive_hit(null, null, 1f);
        Assert.IsTrue(go == null || !go, "Asteroid GameObject should be destroyed upon core destruction.");
    }

    [Test]
    public void AsteroidGrid_Reattach_AcceptsDebrisAndRejectsCore()
    {
        var gridGo = new GameObject("TestGridReattach");
        cleanupList.Add(gridGo);
        var grid = gridGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 3, massmin: 0, massmax: 0);

        var debrisGo = new GameObject("DebrisBlock");
        cleanupList.Add(debrisGo);
        var debris = debrisGo.AddComponent<BlockAsteroid>();
        debris.SetHits(2);
        debris._detached = true;
        debrisGo.transform.position = new Vector3(1f, 0f, 0f);

        var foreignCoreGo = new GameObject("ForeignCore");
        cleanupList.Add(foreignCoreGo);
        var foreignCore = foreignCoreGo.AddComponent<BlockAsteroidCore>();
        foreignCore.SetHits(3);
        foreignCore._detached = true;
        foreignCoreGo.transform.position = new Vector3(0f, 1f, 0f);

        bool attachedDebris = grid.TryReattachBlock(debrisGo);
        bool attachedForeignCore = grid.TryReattachBlock(foreignCoreGo);

        Assert.IsTrue(attachedDebris, "Standard debris should be reattached.");
        Assert.IsFalse(attachedForeignCore, "Foreign core block must not be reattached as a perimeter block.");
        Assert.AreEqual(2, grid.ActiveBlockCount, "Grid should contain 2 blocks (central core + attached debris).");
    }

    [Test]
    public void AsteroidGrid_ConnectivityBFS_DetachesOrphanBlocks()
    {
        var gridGo = new GameObject("TestBFSGrid");
        cleanupList.Add(gridGo);
        var grid = gridGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 3, massmin: 0, massmax: 0);

        // Attach block 1 at (1, 0)
        var b1 = grid.SpawnBlockAtCoord(new Vector2Int(1, 0), 2);
        // Attach block 2 at (2, 0) - only connected through b1
        var b2 = grid.SpawnBlockAtCoord(new Vector2Int(2, 0), 2);
        cleanupList.Add(b2);

        Assert.AreEqual(3, grid.ActiveBlockCount);

        // Destroy b1
        b1.GetComponent<BlockBase>().block_receive_hit(null, null, 10f);

        // b2 should now be detached
        Assert.AreEqual(1, grid.ActiveBlockCount, "Only core should remain in grid.");
        var b2Comp = b2.GetComponent<BlockBase>();
        Assert.IsTrue(b2Comp.IsDetached, "b2 should be marked detached.");
    }

    [Test]
    public void AsteroidBase_MassCalculation_DoesNotDoubleCountCore()
    {
        var go = new GameObject("TestMassAsteroid");
        cleanupList.Add(go);
        var ast = go.AddComponent<AsteroidBase>();
        ast.generate_asteroid(coremass: 5, massmin: 0, massmax: 0, shell: -1);

        // Only the core exists with 5 hits
        int mass = ast.UpdateMass();
        Assert.AreEqual(5, mass, "Mass with only core of 5 hits must be exactly 5.");

        // Add one child block of 3 hits
        var childBlockGo = new GameObject("ChildBlock");
        childBlockGo.transform.SetParent(go.transform);
        var childBlock = childBlockGo.AddComponent<BlockAsteroid>();
        childBlock.SetHits(3);

        int massWithChild = ast.UpdateMass();
        Assert.AreEqual(8, massWithChild, "Mass with core (5) + child (3) must be exactly 8.");
    }

    [Test]
    public void AsteroidBase_Clear_CleansUpCoreAndBlocks()
    {
        var go = new GameObject("TestClearAsteroid");
        cleanupList.Add(go);
        var ast = go.AddComponent<AsteroidBase>();
        ast.generate_asteroid(coremass: 3, massmin: 2, massmax: 4);

        Assert.IsNotNull(ast.coreBlock);
        Assert.Greater(ast.CurrentBlockCount, 0);

        ast.Clear();

        Assert.IsNull(ast.coreBlock, "coreBlock should be null after Clear().");
        Assert.AreEqual(0, ast.CurrentBlockCount, "CurrentBlockCount should be 0 after Clear().");
    }

    [Test]
    public void Player_TakeDamage_ShieldAbsorbsDamageFirst()
    {
        var playerObj = new GameObject("TestPlayer");
        cleanupList.Add(playerObj);
        var p = playerObj.AddComponent<player>();
        p.shield_value = 10f;
        p.health_value = 10f;

        p.TakeDamage(4f);

        Assert.AreEqual(6f, p.shield_value, 0.001f, "Shield should absorb the initial damage.");
        Assert.AreEqual(10f, p.health_value, 0.001f, "Health should remain intact when shield absorbs all damage.");
    }

    [Test]
    public void Player_TakeDamage_ShieldBreakOverflowsToHealth()
    {
        var playerObj = new GameObject("TestPlayer");
        cleanupList.Add(playerObj);
        var p = playerObj.AddComponent<player>();
        p.shield_value = 5f;
        p.health_value = 10f;

        p.TakeDamage(8f);

        Assert.AreEqual(0f, p.shield_value, 0.001f, "Shield should be depleted to 0.");
        Assert.AreEqual(7f, p.health_value, 0.001f, "Remaining damage should reduce health.");
    }

    [Test]
    public void Player_TakeDamage_ZeroBounceImpulse_PreservesLinearVelocity()
    {
        var playerObj = new GameObject("TestPlayer");
        cleanupList.Add(playerObj);
        var rb = playerObj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        var p = playerObj.AddComponent<player>();

        Vector3 initialVelocity = new Vector3(5f, 0f, 0f);
        rb.linearVelocity = initialVelocity;

        p.TakeDamage(2f, direction: Vector3.right, contactPoint: default, bounceImpulse: 0f);

        Assert.AreEqual(initialVelocity.x, rb.linearVelocity.x, 0.001f, "Velocity should not be cancelled when bounceImpulse is 0.");
        Assert.AreEqual(initialVelocity.y, rb.linearVelocity.y, 0.001f);
        Assert.AreEqual(initialVelocity.z, rb.linearVelocity.z, 0.001f);
    }

    [Test]
    public void Player_TakeDamage_PositiveBounceImpulse_DampsIncomingVelocityAndAppliesForce()
    {
        var playerObj = new GameObject("TestPlayer");
        cleanupList.Add(playerObj);
        var rb = playerObj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 1f;
        var p = playerObj.AddComponent<player>();

        rb.linearVelocity = new Vector3(4f, 0f, 0f);
        p.TakeDamage(1f, direction: Vector3.right, contactPoint: default, bounceImpulse: 2f);

        // Linear velocity cancellation happens immediately
        Assert.AreEqual(0f, rb.linearVelocity.x, 0.001f, "Incoming velocity along push direction is immediately zeroed.");

        // Simulate physics step to integrate buffered AddForce impulse into velocity
        var prevMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            Physics.Simulate(0.02f);
            Assert.AreEqual(2f, rb.linearVelocity.x, 0.001f, "After physics simulation step, impulse gives 2 m/s to 1 kg Rigidbody.");
        }
        finally
        {
            Physics.simulationMode = prevMode;
        }
    }

    [Test]
    public void Player_TakeDamage_FatalDamage_TriggersDie()
    {
        var playerObj = new GameObject("TestPlayer");
        cleanupList.Add(playerObj);
        var p = playerObj.AddComponent<player>();
        p.shield_value = 0f;
        p.health_value = 5f;

        bool deathTriggered = false;
        p.OnDeath += () => deathTriggered = true;

        p.TakeDamage(10f);

        Assert.IsTrue(p.isDead, "Player should be marked dead after lethal damage.");
        Assert.IsTrue(deathTriggered, "OnDeath event should be fired on fatal damage.");
    }
}

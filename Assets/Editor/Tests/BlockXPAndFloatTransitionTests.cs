using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BlockXPAndFloatTransitionTests
{
    private List<GameObject> _spawnedObjects;
    private GameObject _playerGo;
    private player _player;
    private PlayerProgression _progression;

    [SetUp]
    public void SetUp()
    {
        _spawnedObjects = new List<GameObject>();

        _playerGo = new GameObject("TestPlayer");
        _spawnedObjects.Add(_playerGo);
        _playerGo.AddComponent<Rigidbody>();
        _player = _playerGo.AddComponent<player>();
        _progression = _playerGo.AddComponent<PlayerProgression>();
        _progression.ResetProgress();
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
    public void BlockBase_Level_DefaultsToAtLeastOne()
    {
        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();

        Assert.GreaterOrEqual(block.Level, 1, "Block Level must default to at least 1.");
        Assert.GreaterOrEqual(block._level, 1, "Block _level field must default to at least 1.");

        block.Level = 0;
        Assert.AreEqual(1, block.Level, "Setting Level to 0 should clamp to at least 1.");
    }

    [Test]
    public void BlockBase_FractionalDamage_SurvivesWithoutPrematureDestruction()
    {
        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        block.SetHits(1f);

        var bulletGo = new GameObject("BulletKinetic");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 0.5f;
        bullet.Initialize(_playerGo);

        // First shot deals 0.5 damage to 1.0 HP block
        float excess = block.block_receive_hit(bullet.transform, bullet, bullet.Damage);

        Assert.IsFalse(block.IsDead, "Block must not be destroyed by 0.5 damage when it has 1.0 HP.");
        Assert.AreEqual(0.5f, block.Hits, 0.001f, "Remaining hits should be 0.5f.");
        Assert.AreEqual(0f, excess, "Excess damage should be 0.");
        Assert.AreEqual(0ul, _progression.CurrentXP, "No XP should be awarded before block is destroyed.");
    }

    [Test]
    public void BlockBase_KineticKill_AwardsXPBasedOnMassHitsMultipliedByLevel()
    {
        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        block.SetHits(1f);
        block.Level = 1;

        var bulletGo = new GameObject("BulletKinetic");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 0.5f;
        bullet.Initialize(_playerGo);

        // Hit 1: 0.5 damage
        block.block_receive_hit(bullet.transform, bullet, bullet.Damage);
        Assert.AreEqual(0ul, _progression.CurrentXP, "First kinetic hit should not award XP.");

        // Hit 2: 0.5 damage (killing blow)
        block.block_receive_hit(bullet.transform, bullet, bullet.Damage);

        Assert.IsTrue(block == null || block.IsDead, "Block should be dead after second 0.5 damage hit.");
        Assert.AreEqual(1ul, _progression.CurrentXP, "Player should receive 1 XP (1 mass * level 1) upon destruction.");
    }

    [Test]
    public void BlockBase_HighHealthAndLevel_AwardsScaledXPOnKill()
    {
        var blockGo = new GameObject("HeavyBlock");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        block.SetHits(3f);
        block.Level = 2; // Expected XP = 3 * 2 = 6

        var bulletGo = new GameObject("BulletKinetic");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 0.5f;
        bullet.Initialize(_playerGo);

        // Deal 5 hits of 0.5 damage (total 2.5 damage, 0.5 HP remaining)
        for (int i = 0; i < 5; i++)
        {
            block.block_receive_hit(bullet.transform, bullet, bullet.Damage);
        }
        Assert.AreEqual(0ul, _progression.CurrentXP, "Partial hits should not award kill XP.");
        Assert.AreEqual(0.5f, block.Hits, 0.001f);

        // 6th hit deals final 0.5 damage and kills block
        block.block_receive_hit(bullet.transform, bullet, bullet.Damage);

        Assert.AreEqual(6ul, _progression.CurrentXP, "Player should receive 6 XP (3 mass * 2 level) upon destruction.");
    }

    [Test]
    public void Player_ReceivesXPOnlyAfterDestruction_AndZeroOnFractionalHits()
    {
        var blockGo = new GameObject("BlockMultiFractional");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        block.SetHits(2.5f);
        block.Level = 2; // Expected kill XP = Mathf.RoundToInt(2.5 * 2) = 5 XP

        var bulletGo = new GameObject("BulletSmallKinetic");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 0.5f;
        bullet.Initialize(_playerGo);

        // 4 hits of 0.5 damage (total 2.0 damage, 0.5 HP remaining)
        for (int i = 0; i < 4; i++)
        {
            block.block_receive_hit(bullet.transform, bullet, bullet.Damage);
            Assert.AreEqual(0ul, _progression.CurrentXP, $"Hit {i + 1} with fractional damage must not grant XP.");
            Assert.IsFalse(block.IsDead, $"Block must still be alive on hit {i + 1}.");
        }

        // 5th hit (0.5 damage, depleting health to 0) destroys the block
        block.block_receive_hit(bullet.transform, bullet, bullet.Damage);

        Assert.IsTrue(block == null || block.IsDead, "Block must be destroyed on 5th hit.");
        Assert.AreEqual(5ul, _progression.CurrentXP, "Full integer XP (Mathf.RoundToInt(2.5 * 2) = 5) must be granted upon block destruction.");
    }

    [Test]
    public void BlockBase_DestructionWithoutPlayer_DoesNotAwardXP()
    {
        var blockGo = new GameObject("Block");
        _spawnedObjects.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();
        block.SetHits(1f);

        // Hit without any player attribution
        block.block_receive_hit(null, null, 1f);

        Assert.IsTrue(block == null || block.IsDead, "Block should be destroyed.");
        Assert.AreEqual(0ul, _progression.CurrentXP, "No XP should be awarded when no player is involved.");
    }

    [Test]
    public void SingleCoreAsteroid_Destruction_AwardsXPBasedOnCoreHitsAndLevel()
    {
        var astGo = new GameObject("SingleCoreAsteroidTest");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 2, massmin: 0, massmax: 0);

        var core = grid.coreBlock;
        Assert.IsNotNull(core, "Core block must exist.");
        Assert.AreEqual(2f, core.Hits, "Core hits should match 2.");
        Assert.AreEqual(2, grid._core_hits, "_core_hits property should delegate and reflect 2.");

        var bulletGo = new GameObject("BulletTest");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 1.0f;
        bullet.Initialize(_playerGo);

        // Hit 1: 1 damage -> core survives with 1 hit left
        core.block_receive_hit(bullet.transform, bullet, bullet.Damage);
        Assert.AreEqual(0ul, _progression.CurrentXP, "Hit 1 must not grant XP.");
        Assert.IsFalse(core.IsDead, "Core must not be dead after 1 damage.");

        // Hit 2: 1 damage -> core destroyed
        core.block_receive_hit(bullet.transform, bullet, bullet.Damage);
        Assert.AreEqual(2ul, _progression.CurrentXP, "Single-core asteroid with hits=2 must award 2 XP (>1) upon destruction.");
    }

    [Test]
    public void ClusterAsteroid_CoreDestruction_AwardsCoreXPPlusChildReward()
    {
        var astGo = new GameObject("ClusterAsteroidTest");
        _spawnedObjects.Add(astGo);
        var grid = astGo.AddComponent<AsteroidGrid>();
        grid.generate_asteroid(coremass: 3, massmin: 4, massmax: 4, blocklvl: 1);

        var core = grid.coreBlock;
        int childCount = grid.ActiveBlockCount - 1; // excluding core
        Assert.Greater(childCount, 0, "Must have perimeter blocks.");

        var bulletGo = new GameObject("BulletTestHeavy");
        _spawnedObjects.Add(bulletGo);
        var bullet = bulletGo.AddComponent<bulletKinetic>();
        bullet.Damage = 3.0f; // one-shot core
        bullet.Initialize(_playerGo);

        core.block_receive_hit(bullet.transform, bullet, bullet.Damage);

        // Expected XP = 3 (core) + childCount * 1 (detached children reward)
        ulong expectedXp = (ulong)(3 + childCount);
        Assert.AreEqual(expectedXp, _progression.CurrentXP, "Core destruction should award core mass + child blocks reward.");
    }

    [Test]
    public void BlockBase_FloatChecks_OptimizedForThousandsOfBlocks()
    {
        const int BLOCK_COUNT = 50000;
        float[] healthArray = new float[BLOCK_COUNT];
        for (int i = 0; i < BLOCK_COUNT; i++)
        {
            healthArray[i] = (i % 4 == 0) ? 0.00001f : 1.5f;
        }

        int deadCount = 0;
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < BLOCK_COUNT; i++)
        {
            if (healthArray[i] <= BlockBase.HEALTH_EPSILON)
            {
                deadCount++;
            }
        }

        sw.Stop();

        Assert.AreEqual(BLOCK_COUNT / 4, deadCount);
        Assert.Less(sw.ElapsedMilliseconds, 20, $"50,000 float checks must complete in < 20ms (took {sw.Elapsed.TotalMilliseconds:F2}ms).");
    }
}

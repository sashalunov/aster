using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WavePowerupDropTests
{
    private GameObject _playerObj;
    private player _player;
    private PlayerProgression _progression;
    private GameObject _waveManagerObj;
    private WaveManager _waveManager;
    private GameObject _powerupManagerObj;
    private PowerupManager _powerupManager;

    [SetUp]
    public void SetUp()
    {
        // Clean up any existing singletons
        foreach (var pm in Object.FindObjectsByType<PowerupManager>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(pm.gameObject);
        }
        foreach (var wm in Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(wm.gameObject);
        }
        foreach (var p in Object.FindObjectsByType<player>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(p.gameObject);
        }

        // Setup Player
        _playerObj = new GameObject("TestPlayer");
        _player = _playerObj.AddComponent<player>();
        _progression = _playerObj.AddComponent<PlayerProgression>();

        // Setup PowerupManager
        _powerupManagerObj = new GameObject("TestPowerupManager");
        _powerupManager = _powerupManagerObj.AddComponent<PowerupManager>();
        PowerupManager.Instance = _powerupManager;

        // Setup WaveManager
        _waveManagerObj = new GameObject("TestWaveManager");
        _waveManager = _waveManagerObj.AddComponent<WaveManager>();
        _waveManager.authoredWaves = new List<WaveDefinition>();
        WaveManager.Instance = _waveManager;
    }

    [TearDown]
    public void TearDown()
    {
        if (_powerupManager != null)
        {
            _powerupManager.ClearAllActive();
        }

        foreach (var pu in Object.FindObjectsByType<PowerupBase>(FindObjectsSortMode.None))
        {
            if (pu != null && pu.gameObject != null) Object.DestroyImmediate(pu.gameObject);
        }

        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_powerupManagerObj != null) Object.DestroyImmediate(_powerupManagerObj);
        if (_waveManagerObj != null) Object.DestroyImmediate(_waveManagerObj);

        PowerupManager.Instance = null;
        WaveManager.Instance = null;
    }

    [Test]
    public void WaveDefinition_DropConfiguration_ClonesAndResetsCorrectly()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.waveNumber = 1;
        wave.blockDropChance = 0.25f;
        wave.coreDropChance = 0.75f;
        wave.useCoreGuaranteedType = true;
        wave.coreGuaranteedType = StandardPowerup.StandardType.ShieldUp;

        wave.dropTable = new List<WaveDropEntry>
        {
            new WaveDropEntry(StandardPowerup.StandardType.UpgradePoint, 70f),
            new WaveDropEntry(StandardPowerup.StandardType.AmmoRefill, 30f)
        };

        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(50, StandardPowerup.StandardType.GunKinetic, false)
        };

        wave.xpThresholdDrops[0].hasDropped = true;

        WaveDefinition cloned = wave.Clone();

        Assert.AreEqual(0.25f, cloned.blockDropChance, 0.001f);
        Assert.AreEqual(0.75f, cloned.coreDropChance, 0.001f);
        Assert.IsTrue(cloned.useCoreGuaranteedType);
        Assert.AreEqual(StandardPowerup.StandardType.ShieldUp, cloned.coreGuaranteedType);
        Assert.AreEqual(2, cloned.dropTable.Count);
        Assert.AreEqual(StandardPowerup.StandardType.UpgradePoint, cloned.dropTable[0].powerupType);
        Assert.AreEqual(1, cloned.xpThresholdDrops.Count);
        Assert.IsFalse(cloned.xpThresholdDrops[0].hasDropped); // Clone should reset runtime state

        wave.ResetRuntimeDrops();
        Assert.IsFalse(wave.xpThresholdDrops[0].hasDropped);

        Object.DestroyImmediate(wave);
        Object.DestroyImmediate(cloned);
    }

    [Test]
    public void HandleBlockDestructionDrop_RespectsBlockDropChance()
    {
        WaveDefinition waveZero = ScriptableObject.CreateInstance<WaveDefinition>();
        waveZero.blockDropChance = 0f;
        waveZero.coreDropChance = 0f;
        _waveManager.authoredWaves.Add(waveZero);
        _waveManager.TriggerCombatImmediately();

        PowerupBase drop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player, forceDrop: false);
        Assert.IsNull(drop, "Expected no drop when blockDropChance is 0");

        PowerupBase forcedDrop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player, forceDrop: true);
        Assert.IsNotNull(forcedDrop, "Expected drop when forceDrop is true");
        Assert.IsTrue(_powerupManager.IsRegistered(forcedDrop));

        Object.DestroyImmediate(waveZero);
    }

    [Test]
    public void HandleBlockDestructionDrop_UsesWaveWeightedDropTable()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.blockDropChance = 1f;
        wave.dropTable = new List<WaveDropEntry>
        {
            new WaveDropEntry(StandardPowerup.StandardType.AmmoRefill, 100f)
        };
        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        PowerupBase drop = _powerupManager.HandleBlockDestructionDrop(new Vector3(5, 5, 0), isCore: false, _player);

        Assert.IsNotNull(drop);
        StandardPowerup sp = drop as StandardPowerup;
        Assert.IsNotNull(sp);
        Assert.AreEqual(StandardPowerup.StandardType.AmmoRefill, sp.Type);
        Assert.AreEqual(new Vector3(5, 5, 0), drop.transform.position);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void HandleBlockDestructionDrop_GuaranteesCoreSpecificDrop()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 1f;
        wave.useCoreGuaranteedType = true;
        wave.coreGuaranteedType = StandardPowerup.StandardType.ShieldUp;

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        PowerupBase drop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: true, _player);

        Assert.IsNotNull(drop);
        StandardPowerup sp = drop as StandardPowerup;
        Assert.IsNotNull(sp);
        Assert.AreEqual(StandardPowerup.StandardType.ShieldUp, sp.Type);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void HandleBlockDestructionDrop_Guarantees100PercentDropAtPlayerXPMilestone()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.blockDropChance = 0f; // Normal blocks drop 0%
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 20, type: StandardPowerup.StandardType.GunKinetic, waveRelative: false, once: true)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // 1. Player has 10 XP (below threshold of 20)
        _progression.AddXP(10);
        Assert.AreEqual(10ul, _progression.CurrentXP);

        PowerupBase drop1 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(drop1, "Drop should not trigger before reaching XP threshold");

        // 2. Player gains 15 more XP -> total 25 XP (meets threshold of 20)
        _progression.AddXP(15);
        Assert.AreEqual(25ul, _progression.CurrentXP);

        PowerupBase drop2 = _powerupManager.HandleBlockDestructionDrop(new Vector3(2, 0, 0), isCore: false, _player);
        Assert.IsNotNull(drop2, "Expected guaranteed 100% milestone drop when XP threshold is met");

        StandardPowerup sp2 = drop2 as StandardPowerup;
        Assert.IsNotNull(sp2);
        Assert.AreEqual(StandardPowerup.StandardType.GunKinetic, sp2.Type);
        Assert.IsTrue(_waveManager.CurrentWaveConfig.xpThresholdDrops[0].hasDropped);

        // 3. Subsequent block destruction in same wave should not drop GunKinetic again (oncePerWave is true)
        PowerupBase drop3 = _powerupManager.HandleBlockDestructionDrop(new Vector3(4, 0, 0), isCore: false, _player);
        Assert.IsNull(drop3, "Once-per-wave milestone drop should not trigger multiple times");

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void HandleBlockDestructionDrop_EvaluatesWaveRelativeXPMilestones()
    {
        // Player already has 100 XP from previous waves
        _progression.AddXP(100);
        Assert.AreEqual(100ul, _progression.CurrentXP);

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            // Requires 15 XP gained during THIS wave
            new WaveXPDropEntry(xpThreshold: 15, type: StandardPowerup.StandardType.GunPlasma, waveRelative: true, once: true)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // At wave start, WaveEarnedXP is 0
        Assert.AreEqual(0ul, _waveManager.WaveEarnedXP);

        // Add 10 XP in this wave -> WaveEarnedXP = 10 (less than 15)
        _progression.AddXP(10);
        Assert.AreEqual(10ul, _waveManager.WaveEarnedXP);

        PowerupBase drop1 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(drop1, "Wave-relative milestone should not trigger before 15 wave XP");

        // Add 10 more XP -> WaveEarnedXP = 20 (>= 15)
        _progression.AddXP(10);
        Assert.AreEqual(20ul, _waveManager.WaveEarnedXP);

        PowerupBase drop2 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNotNull(drop2, "Wave-relative milestone should trigger once wave XP threshold reached");
        StandardPowerup sp2 = drop2 as StandardPowerup;
        Assert.IsNotNull(sp2);
        Assert.AreEqual(StandardPowerup.StandardType.GunPlasma, sp2.Type);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void Block0_Destruction_TriggersPowerupDropFlow()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.blockDropChance = 1.0f; // Guaranteed drop
        wave.dropTable = new List<WaveDropEntry>
        {
            new WaveDropEntry(StandardPowerup.StandardType.UpgradePoint, 100f)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        int initialActive = _powerupManager.ActiveCount;

        // Spawn a standalone block0
        GameObject blockObj = new GameObject("TestBlock");
        block0 b0 = blockObj.AddComponent<block0>();
        b0.isCore = false;
        b0.SetHits(1);

        // Lethal hit
        b0.block_receive_hit(source: _player.transform, b1: null, customDamage: 1);

        // Verify powerup dropped and registered in PowerupManager
        Assert.AreEqual(initialActive + 1, _powerupManager.ActiveCount);

        Object.DestroyImmediate(wave);
    }
}

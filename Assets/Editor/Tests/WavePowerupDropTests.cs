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
    public void WaveDefinition_CoreAndBlockDropChance_ClonesAndResetsCorrectly()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.waveNumber = 1;
        wave.blockDropChance = 0.25f;
        wave.coreDropChance = 0.85f;

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
        Assert.AreEqual(0.85f, cloned.coreDropChance, 0.001f);
        Assert.AreEqual(2, cloned.dropTable.Count);
        Assert.AreEqual(1, cloned.xpThresholdDrops.Count);
        Assert.IsFalse(cloned.xpThresholdDrops[0].hasDropped, "Clone should reset runtime hasDropped state");

        wave.ResetRuntimeDrops();
        Assert.IsFalse(wave.xpThresholdDrops[0].hasDropped, "ResetRuntimeDrops should reset hasDropped state");

        Object.DestroyImmediate(wave);
        Object.DestroyImmediate(cloned);
    }

    [Test]
    public void HandleBlockDestructionDrop_UsesCoreDropChance_WhenIsCoreTrue()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 1f;  // 100% core drop
        wave.blockDropChance = 0f; // 0% block drop
        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // When isCore is true, should drop because coreDropChance is 1
        PowerupBase coreDrop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: true, _player);
        Assert.IsNotNull(coreDrop, "Core destruction should drop powerup when coreDropChance is 1");

        // When isCore is false, should not drop because blockDropChance is 0
        PowerupBase blockDrop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(blockDrop, "Block destruction should NOT drop powerup when blockDropChance is 0");

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void HandleBlockDestructionDrop_UsesBlockDropChance_WhenIsCoreFalse()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;  // 0% core drop
        wave.blockDropChance = 1f; // 100% block drop
        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // When isCore is false, should drop because blockDropChance is 1
        PowerupBase blockDrop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNotNull(blockDrop, "Block destruction should drop powerup when blockDropChance is 1");

        // When isCore is true, should not drop because coreDropChance is 0
        PowerupBase coreDrop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: true, _player);
        Assert.IsNull(coreDrop, "Core destruction should NOT drop powerup when coreDropChance is 0");

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void HandleBlockDestructionDrop_FallsBackToDefaultCoreDrop_ForCore()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 1f;
        wave.dropTable = new List<WaveDropEntry>(); // Empty wave table
        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        PowerupBase drop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: true, _player);
        Assert.IsNotNull(drop, "Core drop should not be null");

        StandardPowerup sp = drop.GetComponentInChildren<StandardPowerup>();
        Assert.IsNotNull(sp, "Core drop should contain StandardPowerup");
        Assert.AreEqual(StandardPowerup.StandardType.ShieldUp, sp.Type, "Default core drop should be ShieldUp");

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void XPThresholdDrops_TriggersGuaranteedDrop_WhenTotalXPThresholdReached()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 20, type: StandardPowerup.StandardType.GunKinetic, waveRelative: false, once: true)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // 1. Below threshold: 10 XP < 20 XP
        _progression.AddXP(10);
        PowerupBase drop1 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(drop1, "Drop should not trigger below XP threshold");

        // 2. Reached threshold: 10 + 15 = 25 XP >= 20 XP
        _progression.AddXP(15);
        PowerupBase drop2 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNotNull(drop2, "Expected guaranteed milestone drop when XP threshold reached");

        StandardPowerup sp = drop2.GetComponentInChildren<StandardPowerup>();
        Assert.IsNotNull(sp);
        Assert.AreEqual(StandardPowerup.StandardType.GunKinetic, sp.Type);
        Assert.IsTrue(_waveManager.CurrentWaveConfig.xpThresholdDrops[0].hasDropped);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void XPThresholdDrops_RespectsOncePerWave()
    {
        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 10, type: StandardPowerup.StandardType.ShieldUp, waveRelative: false, once: true)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        _progression.AddXP(15);

        // First destruction triggers the milestone
        PowerupBase drop1 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNotNull(drop1, "First destruction should trigger milestone");

        // Second destruction in same wave should not trigger again
        PowerupBase drop2 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(drop2, "Once-per-wave milestone drop should not trigger a second time");

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void XPThresholdDrops_EvaluatesWaveRelativeXPMilestones()
    {
        // Player already has 100 XP from previous waves
        _progression.AddXP(100);

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 20, type: StandardPowerup.StandardType.GunPlasma, waveRelative: true, once: true)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        // In this wave, only 10 XP gained (10 < 20)
        _progression.AddXP(10);
        PowerupBase drop1 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNull(drop1, "Wave-relative milestone should not trigger before 20 wave XP");

        // Add 15 more XP in this wave -> 25 wave XP (>= 20)
        _progression.AddXP(15);
        PowerupBase drop2 = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);
        Assert.IsNotNull(drop2, "Wave-relative milestone should trigger once wave XP reached");

        StandardPowerup sp = drop2.GetComponentInChildren<StandardPowerup>();
        Assert.IsNotNull(sp);
        Assert.AreEqual(StandardPowerup.StandardType.GunPlasma, sp.Type);

        Object.DestroyImmediate(wave);
    }

    [Test]
    public void XPThresholdDrops_SpawnsCustomPrefab_WhenProvided()
    {
        // Create a custom powerup prefab object
        GameObject customPrefab = new GameObject("CustomPowerupPrefab");
        StandardPowerup spCustom = customPrefab.AddComponent<StandardPowerup>();
        spCustom.Type = StandardPowerup.StandardType.SpeedUp;

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 5, type: StandardPowerup.StandardType.SpeedUp, customPrefab: customPrefab)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        _progression.AddXP(10);
        PowerupBase drop = _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);

        Assert.IsNotNull(drop, "Custom prefab should be instantiated");
        Assert.IsTrue(drop.name.StartsWith("CustomPowerupPrefab"), "Spawned instance should match custom prefab name");
        Assert.IsTrue(_powerupManager.IsRegistered(drop), "Custom powerup should be registered in PowerupManager");

        Object.DestroyImmediate(customPrefab);
        Object.DestroyImmediate(wave);
    }

    [Test]
    public void XPThresholdDrops_SpawnsNonPowerupPrefab_WhenProvided()
    {
        // Create an arbitrary non-powerup prefab
        GameObject dummyPrefab = new GameObject("DummyPropPrefab");

        WaveDefinition wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.coreDropChance = 0f;
        wave.blockDropChance = 0f;
        wave.xpThresholdDrops = new List<WaveXPDropEntry>
        {
            new WaveXPDropEntry(xpThreshold: 5, type: StandardPowerup.StandardType.UpgradePoint, customPrefab: dummyPrefab)
        };

        _waveManager.authoredWaves.Add(wave);
        _waveManager.TriggerCombatImmediately();

        _progression.AddXP(10);
        _powerupManager.HandleBlockDestructionDrop(Vector3.zero, isCore: false, _player);

        // Find instantiated dummy prop in scene
        GameObject spawnedProp = GameObject.Find("DummyPropPrefab(Clone)");
        Assert.IsNotNull(spawnedProp, "Non-powerup prefab should be instantiated in scene");
        Assert.IsTrue(_waveManager.CurrentWaveConfig.xpThresholdDrops[0].hasDropped, "Threshold should be flagged as dropped");

        Object.DestroyImmediate(dummyPrefab);
        Object.DestroyImmediate(spawnedProp);
        Object.DestroyImmediate(wave);
    }
}

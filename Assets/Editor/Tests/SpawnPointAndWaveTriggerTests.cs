using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SpawnPointAndWaveTriggerTests
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
    public void SpawnPoint_RespawnPlayer_PositionsAndRestoresPlayerShip()
    {
        // 1. Create SpawnPoint
        var spGo = new GameObject("SpawnPoint");
        _spawnedObjects.Add(spGo);
        spGo.transform.position = new Vector3(15f, 25f, 0f);
        spGo.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        var spawnPoint = spGo.AddComponent<SpawnPoint>();

        // 2. Create Player
        var playerGo = new GameObject("PlayerShip");
        _spawnedObjects.Add(playerGo);
        playerGo.transform.position = Vector3.zero;
        playerGo.tag = "Player";

        var rb = playerGo.AddComponent<Rigidbody>();
        rb.useGravity = false;
        var col = playerGo.AddComponent<SphereCollider>();

        var hullGo = new GameObject("Hull");
        hullGo.transform.SetParent(playerGo.transform);
        _spawnedObjects.Add(hullGo);

        var playerComp = playerGo.AddComponent<player>();
        playerComp._ship_hull = hullGo.transform;

        // Damage and kill player
        playerComp.health_value = 0f;
        playerComp.shield_value = 0f;
        playerComp.isDead = true;
        playerComp._can_play = false;
        col.enabled = false;
        hullGo.SetActive(false);
        rb.linearVelocity = new Vector3(5f, 10f, 0f);

        // Respawn via SpawnPoint
        spawnPoint.RespawnPlayer(playerComp);

        // Validate state
        Assert.AreEqual(spGo.transform.position, playerGo.transform.position);
        Assert.AreEqual(spGo.transform.rotation.eulerAngles, playerGo.transform.rotation.eulerAngles);
        Assert.IsFalse(playerComp.isDead);
        Assert.IsTrue(playerComp._can_play);
        Assert.AreEqual(playerComp.health_max_value, playerComp.health_value);
        Assert.AreEqual(playerComp.shield_max_value, playerComp.shield_value);
        Assert.IsTrue(col.enabled);
        Assert.IsTrue(hullGo.activeSelf);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, rb.angularVelocity);
    }

    [Test]
    public void MainMenu_StartGame_RespawnsPlayer_AndDoesNotStartWaves()
    {
        // 1. Create SpawnPoint
        var spGo = new GameObject("SpawnPoint");
        _spawnedObjects.Add(spGo);
        spGo.transform.position = new Vector3(7f, -3f, 0f);
        var spawnPoint = spGo.AddComponent<SpawnPoint>();

        // 2. Create Player
        var playerGo = new GameObject("PlayerShip");
        _spawnedObjects.Add(playerGo);
        playerGo.tag = "Player";
        var rb = playerGo.AddComponent<Rigidbody>();
        var col = playerGo.AddComponent<SphereCollider>();
        var hullGo = new GameObject("Hull");
        hullGo.transform.SetParent(playerGo.transform);
        _spawnedObjects.Add(hullGo);
        var playerComp = playerGo.AddComponent<player>();
        playerComp._ship_hull = hullGo.transform;

        // 3. Create WaveManager
        var wmGo = new GameObject("WaveManager");
        _spawnedObjects.Add(wmGo);
        var waveManager = wmGo.AddComponent<WaveManager>();
        waveManager.SetState(WaveManager.WaveState.Idle);

        // 4. Create MainMenu
        var menuGo = new GameObject("MainMenu");
        _spawnedObjects.Add(menuGo);
        var mainMenu = menuGo.AddComponent<MainMenu>();
        mainMenu._player = playerComp;
        mainMenu.spawnPoint = spawnPoint;
        mainMenu.showOnStart = false;

        // Start game from MainMenu
        mainMenu.StartGame();

        // Player must be at spawn point and playable
        Assert.AreEqual(spGo.transform.position, playerGo.transform.position);
        Assert.IsTrue(playerComp._can_play);
        Assert.IsFalse(playerComp.isDead);

        // Waves must NOT have started (WaveState remains Idle, index remains 0)
        Assert.AreEqual(WaveManager.WaveState.Idle, waveManager.State);
        Assert.AreEqual(0, waveManager.CurrentWaveIndex);
    }

    [Test]
    public void TriggerZone_OnTriggerEnter_StartsWaves_WhenPlayerEnters()
    {
        // 1. Create WaveManager
        var wmGo = new GameObject("WaveManager");
        _spawnedObjects.Add(wmGo);
        var waveManager = wmGo.AddComponent<WaveManager>();
        waveManager.SetState(WaveManager.WaveState.Idle);

        // 2. Create TriggerZone
        var tzGo = new GameObject("TriggerZone");
        _spawnedObjects.Add(tzGo);
        var boxCol = tzGo.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        var triggerZone = tzGo.AddComponent<TriggerZone>();

        // 3. Create Player Collider
        var playerGo = new GameObject("PlayerShip");
        _spawnedObjects.Add(playerGo);
        playerGo.tag = "Player";
        var playerCol = playerGo.AddComponent<SphereCollider>();
        playerCol.isTrigger = false;
        var playerComp = playerGo.AddComponent<player>();

        Assert.IsFalse(triggerZone.HasTriggered);
        Assert.AreEqual(WaveManager.WaveState.Idle, waveManager.State);

        // Simulate trigger entry
        triggerZone.HandleTriggerEnter(playerCol);

        // WaveManager should now be running (Countdown or Combat phase)
        Assert.IsTrue(triggerZone.HasTriggered);
        Assert.AreNotEqual(WaveManager.WaveState.Idle, waveManager.State);
        Assert.GreaterOrEqual(waveManager.CurrentWaveIndex, 1);
    }

    [Test]
    public void TriggerZone_IgnoresTriggersAndNonPlayers()
    {
        // 1. Create WaveManager
        var wmGo = new GameObject("WaveManager");
        _spawnedObjects.Add(wmGo);
        var waveManager = wmGo.AddComponent<WaveManager>();
        waveManager.SetState(WaveManager.WaveState.Idle);

        // 2. Create TriggerZone
        var tzGo = new GameObject("TriggerZone");
        _spawnedObjects.Add(tzGo);
        tzGo.AddComponent<BoxCollider>();
        var triggerZone = tzGo.AddComponent<TriggerZone>();

        // 3. Sensor trigger (e.g., max_view_radius isTrigger = true)
        var sensorGo = new GameObject("SensorSphere");
        _spawnedObjects.Add(sensorGo);
        var sensorCol = sensorGo.AddComponent<SphereCollider>();
        sensorCol.isTrigger = true;

        triggerZone.HandleTriggerEnter(sensorCol);
        Assert.IsFalse(triggerZone.HasTriggered);
        Assert.AreEqual(WaveManager.WaveState.Idle, waveManager.State);

        // 4. Random untagged object (non-player)
        var envGo = new GameObject("EnvironmentProp");
        _spawnedObjects.Add(envGo);
        var envCol = envGo.AddComponent<BoxCollider>();
        envCol.isTrigger = false;

        triggerZone.HandleTriggerEnter(envCol);
        Assert.IsFalse(triggerZone.HasTriggered);
        Assert.AreEqual(WaveManager.WaveState.Idle, waveManager.State);
    }

    [Test]
    public void SpawnPoint_ResetsTriggerZone_OnRespawn()
    {
        // 1. Create TriggerZone
        var tzGo = new GameObject("TriggerZone");
        _spawnedObjects.Add(tzGo);
        tzGo.AddComponent<BoxCollider>();
        var triggerZone = tzGo.AddComponent<TriggerZone>();
        triggerZone.TriggerWaveStart(); // force triggered
        Assert.IsTrue(triggerZone.HasTriggered);

        // 2. Create SpawnPoint linking to TriggerZone
        var spGo = new GameObject("SpawnPoint");
        _spawnedObjects.Add(spGo);
        var spawnPoint = spGo.AddComponent<SpawnPoint>();
        spawnPoint.LinkedTriggerZone = triggerZone;

        // 3. Respawn player
        spawnPoint.RespawnPlayer(null);

        // TriggerZone should be reset
        Assert.IsFalse(triggerZone.HasTriggered);
    }
}

using NUnit.Framework;
using UnityEngine;

public class PowerupSystemTests
{
    private GameObject _managerObj;
    private PowerupManager _manager;
    private GameObject _playerObj;
    private player _player;

    [SetUp]
    public void SetUp()
    {
        _managerObj = new GameObject("TestPowerupManager");
        _manager = _managerObj.AddComponent<PowerupManager>();

        _playerObj = new GameObject("TestPlayerShip");
        _player = _playerObj.AddComponent<player>();
        _player.health_value = 10f;
        _player.health_max_value = 10f;
        _player.shield_value = 10f;
        _player.shield_max_value = 10f;
        _player._fire_hz = 1f;
        _player._fire_rate = 1f;
        _player._bullet_force = 1f;
        _player._bullet_dmg = 1f;
        _player._can_play = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (_manager != null)
        {
            _manager.ClearAllActive();
        }
        if (_managerObj != null) Object.DestroyImmediate(_managerObj);
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
    }

    [Test]
    public void PowerupManager_Instance_ReturnsValidSingleton()
    {
        Assert.IsNotNull(PowerupManager.Instance);
        Assert.AreEqual(_manager, PowerupManager.Instance);
    }

    [Test]
    public void PowerupManager_RegisterAndUnregister_TracksActiveCount()
    {
        GameObject puObj1 = new GameObject("PU1");
        StandardPowerup pu1 = puObj1.AddComponent<StandardPowerup>();

        GameObject puObj2 = new GameObject("PU2");
        StandardPowerup pu2 = puObj2.AddComponent<StandardPowerup>();

        _manager.Register(pu1);
        _manager.Register(pu2);

        Assert.AreEqual(2, _manager.ActiveCount);
        Assert.IsTrue(_manager.IsRegistered(pu1));
        Assert.IsTrue(_manager.IsRegistered(pu2));

        _manager.Unregister(pu1);
        Assert.AreEqual(1, _manager.ActiveCount);
        Assert.IsFalse(_manager.IsRegistered(pu1));

        Object.DestroyImmediate(puObj1);
        Object.DestroyImmediate(puObj2);
    }

    [Test]
    public void PowerupManager_ApplySpeedUp_EnforcesMaxFireHzCap()
    {
        _player._fire_hz = 1f;

        // Apply multiple speed buffs
        for (int i = 0; i < 20; i++)
        {
            _manager.ApplySpeedUp(_player, 1f);
        }

        Assert.AreEqual(PowerupManager.MAX_FIRE_HZ, _player._fire_hz, 0.001f);
        Assert.AreEqual(1f / PowerupManager.MAX_FIRE_HZ, _player._fire_rate, 0.001f);
    }

    [Test]
    public void PowerupManager_ApplyPowerAndDamageUp_EnforcesLimits()
    {
        _player._bullet_force = 1f;
        _player._bullet_dmg = 1f;

        for (int i = 0; i < 50; i++)
        {
            _manager.ApplyPowerUp(_player, 2f);
            _manager.ApplyDamageUp(_player, 2f);
        }

        Assert.AreEqual(PowerupManager.MAX_BULLET_FORCE, _player._bullet_force, 0.001f);
        Assert.AreEqual(PowerupManager.MAX_BULLET_DMG, _player._bullet_dmg, 0.001f);
    }

    [Test]
    public void PowerupManager_ApplyShield_EnforcesOverchargeCeiling()
    {
        _player.shield_max_value = 10f;
        _player.shield_value = 10f;

        // Apply large shield boosts
        for (int i = 0; i < 20; i++)
        {
            _manager.ApplyShield(_player, 5f);
        }

        float maxAllowed = _player.shield_max_value * PowerupManager.MAX_SHIELD_OVERCHARGE_RATIO;
        Assert.AreEqual(maxAllowed, _player.shield_value, 0.001f);
    }

    [Test]
    public void StandardPowerup_TryCollect_AppliesSpeedBuffAndMarksCollected()
    {
        GameObject puObj = new GameObject("SpeedPowerup");
        StandardPowerup pu = puObj.AddComponent<StandardPowerup>();
        pu.Type = StandardPowerup.StandardType.SpeedUp;

        bool eventFired = false;
        PowerupManager.OnPowerupCollected += (p, pl) => eventFired = true;

        float startingHz = _player._fire_hz;
        bool collected = pu.TryCollect(_player);

        Assert.IsTrue(collected, "TryCollect should return true for active player");
        Assert.IsTrue(pu.IsCollected, "Powerup should be marked collected");
        Assert.Greater(_player._fire_hz, startingHz);
        Assert.IsTrue(eventFired, "OnPowerupCollected should fire");
    }

    [Test]
    public void StandardPowerup_TryCollect_IgnoredWhenDead()
    {
        _player.isDead = true;

        GameObject puObj = new GameObject("DamagePowerup");
        StandardPowerup pu = puObj.AddComponent<StandardPowerup>();
        pu.Type = StandardPowerup.StandardType.DamageUp;

        float startingDmg = _player._bullet_dmg;
        bool collected = pu.TryCollect(_player);

        Assert.IsFalse(collected, "Dead player should not be able to collect powerups");
        Assert.IsFalse(pu.IsCollected);
        Assert.AreEqual(startingDmg, _player._bullet_dmg);

        Object.DestroyImmediate(puObj);
    }

    [Test]
    public void PowerupManager_CollectAllRemaining_CollectsAllActivePowerups()
    {
        GameObject puObj1 = new GameObject("PU1");
        StandardPowerup pu1 = puObj1.AddComponent<StandardPowerup>();
        pu1.Type = StandardPowerup.StandardType.DamageUp;

        GameObject puObj2 = new GameObject("PU2");
        StandardPowerup pu2 = puObj2.AddComponent<StandardPowerup>();
        pu2.Type = StandardPowerup.StandardType.DamageUp;

        _manager.Register(pu1);
        _manager.Register(pu2);
        Assert.AreEqual(2, _manager.ActiveCount);

        int count = _manager.CollectAllRemaining(_player);

        Assert.AreEqual(2, count);
        Assert.AreEqual(0, _manager.ActiveCount);
        Assert.AreEqual(3f, _player._bullet_dmg, 0.001f); // 1 base + 1 + 1
    }
}

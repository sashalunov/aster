using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GunPowerupAndUpgradeTests
{
    private GameObject _playerObj;
    private player _player;
    private GameObject _pmObj;
    private PowerupManager _powerupManager;

    [SetUp]
    public void SetUp()
    {
        _playerObj = new GameObject("TestPlayer");
        _playerObj.AddComponent<Rigidbody>();
        _playerObj.AddComponent<SphereCollider>();
        _player = _playerObj.AddComponent<player>();

        _pmObj = new GameObject("PowerupManager");
        _powerupManager = _pmObj.AddComponent<PowerupManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_pmObj != null) Object.DestroyImmediate(_pmObj);

        foreach (var p in Object.FindObjectsByType<PowerupBase>(FindObjectsSortMode.None))
        {
            if (p != null) Object.DestroyImmediate(p.gameObject);
        }

        foreach (var g in Object.FindObjectsByType<Gun>(FindObjectsSortMode.None))
        {
            if (g != null) Object.DestroyImmediate(g.gameObject);
        }
    }

    [Test]
    public void StandardPowerup_GunKinetic_AttachesKineticGunToPlayer()
    {
        GameObject pwpObj = new GameObject("PwpKinetic");
        StandardPowerup sp = pwpObj.AddComponent<StandardPowerup>();
        sp.Type = StandardPowerup.StandardType.GunKinetic;

        Assert.AreEqual(0, _player.GunCount);

        bool collected = sp.TryCollect(_player);
        Assert.IsTrue(collected);
        Assert.AreEqual(1, _player.GunCount);

        List<Gun> guns = _player.GetEquippedGuns();
        Assert.AreEqual(1, guns.Count);
        Assert.AreEqual(GunKinetic.DEFAULT_GUN_ID, guns[0].Data.gunId);
    }

    [Test]
    public void StandardPowerup_GunPlasma_AttachesPlasmaGunToPlayer()
    {
        GameObject pwpObj = new GameObject("PwpPlasma");
        StandardPowerup sp = pwpObj.AddComponent<StandardPowerup>();
        sp.Type = StandardPowerup.StandardType.GunPlasma;

        Assert.AreEqual(0, _player.GunCount);

        bool collected = sp.TryCollect(_player);
        Assert.IsTrue(collected);
        Assert.AreEqual(1, _player.GunCount);

        List<Gun> guns = _player.GetEquippedGuns();
        Assert.AreEqual(1, guns.Count);
        Assert.AreEqual(GunPlasma.DEFAULT_GUN_ID, guns[0].Data.gunId);
    }

    [Test]
    public void Player_SpendUpgradePoints_CanTargetSpecificGunArchetype()
    {
        _player.AddGun(Resources.Load<GameObject>("gunKinetic").GetComponent<Gun>());
        _player.AddGun(Resources.Load<GameObject>("gunPlasma").GetComponent<Gun>());
        Assert.AreEqual(2, _player.GunCount);

        List<Gun> guns = _player.GetEquippedGuns();
        Gun kineticGun = guns.Find(g => g.Data.gunId == GunKinetic.DEFAULT_GUN_ID);
        Gun plasmaGun = guns.Find(g => g.Data.gunId == GunPlasma.DEFAULT_GUN_ID);

        float initialKineticDmg = kineticGun.EffectiveDamage;
        float initialPlasmaDmg = plasmaGun.EffectiveDamage;

        _player.UpgradePoints = 2;

        // Spend 1 point specifically on kinetic gun damage
        bool success = _player.SpendUpgradePoints(WeaponStatType.Damage, 1, GunKinetic.DEFAULT_GUN_ID);
        Assert.IsTrue(success);
        Assert.AreEqual(1, _player.UpgradePoints);

        Assert.AreEqual(initialKineticDmg + 1.0f, kineticGun.EffectiveDamage, "Kinetic gun damage should increase");
        Assert.AreEqual(initialPlasmaDmg, plasmaGun.EffectiveDamage, "Plasma gun damage should remain unchanged");

        // Spend 1 point specifically on plasma gun fire rate
        float initialPlasmaRate = plasmaGun.EffectiveFireRate;
        float initialKineticRate = kineticGun.EffectiveFireRate;

        bool success2 = _player.SpendUpgradePoints(WeaponStatType.FireRate, 1, GunPlasma.DEFAULT_GUN_ID);
        Assert.IsTrue(success2);
        Assert.AreEqual(0, _player.UpgradePoints);

        Assert.AreEqual(initialPlasmaRate + 0.5f, plasmaGun.EffectiveFireRate, "Plasma fire rate should increase");
        Assert.AreEqual(initialKineticRate, kineticGun.EffectiveFireRate, "Kinetic fire rate should remain unchanged");
    }

    [Test]
    public void StandardPowerup_AmmoRefill_RefillsFiniteWeapons()
    {
        GunSocket socket = _player.AddSocket("socket0");
        GameObject gunObj = new GameObject("FiniteGun");
        Gun gun = gunObj.AddComponent<Gun>();

        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = "finite_gun";
        data.bulletPrefab = Resources.Load<GameObject>("bulletKinetic");
        data.ammo_quantity = 10;
        gun.Data = data;
        socket.AttachGunInstance(gun);

        // Consume 6 ammo
        gun.CurrentAmmo = 4;
        Assert.AreEqual(4, gun.CurrentAmmo);

        // Pick up AmmoRefill powerup
        GameObject pwpObj = new GameObject("PwpAmmo");
        StandardPowerup sp = pwpObj.AddComponent<StandardPowerup>();
        sp.Type = StandardPowerup.StandardType.AmmoRefill;

        sp.TryCollect(_player);

        Assert.AreEqual(10, gun.CurrentAmmo, "Gun ammo should be completely refilled to MaxAmmo");

        Object.DestroyImmediate(gunObj);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void PowerupPrefabs_ExistAndHaveCorrectTypes()
    {
        GameObject pwpKinetic = Resources.Load<GameObject>("pwpGunKinetic") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Powerups/pwpGunKinetic.prefab");
        Assert.IsNotNull(pwpKinetic, "pwpGunKinetic prefab should exist");
        StandardPowerup spK = pwpKinetic.GetComponent<StandardPowerup>();
        Assert.IsNotNull(spK);
        Assert.AreEqual(StandardPowerup.StandardType.GunKinetic, spK.Type);

        GameObject pwpPlasma = Resources.Load<GameObject>("pwpGunPlasma") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Powerups/pwpGunPlasma.prefab");
        Assert.IsNotNull(pwpPlasma, "pwpGunPlasma prefab should exist");
        StandardPowerup spP = pwpPlasma.GetComponent<StandardPowerup>();
        Assert.IsNotNull(spP);
        Assert.AreEqual(StandardPowerup.StandardType.GunPlasma, spP.Type);

        GameObject pwpAmmo = Resources.Load<GameObject>("pwpAmmo") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Powerups/pwpAmmo.prefab");
        Assert.IsNotNull(pwpAmmo, "pwpAmmo prefab should exist");
        StandardPowerup spA = pwpAmmo.GetComponent<StandardPowerup>();
        Assert.IsNotNull(spA);
        Assert.AreEqual(StandardPowerup.StandardType.AmmoRefill, spA.Type);

        GameObject pwpUpgradePoint = Resources.Load<GameObject>("pwpUpgradePoint") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Powerups/pwpUpgradePoint.prefab");
        Assert.IsNotNull(pwpUpgradePoint, "pwpUpgradePoint prefab should exist");
        StandardPowerup spUp = pwpUpgradePoint.GetComponent<StandardPowerup>();
        Assert.IsNotNull(spUp);
        Assert.AreEqual(StandardPowerup.StandardType.UpgradePoint, spUp.Type);
    }

    [Test]
    public void StandardPowerup_UpgradePoint_GrantsPointsToPlayer()
    {
        Assert.AreEqual(0, _player.UpgradePoints);

        int eventFiredCount = 0;
        int lastReportedPoints = 0;
        _player.OnUpgradePointsChanged += (pts) => {
            eventFiredCount++;
            lastReportedPoints = pts;
        };

        GameObject pwpObj = new GameObject("PwpPoint");
        StandardPowerup sp = pwpObj.AddComponent<StandardPowerup>();
        sp.Type = StandardPowerup.StandardType.UpgradePoint;
        sp.Potency = 3f;

        bool collected = sp.TryCollect(_player);
        Assert.IsTrue(collected);
        Assert.AreEqual(3, _player.UpgradePoints);
        Assert.IsTrue(eventFiredCount > 0);
        Assert.AreEqual(3, lastReportedPoints);
    }

    [Test]
    public void Player_SpendUpgradePoints_Damage_IncreasesGunAndPlayerDamage()
    {
        _player.AddGun(Resources.Load<GameObject>("gunKinetic").GetComponent<Gun>());
        Gun gun = _player.GetEquippedGuns()[0];

        _player.UpgradePoints = 2;
        float playerBaseDmg = _player._bullet_dmg;
        float gunBaseDmg = gun.EffectiveDamage;

        bool success = _player.UpgradeDamageWithPoints(1);
        Assert.IsTrue(success);
        Assert.AreEqual(1, _player.UpgradePoints);
        Assert.AreEqual(playerBaseDmg + 1.0f, _player._bullet_dmg);
        Assert.AreEqual(gunBaseDmg + 1.0f, gun.EffectiveDamage);
    }

    [Test]
    public void Player_SpendUpgradePoints_Force_IncreasesGunAndPlayerForce()
    {
        _player.AddGun(Resources.Load<GameObject>("gunKinetic").GetComponent<Gun>());
        Gun gun = _player.GetEquippedGuns()[0];

        _player.UpgradePoints = 1;
        float playerBaseForce = _player._bullet_force;
        float gunBaseForce = gun.EffectiveForce;

        bool success = _player.UpgradeForceWithPoints(1);
        Assert.IsTrue(success);
        Assert.AreEqual(0, _player.UpgradePoints);
        Assert.AreEqual(playerBaseForce + 2.0f, _player._bullet_force);
        Assert.AreEqual(gunBaseForce + 2.0f, gun.EffectiveForce);
    }

    [Test]
    public void Player_SpendUpgradePoints_FireRate_IncreasesGunAndPlayerRate()
    {
        _player.AddGun(Resources.Load<GameObject>("gunPlasma").GetComponent<Gun>());
        Gun gun = _player.GetEquippedGuns()[0];

        _player.UpgradePoints = 1;
        float playerBaseHz = _player._fire_hz;
        float gunBaseRate = gun.EffectiveFireRate;

        bool success = _player.UpgradeFireRateWithPoints(1);
        Assert.IsTrue(success);
        Assert.AreEqual(0, _player.UpgradePoints);
        Assert.AreEqual(playerBaseHz + 0.5f, _player._fire_hz);
        Assert.AreEqual(gunBaseRate + 0.5f, gun.EffectiveFireRate);
    }

    [Test]
    public void Player_SpendUpgradePoints_FailsWhenInsufficientPoints()
    {
        _player.UpgradePoints = 0;
        float baseDmg = _player._bullet_dmg;

        bool success = _player.UpgradeDamageWithPoints(1);
        Assert.IsFalse(success);
        Assert.AreEqual(0, _player.UpgradePoints);
        Assert.AreEqual(baseDmg, _player._bullet_dmg);

        bool successForce = _player.UpgradeForceWithPoints(1);
        Assert.IsFalse(successForce);

        bool successRate = _player.UpgradeFireRateWithPoints(1);
        Assert.IsFalse(successRate);
    }
}

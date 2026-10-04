using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GunMigrationTests
{
    private GameObject _playerObj;
    private player _player;
    private GameObject _testAsteroidObj;
    private AsteroidGrid _asteroid;

    [SetUp]
    public void SetUp()
    {
        _playerObj = new GameObject("TestPlayer");
        _playerObj.AddComponent<Rigidbody>();
        _playerObj.AddComponent<SphereCollider>();
        _player = _playerObj.AddComponent<player>();

        _testAsteroidObj = new GameObject("TestAsteroid");
        _testAsteroidObj.AddComponent<Rigidbody>();
        _asteroid = _testAsteroidObj.AddComponent<AsteroidGrid>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_testAsteroidObj != null) Object.DestroyImmediate(_testAsteroidObj);

        // Clean up any stray projectiles in scene
        foreach (var proj in Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
        {
            if (proj != null) Object.DestroyImmediate(proj.gameObject);
        }

        // Clean up any stray PowerupManager instances
        foreach (var pm in Object.FindObjectsByType<PowerupManager>(FindObjectsSortMode.None))
        {
            if (pm != null) Object.DestroyImmediate(pm.gameObject);
        }
    }

    [Test]
    public void GunKinetic_InitializesDefaultData_AndFiresBulletKinetic()
    {
        GameObject gunObj = new GameObject("KineticGun");
        GunKinetic gun = gunObj.AddComponent<GunKinetic>();
        gun.SetOwner(_playerObj);

        Assert.IsNotNull(gun.Data);
        Assert.AreEqual(GunKinetic.DEFAULT_GUN_ID, gun.Data.gunId);
        Assert.AreEqual(2.0f, gun.Data.bulletDamage);
        Assert.AreEqual(12.0f, gun.Data.bulletForce);

        bool fired = gun.TryFire();
        Assert.IsTrue(fired, "GunKinetic should fire successfully");

        // Verify spawned bullet
        bulletKinetic bullet = Object.FindAnyObjectByType<bulletKinetic>();
        Assert.IsNotNull(bullet, "Should spawn a bulletKinetic instance");
        Assert.AreEqual(2, bullet.Damage);
        Assert.AreEqual(_playerObj, bullet.Owner);
        Assert.AreEqual(_player, bullet._player);

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void GunPlasma_InitializesDefaultData_AndFiresBulletPlasma()
    {
        GameObject gunObj = new GameObject("PlasmaGun");
        GunPlasma gun = gunObj.AddComponent<GunPlasma>();
        gun.SetOwner(_playerObj);

        Assert.IsNotNull(gun.Data);
        Assert.AreEqual(GunPlasma.DEFAULT_GUN_ID, gun.Data.gunId);
        Assert.AreEqual(1.0f, gun.Data.bulletDamage);
        Assert.AreEqual(2, gun.Data.burstCount);

        bool fired = gun.TryFire();
        Assert.IsTrue(fired, "GunPlasma should fire successfully");

        bulletPlasma bullet = Object.FindAnyObjectByType<bulletPlasma>();
        Assert.IsNotNull(bullet, "Should spawn a bulletPlasma instance");
        Assert.AreEqual(1, bullet.Damage);
        Assert.AreEqual(_playerObj, bullet.Owner);

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void BulletKinetic_DamagesBlock0_AndAwardsXPToPlayer()
    {
        GameObject blockObj = new GameObject("Block");
        blockObj.tag = "block";
        BoxCollider col = blockObj.AddComponent<BoxCollider>();
        block0 b0 = blockObj.AddComponent<block0>();
        b0.SetHits(5);

        GameObject bulletObj = new GameObject("KineticBullet");
        bulletObj.AddComponent<SphereCollider>();
        bulletObj.AddComponent<Rigidbody>();
        bulletKinetic bullet = bulletObj.AddComponent<bulletKinetic>();
        bullet.Initialize(_playerObj, 3, 10f);

        ulong xpBefore = _player.Progression.CurrentXP;

        // Damage block with bullet
        b0.block_receive_hit(bullet.transform, null, bullet.Damage);

        Assert.AreEqual(2, b0._hits, "Block hits should reduce from 5 to 2 (3 damage)");
        Assert.AreEqual(xpBefore + 3, _player.Progression.CurrentXP, "Player should receive XP equal to damage");

        Object.DestroyImmediate(blockObj);
        Object.DestroyImmediate(bulletObj);
    }

    [Test]
    public void Player_AimWeapons_RotatesMountedSockets_AndDirectGuns()
    {
        GunSocket socket = _player.AddSocket("socket0");
        GameObject gunObj = new GameObject("Gun");
        Gun gun = gunObj.AddComponent<Gun>();
        socket.AttachGunInstance(gun);

        Vector3 targetPos = new Vector3(0f, 100f, 0f); // Directly above (+Y)
        _player.AimWeapons(targetPos);

        // With target directly above, local Z rotation in aster aiming math is 0 degrees
        float zRot = gun.TurretPoint.eulerAngles.z;
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, zRot), 1.0f);
    }

    [Test]
    public void Player_FireWeapons_FiresMountedGunsOnSockets()
    {
        GunSocket socket = _player.AddSocket("socket0");
        GameObject gunObj = new GameObject("KineticGun");
        GunKinetic gun = gunObj.AddComponent<GunKinetic>();
        gun.EnsureKineticData();
        socket.AttachGunInstance(gun);

        bool fired = _player.FireWeapons();
        Assert.IsTrue(fired, "FireWeapons should return true when mounted gun fires");

        bulletKinetic bullet = Object.FindAnyObjectByType<bulletKinetic>();
        Assert.IsNotNull(bullet);
    }

    [Test]
    public void Player_AddGun_MountsToEmptySocket_OrCreatesNewSocket()
    {
        Assert.AreEqual(0, _player.SocketCount);

        // Pre-create 1 socket
        _player.AddSocket("test_socket");
        Assert.AreEqual(1, _player.SocketCount);
        Assert.IsTrue(_player.HasAvailableSocket());

        GameObject gunPrefab = PrefabManager.Get(PrefabId.GunKinetic);
        Assert.IsNotNull(gunPrefab);

        // 1. Mounts to empty socket
        bool success = _player.AddGun(gunPrefab.GetComponent<Gun>());
        Assert.IsTrue(success);
        Assert.AreEqual(1, _player.SocketCount);
        Assert.IsFalse(_player.HasAvailableSocket(), "Socket should now be occupied");
        Assert.AreEqual(1, _player.GunCount);

        // 2. Add another gun: automatically creates a new socket because first is full
        bool success2 = _player.AddGun(gunPrefab.GetComponent<Gun>());
        Assert.IsTrue(success2);
        Assert.AreEqual(2, _player.SocketCount, "Should create a new socket");
        Assert.AreEqual(2, _player.GunCount);
    }

    [Test]
    public void Gun_StatUpgrades_ApplyProperlyAndEnhanceEffectiveStats()
    {
        GameObject gunObj = new GameObject("UpgradableGun");
        GunKinetic modernGun = gunObj.AddComponent<GunKinetic>();
        modernGun.SetOwner(_playerObj);

        float baseDmg = modernGun.Data.bulletDamage;
        float baseForce = modernGun.Data.bulletForce;

        modernGun.UpgradeDamage(2f);
        modernGun.UpgradeForce(4f);
        modernGun.UpgradeBurst(1);

        Assert.AreEqual(baseDmg + 2f, modernGun.EffectiveDamage);
        Assert.AreEqual(baseForce + 4f, modernGun.EffectiveForce);
        Assert.AreEqual(2, modernGun.EffectiveBurstCount);

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void Gun_AmmoSystem_RespectsInfiniteAndFiniteAmmo()
    {
        GameObject gunObj = new GameObject("AmmoGun");
        Gun gun = gunObj.AddComponent<Gun>();

        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = "ammo_test";
        data.bulletPrefab = PrefabManager.Get(PrefabId.BulletKinetic);
        data.ammo_quantity = -1; // Infinite ammo
        gun.Data = data;

        Assert.IsTrue(gun.IsInfiniteAmmo);
        Assert.IsTrue(gun.HasAmmo);

        // Finite ammo test
        data.ammo_quantity = 5;
        gun.InitializeAmmo();

        Assert.IsFalse(gun.IsInfiniteAmmo);
        Assert.AreEqual(5, gun.CurrentAmmo);
        Assert.AreEqual(5, gun.MaxAmmo);
        Assert.IsTrue(gun.HasAmmo);

        // Consume ammo on fire
        gun.TryFire();
        Assert.AreEqual(4, gun.CurrentAmmo);

        // Refill ammo
        gun.RefillAmmo();
        Assert.AreEqual(5, gun.CurrentAmmo);

        Object.DestroyImmediate(gunObj);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void PowerupManager_ApplyExtraGun_AddsGunToPlayer()
    {
        _player.AddSocket("initial_socket");
        int initialGuns = _player.GunCount;

        bool result = PowerupManager.Instance.ApplyExtraGun(_player);
        Assert.IsTrue(result);
        Assert.AreEqual(initialGuns + 1, _player.GunCount);
    }
}

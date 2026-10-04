using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PlayerWeaponCheckTests
{
    private GameObject _playerObj;
    private player _player;
    private GunData _testGunData;
    private GameObject _bulletPrefab;

    [SetUp]
    public void SetUp()
    {
        _playerObj = new GameObject("TestPlayer");
        _player = _playerObj.AddComponent<player>();

        _bulletPrefab = new GameObject("TestBulletPrefab");
        _bulletPrefab.AddComponent<Rigidbody>();
        _bulletPrefab.AddComponent<SphereCollider>();

        _testGunData = ScriptableObject.CreateInstance<GunData>();
        _testGunData.gunId = "test_gun";
        _testGunData.displayName = "Test Gun";
        _testGunData.bulletPrefab = _bulletPrefab;
    }

    [TearDown]
    public void TearDown()
    {
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_bulletPrefab != null) Object.DestroyImmediate(_bulletPrefab);
        if (_testGunData != null) Object.DestroyImmediate(_testGunData);
    }

    [Test]
    public void HasSockets_ReturnsFalse_WhenNoSocketsExist()
    {
        Assert.IsFalse(_player.HasSockets());
        Assert.IsFalse(_player.HasAnySockets());
        Assert.AreEqual(0, _player.SocketCount);
        Assert.IsFalse(_player.HasAvailableSocket());
    }

    [Test]
    public void HasSockets_ReturnsTrue_WhenSocketAddedAsChild()
    {
        GameObject socketObj = new GameObject("SocketChild");
        socketObj.transform.SetParent(_playerObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        Assert.IsTrue(_player.HasSockets());
        Assert.IsTrue(_player.HasAnySockets());
        Assert.AreEqual(1, _player.SocketCount);
        Assert.IsTrue(_player.HasAvailableSocket());
    }

    [Test]
    public void HasSockets_ReturnsTrue_WhenSocketInSerializedList()
    {
        GameObject socketObj = new GameObject("ExternalSocket");
        GunSocket socket = socketObj.AddComponent<GunSocket>();
        _player._sockets.Add(socket);

        Assert.IsTrue(_player.HasSockets());
        Assert.AreEqual(1, _player.SocketCount);

        Object.DestroyImmediate(socketObj);
    }

    [Test]
    public void HasGuns_ReturnsFalse_WhenNoGunsExist()
    {
        Assert.IsFalse(_player.HasGuns());
        Assert.IsFalse(_player.HasAnyGuns());
        Assert.AreEqual(0, _player.GunCount);
    }

    [Test]
    public void HasGuns_ReturnsTrue_ForGunInList()
    {
        GameObject gunObj = new GameObject("PlayerGun");
        gunObj.transform.SetParent(_playerObj.transform);
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;
        _player._guns.Add(gun);

        Assert.IsTrue(_player.HasGuns());
        Assert.IsTrue(_player.HasAnyGuns());
        Assert.AreEqual(1, _player.GunCount);
    }

    [Test]
    public void HasGuns_ReturnsTrue_ForGunInChildEvenIfNotInList()
    {
        GameObject gunObj = new GameObject("PlayerGunChild");
        gunObj.transform.SetParent(_playerObj.transform);
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;

        Assert.IsTrue(_player.HasGuns());
        Assert.AreEqual(1, _player.GunCount);
    }

    [Test]
    public void HasGuns_ReturnsTrue_ForMountedGunOnSocket()
    {
        GameObject socketObj = new GameObject("SocketChild");
        socketObj.transform.SetParent(_playerObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        GameObject gunObj = new GameObject("MountedGun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;

        socket.AttachGunInstance(gun);

        Assert.IsTrue(socket.HasGun);
        Assert.IsTrue(_player.HasGuns());
        Assert.IsTrue(_player.HasAnyGuns());
        Assert.AreEqual(1, _player.GunCount);
        // Sockets is now occupied, so HasAvailableSocket should be false
        Assert.IsFalse(_player.HasAvailableSocket());
    }

    [Test]
    public void HasSocketsAndGuns_ReturnsTrue_OnlyWhenBothPresent()
    {
        // 1. Initial state: neither
        Assert.IsFalse(_player.HasSocketsAndGuns());
        Assert.IsFalse(_player.HasAnySocketsAndGuns());
        Assert.IsFalse(_player.HasAnySocketsOrGuns());

        // 2. Add socket only: has socket, no gun
        GameObject socketObj = new GameObject("SocketChild");
        socketObj.transform.SetParent(_playerObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        Assert.IsTrue(_player.HasSockets());
        Assert.IsFalse(_player.HasGuns());
        Assert.IsFalse(_player.HasSocketsAndGuns());
        Assert.IsTrue(_player.HasAnySocketsOrGuns());

        // 3. Mount gun: has both socket and gun
        GameObject gunObj = new GameObject("Gun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;
        socket.AttachGunInstance(gun);

        Assert.IsTrue(_player.HasSockets());
        Assert.IsTrue(_player.HasGuns());
        Assert.IsTrue(_player.HasSocketsAndGuns());
        Assert.IsTrue(_player.HasAnySocketsAndGuns());
        Assert.IsTrue(_player.HasAnySocketsOrGuns());

        // 4. Check out parameters and tuple
        bool hasSockets, hasGuns;
        bool both = _player.CheckSocketsAndGuns(out hasSockets, out hasGuns);
        Assert.IsTrue(both);
        Assert.IsTrue(hasSockets);
        Assert.IsTrue(hasGuns);

        var status = _player.CheckSocketsAndGunsStatus();
        Assert.IsTrue(status.hasSockets);
        Assert.IsTrue(status.hasGuns);
    }

    [Test]
    public void PlayerShip_Prefab_HasSocketsOnLoad()
    {
        GameObject prefab = PrefabManager.Get(PrefabId.PlayerShip);
        Assert.IsNotNull(prefab, "Player_ship prefab should exist in catalog");

        GameObject instance = Object.Instantiate(prefab);
        player p = instance.GetComponent<player>();
        Assert.IsNotNull(p, "Player_ship should have a player component");

        // Player_ship prefab has 3 GunSockets configured under hull/gun_sockets
        Assert.IsTrue(p.HasSockets(), "Player_ship must have sockets");
        Assert.AreEqual(3, p.SocketCount, "Player_ship prefab should have 3 sockets");

        // Sockets start unoccupied
        Assert.IsTrue(p.HasAvailableSocket());
        Assert.IsFalse(p.HasGuns());
        Assert.IsFalse(p.HasSocketsAndGuns());

        // Mount a gun on one of the sockets
        List<GunSocket> sockets = p.GetSockets();
        Assert.AreEqual(3, sockets.Count);

        GameObject gunObj = new GameObject("TestGun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;

        sockets[0].AttachGunInstance(gun);

        Assert.IsTrue(p.HasGuns());
        Assert.IsTrue(p.HasSocketsAndGuns());
        Assert.AreEqual(1, p.GunCount);
        Assert.IsTrue(p.HasAvailableSocket(), "Still has 2 available sockets");

        Object.DestroyImmediate(instance);
    }
}

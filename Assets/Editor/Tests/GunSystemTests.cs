using NUnit.Framework;
using UnityEngine;

public class GunSystemTests
{
    private GameObject _vesselObj;
    private Rigidbody _vesselRb;
    private Collider _vesselCol;

    private GameObject _bulletPrefab;
    private GunData _testGunData;

    [SetUp]
    public void SetUp()
    {
        _vesselObj = new GameObject("TestShip");
        _vesselRb = _vesselObj.AddComponent<Rigidbody>();
        _vesselCol = _vesselObj.AddComponent<BoxCollider>();

        _bulletPrefab = new GameObject("TestBulletPrefab");
        _bulletPrefab.AddComponent<Rigidbody>();
        _bulletPrefab.AddComponent<SphereCollider>();

        _testGunData = ScriptableObject.CreateInstance<GunData>();
        _testGunData.gunId = "test_laser";
        _testGunData.displayName = "Test Laser";
        _testGunData.fireRate = 2.0f; // 0.5s interval
        _testGunData.bulletDamage = 3.0f;
        _testGunData.bulletForce = 10.0f;
        _testGunData.bulletPrefab = _bulletPrefab;
    }

    [TearDown]
    public void TearDown()
    {
        if (_vesselObj != null) Object.DestroyImmediate(_vesselObj);
        if (_bulletPrefab != null) Object.DestroyImmediate(_bulletPrefab);
        if (_testGunData != null) Object.DestroyImmediate(_testGunData);
    }

    [Test]
    public void Gun_SetOwner_CachesOwnerComponents()
    {
        GameObject gunObj = new GameObject("Gun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;

        gun.SetOwner(_vesselObj);

        Assert.AreEqual(_vesselObj, gun.Owner);
        Assert.AreEqual(_vesselRb, gun.OwnerRigidbody);
        Assert.IsNotNull(gun.OwnerColliders);
        Assert.AreEqual(1, gun.OwnerColliders.Length);
        Assert.AreEqual(_vesselCol, gun.OwnerColliders[0]);

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void Gun_TryFire_FiresAndEnforcesRateLimit()
    {
        GameObject gunObj = new GameObject("Gun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;
        gun.SetOwner(_vesselObj);

        bool firedEvent = false;
        gun.OnFired += (g) => firedEvent = true;

        bool firstShot = gun.TryFire();
        Assert.IsTrue(firstShot, "First shot should succeed");
        Assert.IsTrue(firedEvent, "OnFired should be invoked");

        // Immediate subsequent shot should be blocked by cooldown
        bool secondShot = gun.TryFire();
        Assert.IsFalse(secondShot, "Immediate shot should fail due to fire rate cooldown");

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void Gun_AimAt_PointsTurretTowardsTarget()
    {
        GameObject gunObj = new GameObject("Gun");
        Gun gun = gunObj.AddComponent<Gun>();
        gun.Data = _testGunData;

        // Target directly above gun (+Y)
        gun.AimAt(gunObj.transform.position + new Vector3(0f, 10f, 0f));
        // Angle should point up (0 degrees local Z in aster)
        Assert.AreEqual(0f, gun.TurretPoint.eulerAngles.z, 0.5f);

        // Target to the right (+X) -> 270 or -90 degrees
        gun.AimAt(gunObj.transform.position + new Vector3(10f, 0f, 0f));
        Assert.AreEqual(270f, gun.TurretPoint.eulerAngles.z, 0.5f);

        Object.DestroyImmediate(gunObj);
    }

    [Test]
    public void GunSocket_AttachGun_MountsAndSetsOwnership()
    {
        GameObject socketObj = new GameObject("Socket");
        socketObj.transform.SetParent(_vesselObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        GameObject gunPrefab = new GameObject("GunPrefab");
        Gun gunComponent = gunPrefab.AddComponent<Gun>();
        gunComponent.Data = _testGunData;

        bool mountEventFired = false;
        socket.OnGunMounted += (g) => mountEventFired = true;

        bool attached = socket.AttachGun(gunComponent);

        Assert.IsTrue(attached);
        Assert.IsTrue(socket.HasGun);
        Assert.IsNotNull(socket.MountedGun);
        Assert.AreEqual(_vesselObj, socket.MountedGun.Owner);
        Assert.IsTrue(mountEventFired);

        Object.DestroyImmediate(gunPrefab);
        Object.DestroyImmediate(socketObj);
    }

    [Test]
    public void GunSocket_DetachGun_RemovesMountedGun()
    {
        GameObject socketObj = new GameObject("Socket");
        socketObj.transform.SetParent(_vesselObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        GameObject gunPrefab = new GameObject("GunPrefab");
        Gun gunComponent = gunPrefab.AddComponent<Gun>();
        gunComponent.Data = _testGunData;

        socket.AttachGun(gunComponent);
        Assert.IsTrue(socket.HasGun);

        bool unmountEventFired = false;
        socket.OnGunUnmounted += (g) => unmountEventFired = true;

        socket.DetachGun();

        Assert.IsFalse(socket.HasGun);
        Assert.IsNull(socket.MountedGun);
        Assert.IsTrue(unmountEventFired);

        Object.DestroyImmediate(gunPrefab);
        Object.DestroyImmediate(socketObj);
    }

    [Test]
    public void GunSocket_TryFireMountedGun_DelegatesToGun()
    {
        GameObject socketObj = new GameObject("Socket");
        socketObj.transform.SetParent(_vesselObj.transform);
        GunSocket socket = socketObj.AddComponent<GunSocket>();

        GameObject gunPrefab = new GameObject("GunPrefab");
        Gun gunComponent = gunPrefab.AddComponent<Gun>();
        gunComponent.Data = _testGunData;

        socket.AttachGun(gunComponent);

        bool fired = socket.TryFireMountedGun();
        Assert.IsTrue(fired);

        Object.DestroyImmediate(gunPrefab);
        Object.DestroyImmediate(socketObj);
    }
}

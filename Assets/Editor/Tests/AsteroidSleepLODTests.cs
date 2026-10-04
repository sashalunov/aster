using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class AsteroidSleepLODTests
{
    private GameObject _playerObj;
    private player _player;
    private GameObject _asteroidObj;
    private AsteroidGrid _asteroidGrid;

    [SetUp]
    public void SetUp()
    {
        _playerObj = new GameObject("TestPlayer");
        _player = _playerObj.AddComponent<player>();

        GameObject asterPrefab = Resources.Load<GameObject>("AsteroidGrid");
        if (asterPrefab != null)
        {
            _asteroidObj = Object.Instantiate(asterPrefab, new Vector3(20, 0, 0), Quaternion.identity);
            _asteroidGrid = _asteroidObj.GetComponent<AsteroidGrid>();
        }
        else
        {
            _asteroidObj = new GameObject("TestAsteroid");
            _asteroidGrid = _asteroidObj.AddComponent<AsteroidGrid>();
        }

        _asteroidGrid.generate_asteroid(3, 2, 4);
        _asteroidGrid.SyncGrid();
    }

    [TearDown]
    public void TearDown()
    {
        if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        if (_asteroidObj != null) Object.DestroyImmediate(_asteroidObj);
    }

    [Test]
    public void AsteroidBase_SetSleeping_TogglesStateAndPreservesVelocity()
    {
        Rigidbody rb = _asteroidGrid.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb);

        Vector3 originalVel = new Vector3(2.5f, -1.2f, 0f);
        Vector3 originalAngVel = new Vector3(0f, 0f, 0.5f);

        rb.isKinematic = false;
        rb.linearVelocity = originalVel;
        rb.angularVelocity = originalAngVel;

        // Transition to sleep
        _asteroidGrid.SetSleeping(true);

        Assert.IsTrue(_asteroidGrid.IsSleeping);
        Assert.IsTrue(rb.isKinematic);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, rb.angularVelocity);

        // Wake up
        _asteroidGrid.SetSleeping(false);

        Assert.IsFalse(_asteroidGrid.IsSleeping);
        Assert.IsFalse(rb.isKinematic);
        Assert.AreEqual(originalVel.x, rb.linearVelocity.x, 0.01f);
        Assert.AreEqual(originalVel.y, rb.linearVelocity.y, 0.01f);
        Assert.AreEqual(originalAngVel.z, rb.angularVelocity.z, 0.01f);
    }

    [Test]
    public void AsteroidGrid_SetSleeping_PausesAndResumesParticles()
    {
        ParticleSystem ps = _asteroidObj.GetComponentInChildren<ParticleSystem>();
        if (ps == null)
        {
            GameObject psObj = new GameObject("TestPS");
            psObj.transform.SetParent(_asteroidObj.transform);
            ps = psObj.AddComponent<ParticleSystem>();
        }

        ps.Play();
        Assert.IsTrue(ps.isPlaying);

        _asteroidGrid.SetSleeping(true);
        Assert.IsTrue(_asteroidGrid.IsSleeping);
        Assert.IsTrue(ps.isPaused);

        _asteroidGrid.SetSleeping(false);
        Assert.IsFalse(_asteroidGrid.IsSleeping);
        Assert.IsTrue(ps.isPlaying);
    }

    [Test]
    public void Player_OnTriggerExit_Core_PutsAsteroidToSleepWithoutDestroyingIt()
    {
        Transform coreT = _asteroidObj.transform.Find("core_block");
        Assert.IsNotNull(coreT, "Expected core_block on asteroid");

        Collider coreCol = coreT.GetComponent<Collider>();
        if (coreCol == null) coreCol = coreT.gameObject.AddComponent<BoxCollider>();

        Assert.IsFalse(_asteroidGrid.IsSleeping);

        // Simulate trigger exit with core collider
        _player.HandleTriggerExit(coreCol);

        // Verify the asteroid transitioned to sleep and is NOT destroyed!
        Assert.IsTrue(_asteroidGrid.IsSleeping, "Asteroid must be sleeping after core exits trigger");
        Assert.IsNotNull(_asteroidGrid.gameObject, "Asteroid must NOT be destroyed on trigger exit");
        Assert.IsNotNull(coreT.gameObject, "Core must NOT be destroyed on trigger exit");
        Assert.IsFalse(_asteroidGrid.coreBlock._dead, "Core block must remain alive");
    }

    [Test]
    public void Player_OnTriggerEnter_WakesUpAsteroid()
    {
        Transform coreT = _asteroidObj.transform.Find("core_block");
        Collider coreCol = coreT.GetComponent<Collider>();
        if (coreCol == null) coreCol = coreT.gameObject.AddComponent<BoxCollider>();

        // Put to sleep first
        _asteroidGrid.SetSleeping(true);
        Assert.IsTrue(_asteroidGrid.IsSleeping);

        // Simulate trigger enter
        _player.HandleTriggerEnter(coreCol);

        // Verify woke up
        Assert.IsFalse(_asteroidGrid.IsSleeping, "Asteroid must wake up when entering trigger");
    }

    [Test]
    public void Asteroid_WakesUpAutomaticallyWhenDamaged()
    {
        _asteroidGrid.SetHits(5);
        _asteroidGrid.SetSleeping(true);
        Assert.IsTrue(_asteroidGrid.IsSleeping);

        // Damage the core while asleep
        _asteroidGrid.core_receive_hit(source: null, b1: null);

        // Verify woke up
        Assert.IsFalse(_asteroidGrid.IsSleeping, "Asteroid must automatically wake up upon receiving a hit");
    }
}

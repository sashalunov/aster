using NUnit.Framework;
using UnityEngine;

public class ProjectileSystemTests
{
    private GameObject _shooterObj;
    private Rigidbody _shooterRb;
    private BoxCollider _shooterCol;

    private GameObject _targetObj;
    private player _targetPlayer;
    private BoxCollider _targetCol;

    [SetUp]
    public void SetUp()
    {
        _shooterObj = new GameObject("ShooterShip");
        _shooterRb = _shooterObj.AddComponent<Rigidbody>();
        _shooterCol = _shooterObj.AddComponent<BoxCollider>();

        _targetObj = new GameObject("TargetShip");
        _targetPlayer = _targetObj.AddComponent<player>();
        _targetPlayer.health_value = 10f;
        _targetPlayer.health_max_value = 10f;
        _targetPlayer.shield_value = 0f;
        _targetPlayer.shield_max_value = 10f;
        _targetCol = _targetObj.AddComponent<BoxCollider>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_shooterObj != null) Object.DestroyImmediate(_shooterObj);
        if (_targetObj != null) Object.DestroyImmediate(_targetObj);
    }

    private class TestProjectile : ProjectileBase
    {
        public void TriggerHit(Collider col)
        {
            OnTriggerEnter(col);
        }
    }

    [Test]
    public void ProjectileBase_Initialize_SetsStatsAndOwner()
    {
        GameObject projObj = new GameObject("Projectile");
        projObj.AddComponent<SphereCollider>();
        TestProjectile proj = projObj.AddComponent<TestProjectile>();

        proj.Initialize(_shooterObj, 5, 2.5f);

        Assert.AreEqual(5, proj.Damage);
        Assert.AreEqual(2.5f, proj.Mass);
        Assert.AreEqual(_shooterObj, proj.Owner);

        Object.DestroyImmediate(projObj);
    }

    [Test]
    public void ProjectileBase_OnHitPlayer_InflictsDamageToOpponent()
    {
        GameObject projObj = new GameObject("Projectile");
        projObj.AddComponent<SphereCollider>();
        TestProjectile proj = projObj.AddComponent<TestProjectile>();
        proj.Initialize(_shooterObj, 4);

        float initialHealth = _targetPlayer.health_value;
        proj.TriggerHit(_targetCol);

        Assert.AreEqual(initialHealth - 4f, _targetPlayer.health_value, 0.001f);
        Assert.IsTrue(proj.IsDead, "Projectile should expire after hitting target without penetration");
    }

    [Test]
    public void ProjectileBase_FriendlyFire_IgnoredAgainstSelf()
    {
        GameObject projObj = new GameObject("Projectile");
        projObj.AddComponent<SphereCollider>();
        TestProjectile proj = projObj.AddComponent<TestProjectile>();
        proj.Initialize(_targetObj, 5); // Target is the shooter

        float initialHealth = _targetPlayer.health_value;
        proj.TriggerHit(_targetCol);

        Assert.AreEqual(initialHealth, _targetPlayer.health_value, "Shooter must not damage themselves with own bullet");
        Assert.IsFalse(proj.IsDead, "Projectile should not expire on friendly collision");

        Object.DestroyImmediate(projObj);
    }

    [Test]
    public void ProjectileBase_Penetration_AllowsMultipleHitsBeforeExpiring()
    {
        GameObject projObj = new GameObject("PiercingProjectile");
        projObj.AddComponent<SphereCollider>();
        TestProjectile proj = projObj.AddComponent<TestProjectile>();
        proj.Initialize(_shooterObj, 2);
        proj.Penetration = 3;

        // Hit 1
        proj.TriggerHit(_targetCol);
        Assert.AreEqual(2, proj.Penetration);
        Assert.IsFalse(proj.IsDead, "Projectile should still be alive after first penetration");

        // Hit 2
        proj.TriggerHit(_targetCol);
        Assert.AreEqual(1, proj.Penetration);
        Assert.IsFalse(proj.IsDead);

        // Hit 3 -> should expire
        proj.TriggerHit(_targetCol);
        Assert.AreEqual(0, proj.Penetration);
        Assert.IsTrue(proj.IsDead, "Projectile should expire after consuming all penetration");
    }

    [Test]
    public void Bullet1_BackwardsCompatibility_MapsPlayerAndHitDamage()
    {
        GameObject b1Obj = new GameObject("Bullet1Instance");
        b1Obj.AddComponent<SphereCollider>();
        bullet1 b1 = b1Obj.AddComponent<bullet1>();

        b1._player = _targetPlayer;
        Assert.AreEqual(_targetObj, b1.Owner);
        Assert.AreEqual(_targetPlayer, b1._player);

        b1._hit_damage = 7;
        Assert.AreEqual(7, b1.Damage);
        Assert.AreEqual(7, b1._hit_damage);

        b1.bullet_mass = 3.5f;
        Assert.AreEqual(3.5f, b1.Mass);

        Object.DestroyImmediate(b1Obj);
    }
}

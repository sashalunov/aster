using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class TurretAITests
{
    [Test]
    public void TurretAI_PrefabHasRequiredComponentsAndReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Turrets/AI_gun0.prefab");
        Assert.IsNotNull(prefab, "AI_gun0 prefab must exist at Assets/Prefabs/Enemies/Turrets/AI_gun0.prefab");

        TurretAI turret = prefab.GetComponent<TurretAI>();
        Assert.IsNotNull(turret, "AI_gun0 prefab must have TurretAI component");

        AudioSource audio = prefab.GetComponent<AudioSource>();
        Assert.IsNotNull(audio, "AI_gun0 prefab must have AudioSource component");

        Assert.IsNotNull(turret.turretPart, "turretPart must be assigned");
        Assert.AreEqual("turret0", turret.turretPart.name);

        Assert.IsNotNull(turret.muzzlePoint, "muzzlePoint must be assigned");
        Assert.AreEqual("ps_muzzle", turret.muzzlePoint.name);

        Assert.IsNotNull(turret.animator, "animator must be assigned");
        Assert.IsNotNull(turret.bulletPrefab, "bulletPrefab must be assigned");
        Assert.IsNotNull(turret.fireSound, "fireSound must be assigned");

        Assert.Greater(turret.detectionRange, 0f, "detectionRange must be positive");
        Assert.Greater(turret.rotationSpeed, 0f, "rotationSpeed must be positive");
        Assert.Greater(turret.fireRate, 0f, "fireRate must be positive");
        Assert.Greater(turret.fireForce, 0f, "fireForce must be positive");
    }

    [Test]
    public void TurretAI_AutoConfigureReferences_ResolvesChildTransforms()
    {
        GameObject root = new GameObject("TestTurret");
        GameObject hull = new GameObject("hull");
        hull.transform.SetParent(root.transform);
        GameObject turret0 = new GameObject("turret0");
        turret0.transform.SetParent(hull.transform);
        GameObject gun0 = new GameObject("gun0");
        gun0.transform.SetParent(turret0.transform);
        GameObject muzzle = new GameObject("ps_muzzle");
        muzzle.transform.SetParent(gun0.transform);
        Animator anim = turret0.AddComponent<Animator>();

        TurretAI ai = root.AddComponent<TurretAI>();
        ai.AutoConfigureReferences();

        Assert.AreSame(turret0.transform, ai.turretPart, "turretPart should auto-resolve to turret0");
        Assert.AreSame(muzzle.transform, ai.muzzlePoint, "muzzlePoint should auto-resolve to ps_muzzle");
        Assert.AreSame(anim, ai.animator, "animator should auto-resolve to Animator on turret0");

        Object.DestroyImmediate(root);
    }

    [Test]
    public void TurretAI_AimAngleCalculation_PointsTowardsTarget()
    {
        // Target is directly below turret at (0, -10, 0)
        Vector3 turretPos = new Vector3(0f, 0f, 0f);
        Vector3 targetPos = new Vector3(0f, -10f, 0f);
        Vector3 dir = targetPos - turretPos;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, targetAngle);

        Vector3 aimDirection = rot * Vector3.up;
        float angleDiff = Vector3.Angle(dir.normalized, aimDirection);

        Assert.Less(angleDiff, 0.01f, "Aim direction should align with direction to target");
    }

    [Test]
    public void TurretAI_CalculateAimRotation_3D_PointsTowardsTargetInAnyDirection()
    {
        Vector3[] testDirections = new Vector3[]
        {
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, -10f),
            new Vector3(5f, 10f, 15f),
            new Vector3(-8f, -4f, 12f),
            new Vector3(12f, -6f, -9f),
            new Vector3(0f, 15f, 0f),
            new Vector3(0f, -15f, 0f)
        };

        foreach (var dir in testDirections)
        {
            Quaternion rot3D = TurretAI.CalculateAimRotation(dir, false);
            Vector3 aimDir = rot3D * Vector3.up;
            float angleDiff = Vector3.Angle(dir.normalized, aimDir);

            Assert.Less(angleDiff, 0.01f, $"3D aim direction should align with target direction {dir}");
        }
    }

    [Test]
    public void TurretAI_Shoot_UnfreezesBulletZAndFiresIn3D()
    {
        GameObject turretObj = new GameObject("TestTurret");
        TurretAI ai = turretObj.AddComponent<TurretAI>();
        ai.restrictToXYPlane = false;
        ai.alignBulletZWithTarget = false;
        ai.fireForce = 10f;

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(turretObj.transform);
        muzzle.transform.position = new Vector3(1f, 2f, 3f);
        ai.muzzlePoint = muzzle.transform;

        // Aim muzzle / turret along +Z axis
        turretObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        // Load bullet1 prefab
        ai.bulletPrefab = Resources.Load<GameObject>("bullet1");
        Assert.IsNotNull(ai.bulletPrefab, "bullet1 prefab should be loadable from Resources");

        // Fire bullet
        ai.Shoot();

        bullet1 spawnedBullet = Object.FindAnyObjectByType<bullet1>();
        Assert.IsNotNull(spawnedBullet, "Bullet should be spawned by Shoot()");

        Rigidbody rb = spawnedBullet.GetComponent<Rigidbody>();
        Assert.IsNotNull(rb, "Spawned bullet should have a Rigidbody");

        // Rigidbody should have FreezePositionZ cleared so it can move in 3D
        bool isZFrozen = (rb.constraints & RigidbodyConstraints.FreezePositionZ) != 0;
        Assert.IsFalse(isZFrozen, "FreezePositionZ constraint should be removed so bullet can move along Z axis");

        // Spawn position should be at muzzle's 3D position
        Assert.AreEqual(muzzle.transform.position.z, spawnedBullet.transform.position.z, 0.01f, "Bullet Z position should be at muzzle's Z position");

        Object.DestroyImmediate(spawnedBullet.gameObject);
        Object.DestroyImmediate(turretObj);
    }

    [Test]
    public void Player_TakeDamage_ReducesShield()
    {
        GameObject playerObj = new GameObject("TestPlayer");
        player p = playerObj.AddComponent<player>();
        p.shield_value = 10f;
        p.shield_max_value = 10f;

        p.TakeDamage(3f);

        Assert.AreEqual(7f, p.shield_value, 0.001f, "Player shield should be reduced by damage amount");

        Object.DestroyImmediate(playerObj);
    }

    [Test]
    public void Player_TakeDamage_LethalDamage_SetsIsDeadAndInvokesOnDeathOnce()
    {
        GameObject playerObj = new GameObject("TestPlayer");
        player p = playerObj.AddComponent<player>();
        p.shield_value = 5f;
        p.shield_max_value = 10f;
        p.health_value = 5f;
        p.health_max_value = 10f;

        int onDeathCount = 0;
        p.OnDeath += () => onDeathCount++;

        p.TakeDamage(10f);

        Assert.IsTrue(p.isDead, "Player should be flagged as isDead after lethal damage");
        Assert.IsFalse(p._can_play, "_can_play should be false after death");
        Assert.AreEqual(0f, p.shield_value, "Shield value should be clamped to 0 on death");
        Assert.AreEqual(0f, p.health_value, "Health value should be clamped to 0 on death");
        Assert.AreEqual(1, onDeathCount, "OnDeath should be invoked exactly once");

        // Subsequent damage should be ignored
        p.TakeDamage(10f);
        Assert.AreEqual(1, onDeathCount, "OnDeath should not be re-invoked on subsequent hits");
        Assert.AreEqual(0f, p.shield_value, "Shield should not decrease below 0");
        Assert.AreEqual(0f, p.health_value, "Health should not decrease below 0");

        Object.DestroyImmediate(playerObj);
    }
}

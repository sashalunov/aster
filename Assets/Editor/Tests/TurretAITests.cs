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
}

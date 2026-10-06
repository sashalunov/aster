using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ProjectileImpulseTests
{
    private readonly List<GameObject> cleanupList = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < cleanupList.Count; i++)
        {
            if (cleanupList[i] != null)
            {
                Object.DestroyImmediate(cleanupList[i]);
            }
        }
        cleanupList.Clear();
    }

    [Test]
    public void CalculateBounceImpulse_DefaultValues_CalculatesCorrectly()
    {
        var projGo = new GameObject("TestProjectile");
        cleanupList.Add(projGo);
        var proj = projGo.AddComponent<bulletKinetic>();

        // Formula: (mass + penetration + damage) * impulseMultiplier
        // Defaults: mass = 0.1f, penetration = 1, damage = 1f, impulseMultiplier = 1f => 2.1f
        float expected = (proj.Mass + proj.Penetration + proj.Damage) * proj.ImpulseMultiplier;
        Assert.AreEqual(2.1f, expected, 0.001f);
        Assert.AreEqual(expected, proj.CalculateBounceImpulse(), 0.001f);
    }

    [Test]
    public void CalculateBounceImpulse_CustomValuesAndMultiplier_CalculatesCorrectly()
    {
        var projGo = new GameObject("TestProjectileCustom");
        cleanupList.Add(projGo);
        var proj = projGo.AddComponent<bulletKinetic>();

        proj.Mass = 0.5f;
        proj.Penetration = 3;
        proj.Damage = 4f;
        proj.ImpulseMultiplier = 2.5f;

        float expected = (0.5f + 3 + 4f) * 2.5f; // 7.5 * 2.5 = 18.75f
        Assert.AreEqual(18.75f, expected, 0.001f);
        Assert.AreEqual(expected, proj.CalculateBounceImpulse(), 0.001f);
    }

    [Test]
    public void CalculateBounceImpulse_ChangingMultiplier_ScalesImpulseDirectly()
    {
        var projGo = new GameObject("TestProjectileMultiplier");
        cleanupList.Add(projGo);
        var proj = projGo.AddComponent<bulletKinetic>();

        proj.Mass = 1f;
        proj.Penetration = 1;
        proj.Damage = 2f; // sum = 4f

        proj.ImpulseMultiplier = 0.5f;
        Assert.AreEqual(2.0f, proj.CalculateBounceImpulse(), 0.001f);

        proj.ImpulseMultiplier = 3.0f;
        Assert.AreEqual(12.0f, proj.CalculateBounceImpulse(), 0.001f);
    }

    [Test]
    public void ApplyImpactImpulse_AppliesCalculatedImpulseToCompoundRigidbody()
    {
        // Root compound asteroid Rigidbody
        var parentGo = new GameObject("ParentAsteroidBody");
        cleanupList.Add(parentGo);
        var parentRb = parentGo.AddComponent<Rigidbody>();
        parentRb.mass = 4f;
        parentRb.useGravity = false;
        parentRb.linearDamping = 0f;
        parentRb.angularDamping = 0f;
        parentRb.linearVelocity = Vector3.zero;

        // Child block with collider
        var childGo = new GameObject("ChildBlock");
        childGo.transform.SetParent(parentGo.transform);
        var childCol = childGo.AddComponent<BoxCollider>();

        // Projectile positioned below target
        var projGo = new GameObject("TestBulletCompound");
        cleanupList.Add(projGo);
        projGo.transform.position = new Vector3(0f, -5f, 0f);
        projGo.transform.rotation = Quaternion.identity; // up is (0, 1, 0)
        var proj = projGo.AddComponent<bulletKinetic>();
        proj.Mass = 0.5f;
        proj.Penetration = 1;
        proj.Damage = 2.5f;
        proj.ImpulseMultiplier = 2f;

        float impulse = proj.CalculateBounceImpulse(); // (0.5 + 1 + 2.5) * 2 = 8f
        Assert.AreEqual(8f, impulse, 0.001f);

        var method = typeof(ProjectileBase).GetMethod("ApplyImpactImpulse", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(proj, new object[] { childCol, impulse });

        var prevMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            Physics.Simulate(0.02f);
            Assert.AreEqual(8f / 4f, parentRb.linearVelocity.y, 0.01f);
        }
        finally
        {
            Physics.simulationMode = prevMode;
        }
    }

    [Test]
    public void ApplyImpactImpulse_AppliesCalculatedImpulseToDirectRigidbody()
    {
        var targetGo = new GameObject("DirectTargetBody");
        cleanupList.Add(targetGo);
        var targetRb = targetGo.AddComponent<Rigidbody>();
        targetRb.mass = 5f;
        targetRb.useGravity = false;
        targetRb.linearDamping = 0f;
        targetRb.angularDamping = 0f;
        targetRb.linearVelocity = Vector3.zero;
        var col = targetGo.AddComponent<BoxCollider>();

        var projGo = new GameObject("TestBulletDirect");
        cleanupList.Add(projGo);
        projGo.transform.position = new Vector3(0f, -5f, 0f);
        projGo.transform.rotation = Quaternion.identity;
        var proj = projGo.AddComponent<bulletKinetic>();
        proj.Mass = 1f;
        proj.Penetration = 2;
        proj.Damage = 2f;
        proj.ImpulseMultiplier = 1.5f;

        float impulse = proj.CalculateBounceImpulse(); // (1 + 2 + 2) * 1.5 = 7.5f
        Assert.AreEqual(7.5f, impulse, 0.001f);

        var method = typeof(ProjectileBase).GetMethod("ApplyImpactImpulse", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(proj, new object[] { col, impulse });

        var prevMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            Physics.Simulate(0.02f);
            Assert.AreEqual(7.5f / 5f, targetRb.linearVelocity.y, 0.01f);
        }
        finally
        {
            Physics.simulationMode = prevMode;
        }
    }

    [Test]
    public void Player_TakeDamage_AppliesCalculatedImpulseToPlayerRigidbody()
    {
        var playerGo = new GameObject("TestPlayerImpulse");
        cleanupList.Add(playerGo);
        var p = playerGo.AddComponent<player>();
        var playerRb = playerGo.GetComponent<Rigidbody>();
        if (playerRb == null) playerRb = playerGo.AddComponent<Rigidbody>();
        playerRb.mass = 2f;
        playerRb.useGravity = false;
        playerRb.linearDamping = 0f;
        playerRb.angularDamping = 0f;
        playerRb.linearVelocity = Vector3.zero;

        var projGo = new GameObject("TestBulletPlayer");
        cleanupList.Add(projGo);
        projGo.transform.position = new Vector3(0f, -1f, 0f);
        projGo.transform.rotation = Quaternion.identity;
        var proj = projGo.AddComponent<bulletKinetic>();
        proj.Mass = 0.5f;
        proj.Penetration = 1;
        proj.Damage = 2f;
        proj.ImpulseMultiplier = 1f;

        float bounceImpulse = proj.CalculateBounceImpulse(); // (0.5 + 1 + 2) * 1 = 3.5f
        Assert.AreEqual(3.5f, bounceImpulse, 0.001f);

        Vector3 hitDir = (playerGo.transform.position - projGo.transform.position).normalized; // (0, 1, 0)
        p.TakeDamage(proj.Damage, hitDir, projGo.transform.position, bounceImpulse);

        var prevMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            Physics.Simulate(0.02f);
            Assert.AreEqual(3.5f / 2f, playerRb.linearVelocity.y, 0.01f);
        }
        finally
        {
            Physics.simulationMode = prevMode;
        }
    }

    [Test]
    public void BlockBase_CalculateImpactImpulse_UsesUnifiedBounceImpulse()
    {
        var blockGo = new GameObject("TestBlockImpulse");
        cleanupList.Add(blockGo);
        var block = blockGo.AddComponent<BlockAsteroid>();

        var projGo = new GameObject("TestBulletForBlock");
        cleanupList.Add(projGo);
        projGo.transform.rotation = Quaternion.identity; // up is (0, 1, 0)
        var proj = projGo.AddComponent<bulletKinetic>();
        proj.Mass = 0.5f;
        proj.Penetration = 2;
        proj.Damage = 3.5f;
        proj.ImpulseMultiplier = 2f;

        float expectedImpulse = proj.CalculateBounceImpulse(); // (0.5 + 2 + 3.5) * 2 = 12f
        Assert.AreEqual(12f, expectedImpulse, 0.001f);

        var calcMethod = typeof(BlockBase).GetMethod("CalculateImpactImpulse", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 calculated = (Vector3)calcMethod.Invoke(block, new object[] { projGo.transform, proj, Vector3.zero });

        Assert.AreEqual(0f, calculated.x, 0.001f);
        Assert.AreEqual(12f, calculated.y, 0.001f);
        Assert.AreEqual(0f, calculated.z, 0.001f);
    }
}

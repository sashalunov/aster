using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class TankAITests
{
    [Test]
    public void TankAI_AutoConfigureReferences_AddsColliderAndRigidbody()
    {
        GameObject tank = new GameObject("TestTank");
        TankAI ai = tank.AddComponent<TankAI>();
        ai.AutoConfigureReferences();

        Assert.IsNotNull(ai.col, "TankAI should configure a Collider");
        Assert.IsNotNull(ai.rb, "TankAI should configure a Rigidbody");
        Assert.IsFalse(ai.rb.useGravity, "Tank Rigidbody should not use gravity on 2D plane");
        Assert.IsTrue((ai.rb.constraints & RigidbodyConstraints.FreezePositionZ) != 0, "Tank Rigidbody should freeze position Z");

        TankAI.UnregisterTank(ai);
        Object.DestroyImmediate(tank);
    }

    [Test]
    public void TankAI_CalculateChassisRotation_PointsLocalYTowardsDirection()
    {
        Vector3[] testDirections = new Vector3[]
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 1f, 0f).normalized,
            new Vector3(-1f, 1f, 0f).normalized
        };

        foreach (var dir in testDirections)
        {
            Quaternion rot = TankAI.CalculateChassisRotation(dir);
            Vector3 forward = rot * Vector3.up; // Local Y is forward!
            float angleDiff = Vector3.Angle(dir, forward);

            Assert.Less(angleDiff, 0.01f, $"Local Y should align with target direction {dir}");
            Assert.AreEqual(0f, rot.eulerAngles.x, 0.01f, "X rotation should be 0");
            Assert.AreEqual(0f, rot.eulerAngles.y, 0.01f, "Y rotation should be 0");
        }
    }

    [Test]
    public void TankAI_MutualRegistry_RegistersOnEnableAndUnregistersOnDisable()
    {
        GameObject tankA = new GameObject("TankA");
        TankAI aiA = tankA.AddComponent<TankAI>();
        TankAI.RegisterTank(aiA);

        GameObject tankB = new GameObject("TankB");
        TankAI aiB = tankB.AddComponent<TankAI>();
        TankAI.RegisterTank(aiB);

        Assert.IsTrue(TankAI.AllTanks.Contains(aiA), "Tank A should be registered in AllTanks");
        Assert.IsTrue(TankAI.AllTanks.Contains(aiB), "Tank B should be registered in AllTanks");

        TankAI.UnregisterTank(aiA);
        Assert.IsFalse(TankAI.AllTanks.Contains(aiA), "Unregistered tank A should not be in AllTanks");

        TankAI.UnregisterTank(aiB);
        Object.DestroyImmediate(tankA);
        Object.DestroyImmediate(tankB);
    }

    [Test]
    public void TankAI_ComputeTankAvoidanceVector_ProducesRepulsionAwayFromOtherTank()
    {
        // Tank A at origin, identity rotation (forward is +Y)
        GameObject tankA = new GameObject("TankA");
        tankA.transform.position = Vector3.zero;
        tankA.transform.rotation = Quaternion.identity;
        TankAI aiA = tankA.AddComponent<TankAI>();
        aiA.avoidOtherTanks = true;
        aiA.tankAvoidanceRadius = 4f;
        aiA.emergencyStopDistance = 1.5f;

        // Tank B 2.5 units ahead of Tank A at (0, 2.5, 0)
        GameObject tankB = new GameObject("TankB");
        tankB.transform.position = new Vector3(0f, 2.5f, 0f);
        tankB.transform.rotation = Quaternion.Euler(0f, 0f, 180f); // facing down (-Y)
        TankAI aiB = tankB.AddComponent<TankAI>();

        TankAI.RegisterTank(aiA);
        TankAI.RegisterTank(aiB);

        Vector3 avoidance = aiA.ComputeTankAvoidanceVector(out bool emergencyStop, out bool shouldReverse);

        Assert.Greater(avoidance.sqrMagnitude, 0.01f, "Avoidance vector should be non-zero when another tank is ahead");
        Assert.IsFalse(emergencyStop, "At 2.5m, should not trigger emergency stop (< 1.5m)");

        // Avoidance should push away from Tank B (negative Y direction)
        Assert.Less(avoidance.y, 0f, "Avoidance force should push away from the tank ahead (negative Y)");

        TankAI.UnregisterTank(aiA);
        TankAI.UnregisterTank(aiB);
        Object.DestroyImmediate(tankA);
        Object.DestroyImmediate(tankB);
    }

    [Test]
    public void TankAI_EmergencyStop_TriggersWhenTanksAreCriticallyClose()
    {
        GameObject tankA = new GameObject("TankA");
        tankA.transform.position = Vector3.zero;
        tankA.transform.rotation = Quaternion.identity; // forward is +Y
        TankAI aiA = tankA.AddComponent<TankAI>();
        aiA.avoidOtherTanks = true;
        aiA.tankAvoidanceRadius = 4f;
        aiA.emergencyStopDistance = 1.8f;

        GameObject tankB = new GameObject("TankB");
        tankB.transform.position = new Vector3(0f, 1.2f, 0f); // within 1.8m
        TankAI aiB = tankB.AddComponent<TankAI>();

        TankAI.RegisterTank(aiA);
        TankAI.RegisterTank(aiB);

        aiA.ComputeTankAvoidanceVector(out bool emergencyStop, out bool shouldReverse);

        Assert.IsTrue(emergencyStop, "Emergency stop should trigger when distance < emergencyStopDistance");

        TankAI.UnregisterTank(aiA);
        TankAI.UnregisterTank(aiB);
        Object.DestroyImmediate(tankA);
        Object.DestroyImmediate(tankB);
    }

    [Test]
    public void TankAI_GetRandomDestination_StaysWithinRoamRadiusAndEnforcesMinDistance()
    {
        GameObject tank = new GameObject("TestTank");
        tank.transform.position = new Vector3(5f, 5f, 2f);
        TankAI ai = tank.AddComponent<TankAI>();
        ai.Initialize();
        ai.roamAroundSpawn = true;
        ai.roamRadius = 8f;

        for (int i = 0; i < 20; i++)
        {
            Vector3 dest = ai.GetRandomDestination();
            float distFromCurrent = Vector2.Distance(new Vector2(tank.transform.position.x, tank.transform.position.y),
                                                     new Vector2(dest.x, dest.y));
            float distFromSpawn = Vector2.Distance(new Vector2(ai.transform.position.x, ai.transform.position.y),
                                                   new Vector2(dest.x, dest.y));

            Assert.GreaterOrEqual(distFromCurrent, 2.0f, "Destination should enforce minimum travel distance from current position");
            Assert.LessOrEqual(distFromSpawn, ai.roamRadius + 0.1f, "Destination should stay within roam radius");
            Assert.AreEqual(2f, dest.z, 0.01f, "Destination should preserve the ground Z coordinate");
        }

        TankAI.UnregisterTank(ai);
        Object.DestroyImmediate(tank);
    }
}

using NUnit.Framework;
using UnityEngine;

public class WaveManagerTests
{
    private GameObject managerObj;
    private WaveManager waveManager;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("TestWaveManager");
        waveManager = managerObj.AddComponent<WaveManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (managerObj != null)
        {
            Object.DestroyImmediate(managerObj);
        }
    }

    [Test]
    public void WaveManager_InitialState_IsIdle()
    {
        Assert.AreEqual(WaveManager.WaveState.Idle, waveManager.State);
        Assert.AreEqual(0, waveManager.CurrentWaveIndex);
        Assert.IsFalse(waveManager.CanSpawn);
    }

    [Test]
    public void WaveManager_StartRun_SetsWave1AndCountdownState()
    {
        bool stateChangedFired = false;
        WaveManager.WaveState reportedOld = WaveManager.WaveState.Idle;
        WaveManager.WaveState reportedNew = WaveManager.WaveState.Idle;

        waveManager.OnStateChanged += (oldState, newState) =>
        {
            stateChangedFired = true;
            reportedOld = oldState;
            reportedNew = newState;
        };

        waveManager.StartRun();

        Assert.AreEqual(1, waveManager.CurrentWaveIndex);
        Assert.AreEqual(WaveManager.WaveState.Countdown, waveManager.State);
        Assert.IsTrue(stateChangedFired);
        Assert.AreEqual(WaveManager.WaveState.Idle, reportedOld);
        Assert.AreEqual(WaveManager.WaveState.Countdown, reportedNew);
    }

    [Test]
    public void WaveManager_CountdownExpiration_TransitionsToCombat()
    {
        waveManager.countdownDuration = 2.0f;
        waveManager.StartRun();

        Assert.AreEqual(WaveManager.WaveState.Countdown, waveManager.State);

        // Tick 1 second -> still in countdown
        waveManager.Tick(1.0f);
        Assert.AreEqual(WaveManager.WaveState.Countdown, waveManager.State);
        Assert.AreEqual(1.0f, waveManager.StateTimer, 0.001f);

        // Tick another 1 second -> enters Combat
        waveManager.Tick(1.0f);
        Assert.AreEqual(WaveManager.WaveState.Combat, waveManager.State);
        Assert.IsTrue(waveManager.CanSpawn);
        Assert.Greater(waveManager.RemainingThreatBudget, 0);
    }

    [Test]
    public void WaveManager_CombatDurationExpiry_CompletesWaveAndEntersIntermission()
    {
        waveManager.StartRun();
        waveManager.TriggerCombatImmediately();

        Assert.AreEqual(WaveManager.WaveState.Combat, waveManager.State);
        float duration = waveManager.CurrentWaveConfig.duration;
        Assert.Greater(duration, 0f);

        bool waveCompletedFired = false;
        int completedWaveNum = 0;
        waveManager.OnWaveCompleted += (waveNum, config) =>
        {
            waveCompletedFired = true;
            completedWaveNum = waveNum;
        };

        // Advance beyond wave duration
        waveManager.Tick(duration + 0.1f);

        Assert.IsTrue(waveCompletedFired);
        Assert.AreEqual(1, completedWaveNum);
        Assert.AreEqual(WaveManager.WaveState.Intermission, waveManager.State);
        Assert.IsFalse(waveManager.CanSpawn);
    }

    [Test]
    public void WaveManager_ThreatBudgetAndTracking_ClearsWaveWhenQuotaAndThreatsZero()
    {
        waveManager.StartRun();
        waveManager.TriggerCombatImmediately();

        GameObject threat1 = new GameObject("Threat1");
        GameObject threat2 = new GameObject("Threat2");

        waveManager.RegisterThreat(threat1);
        waveManager.RegisterThreat(threat2);
        Assert.AreEqual(2, waveManager.ActiveThreatCount);

        // Consume remaining threat budget
        while (waveManager.RemainingThreatBudget > 0)
        {
            waveManager.ConsumeThreatBudget(1);
        }
        Assert.AreEqual(0, waveManager.RemainingThreatBudget);

        // Unregister first threat -> still in combat because 1 threat remains
        waveManager.UnregisterThreat(threat1);
        Assert.AreEqual(1, waveManager.ActiveThreatCount);
        waveManager.Tick(0.1f);
        Assert.AreEqual(WaveManager.WaveState.Combat, waveManager.State);

        // Unregister second threat -> both budget and threats are zero, wave completes
        waveManager.UnregisterThreat(threat2);
        Assert.AreEqual(0, waveManager.ActiveThreatCount);
        waveManager.Tick(0.1f);

        Assert.AreEqual(WaveManager.WaveState.Intermission, waveManager.State);

        Object.DestroyImmediate(threat1);
        Object.DestroyImmediate(threat2);
    }

    [Test]
    public void WaveManager_ProceduralWave_ScalesBeyondAuthoredWaves()
    {
        int beyondWaveIndex = 99;
        WaveManager.WaveDefinition procWave = waveManager.GetWaveDefinition(beyondWaveIndex);

        Assert.IsNotNull(procWave);
        Assert.AreEqual(beyondWaveIndex, procWave.waveNumber);
        Assert.Greater(procWave.threatBudget, 100);
        Assert.Greater(procWave.rewardCredits, 100);
        Assert.Greater(procWave.rewardXP, 100);
    }
}

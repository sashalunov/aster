using System;
using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    public event Action<ulong, ulong> OnXPChanged;
    public event Action<int> OnPlayerLevelUp;
    public event Action<int> OnWaveCompleted;

    [Header("Current Progress")]
    [SerializeField] private ulong currentXP = 0;
    [SerializeField] private int playerLevel = 0;
    [SerializeField] private int waveLevel = 0;

    public ulong CurrentXP => currentXP;
    public int PlayerLevel => playerLevel;

    public int WaveLevel => waveLevel;

    [Header("Progression Thresholds")]
    public ulong[] levelUps = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000, 1000000, 2000000, 5000000 };
    public ulong[] weaponUnlocks = { 5, 50, 150, 500, 1000, 1500, 2000, 5000, 8000, 10000, 15000, 20000, 30000, 50000, 100000 };
    public ulong[] waveGoals = { 10, 100, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000, 1000000, 2000000, 5000000 };

    public int AddXP(int amount, Transform source = null)
    {
        if (amount <= 0) return playerLevel;

        currentXP += (ulong)amount;
        Vector3 sourcePos = source != null ? source.position : transform.position;

        CheckPlayerLevelUp();
        CheckWaveCompletion();

        ulong nextGoal = GetNextXPGoal();
        OnXPChanged?.Invoke(currentXP, nextGoal);

        return playerLevel;
    }

    public ulong GetNextXPGoal()
    {
        if (waveGoals != null && waveLevel < waveGoals.Length)
        {
            return waveGoals[waveLevel];
        }
        if (levelUps != null && playerLevel < levelUps.Length)
        {
            return levelUps[playerLevel];
        }
        return 0;
    }

    private void CheckPlayerLevelUp()
    {
        if (levelUps != null && playerLevel < levelUps.Length)
        {
            if (currentXP >= levelUps[playerLevel])
            {
                playerLevel++;
                OnPlayerLevelUp?.Invoke(playerLevel);
            }
        }
    }

    private void CheckWaveCompletion()
    {
        if (waveGoals != null && waveLevel < waveGoals.Length)
        {
            if (currentXP >= waveGoals[waveLevel])
            {
                waveLevel++;
                OnWaveCompleted?.Invoke(waveLevel);
            }
        }
    }

    public void ResetProgress()
    {
        currentXP = 0;
        playerLevel = 0;
        waveLevel = 0;
        OnXPChanged?.Invoke(currentXP, GetNextXPGoal());
    }
}

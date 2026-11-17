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
    public ulong[] levelUps = { 5, 50, 200, 400, 800, 1600, 3200, 6400, 12800, 25600 };
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

    /// <summary>
    /// Returns the target XP milestone for the next player level.
    /// </summary>
    public ulong GetNextPlayerLevelXP()
    {
        if (levelUps != null && playerLevel < levelUps.Length)
        {
            return levelUps[playerLevel];
        }
        return 0;
    }

    /// <summary>
    /// Returns the XP milestone of the previous player level (or 0 for level 0).
    /// </summary>
    public ulong GetPreviousPlayerLevelXP()
    {
        if (levelUps != null && playerLevel > 0 && playerLevel <= levelUps.Length)
        {
            return levelUps[playerLevel - 1];
        }
        return 0;
    }

    /// <summary>
    /// Returns a 0..1 normalized progress towards the next player level based on XP earned within the current level.
    /// </summary>
    public float GetPlayerLevelProgress()
    {
        if (levelUps == null || levelUps.Length == 0) return 0f;
        if (playerLevel >= levelUps.Length) return 1f;

        ulong prevXP = playerLevel > 0 ? levelUps[playerLevel - 1] : 0;
        ulong targetXP = levelUps[playerLevel];

        if (targetXP <= prevXP) return 1f;
        if (currentXP <= prevXP) return 0f;

        float progress = (float)(currentXP - prevXP) / (float)(targetXP - prevXP);
        return Mathf.Clamp01(progress);
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

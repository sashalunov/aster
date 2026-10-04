using UnityEngine;

/// <summary>
/// ScriptableObject data asset defining authored and procedural wave configurations.
/// Controls game pacing, threat budgets, asteroid cluster composition, and wave completion rewards.
/// </summary>
[CreateAssetMenu(fileName = "WaveDefinition", menuName = "Aster/Wave Definition", order = 1)]
public class WaveDefinition : ScriptableObject
{
    [Header("Identity")]
    public int waveNumber = 1;
    public string waveTitle = "Wave 1";

    [Header("Objectives & Scaling")]
    [Tooltip("Target XP required to clear this wave. If 0 or negative, resolves automatically from PlayerProgression.waveGoals.")]
    public ulong targetXPGoal = 0;

    [Tooltip("Max combat duration in seconds (optional fallback if useWaveTimer is enabled).")]
    public float duration = 30f;

    [Tooltip("Total threat points/budget to spawn during this wave.")]
    public int threatBudget = 10;

    [Header("Pacing")]
    [Tooltip("Target interval in seconds between spawning threats.")]
    public float spawnInterval = 2.0f;

    [Header("Asteroid Scaling")]
    public int minMass = 2;
    public int maxMass = 4;
    public int shellLevel = 1;
    public int blockLevel = 1;

    [Header("Rewards")]
    public int rewardCredits = 25;
    public int rewardXP = 50;
    public bool grantExtraGun = false;

    /// <summary>
    /// Creates an in-memory clone of this wave definition asset.
    /// </summary>
    public virtual WaveDefinition Clone()
    {
        WaveDefinition clone = ScriptableObject.CreateInstance<WaveDefinition>();
        clone.waveNumber = this.waveNumber;
        clone.waveTitle = this.waveTitle;
        clone.targetXPGoal = this.targetXPGoal;
        clone.duration = this.duration;
        clone.threatBudget = this.threatBudget;
        clone.spawnInterval = this.spawnInterval;
        clone.minMass = this.minMass;
        clone.maxMass = this.maxMass;
        clone.shellLevel = this.shellLevel;
        clone.blockLevel = this.blockLevel;
        clone.rewardCredits = this.rewardCredits;
        clone.rewardXP = this.rewardXP;
        clone.grantExtraGun = this.grantExtraGun;
        return clone;
    }

    /// <summary>
    /// Factory helper for creating WaveDefinition instances procedurally or in tests.
    /// </summary>
    public static WaveDefinition Create(
        int waveNumber = 1,
        string waveTitle = "Wave 1",
        float duration = 30f,
        int threatBudget = 10,
        float spawnInterval = 2.0f,
        int minMass = 2,
        int maxMass = 4,
        int rewardCredits = 25,
        int rewardXP = 50,
        bool grantExtraGun = false,
        ulong targetXPGoal = 0,
        int shellLevel = 1,
        int blockLevel = 1)
    {
        WaveDefinition def = ScriptableObject.CreateInstance<WaveDefinition>();
        def.waveNumber = waveNumber;
        def.waveTitle = waveTitle;
        def.duration = duration;
        def.threatBudget = threatBudget;
        def.spawnInterval = spawnInterval;
        def.minMass = minMass;
        def.maxMass = maxMass;
        def.rewardCredits = rewardCredits;
        def.rewardXP = rewardXP;
        def.grantExtraGun = grantExtraGun;
        def.targetXPGoal = targetXPGoal;
        def.shellLevel = shellLevel;
        def.blockLevel = blockLevel;
        return def;
    }
}

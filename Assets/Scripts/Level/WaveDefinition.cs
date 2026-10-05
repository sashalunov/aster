using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable entry for wave-specific weighted powerup drop tables.
/// </summary>
[System.Serializable]
public class WaveDropEntry
{
    [Tooltip("Prefab containing a PowerupBase component. If null, standard powerup prefab is instantiated with powerupType.")]
    public GameObject prefab;

    [Tooltip("Standard powerup type to assign if prefab is null or standard powerup.")]
    public StandardPowerup.StandardType powerupType = StandardPowerup.StandardType.UpgradePoint;

    [Tooltip("Relative drop weight (higher = more frequent).")]
    [Range(0.01f, 100f)]
    public float weight = 1f;

    [Tooltip("Optional label/description for this drop.")]
    public string label = "Drop";

    public WaveDropEntry() { }

    public WaveDropEntry(StandardPowerup.StandardType type, float dropWeight = 1f, GameObject customPrefab = null, string dropLabel = null)
    {
        powerupType = type;
        weight = dropWeight;
        prefab = customPrefab;
        label = dropLabel ?? type.ToString();
    }

    public WaveDropEntry Clone()
    {
        return new WaveDropEntry
        {
            prefab = this.prefab,
            powerupType = this.powerupType,
            weight = this.weight,
            label = this.label
        };
    }
}

/// <summary>
/// Serializable entry for guaranteed 100% powerup drops triggered when player reaches specific XP milestones.
/// </summary>
[System.Serializable]
public class WaveXPDropEntry
{
    [Tooltip("Player XP required to trigger this guaranteed drop.")]
    public ulong xpThreshold = 0;

    [Tooltip("If true, xpThreshold is evaluated relative to XP gained during this wave instead of total lifetime XP.")]
    public bool isWaveRelative = false;

    [Tooltip("Specific powerup prefab to spawn. If null, standard powerup prefab is instantiated with powerupType.")]
    public GameObject prefab;

    [Tooltip("Standard powerup type to spawn if prefab is null or standard powerup.")]
    public StandardPowerup.StandardType powerupType = StandardPowerup.StandardType.UpgradePoint;

    [Tooltip("Whether this milestone drops only once during this wave.")]
    public bool oncePerWave = true;

    [Tooltip("Runtime state: whether this threshold drop has already occurred in the current wave.")]
    [System.NonSerialized]
    public bool hasDropped = false;

    public WaveXPDropEntry() { }

    public WaveXPDropEntry(ulong xpThreshold, StandardPowerup.StandardType type, bool waveRelative = false, GameObject customPrefab = null, bool once = true)
    {
        this.xpThreshold = xpThreshold;
        this.powerupType = type;
        this.isWaveRelative = waveRelative;
        this.prefab = customPrefab;
        this.oncePerWave = once;
        this.hasDropped = false;
    }

    public WaveXPDropEntry Clone()
    {
        return new WaveXPDropEntry
        {
            xpThreshold = this.xpThreshold,
            isWaveRelative = this.isWaveRelative,
            prefab = this.prefab,
            powerupType = this.powerupType,
            oncePerWave = this.oncePerWave,
            hasDropped = false
        };
    }
}

/// <summary>
/// ScriptableObject data asset defining authored and procedural wave configurations.
/// Controls game pacing, threat budgets, asteroid cluster composition, wave completion rewards,
/// and customizable powerup drop tables including guaranteed 100% XP milestone drops.
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
   

    [Header("Powerup Drops - General")]
    [Tooltip("Probability [0, 1] of a regular (non-core) block dropping a powerup upon destruction.")]
    [Range(0f, 1f)]
    public float blockDropChance = 0.15f;

    public GameObject blockGuaranteedPrefab;

    public bool useGuaranteedType = false;

    public StandardPowerup.StandardType blockGuaranteedType = StandardPowerup.StandardType.ShieldUp;

    [Header("Powerup Drops - Weighted Table")]
    [Tooltip("Weighted drop table for this wave. If empty, falls back to PowerupManager default drop table.")]
    public List<WaveDropEntry> dropTable = new List<WaveDropEntry>();

    [Header("Powerup Drops - 100% XP Milestones")]
    [Tooltip("Guaranteed 100% powerup drops triggered when player reaches specific XP thresholds.")]
    public List<WaveXPDropEntry> xpThresholdDrops = new List<WaveXPDropEntry>();

    /// <summary>
    /// Resets runtime drop tracking flags (e.g. at the start of a wave).
    /// </summary>
    public void ResetRuntimeDrops()
    {
        if (xpThresholdDrops != null)
        {
            for (int i = 0; i < xpThresholdDrops.Count; i++)
            {
                if (xpThresholdDrops[i] != null)
                {
                    xpThresholdDrops[i].hasDropped = false;
                }
            }
        }
    }

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

        clone.blockDropChance = this.blockDropChance;
        clone.blockGuaranteedPrefab = this.blockGuaranteedPrefab;
        clone.useGuaranteedType = this.useGuaranteedType;
        clone.blockGuaranteedType = this.blockGuaranteedType;

        if (this.dropTable != null)
        {
            clone.dropTable = new List<WaveDropEntry>();
            for (int i = 0; i < this.dropTable.Count; i++)
            {
                clone.dropTable.Add(this.dropTable[i]?.Clone());
            }
        }

        if (this.xpThresholdDrops != null)
        {
            clone.xpThresholdDrops = new List<WaveXPDropEntry>();
            for (int i = 0; i < this.xpThresholdDrops.Count; i++)
            {
                clone.xpThresholdDrops.Add(this.xpThresholdDrops[i]?.Clone());
            }
        }

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
        int blockLevel = 1,
        float blockDropChance = 0.15f,
        GameObject blockGuaranteedPrefab = null,
        bool useGuaranteedType = false,
        StandardPowerup.StandardType blockGuaranteedType = StandardPowerup.StandardType.ShieldUp,
        List<WaveDropEntry> dropTable = null,
        List<WaveXPDropEntry> xpThresholdDrops = null)
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
        def.targetXPGoal = targetXPGoal;
        def.shellLevel = shellLevel;
        def.blockLevel = blockLevel;

        def.blockDropChance = blockDropChance;

        def.blockGuaranteedPrefab = blockGuaranteedPrefab;
        def.useGuaranteedType = useGuaranteedType;
        def.blockGuaranteedType = blockGuaranteedType;
        def.dropTable = dropTable != null ? new List<WaveDropEntry>(dropTable) : new List<WaveDropEntry>();
        def.xpThresholdDrops = xpThresholdDrops != null ? new List<WaveXPDropEntry>(xpThresholdDrops) : new List<WaveXPDropEntry>();

        return def;
    }
}

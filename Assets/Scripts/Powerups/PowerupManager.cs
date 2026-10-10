using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Central manager for powerup systems.
/// Handles active powerup tracking, weighted drop tables, safe stat clamping,
/// floating visual/audio feedback caching, and end-of-wave collection routines.
/// </summary>
public class PowerupManager : MonoBehaviour
{
    private static PowerupManager _instance;
    public static PowerupManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PowerupManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("PowerupManager");
                    _instance = go.AddComponent<PowerupManager>();
                }
            }
            return _instance;
        }
        set => _instance = value;
    }

    [System.Serializable]
    public class DropEntry
    {
        [Tooltip("Prefab containing a PowerupBase component.")]
        public GameObject prefab;

        [Tooltip("Relative drop weight (higher = more frequent).")]
        [Range(0.01f, 100f)]
        public float weight = 1f;

        [Tooltip("Optional identifier/label for this drop entry.")]
        public string dropId = "drop";
    }

    [Header("Drop Table Configuration")]
    [Tooltip("Configurable weighted drop table for spawning random powerups.")]
    public List<DropEntry> dropTable = new List<DropEntry>();

    [Header("Stat Caps & Balance")]
    public const float MAX_FIRE_HZ = 12f;
    public const float MAX_BULLET_FORCE = 20f;
    public const float MAX_BULLET_DMG = 25f;
    public const int MAX_GUNS = 8;
    public const float MAX_SHIELD_OVERCHARGE_RATIO = 1.5f;

    [Header("Cached Feedback Prefabs")]
    [Tooltip("Floating upgrade text prefab.")]
    [SerializeField] private GameObject showUpgradePrefab;

    [Tooltip("Floating shield text prefab.")]
    [SerializeField] private GameObject shieldUpgradePrefab;

    // Active powerups registry
    private readonly HashSet<PowerupBase> _activePowerups = new HashSet<PowerupBase>();
    public IReadOnlyCollection<PowerupBase> ActivePowerups => _activePowerups;
    public int ActiveCount => _activePowerups.Count;

    public bool IsRegistered(PowerupBase powerup) => powerup != null && _activePowerups.Contains(powerup);

    // Events
    public static event Action<PowerupBase, player> OnPowerupCollected;
    public static event Action<PowerupBase> OnPowerupSpawned;
    public static event Action<PowerupBase> OnPowerupDespawned;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _instance = this;
                PreloadPrefabs();
                return;
            }
#endif
            Destroy(gameObject);
            return;
        }
        _instance = this;

        PreloadPrefabs();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void PreloadPrefabs()
    {
        if (showUpgradePrefab == null)
        {
            showUpgradePrefab = PrefabManager.Get(PrefabId.ShowUpgradeFx);
        }
        if (shieldUpgradePrefab == null)
        {
            shieldUpgradePrefab = PrefabManager.Get(PrefabId.ShieldUpgradeFx);
        }
    }

    // =========================================================================
    // Registry Management
    // =========================================================================

    public void Register(PowerupBase powerup)
    {
        if (powerup != null && !_activePowerups.Contains(powerup))
        {
            _activePowerups.Add(powerup);
            OnPowerupSpawned?.Invoke(powerup);
        }
    }

    public void Unregister(PowerupBase powerup)
    {
        if (powerup != null && _activePowerups.Contains(powerup))
        {
            _activePowerups.Remove(powerup);
        }
    }

    public void NotifyPowerupCollected(PowerupBase powerup, player targetPlayer)
    {
        Unregister(powerup);
        OnPowerupCollected?.Invoke(powerup, targetPlayer);
    }

    public void NotifyPowerupDespawned(PowerupBase powerup)
    {
        Unregister(powerup);
        OnPowerupDespawned?.Invoke(powerup);
    }

    /// <summary>
    /// Collects all remaining active powerups for the player at once.
    /// Ideal for end-of-wave bonuses and eliminates scene-wide searches.
    /// </summary>
    public int CollectAllRemaining(player targetPlayer)
    {
        if (targetPlayer == null) return 0;

        List<PowerupBase> snapshot = new List<PowerupBase>(_activePowerups);
        int collectedCount = 0;

        foreach (var p in snapshot)
        {
            if (p != null && !p.IsCollected)
            {
                if (p.TryCollect(targetPlayer))
                {
                    collectedCount++;
                }
            }
        }

        _activePowerups.Clear();
        return collectedCount;
    }

    /// <summary>
    /// Clears and destroys all active powerups immediately (e.g., on game over or reset).
    /// </summary>
    public void ClearAllActive()
    {
        List<PowerupBase> snapshot = new List<PowerupBase>(_activePowerups);
        _activePowerups.Clear();

        foreach (var p in snapshot)
        {
            if (p != null && p.gameObject != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(p.gameObject);
                else
                    Destroy(p.gameObject);
#else
                Destroy(p.gameObject);
#endif
            }
        }
    }

    // =========================================================================
    // Safe Stat Application Helpers (Enforces Boundaries & Clamps)
    // =========================================================================

    public bool ApplyXP(player p, int amount = -1)
    {
        if (p == null) return false;
        if (amount <= 0)
        {
            int wave = WaveManager.Instance != null && WaveManager.Instance.CurrentWaveIndex > 0
                ? WaveManager.Instance.CurrentWaveIndex
                : 1;
            amount = 50 * wave;
        }
        p.AddXP(amount);
        return true;
    }

    public bool ApplyAmmoFlak(player p, int amount = -1)
    {
        if (p == null) return false;

        bool hasFlak = false;
        List<Gun> guns = p.GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            if (guns[i] != null && guns[i].Data != null && guns[i].Data.gunId == GunFlak.DEFAULT_GUN_ID)
            {
                hasFlak = true;
                if (amount < 0)
                    guns[i].RefillAmmo();
                else
                    guns[i].AddAmmo(amount);
            }
        }

        if (!hasFlak)
        {
            if (!ApplyGun(p, "gunFlak"))
            {
                p.RefillAllWeaponsAmmo(amount);
            }
        }

        return true;
    }

    public bool ApplyShield(player p, float amount = 1f, float maxOverchargeRatio = MAX_SHIELD_OVERCHARGE_RATIO)
    {
        if (p == null) return false;

        float ceiling = p.shield_max_value * maxOverchargeRatio;
        p.shield_value = Mathf.Clamp(p.shield_value + amount, 0f, ceiling);
        p.UpdateShieldHUD();
        return true;
    }

    public bool ApplyExtraGun(player p)
    {
        if (p == null) return false;
        return p.AddGun();
    }

    public bool ApplyGun(player p, string gunResourceName)
    {
        if (p == null) return false;
        GameObject prefabObj = PrefabManager.Get(gunResourceName);
        if (prefabObj == null) return false;
        Gun gunComp = prefabObj.GetComponent<Gun>();
        if (gunComp == null) return false;
        return p.AddGun(gunComp);
    }

    public bool ApplyGun(player p, Gun gunPrefab)
    {
        if (p == null) return false;
        return p.AddGun(gunPrefab);
    }

    public bool ApplyGunUpgrade(player p, string gunId, float dmgBonus = 1f, float rateBonus = 0f, float forceBonus = 0f, int burstBonus = 0)
    {
        if (p == null) return false;
        return p.UpgradeGuns(gunId, dmgBonus, rateBonus, forceBonus, burstBonus) > 0;
    }

    public bool ApplyAmmoKinetic(player p, int amount = -1)
    {
        if (p == null) return false;
        p.RefillAllWeaponsAmmo(amount);
        return true;
    }
    public bool ApplyAmmoKineticExplosive(player p, int amount = -1)
    {
        if (p == null) return false;

        int ammoToGive = amount > 0 ? amount : 20;
        bool hasKinetic = false;
        List<Gun> guns = p.GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            if (guns[i] != null && guns[i].Data != null && guns[i].Data.gunId == GunKinetic.DEFAULT_GUN_ID)
            {
                hasKinetic = true;
                guns[i].OverrideBulletPrefab = PrefabManager.Get(PrefabId.bulletKineticExplosive);
                guns[i].HasOverrideAmmo = true;
                guns[i].CurrentAmmo = ammoToGive;
            }
        }

        if (!hasKinetic)
        {
            // If the player doesn't have a Kinetic Cannon yet, grant them one!
            if (p.AddGun(PrefabManager.Get<Gun>(PrefabId.GunKinetic)))
            {
                guns = p.GetEquippedGuns();
                for (int i = 0; i < guns.Count; i++)
                {
                    if (guns[i] != null && guns[i].Data != null && guns[i].Data.gunId == GunKinetic.DEFAULT_GUN_ID)
                    {
                        guns[i].OverrideBulletPrefab = PrefabManager.Get(PrefabId.bulletKineticExplosive);
                        guns[i].HasOverrideAmmo = true;
                        guns[i].CurrentAmmo = ammoToGive;
                        break;
                    }
                }
            }
        }

        return true;
    }
    public bool ApplyUpgradePoint(player p, int amount = 1)
    {
        if (p == null) return false;
        p.AddUpgradePoints(amount);
        return true;
    }

    // =========================================================================
    // Spawning & Factories
    // =========================================================================

    /// <summary>
    /// Spawns a powerup from a prefab at the given position and registers it.
    /// </summary>
    public T SpawnPowerup<T>(T prefab, Vector3 position, Quaternion? rotation = null) where T : PowerupBase
    {
        if (prefab == null) return null;

        Quaternion rot = rotation ?? Quaternion.identity;
        T instance = Instantiate(prefab, position, rot);
        Register(instance);
        return instance;
    }

    /// <summary>
    /// Selects and spawns a random powerup from the configured drop table based on weights.
    /// </summary>
    public PowerupBase SpawnRandomPowerup(Vector3 position)
    {
        if (dropTable == null || dropTable.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < dropTable.Count; i++)
        {
            if (dropTable[i].prefab != null && dropTable[i].weight > 0f)
            {
                totalWeight += dropTable[i].weight;
            }
        }

        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < dropTable.Count; i++)
        {
            var entry = dropTable[i];
            if (entry.prefab == null || entry.weight <= 0f) continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                GameObject spawned = Instantiate(entry.prefab, position, Quaternion.identity);
                PowerupBase pu = spawned.GetComponent<PowerupBase>();
                if (pu != null)
                {
                    Register(pu);
                }
                return pu;
            }
        }

        return null;
    }

    // =========================================================================
    // Wave & Block Drop Resolution
    // =========================================================================

    /// <summary>
    /// Resolves and spawns powerup drops on block or core destruction based on the active wave configuration,
    /// 100% XP milestone thresholds, and fallback drop tables.
    /// </summary>
    public PowerupBase HandleBlockDestructionDrop(Vector3 position, bool isCore, player targetPlayer = null, bool forceDrop = false)
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindAnyObjectByType<player>();
        }

        ulong currentXP = 0;
        if (targetPlayer != null && targetPlayer.Progression != null)
        {
            currentXP = targetPlayer.Progression.CurrentXP;
        }

        WaveManager wm = WaveManager.Instance;
        WaveDefinition wave = wm != null ? wm.CurrentWaveConfig : null;
        ulong waveEarnedXP = wm != null ? wm.WaveEarnedXP : 0;

        // 1. Check for guaranteed 100% XP milestone drops in the active wave
        if (wave != null && wave.xpThresholdDrops != null && wave.xpThresholdDrops.Count > 0)
        {
            List<PowerupBase> milestoneDrops = null;
            bool anyMilestoneTriggered = false;

            for (int i = 0; i < wave.xpThresholdDrops.Count; i++)
            {
                var xpDrop = wave.xpThresholdDrops[i];
                if (xpDrop == null) continue;
                if (xpDrop.oncePerWave && xpDrop.hasDropped) continue;

                ulong xpToCheck = xpDrop.isWaveRelative ? waveEarnedXP : currentXP;
                if (xpToCheck >= xpDrop.xpThreshold)
                {
                    xpDrop.hasDropped = true;
                    anyMilestoneTriggered = true;
                    if (milestoneDrops == null) milestoneDrops = new List<PowerupBase>();

                    Vector3 spawnPos = position;
                    if (milestoneDrops.Count > 0)
                    {
                        spawnPos += new Vector3(milestoneDrops.Count * 0.75f, 0f, 0f);
                    }

                    PowerupBase spawned = SpawnDropEntry(xpDrop.prefab, xpDrop.powerupType, spawnPos);
                    if (spawned != null)
                    {
                        milestoneDrops.Add(spawned);
                    }
                }
            }

            if (milestoneDrops != null && milestoneDrops.Count > 0)
            {
                return milestoneDrops[0];
            }
            if (anyMilestoneTriggered)
            {
                return null;
            }
        }

        // 2. Core vs Regular Block drop handling
        float dropChance = isCore
            ? (wave != null ? wave.coreDropChance : 0.65f)
            : (wave != null ? wave.blockDropChance : 0.15f);

        if (forceDrop || UnityEngine.Random.value <= dropChance)
        {
            if (wave != null && wave.dropTable != null && wave.dropTable.Count > 0)
            {
                return SpawnFromWaveDropTable(wave.dropTable, position);
            }
            if (dropTable != null && dropTable.Count > 0)
            {
                return SpawnRandomPowerup(position);
            }
            return isCore ? SpawnDefaultCoreDrop(position) : SpawnDefaultBlockDrop(position);
        }

        return null;
    }

    /// <summary>
    /// Spawns a powerup from a specified prefab or assigns powerupType onto a newly instantiated standard powerup.
    /// Supports both custom prefabs and standard type prefabs from PrefabManager.
    /// </summary>
    public PowerupBase SpawnDropEntry(GameObject prefab, StandardPowerup.StandardType powerupType, Vector3 position)
    {
        int currentWave = WaveManager.Instance != null && WaveManager.Instance.CurrentWaveIndex > 0
            ? WaveManager.Instance.CurrentWaveIndex
            : 1;

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            StandardPowerup customSp = instance.GetComponentInChildren<StandardPowerup>();
            if (customSp != null)
            {
                customSp.WaveNumber = currentWave;
                if (prefab == PrefabManager.Get(PrefabId.PowerupDefault))
                {
                    customSp.Type = powerupType;
                }
            }

            PowerupBase pu = instance.GetComponentInChildren<PowerupBase>();
            if (pu != null)
            {
                Register(pu);
            }
            return pu;
        }

        GameObject template = GetPrefabForStandardType(powerupType);
        if (template == null)
        {
            template = PrefabManager.Get(PrefabId.PowerupDefault);
        }

        GameObject spawnedObj = null;
        if (template != null)
        {
            spawnedObj = Instantiate(template, position, Quaternion.identity);
        }
        else
        {
            spawnedObj = new GameObject("Powerup_" + powerupType);
            spawnedObj.transform.position = position;
            spawnedObj.AddComponent<StandardPowerup>();
        }

        StandardPowerup sp = spawnedObj.GetComponentInChildren<StandardPowerup>();
        if (sp != null)
        {
            sp.WaveNumber = currentWave;
            sp.Type = powerupType;
        }

        PowerupBase pb = spawnedObj.GetComponentInChildren<PowerupBase>();
        if (pb != null)
        {
            Register(pb);
        }
        return pb;
    }

    /// <summary>
    /// Resolves the dedicated prefab for a standard powerup type from PrefabManager if available.
    /// </summary>
    public static GameObject GetPrefabForStandardType(StandardPowerup.StandardType powerupType)
    {
        switch (powerupType)
        {
            case StandardPowerup.StandardType.GunKinetic:
                return PrefabManager.Get(PrefabId.PowerupGunKinetic);
            case StandardPowerup.StandardType.GunPlasma:
                return PrefabManager.Get(PrefabId.PowerupGunPlasma);
            case StandardPowerup.StandardType.ShieldUp:
                return PrefabManager.Get(PrefabId.PowerupShield) ?? PrefabManager.Get("pwpShieldUp");
            case StandardPowerup.StandardType.AmmoKinetic:
                return PrefabManager.Get(PrefabId.PowerupAmmoKinetic);
            case StandardPowerup.StandardType.AmmoKineticExplosive:
                return PrefabManager.Get(PrefabId.PowerupAmmoExplosive);
            case StandardPowerup.StandardType.AmmoFlak:
                return PrefabManager.Get(PrefabId.PowerupAmmoFlak);
            case StandardPowerup.StandardType.UpgradePoint:
                return PrefabManager.Get(PrefabId.PowerupUpgradePoint);
            case StandardPowerup.StandardType.XpUp:
                return PrefabManager.Get(PrefabId.PowerupXP);
            default:
                return PrefabManager.Get(PrefabId.PowerupDefault);
        }
    }

    /// <summary>
    /// Spawns a powerup from a prefab GameObject and registers it.
    /// </summary>
    public PowerupBase SpawnPowerupFromPrefab(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        StandardPowerup sp = instance.GetComponentInChildren<StandardPowerup>();
        if (sp != null && WaveManager.Instance != null && WaveManager.Instance.CurrentWaveIndex > 0)
        {
            sp.WaveNumber = WaveManager.Instance.CurrentWaveIndex;
        }

        PowerupBase pu = instance.GetComponentInChildren<PowerupBase>();
        if (pu != null)
        {
            Register(pu);
        }
        return pu;
    }

    /// <summary>
    /// Rolls and spawns a powerup from a wave's weighted drop table.
    /// </summary>
    public PowerupBase SpawnFromWaveDropTable(List<WaveDropEntry> entries, Vector3 position)
    {
        if (entries == null || entries.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].weight > 0f)
            {
                totalWeight += entries[i].weight;
            }
        }

        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.weight <= 0f) continue;

            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                return SpawnDropEntry(entry.prefab, entry.powerupType, position);
            }
        }

        return null;
    }

    /// <summary>
    /// Default core drop fallback: spawns powerup_shield or ShieldUp.
    /// </summary>
    public PowerupBase SpawnDefaultCoreDrop(Vector3 position)
    {
        GameObject shieldPrefab = PrefabManager.Get(PrefabId.PowerupShield);
        if (shieldPrefab != null)
        {
            return SpawnPowerupFromPrefab(shieldPrefab, position);
        }
        return SpawnDropEntry(null, StandardPowerup.StandardType.ShieldUp, position);
    }

    /// <summary>
    /// Default standard block drop fallback.
    /// </summary>
    public PowerupBase SpawnDefaultBlockDrop(Vector3 position)
    {
        StandardPowerup.StandardType[] pool = new StandardPowerup.StandardType[]
        {
            StandardPowerup.StandardType.UpgradePoint,

            StandardPowerup.StandardType.XpUp,

            StandardPowerup.StandardType.ShieldUp
        };
        StandardPowerup.StandardType selected = pool[UnityEngine.Random.Range(0, pool.Length)];
        return SpawnDropEntry(null, selected, position);
    }

    // =========================================================================
    // Floating Feedback FX
    // =========================================================================

    /// <summary>
    /// Spawns floating 3D text notification above the pickup location.
    /// </summary>
    public void SpawnFloatingFeedback(string text, Vector3 position, Color? tint = null)
    {
        PreloadPrefabs();

        GameObject template = showUpgradePrefab;
        if (template == null) return;

        GameObject fx = Instantiate(template, position, Quaternion.identity);
        if (fx != null)
        {
            TextMeshPro tmp = fx.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.SetText(text);
                if (tint.HasValue)
                {
                    tmp.color = tint.Value;
                }

                ParticleSystem ps = fx.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = tint ?? Color.white;
                }
            }
            //fx.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);
        }
    }
}

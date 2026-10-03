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
            showUpgradePrefab = Resources.Load<GameObject>("show_upgrade");
        }
        if (shieldUpgradePrefab == null)
        {
            shieldUpgradePrefab = Resources.Load<GameObject>("shield_upgrade");
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

    public bool ApplySpeedUp(player p, float hzIncrease = 1f)
    {
        if (p == null) return false;

        p._fire_hz = Mathf.Clamp(p._fire_hz + hzIncrease, 1f, MAX_FIRE_HZ);
        p._fire_rate = 1f / p._fire_hz;
        p.UpdateWeaponHUD();
        return true;
    }

    public bool ApplyPowerUp(player p, float forceIncrease = 1f)
    {
        if (p == null) return false;

        p._bullet_force = Mathf.Clamp(p._bullet_force + forceIncrease, 1f, MAX_BULLET_FORCE);
        p.UpdateWeaponHUD();
        return true;
    }

    public bool ApplyDamageUp(player p, float damageIncrease = 1f)
    {
        if (p == null) return false;

        p._bullet_dmg = Mathf.Clamp(p._bullet_dmg + damageIncrease, 1f, MAX_BULLET_DMG);
        p.UpdateWeaponHUD();
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
        GameObject prefabObj = Resources.Load<GameObject>(gunResourceName);
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

    public bool ApplyAmmoRefill(player p, int amount = -1)
    {
        if (p == null) return false;
        p.RefillAllWeaponsAmmo(amount);
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
            }
            fx.transform.localScale = new Vector3(1.6f, 1.6f, 1.2f);
        }
    }
}

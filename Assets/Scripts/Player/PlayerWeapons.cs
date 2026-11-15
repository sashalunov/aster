using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-specific extension of WeaponSystem.
/// Handles player combat stat progression (damage, force, fire rate),
/// spending upgrade points, stat synchronization with equipped guns,
/// and weapon HUD / floating combat text event dispatching.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeapons : WeaponSystem
{
    [Header("Player Base Combat Stats")]
    [Tooltip("Base bullet damage multiplier.")]
    [SerializeField] private float _bullet_dmg = 1.0f;

    [Tooltip("Base bullet impact/impulse force multiplier.")]
    [SerializeField] private float _bullet_force = 1.0f;

    [Tooltip("Fire rate in Hertz (shots per second).")]
    [SerializeField] private float _fire_hz = 1.0f;

    [Tooltip("Fire interval in seconds (1 / fire_hz).")]
    [SerializeField] private float _fire_rate = 1.0f;

    // Cached player reference
    private player _player;
    public player Player => _player;

    // Public properties for stats
    public float BulletDamage
    {
        get => _bullet_dmg;
        set => _bullet_dmg = value;
    }

    public float BulletForce
    {
        get => _bullet_force;
        set => _bullet_force = value;
    }

    public float FireHz
    {
        get => _fire_hz;
        set
        {
            _fire_hz = Mathf.Max(0.01f, value);
            _fire_rate = 1f / _fire_hz;
        }
    }

    public float FireRate
    {
        get => _fire_rate;
        set
        {
            _fire_rate = Mathf.Max(0.01f, value);
            _fire_hz = 1f / _fire_rate;
        }
    }

    // Direct compatibility properties matching legacy player field names
    public float bullet_dmg
    {
        get => _bullet_dmg;
        set => _bullet_dmg = value;
    }

    public float bullet_force
    {
        get => _bullet_force;
        set => _bullet_force = value;
    }

    public float fire_hz
    {
        get => _fire_hz;
        set => FireHz = value;
    }

    public float fire_rate
    {
        get => _fire_rate;
        set => FireRate = value;
    }

    // Events
    public event Action<float, float, float> OnWeaponStatsChanged;

    protected override void Awake()
    {
        if (_player == null)
        {
            _player = GetComponent<player>();
        }

        // Prefab reference migration fallback if PlayerWeapons is uninitialized on legacy prefabs
        if (_player != null)
        {
            if ((_sockets == null || _sockets.Count == 0) && _player._sockets != null && _player._sockets.Count > 0)
            {
                _sockets = new List<GunSocket>(_player._sockets);
            }
            if (_gunsContainer == null && _player._guns_container != null)
            {
                _gunsContainer = _player._guns_container;
            }
            if ((_guns == null || _guns.Count == 0) && _player._guns != null && _player._guns.Count > 0)
            {
                _guns = new List<Gun>(_player._guns);
            }
        }

        base.Awake();
    }

    protected virtual void Start()
    {
        UpdateWeaponHUD();
    }

    #region Gun Lifecycle & Event Overrides

    /// <summary>
    /// Invoked whenever a gun is mounted to a socket or attached directly.
    /// Synchronizes the gun's stats with player upgrades and notifies the HUD.
    /// </summary>
    protected override void OnGunAttached(GunSocket socket, Gun gun)
    {
        base.OnGunAttached(socket, gun);
        SyncGunWithPlayerUpgrades(gun);
        UpdateWeaponHUD();
    }

    /// <summary>
    /// Detaches and returns the mounted gun from a socket, refreshing HUD afterwards.
    /// </summary>
    public override Gun DetachGun(int socketIndex = 0)
    {
        Gun detached = base.DetachGun(socketIndex);
        if (detached != null)
        {
            UpdateWeaponHUD();
        }
        return detached;
    }

    /// <summary>
    /// Upgrades all guns matching gunId and refreshes weapon HUD.
    /// </summary>
    public override int UpgradeGuns(string gunId, float dmgBonus = 0f, float rateBonus = 0f, float forceBonus = 0f, int burstBonus = 0)
    {
        int count = base.UpgradeGuns(gunId, dmgBonus, rateBonus, forceBonus, burstBonus);
        UpdateWeaponHUD();
        return count;
    }

    #endregion

    #region HUD & Feedback Integration

    /// <summary>
    /// Updates weapon HUD displays and broadcasts current effective weapon stats (force, interval, damage).
    /// </summary>
    public void UpdateWeaponHUD()
    {
        float force = _bullet_force;
        float rateHz = _fire_hz;
        float dmg = _bullet_dmg;

        List<Gun> guns = GetEquippedGuns(false);
        if (guns.Count > 0 && guns[0] != null)
        {
            force = guns[0].EffectiveForce;
            rateHz = guns[0].EffectiveFireRate;
            dmg = guns[0].EffectiveDamage;
        }

        float fireRateInterval = rateHz > 0f ? (1f / rateHz) : 0f;
        OnWeaponStatsChanged?.Invoke(force, fireRateInterval, dmg);
    }

    #endregion

    #region Gun Stat Synchronization

    /// <summary>
    /// Synchronizes a gun instance with the player's accumulated upgrade bonuses.
    /// </summary>
    public void SyncGunWithPlayerUpgrades(Gun g)
    {
        if (g == null) return;
        float dmgDelta = Mathf.Max(0f, _bullet_dmg - 1.0f);
        float forceDelta = Mathf.Max(0f, _bullet_force - 1.0f);
        float rateDelta = Mathf.Max(0f, _fire_hz - 1.0f);
        if (dmgDelta > 0f || forceDelta > 0f || rateDelta > 0f)
        {
            g.UpgradeStats(dmgDelta, rateDelta, forceDelta);
        }
    }

    /// <summary>
    /// Synchronizes all equipped guns with the player's accumulated upgrade bonuses.
    /// </summary>
    public void SyncAllEquippedGunsWithPlayerUpgrades()
    {
        List<Gun> guns = GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            SyncGunWithPlayerUpgrades(guns[i]);
        }
    }

    #endregion

    #region Upgrade Points Allocation

    /// <summary>
    /// Spends collected upgrade points on a weapon stat (Damage, Force, or FireRate).
    /// Enhances both base ship weapon attributes and currently equipped Gun instances.
    /// </summary>
    public bool SpendUpgradePoints(WeaponStatType stat, int pointsCost = 1, string gunId = null)
    {
        if (pointsCost <= 0) return false;

        if (_player != null)
        {
            if (!_player.HasUpgradePoints(pointsCost)) return false;
            _player.UpgradePoints -= pointsCost;
        }

        switch (stat)
        {
            case WeaponStatType.Damage:
                _bullet_dmg = Mathf.Clamp(_bullet_dmg + 1f * pointsCost, 1f, PowerupManager.MAX_BULLET_DMG);
                UpgradeGuns(gunId, dmgBonus: 1f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+DMG UPGRADE!", transform.position, new Color(1f, 0.3f, 0.3f));
                }
                break;

            case WeaponStatType.Force:
                _bullet_force = Mathf.Clamp(_bullet_force + 1f * pointsCost, 1f, PowerupManager.MAX_BULLET_FORCE);
                UpgradeGuns(gunId, forceBonus: 2f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+FORCE UPGRADE!", transform.position, new Color(0.9f, 0.3f, 1f));
                }
                break;

            case WeaponStatType.FireRate:
                _fire_hz = Mathf.Clamp(_fire_hz + 0.5f * pointsCost, 1f, PowerupManager.MAX_FIRE_HZ);
                _fire_rate = 1f / _fire_hz;
                UpgradeGuns(gunId, rateBonus: 0.5f * pointsCost);
                if (PowerupManager.Instance != null)
                {
                    PowerupManager.Instance.SpawnFloatingFeedback("+RATE UPGRADE!", transform.position, new Color(1f, 0.9f, 0.2f));
                }
                break;
        }

        UpdateWeaponHUD();
        return true;
    }

    /// <summary>
    /// Spends upgrade points to boost weapon damage.
    /// </summary>
    public bool UpgradeDamageWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.Damage, pointsCost, gunId);

    /// <summary>
    /// Spends upgrade points to boost projectile impulse force.
    /// </summary>
    public bool UpgradeForceWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.Force, pointsCost, gunId);

    /// <summary>
    /// Spends upgrade points to boost weapon fire rate.
    /// </summary>
    public bool UpgradeFireRateWithPoints(int pointsCost = 1, string gunId = null) =>
        SpendUpgradePoints(WeaponStatType.FireRate, pointsCost, gunId);

    #endregion
}

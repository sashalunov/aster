using System;
using UnityEngine;

public enum MetaUpgradeType
{
    HullArmor,
    ShieldCapacitor,
    IonThrusters,
    PlasmaCannons
}

/// <summary>
/// Manages persistent roguelite meta-progression across runs.
/// Handles saved upgrade levels, bankable scrap currency, and stat applications.
/// </summary>
public static class PlayerMetaProgression
{
    public const string PREF_SCRAP = "Aster_Meta_Scrap";
    public const string PREF_HULL = "Aster_Meta_Hull";
    public const string PREF_SHIELD = "Aster_Meta_Shield";
    public const string PREF_THRUST = "Aster_Meta_Thrust";
    public const string PREF_WEAPON = "Aster_Meta_Weapon";

    public const int MAX_UPGRADE_LEVEL = 10;

    public static event Action OnProgressionChanged;

    public static int BankedScrap
    {
        get => PlayerPrefs.GetInt(PREF_SCRAP, 0);
        set
        {
            PlayerPrefs.SetInt(PREF_SCRAP, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnProgressionChanged?.Invoke();
        }
    }

    public static int GetUpgradeLevel(MetaUpgradeType type)
    {
        switch (type)
        {
            case MetaUpgradeType.HullArmor:
                return Mathf.Clamp(PlayerPrefs.GetInt(PREF_HULL, 0), 0, MAX_UPGRADE_LEVEL);
            case MetaUpgradeType.ShieldCapacitor:
                return Mathf.Clamp(PlayerPrefs.GetInt(PREF_SHIELD, 0), 0, MAX_UPGRADE_LEVEL);
            case MetaUpgradeType.IonThrusters:
                return Mathf.Clamp(PlayerPrefs.GetInt(PREF_THRUST, 0), 0, MAX_UPGRADE_LEVEL);
            case MetaUpgradeType.PlasmaCannons:
                return Mathf.Clamp(PlayerPrefs.GetInt(PREF_WEAPON, 0), 0, MAX_UPGRADE_LEVEL);
            default:
                return 0;
        }
    }

    public static void SetUpgradeLevel(MetaUpgradeType type, int level)
    {
        int clamped = Mathf.Clamp(level, 0, MAX_UPGRADE_LEVEL);
        switch (type)
        {
            case MetaUpgradeType.HullArmor:
                PlayerPrefs.SetInt(PREF_HULL, clamped);
                break;
            case MetaUpgradeType.ShieldCapacitor:
                PlayerPrefs.SetInt(PREF_SHIELD, clamped);
                break;
            case MetaUpgradeType.IonThrusters:
                PlayerPrefs.SetInt(PREF_THRUST, clamped);
                break;
            case MetaUpgradeType.PlasmaCannons:
                PlayerPrefs.SetInt(PREF_WEAPON, clamped);
                break;
        }
        PlayerPrefs.Save();
        OnProgressionChanged?.Invoke();
    }

    public static int GetUpgradeCost(MetaUpgradeType type)
    {
        int currentLevel = GetUpgradeLevel(type);
        if (currentLevel >= MAX_UPGRADE_LEVEL) return -1; // Max level reached

        switch (type)
        {
            case MetaUpgradeType.HullArmor:
                return 50 * (currentLevel + 1);
            case MetaUpgradeType.ShieldCapacitor:
                return 50 * (currentLevel + 1);
            case MetaUpgradeType.IonThrusters:
                return 40 * (currentLevel + 1);
            case MetaUpgradeType.PlasmaCannons:
                return 60 * (currentLevel + 1);
            default:
                return 50;
        }
    }

    public static bool CanAffordUpgrade(MetaUpgradeType type)
    {
        int cost = GetUpgradeCost(type);
        if (cost <= 0) return false;
        return BankedScrap >= cost;
    }

    public static bool TryPurchaseUpgrade(MetaUpgradeType type)
    {
        int cost = GetUpgradeCost(type);
        if (cost <= 0 || BankedScrap < cost) return false;

        BankedScrap -= cost;
        SetUpgradeLevel(type, GetUpgradeLevel(type) + 1);
        return true;
    }

    public static void AddScrap(int amount)
    {
        if (amount > 0)
        {
            BankedScrap += amount;
        }
    }

    /// <summary>
    /// Checks whether any meta progression data (scrap, upgrades) is stored in PlayerPrefs.
    /// </summary>
    public static bool HasAnyProgress()
    {
        return PlayerPrefs.HasKey(PREF_SCRAP)
            || PlayerPrefs.HasKey(PREF_HULL)
            || PlayerPrefs.HasKey(PREF_SHIELD)
            || PlayerPrefs.HasKey(PREF_THRUST)
            || PlayerPrefs.HasKey(PREF_WEAPON);
    }

    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteKey(PREF_SCRAP);
        PlayerPrefs.DeleteKey(PREF_HULL);
        PlayerPrefs.DeleteKey(PREF_SHIELD);
        PlayerPrefs.DeleteKey(PREF_THRUST);
        PlayerPrefs.DeleteKey(PREF_WEAPON);
        PlayerPrefs.Save();
        OnProgressionChanged?.Invoke();
    }

    public static void ApplyTo(player p)
    {
        if (p == null) return;

        int hullLvl = GetUpgradeLevel(MetaUpgradeType.HullArmor);
        int shieldLvl = GetUpgradeLevel(MetaUpgradeType.ShieldCapacitor);
        int thrustLvl = GetUpgradeLevel(MetaUpgradeType.IonThrusters);
        int weaponLvl = GetUpgradeLevel(MetaUpgradeType.PlasmaCannons);

        p.health_max_value = 10f + (hullLvl * 2f);
        p.health_value = p.health_max_value;

        p.shield_max_value = 10f + (shieldLvl * 2f);
        p.shield_value = p.shield_max_value;

        p._thrust_force = 5.3f + (thrustLvl * 0.8f);

        p._bullet_dmg = 1.0f + (weaponLvl * 0.5f);
        p._bullet_force = 1.0f + (weaponLvl * 0.5f);
        p.SyncAllEquippedGunsWithPlayerUpgrades();

        p.UpdateHealthHUD();
        p.UpdateShieldHUD();
        p.UpdateWeaponHUD();
    }
}

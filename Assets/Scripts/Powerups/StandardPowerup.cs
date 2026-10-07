using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standard concrete powerup implementing foundational ship upgrade types.
/// Serves as a reference implementation for PowerupBase and provides ready-to-use powerups.
/// </summary>
public class StandardPowerup : PowerupBase
{
    public enum StandardType
    {
        XpUp,
        ShieldUp,
        ExtraSocket,
        ScrapSalvage,
        GunKinetic,
        GunPlasma,
        GunFlak,
        AmmoRefill,
        AmmoFlak,
        UpgradePoint
    }

    [Header("Standard Powerup Configuration")]
    [SerializeField] private StandardType type = StandardType.UpgradePoint;
    [SerializeField] private float potency = 1f;

    public StandardType Type
    {
        get => type;
        set
        {
            type = value;
            ApplyConfigurationForType();
            UpdateVisuals();
        }
    }

    public float Potency
    {
        get => potency;
        set => potency = value;
    }

    protected override void Awake()
    {
        base.Awake();
        ApplyConfigurationForType();
    }

    public void ApplyConfigurationForType()
    {
        switch (type)
        {
            case StandardType.XpUp:
                powerupId = "xp_up";
                displayName = "+XP!";
                themeColor = new Color(0.2f, 1f, 0.4f); // Emerald / bright green
                break;
            case StandardType.ShieldUp:
                powerupId = "shield_up";
                displayName = "SHIELD UP!";
                themeColor = new Color(0.2f, 0.8f, 1f); // Cyan
                break;
            case StandardType.ExtraSocket:
                powerupId = "extra_socket";
                displayName = "EXTRA SOCKET!";
                themeColor = new Color(0.3f, 1f, 0.4f); // Green
                break;
            case StandardType.ScrapSalvage:
                powerupId = "scrap_salvage";
                displayName = "SCRAP +25";
                themeColor = new Color(1f, 0.6f, 0.1f); // Orange
                break;
            case StandardType.GunKinetic:
                powerupId = "gun_kinetic";
                displayName = "KINETIC CANNON!";
                themeColor = new Color(1f, 0.55f, 0.1f); // Amber / Orange
                break;
            case StandardType.GunPlasma:
                powerupId = "gun_plasma";
                displayName = "PLASMA REPEATER!";
                themeColor = new Color(0.2f, 0.8f, 1f); // Electric Blue / Cyan
                break;
            case StandardType.GunFlak:
                powerupId = "gun_flak";
                displayName = "FLAK CANNON!";
                themeColor = new Color(0.2f, 0.8f, 1f); // Electric Blue / Cyan
                break;
            case StandardType.AmmoRefill:
                powerupId = "ammo_refill";
                displayName = "AMMO REFILL!";
                themeColor = new Color(0.3f, 1f, 0.5f); // Bright Green
                break;
            case StandardType.AmmoFlak:
                powerupId = "ammo_flak";
                displayName = "FLAK AMMO!";
                themeColor = new Color(1f, 0.55f, 0.15f); // Orange / Amber
                break;
            case StandardType.UpgradePoint:
                powerupId = "upgrade_point";
                displayName = "UPGRADE POINT!";
                themeColor = new Color(1f, 0.85f, 0.1f); // Gold / Yellow
                break;
        }
    }

    public override bool ApplyEffect(player targetPlayer)
    {
        if (targetPlayer == null || targetPlayer.isDead) return false;

        if (PowerupManager.Instance != null)
        {
            switch (type)
            {
                case StandardType.XpUp:
                    return PowerupManager.Instance.ApplyXP(targetPlayer, Mathf.Max(1, Mathf.RoundToInt(potency )));
                case StandardType.ShieldUp:
                    return PowerupManager.Instance.ApplyShield(targetPlayer, potency);
                case StandardType.ExtraSocket:
                    return PowerupManager.Instance.ApplyExtraGun(targetPlayer);
                case StandardType.ScrapSalvage:
                    PlayerMetaProgression.AddScrap(Mathf.RoundToInt(potency * 25f));
                    return true;
                case StandardType.GunKinetic:
                    return PowerupManager.Instance.ApplyGun(targetPlayer, "gunKinetic");
                case StandardType.GunPlasma:
                    return PowerupManager.Instance.ApplyGun(targetPlayer, "gunPlasma");
                case StandardType.GunFlak:
                    return PowerupManager.Instance.ApplyGun(targetPlayer, "gunFlak");
                case StandardType.AmmoRefill:
                    return PowerupManager.Instance.ApplyAmmoRefill(targetPlayer);
                case StandardType.AmmoFlak:
                    return PowerupManager.Instance.ApplyAmmoFlak(targetPlayer, Mathf.RoundToInt(potency * 20f));
                case StandardType.UpgradePoint:
                    return PowerupManager.Instance.ApplyUpgradePoint(targetPlayer, Mathf.Max(1, Mathf.RoundToInt(potency)));
            }
        }
        else
        {
            // Fallback direct application if manager not initialized
            switch (type)
            {
                case StandardType.XpUp:
                    targetPlayer.AddXP(Mathf.Max(1, Mathf.RoundToInt(potency * 50f)));
                    return true;
                case StandardType.ShieldUp:
                    targetPlayer.shield_value = Mathf.Min(targetPlayer.shield_value + potency, targetPlayer.shield_max_value * 1.5f);
                    targetPlayer.UpdateShieldHUD();
                    return true;
                case StandardType.ExtraSocket:
                    targetPlayer.AddGun();
                    return true;
                case StandardType.ScrapSalvage:
                    PlayerMetaProgression.AddScrap(Mathf.RoundToInt(potency * 25f));
                    return true;
                case StandardType.GunKinetic:
                    return targetPlayer.AddGun(PrefabManager.Get<Gun>(PrefabId.GunKinetic));
                case StandardType.GunPlasma:
                    return targetPlayer.AddGun(PrefabManager.Get<Gun>(PrefabId.GunPlasma));
                case StandardType.GunFlak:
                    return targetPlayer.AddGun(PrefabManager.Get<Gun>(PrefabId.GunFlak));
                case StandardType.AmmoRefill:
                    targetPlayer.RefillAllWeaponsAmmo();
                    return true;
                case StandardType.AmmoFlak:
                    bool hasFlak = false;
                    List<Gun> flakGuns = targetPlayer.GetEquippedGuns();
                    for (int i = 0; i < flakGuns.Count; i++)
                    {
                        if (flakGuns[i] != null && flakGuns[i].Data != null && flakGuns[i].Data.gunId == GunFlak.DEFAULT_GUN_ID)
                        {
                            hasFlak = true;
                            flakGuns[i].RefillAmmo();
                        }
                    }
                    if (!hasFlak)
                    {
                        targetPlayer.AddGun(PrefabManager.Get<Gun>(PrefabId.GunFlak));
                    }
                    return true;
                case StandardType.UpgradePoint:
                    targetPlayer.AddUpgradePoints(Mathf.Max(1, Mathf.RoundToInt(potency)));
                    return true;
            }
        }

        return false;
    }
}

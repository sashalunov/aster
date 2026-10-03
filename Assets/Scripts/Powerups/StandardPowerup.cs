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
        SpeedUp,
        PowerUp,
        DamageUp,
        ShieldUp,
        ExtraSocket,
        ScrapSalvage,
        GunKinetic,
        GunPlasma,
        AmmoRefill,
        UpgradePoint
    }

    [Header("Standard Powerup Configuration")]
    [SerializeField] private StandardType type = StandardType.SpeedUp;
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
            case StandardType.SpeedUp:
                powerupId = "speed_up";
                displayName = "SPEED UP!";
                themeColor = new Color(1f, 0.9f, 0.2f); // Yellow
                break;
            case StandardType.PowerUp:
                powerupId = "power_up";
                displayName = "POWER UP!";
                themeColor = new Color(0.9f, 0.3f, 1f); // Magenta / Purple
                break;
            case StandardType.DamageUp:
                powerupId = "damage_up";
                displayName = "DAMAGE UP!";
                themeColor = new Color(1f, 0.25f, 0.25f); // Red
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
            case StandardType.AmmoRefill:
                powerupId = "ammo_refill";
                displayName = "AMMO REFILL!";
                themeColor = new Color(0.3f, 1f, 0.5f); // Bright Green
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
                case StandardType.SpeedUp:
                    return PowerupManager.Instance.ApplySpeedUp(targetPlayer, potency);
                case StandardType.PowerUp:
                    return PowerupManager.Instance.ApplyPowerUp(targetPlayer, potency);
                case StandardType.DamageUp:
                    return PowerupManager.Instance.ApplyDamageUp(targetPlayer, potency);
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
                case StandardType.AmmoRefill:
                    return PowerupManager.Instance.ApplyAmmoRefill(targetPlayer);
                case StandardType.UpgradePoint:
                    return PowerupManager.Instance.ApplyUpgradePoint(targetPlayer, Mathf.Max(1, Mathf.RoundToInt(potency)));
            }
        }
        else
        {
            // Fallback direct application if manager not initialized
            switch (type)
            {
                case StandardType.SpeedUp:
                    targetPlayer._fire_hz = Mathf.Min(targetPlayer._fire_hz + potency, 12f);
                    targetPlayer._fire_rate = 1f / targetPlayer._fire_hz;
                    targetPlayer.UpdateWeaponHUD();
                    return true;
                case StandardType.PowerUp:
                    targetPlayer._bullet_force = Mathf.Min(targetPlayer._bullet_force + potency, 20f);
                    targetPlayer.UpdateWeaponHUD();
                    return true;
                case StandardType.DamageUp:
                    targetPlayer._bullet_dmg = Mathf.Min(targetPlayer._bullet_dmg + potency, 25f);
                    targetPlayer.UpdateWeaponHUD();
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
                    return targetPlayer.AddGun(Resources.Load<GameObject>("gunKinetic")?.GetComponent<Gun>());
                case StandardType.GunPlasma:
                    return targetPlayer.AddGun(Resources.Load<GameObject>("gunPlasma")?.GetComponent<Gun>());
                case StandardType.AmmoRefill:
                    targetPlayer.RefillAllWeaponsAmmo();
                    return true;
                case StandardType.UpgradePoint:
                    targetPlayer.AddUpgradePoints(Mathf.Max(1, Mathf.RoundToInt(potency)));
                    return true;
            }
        }

        return false;
    }
}

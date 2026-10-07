using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Standard concrete powerup implementing foundational ship upgrade types.
/// Serves as a reference implementation for PowerupBase and provides ready-to-use powerups.
/// Dynamically reconfigures procedural visuals (label text, text color, glow particle color, and emissive material).
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

    [Tooltip("The wave progression level used to scale XP reward. Defaults to current active wave.")]
    [SerializeField] private int waveNumber = 1;

    [Header("Procedural Visual Components")]
    [Tooltip("Text component displaying the powerup label. Auto-resolved if unassigned.")]
    [SerializeField] private TMP_Text labelText;

    [Tooltip("Particle system for the ambient/glow effect. Auto-resolved if unassigned.")]
    [SerializeField] private ParticleSystem pfxGlow;

    [Tooltip("MeshRenderer of the spherical orb for emissive color tinting. Auto-resolved if unassigned.")]
    [SerializeField] private MeshRenderer sphereRenderer;

    private Material _sphereMaterialInstance;

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

    public int WaveNumber
    {
        get => waveNumber;
        set
        {
            waveNumber = Mathf.Max(1, value);
            ApplyConfigurationForType();
            UpdateVisuals();
        }
    }

    public static int GetCurrentWaveNumber()
    {
        if (WaveManager.Instance != null && WaveManager.Instance.CurrentWaveIndex > 0)
        {
            return WaveManager.Instance.CurrentWaveIndex;
        }

        player p = Object.FindAnyObjectByType<player>();
        if (p != null && p.Progression != null)
        {
            return Mathf.Max(1, p.Progression.WaveLevel + 1);
        }

        return 1;
    }

    public int CalculateXpAmount()
    {
        int wave = waveNumber > 0 ? waveNumber : GetCurrentWaveNumber();
        const int baseXp = 1;
        return Mathf.Max(1, Mathf.RoundToInt(baseXp * wave * potency));
    }

    protected override void Awake()
    {
        base.Awake();
        if (waveNumber <= 1)
        {
            waveNumber = GetCurrentWaveNumber();
        }
        ApplyConfigurationForType();
        UpdateVisuals();
    }

    protected override void Start()
    {
        if (waveNumber <= 1)
        {
            waveNumber = GetCurrentWaveNumber();
        }
        base.Start();
    }

    private void OnValidate()
    {
        ApplyConfigurationForType();
        UpdateVisuals();
    }

    public void ApplyConfigurationForType()
    {
        switch (type)
        {
            case StandardType.XpUp:
                powerupId = "xp_up";
                int xpAmount = CalculateXpAmount();
                displayName = $"+{xpAmount} XP";
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

    /// <summary>
    /// Returns the concise text label displayed on the procedural 3D powerup mesh.
    /// </summary>
    public virtual string GetPickupLabel()
    {
        switch (type)
        {
            case StandardType.XpUp: return $"+{CalculateXpAmount()} XP";
            case StandardType.ShieldUp: return "SHIELD UP!";
            case StandardType.ExtraSocket: return "EXTRA SOCKET!";
            case StandardType.ScrapSalvage: return "SCRAP +25";
            case StandardType.GunKinetic: return "KINETIC!";
            case StandardType.GunPlasma: return "PLASMA!";
            case StandardType.GunFlak: return "FLAK!";
            case StandardType.AmmoRefill: return "AMMO REFILL!";
            case StandardType.AmmoFlak: return "FLAK AMMO!";
            case StandardType.UpgradePoint: return "UPGRADE POINT!";
            default: return !string.IsNullOrEmpty(displayName) ? displayName : "POWER UP!";
        }
    }

    /// <summary>
    /// Updates label text, text color, pfx_glow start color, and sphere material instance emissive color.
    /// </summary>
    public override void UpdateVisuals()
    {
        base.UpdateVisuals();

        // 1. Resolve visual components if needed
        if (labelText == null)
        {
            Transform textT = transform.Find("text");
            if (textT != null) labelText = textT.GetComponent<TMP_Text>();
            if (labelText == null) labelText = GetComponentInChildren<TMP_Text>();
        }

        if (pfxGlow == null)
        {
            Transform pfxT = transform.Find("pfx_glow");
            if (pfxT != null) pfxGlow = pfxT.GetComponent<ParticleSystem>();
            if (pfxGlow == null) pfxGlow = GetComponentInChildren<ParticleSystem>();
        }

        if (sphereRenderer == null)
        {
            Transform sphereT = transform.Find("sphere");
            if (sphereT != null) sphereRenderer = sphereT.GetComponent<MeshRenderer>();
        }

        // 2. Update text and text color
        if (labelText != null)
        {
            labelText.text = GetPickupLabel();
            labelText.color = themeColor;
        }

        // 3. Update pfx_glow start color
        if (pfxGlow != null)
        {
            var main = pfxGlow.main;
            Color glowColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(glowColor);
        }

        // 4. Update sphere material instance emissive color
        if (sphereRenderer != null)
        {
#if UNITY_EDITOR
            bool isPrefabAsset = UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            bool isPrefabAsset = false;
#endif
            if (Application.isPlaying && !isPrefabAsset)
            {
                if (_sphereMaterialInstance == null)
                {
                    _sphereMaterialInstance = sphereRenderer.material; // creates runtime instance
                }

                if (_sphereMaterialInstance != null)
                {
                    _sphereMaterialInstance.EnableKeyword("_EMISSION");
                    _sphereMaterialInstance.SetColor("_EmissionColor", themeColor);
                    if (_sphereMaterialInstance.HasProperty("_Color"))
                    {
                        Color currentBase = _sphereMaterialInstance.GetColor("_Color");
                        _sphereMaterialInstance.SetColor("_Color", new Color(themeColor.r, themeColor.g, themeColor.b, currentBase.a));
                    }
                }
            }
            else
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                sphereRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", themeColor);
                if (sphereRenderer.sharedMaterial != null && sphereRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    Color currentBase = sphereRenderer.sharedMaterial.GetColor("_Color");
                    mpb.SetColor("_Color", new Color(themeColor.r, themeColor.g, themeColor.b, currentBase.a));
                }
                sphereRenderer.SetPropertyBlock(mpb);
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (_sphereMaterialInstance != null)
        {
            Destroy(_sphereMaterialInstance);
            _sphereMaterialInstance = null;
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
                    return PowerupManager.Instance.ApplyXP(targetPlayer, CalculateXpAmount());
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
                    targetPlayer.AddXP(CalculateXpAmount());
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


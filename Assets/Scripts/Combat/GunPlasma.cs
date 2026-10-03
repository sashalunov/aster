using UnityEngine;

/// <summary>
/// Plasma Repeater weapon component.
/// Rapid-fires high-energy thermal bolts using bulletPlasma in tight 2-round bursts.
/// Inherits from modern universal Gun class.
/// </summary>
public class GunPlasma : Gun
{
    public const string DEFAULT_GUN_ID = "gun_plasma";
    public const string DEFAULT_DISPLAY_NAME = "Plasma Repeater";

    protected virtual void Reset()
    {
        EnsurePlasmaData();
    }

    private void OnValidate()
    {
        EnsurePlasmaData();
    }

    private void Awake()
    {
        EnsurePlasmaData();
        AutoResolveComponents();
    }

    /// <summary>
    /// Ensures valid GunData configured for Plasma Repeater archetype.
    /// </summary>
    public void EnsurePlasmaData()
    {
        if (Data == null)
        {
            Data = CreatePlasmaGunData();
        }
    }

    /// <summary>
    /// Factory method to create a standard Plasma GunData asset in memory.
    /// </summary>
    public static GunData CreatePlasmaGunData(GameObject bulletPrefab = null)
    {
        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = DEFAULT_GUN_ID;
        data.displayName = DEFAULT_DISPLAY_NAME;
        data.bulletDamage = 2.0f;
        data.bulletForce = 1.0f;
        data.fireRate = 1.5f;
        data.bulletLifetime = 3.0f;
        data.spreadAngle = 3.5f;
        data.projectilesPerShot = 1;
        data.burstCount = 2;
        data.burstInterval = 0.07f;

        if (bulletPrefab != null)
        {
            data.bulletPrefab = bulletPrefab;
        }
        else
        {
            data.bulletPrefab = Resources.Load<GameObject>("bulletPlasma");
        }

        return data;
    }
}

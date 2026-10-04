using UnityEngine;

/// <summary>
/// Kinetic Cannon weapon component.
/// Fires high-velocity, high-impact solid projectiles using bulletKinetic.
/// Inherits from modern universal Gun class.
/// </summary>
public class GunFlak : Gun
{
    public const string DEFAULT_GUN_ID = "gun_flak";
    public const string DEFAULT_DISPLAY_NAME = "Flak Cannon";

    protected virtual void Reset()
    {
        EnsureKineticData();
    }

    private void OnValidate()
    {
        EnsureKineticData();
    }

    private void Awake()
    {
        EnsureKineticData();
        AutoResolveComponents();
    }

    /// <summary>
    /// Ensures valid GunData configured for Kinetic Cannon archetype.
    /// </summary>
    public void EnsureKineticData()
    {
        if (Data == null)
        {
            Data = CreateKineticGunData();
        }
    }

    /// <summary>
    /// Factory method to create a standard Kinetic GunData asset in memory.
    /// </summary>
    public static GunData CreateKineticGunData(GameObject bulletPrefab = null)
    {
        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = DEFAULT_GUN_ID;
        data.displayName = DEFAULT_DISPLAY_NAME;
        data.bulletDamage = 2.0f;
        data.bulletForce = 12.0f;
        data.fireRate = 1.0f;
        data.bulletLifetime = 4.0f;
        data.spreadAngle = 1.0f;
        data.projectilesPerShot = 1;
        data.burstCount = 1;
        data.ammo_quantity = -1;

        if (bulletPrefab != null)
        {
            data.bulletPrefab = bulletPrefab;
        }
        else
        {
            data.bulletPrefab = PrefabManager.Get(PrefabId.BulletKinetic);
        }

        return data;
    }
}

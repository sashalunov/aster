using UnityEngine;

/// <summary>
/// Kinetic Cannon weapon component.
/// Fires high-velocity, high-impact solid projectiles using bulletKinetic.
/// Inherits from modern universal Gun class.
/// </summary>
public class GunDouble : Gun
{
    public const string DEFAULT_GUN_ID = "gun_double";
    public const string DEFAULT_DISPLAY_NAME = "Double Gun";

    protected virtual void Reset()
    {
        EnsureDoubleData();
    }

    private void OnValidate()
    {
        EnsureDoubleData();
    }

    protected override void Awake()
    {
        EnsureDoubleData();
        base.Awake();
    }

    /// <summary>
    /// Ensures valid GunData configured for Double Gun archetype.
    /// </summary>
    public void EnsureDoubleData()
    {
        if (Data == null)
        {
            Data = CreateDoubleGunData();
        }
    }

    /// <summary>
    /// Factory method to create a standard Kinetic GunData asset in memory.
    /// </summary>
    public static GunData CreateDoubleGunData(GameObject bulletPrefab = null)
    {
        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = DEFAULT_GUN_ID;
        data.displayName = DEFAULT_DISPLAY_NAME;
        data.fireForce = 1.0f;
        data.fireRate = 1.0f;
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

using UnityEngine;
// Flak Cannon weapon component.
public class GunFlak : Gun
{
    public const string DEFAULT_GUN_ID = "gun_flak";
    public const string DEFAULT_DISPLAY_NAME = "Flak Cannon";

    protected virtual void Reset()
    {
        EnsureFlakData();
    }

    private void OnValidate()
    {
        EnsureFlakData();
    }

    private void Awake()
    {
        EnsureFlakData();
        AutoResolveComponents();
    }

    /// <summary>
    /// Ensures valid GunData configured for Flak Cannon archetype.
    /// </summary>
    public void EnsureFlakData()
    {
        if (Data == null)
        {
            Data = CreateFlakGunData();
        }
    }

    /// <summary>
    /// Factory method to create a standard Flak GunData asset in memory.
    /// </summary>
    public static GunData CreateFlakGunData(GameObject bulletPrefab = null)
    {
        GunData data = ScriptableObject.CreateInstance<GunData>();
        data.gunId = DEFAULT_GUN_ID;
        data.displayName = DEFAULT_DISPLAY_NAME;
        data.fireForce = 5.0f;
        data.fireRate = 1.0f;
        data.spreadAngle = 15.0f;
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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universal gun component. Can be mounted on player ships, enemy tanks, or turrets.
/// Operates autonomously based on GunData stats (damage, rate, spread, bursts).
/// Decoupled from specific owner classes to support plug-and-play sockets.
/// </summary>
[DisallowMultipleComponent]
public class Gun : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Data asset defining this weapon's stats and projectile prefabs.")]
    [SerializeField] private GunData gunData;

    [Header("Mount & Points")]
    [Tooltip("Transform from which projectiles and muzzle flashes are spawned.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("Optional rotating sub-transform for aiming independent of the base.")]
    [SerializeField] private Transform turretPoint;

    [Header("Feedback")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private string fireAnimationName = "urret_fire";

    [Header("Ammunition Runtime State")]
    [Tooltip("Current ammunition count. -1 indicates infinite ammunition.")]
    [SerializeField] private int currentAmmo = -1;

    [Header("Runtime Stat Modifiers / Upgrades")]
    [SerializeField] private float damageBonus = 0f;
    [SerializeField] private float fireRateBonus = 0f;
    [SerializeField] private float fireForceBonus = 0f;
    [SerializeField] private float spreadReduction = 0f;
    [SerializeField] private int bonusProjectilesPerShot = 0;
    [SerializeField] private int bonusBurstCount = 0;
    [SerializeField] private int bonusMaxAmmo = 0;

    // Public properties
    public GunData Data
    {
        get => gunData;
        set
        {
            gunData = value;
            if (gunData != null && gunData.ammo_quantity >= 0 && currentAmmo < 0)
            {
                currentAmmo = gunData.ammo_quantity;
            }
        }
    }

    public Transform MuzzlePoint => muzzlePoint != null ? muzzlePoint : transform;
    public Transform TurretPoint => turretPoint != null ? turretPoint : transform;
    public GameObject Owner { get; private set; }
    public Rigidbody OwnerRigidbody { get; private set; }
    public Collider[] OwnerColliders { get; private set; }

    public int CurrentAmmo
    {
        get => currentAmmo;
        set
        {
            currentAmmo = value;
            OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        }
    }

    public int MaxAmmo => EffectiveMaxAmmo;
    public bool IsInfiniteAmmo => gunData == null || gunData.ammo_quantity < 0;
    public bool HasAmmo => IsInfiniteAmmo || currentAmmo > 0;

    public float DamageBonus => damageBonus;
    public float FireRateBonus => fireRateBonus;
    public float ForceBonus => fireForceBonus;
    public int BonusBurstCount => bonusBurstCount;

    public float BaseDamage
    {
        get
        {
            if (gunData != null && gunData.bulletPrefab != null)
            {
                ProjectileBase p = gunData.bulletPrefab.GetComponent<ProjectileBase>();
                if (p != null) return p.Damage;
            }
            return 1f;
        }
    }

    public float EffectiveDamage => Mathf.Max(0.1f, BaseDamage + damageBonus);
    public float EffectiveFireRate => Mathf.Max(0.1f, (gunData != null ? gunData.fireRate : 1f) + fireRateBonus);
    public float EffectiveForce => Mathf.Max(0.1f, (gunData != null ? gunData.fireForce : 1f) + fireForceBonus);
    public float EffectiveSpread => Mathf.Max(0f, (gunData != null ? gunData.spreadAngle : 0f) - spreadReduction);
    public int EffectiveProjectilesPerShot => Mathf.Max(1, (gunData != null ? gunData.projectilesPerShot : 1) + bonusProjectilesPerShot);
    public int EffectiveBurstCount => Mathf.Max(1, (gunData != null ? gunData.burstCount : 1) + bonusBurstCount);
    public int EffectiveMaxAmmo => gunData != null && gunData.ammo_quantity >= 0 ? gunData.ammo_quantity + bonusMaxAmmo : -1;

    public bool CanFire => Time.time >= _nextFireTime && HasAmmo;
    public float FireRate => EffectiveFireRate;

    // Events
    public event Action<Gun> OnFired;
    public event Action<int, int> OnAmmoChanged; // (currentAmmo, maxAmmo)
    public event Action<Gun> OnStatsUpgraded;

    public void UpgradeDamage(float delta)
    {
        damageBonus += delta;
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeFireRate(float delta)
    {
        fireRateBonus += delta;
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeForce(float delta)
    {
        fireForceBonus += delta;
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeSpread(float reduction)
    {
        spreadReduction = Mathf.Min(spreadReduction + reduction, gunData != null ? gunData.spreadAngle : 0f);
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeProjectiles(int delta)
    {
        bonusProjectilesPerShot += delta;
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeBurst(int delta)
    {
        bonusBurstCount += delta;
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeMaxAmmo(int delta)
    {
        bonusMaxAmmo += delta;
        if (!IsInfiniteAmmo)
        {
            currentAmmo += delta;
            OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        }
        OnStatsUpgraded?.Invoke(this);
    }

    public void UpgradeStats(float dmgDelta = 0f, float rateDelta = 0f, float forceDelta = 0f, int burstDelta = 0)
    {
        damageBonus += dmgDelta;
        fireRateBonus += rateDelta;
        fireForceBonus += forceDelta;
        bonusBurstCount += burstDelta;
        OnStatsUpgraded?.Invoke(this);
    }

    private float _nextFireTime = 0f;
    private Coroutine _burstRoutine;

    protected virtual void Awake()
    {
        AutoResolveComponents();
        InitializeAmmo();
    }

    public void InitializeAmmo()
    {
        if (gunData != null)
        {
            currentAmmo = gunData.ammo_quantity;
        }
    }

    public void AutoResolveComponents()
    {
        if (turretPoint == null)
        {
            turretPoint = transform.Find("hull/turret0") ?? transform.Find("turret0") ?? transform.Find("turret") ?? transform.Find("point_turret");
            if (turretPoint == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name == "turret0" || child.name == "turret" || child.name == "point_turret")
                    {
                        turretPoint = child;
                        break;
                    }
                }
            }
        }

        if (muzzlePoint == null)
        {
            muzzlePoint = transform.Find("muzzle") ?? transform.Find("fire_point") ?? transform.Find("gun_muzzle_point");
            if (muzzlePoint == null && turretPoint != null)
            {
                muzzlePoint = turretPoint.Find("muzzle") ?? turretPoint.Find("gun_muzzle_point") ?? turretPoint.Find("gun0/ps_muzzle");
            }
            if (muzzlePoint == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    if (child.name == "ps_muzzle" || child.name == "muzzle" || child.name == "gun_muzzle_point" || child.name == "fire_point")
                    {
                        muzzlePoint = child;
                        break;
                    }
                }
            }
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponentInChildren<AudioSource>();
        }
    }

    /// <summary>
    /// Assigns the owner of this gun (e.g. player ship, tank, or turret).
    /// Caches colliders to prevent self-damage and Rigidbody to inherit vessel velocity.
    /// </summary>
    public void SetOwner(GameObject owner)
    {
        Owner = owner;
        if (owner != null)
        {
            OwnerRigidbody = owner.GetComponent<Rigidbody>() ?? owner.GetComponentInParent<Rigidbody>();
            OwnerColliders = owner.GetComponentsInChildren<Collider>();
        }
        else
        {
            OwnerRigidbody = null;
            OwnerColliders = null;
        }
    }

    /// <summary>
    /// Aims the turret / weapon towards a world target position in the 2D XY plane.
    /// </summary>
    public void AimAt(Vector3 targetWorldPosition, float turnSpeedDegrees = -1f)
    {
        Transform aimTransform = TurretPoint;
        Vector3 aimOrigin = aimTransform.position;

        float dx = targetWorldPosition.x - aimOrigin.x;
        float dy = targetWorldPosition.y - aimOrigin.y;

        // In aster, transform.up (local Y) is forward, so angle offset is -90 degrees
        float targetAngle = Mathf.Rad2Deg * Mathf.Atan2(dy, dx) - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);

        if (turnSpeedDegrees > 0f)
        {
            aimTransform.rotation = Quaternion.RotateTowards(aimTransform.rotation, targetRot, turnSpeedDegrees * Time.deltaTime);
        }
        else
        {
            aimTransform.rotation = targetRot;
        }
    }

    /// <summary>
    /// Explicitly sets the Z-axis rotation angle of the aiming turret.
    /// </summary>
    public void SetAimAngle(float zAngleDegrees)
    {
        TurretPoint.localRotation = Quaternion.Euler(0f, 0f, zAngleDegrees);
    }

    /// <summary>
    /// Attempts to fire this weapon if ready.
    /// </summary>
    /// <returns>True if weapon fired successfully, false if still on cooldown or unconfigured.</returns>
    public bool TryFire()
    {
        if (!CanFire || gunData == null || gunData.bulletPrefab == null)
        {
            return false;
        }

        float rate = EffectiveFireRate;
        float interval = rate > 0f ? (1f / rate) : 0.2f;
        _nextFireTime = Time.time + interval;

        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
        }

        if (EffectiveBurstCount > 1 && gameObject.activeInHierarchy)
        {
            _burstRoutine = StartCoroutine(ExecuteBurstFireRoutine());
        }
        else
        {
            FireSingleShot();
        }

        return true;
    }

    private IEnumerator ExecuteBurstFireRoutine()
    {
        int count = Mathf.Max(1, EffectiveBurstCount);
        float interval = Mathf.Max(0.02f, gunData.burstInterval);

        for (int i = 0; i < count; i++)
        {
            FireSingleShot();

            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
        _burstRoutine = null;
    }

    private void FireSingleShot()
    {
        if (gunData == null || gunData.bulletPrefab == null) return;

        Transform muzzle = MuzzlePoint;
        Transform turret = TurretPoint;
        Quaternion baseRot = turret != null ? turret.rotation : muzzle.rotation;

        int count = Mathf.Max(1, EffectiveProjectilesPerShot);
        float spread = EffectiveSpread;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = 0f;
            if (count > 1)
            {
                // Evenly distribute multi-shot pellets across spread cone
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                angleOffset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            }
            else if (spread > 0f)
            {
                angleOffset = UnityEngine.Random.Range(-spread * 0.5f, spread * 0.5f);
            }

            Quaternion bulletRot = baseRot * Quaternion.Euler(0f, 0f, angleOffset);
            SpawnProjectile(muzzle.position, bulletRot);
        }

        PlayFiringFeedback();
        if (!IsInfiniteAmmo)
        {
            currentAmmo--;
            OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        }
        OnFired?.Invoke(this);
    }

    /// <summary>
    /// Adds ammunition to this weapon, clamped to MaxAmmo (if finite).
    /// Returns the actual amount of ammunition added.
    /// </summary>
    public int AddAmmo(int amount)
    {
        if (IsInfiniteAmmo || amount <= 0) return 0;

        int before = currentAmmo;
        int max = MaxAmmo > 0 ? MaxAmmo : int.MaxValue;
        currentAmmo = Mathf.Min(currentAmmo + amount, max);
        OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        return currentAmmo - before;
    }

    /// <summary>
    /// Refills weapon ammunition to maximum capacity.
    /// </summary>
    public void RefillAmmo()
    {
        if (!IsInfiniteAmmo)
        {
            currentAmmo = MaxAmmo;
            OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        }
    }

    private void SpawnProjectile(Vector3 position, Quaternion rotation)
    {
        GameObject bulletObj = Instantiate(gunData.bulletPrefab, position, rotation);
        if (bulletObj == null) return;

        // Initialize ProjectileBase
        ProjectileBase proj = bulletObj.GetComponent<ProjectileBase>();
        if (proj != null)
        {
            proj.Initialize(Owner);
            if (damageBonus != 0f)
            {
                proj.Damage = Mathf.Max(0.1f, proj.Damage + damageBonus);
            }
        }

        // Ignore collisions with owner vessel
        Collider bulletCol = bulletObj.GetComponent<Collider>();
        if (bulletCol != null && OwnerColliders != null)
        {
            foreach (var oc in OwnerColliders)
            {
                if (oc != null && oc.enabled)
                {
                    Physics.IgnoreCollision(bulletCol, oc);
                }
            }
        }

        // Apply owner velocity inheritance + projectile impulse
        Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 inheritVel = OwnerRigidbody != null ? OwnerRigidbody.linearVelocity : Vector3.zero;
            rb.linearVelocity = inheritVel;
            rb.AddForce(bulletObj.transform.up * EffectiveForce, ForceMode.Impulse);
        }
    }

    private void PlayFiringFeedback()
    {
        Transform muzzle = MuzzlePoint;

        if (gunData.muzzleFlashPrefab != null && muzzle != null)
        {
            GameObject flash = Instantiate(gunData.muzzleFlashPrefab, muzzle.position, muzzle.rotation);
            flash.transform.SetParent(muzzle);
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Destroy(flash, 1.0f);
            }
            else
            {
                DestroyImmediate(flash);
            }
#else
            Destroy(flash, 1.0f);
#endif
        }

        if (animator != null && !string.IsNullOrEmpty(fireAnimationName))
        {
            animator.Play(fireAnimationName, 0, 0f);
        }

        if (audioSource != null && gunData.fireSound != null)
        {
            audioSource.PlayOneShot(gunData.fireSound);
        }
    }
}

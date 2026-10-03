using System;
using UnityEngine;

/// <summary>
/// Mount point / hardpoint placed on player ship hulls or enemy bodies.
/// Handles mounting, replacing, aiming, and firing weapons dynamically.
/// </summary>
[DisallowMultipleComponent]
public class GunSocket : MonoBehaviour
{
    [Header("Socket Identity")]
    [Tooltip("Unique socket identifier on this vessel (e.g. 'wing_left', 'wing_right', 'turret_main').")]
    [SerializeField] private string socketId = "primary_0";

    [Tooltip("Optional tag or weapon class allowed on this socket (empty = accept all).")]
    [SerializeField] private string allowedCategory = "";

    [Header("Mount Configuration")]
    [Tooltip("Optional default gun prefab instantiated on Start if empty.")]
    [SerializeField] private Gun defaultGunPrefab;

    [Tooltip("The currently mounted gun instance.")]
    [SerializeField] private Gun mountedGun;

    [Tooltip("Explicit vessel owner GameObject. If unassigned, automatically detects from parent.")]
    [SerializeField] private GameObject explicitOwner;

    // Public properties
    public string SocketId => socketId;
    public string AllowedCategory => allowedCategory;
    public Gun MountedGun => mountedGun;
    public bool HasGun => mountedGun != null;
    public GameObject Owner => ResolveOwner();

    // Events
    public event Action<Gun> OnGunMounted;
    public event Action<Gun> OnGunUnmounted;

    private void Start()
    {
        if (mountedGun == null && defaultGunPrefab != null)
        {
            AttachGun(defaultGunPrefab);
        }
        else if (mountedGun != null)
        {
            mountedGun.SetOwner(ResolveOwner());
        }
    }

    /// <summary>
    /// Finds or returns the owning vessel GameObject.
    /// </summary>
    public GameObject ResolveOwner()
    {
        if (explicitOwner != null) return explicitOwner;

        // Try finding player first
        player p = GetComponentInParent<player>();
        if (p != null) return p.gameObject;

        // Try finding root or parent with a Rigidbody
        Rigidbody rb = GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.gameObject;

        return transform.root != null ? transform.root.gameObject : gameObject;
    }

    /// <summary>
    /// Instantiates and mounts a gun from a prefab onto this socket.
    /// Replaces and destroys any previously mounted gun.
    /// </summary>
    public bool AttachGun(Gun gunPrefab, GameObject customOwner = null)
    {
        if (gunPrefab == null) return false;

        DetachGun();

        Gun instance = Instantiate(gunPrefab, transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        return AttachGunInstance(instance, customOwner);
    }

    /// <summary>
    /// Mounts an existing Gun GameObject instance onto this socket.
    /// </summary>
    public bool AttachGunInstance(Gun gunInstance, GameObject customOwner = null)
    {
        if (gunInstance == null) return false;

        if (mountedGun != null && mountedGun != gunInstance)
        {
            DetachGun();
        }

        mountedGun = gunInstance;
        mountedGun.transform.SetParent(transform);
        mountedGun.transform.localPosition = Vector3.zero;
        mountedGun.transform.localRotation = Quaternion.identity;

        GameObject owner = customOwner != null ? customOwner : ResolveOwner();
        mountedGun.SetOwner(owner);

        OnGunMounted?.Invoke(mountedGun);
        return true;
    }

    /// <summary>
    /// Detaches and unmounts the current gun without destroying it.
    /// </summary>
    public Gun DetachGun()
    {
        if (mountedGun == null) return null;

        Gun detached = mountedGun;
        mountedGun = null;

        OnGunUnmounted?.Invoke(detached);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(detached.gameObject);
        else
            Destroy(detached.gameObject);
#else
        Destroy(detached.gameObject);
#endif

        return detached;
    }

    /// <summary>
    /// Attempts to fire the gun mounted in this socket.
    /// </summary>
    public bool TryFireMountedGun()
    {
        if (mountedGun != null)
        {
            return mountedGun.TryFire();
        }
        return false;
    }

    /// <summary>
    /// Directs the mounted gun to aim at the specified target position.
    /// </summary>
    public void AimMountedGun(Vector3 targetWorldPosition, float turnSpeedDegrees = -1f)
    {
        if (mountedGun != null)
        {
            mountedGun.AimAt(targetWorldPosition, turnSpeedDegrees);
        }
    }
}

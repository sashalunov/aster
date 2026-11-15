using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universal weapon and socket management system for vessels (Player, Bosses, AI ships, Stations).
/// Handles hardpoint sockets, gun lifecycle (mounting, detaching, dynamic creation),
/// aiming towards world targets, full/staggered weapon firing, and ammunition/stat maintenance.
/// </summary>
[DisallowMultipleComponent]
public class WeaponSystem : MonoBehaviour
{
    [Header("Sockets & Hardpoints")]
    [Tooltip("Explicitly assigned or discovered weapon sockets on this vessel.")]
    [SerializeField] protected List<GunSocket> _sockets = new List<GunSocket>();

    [Tooltip("Standalone or direct guns attached to this vessel not housed inside a GunSocket.")]
    [SerializeField] protected List<Gun> _guns = new List<Gun>();

    [Tooltip("Parent transform holding dynamically instantiated sockets and guns. If null, auto-locates or uses this transform.")]
    [SerializeField] protected Transform _gunsContainer;

    [Header("Capacity & Audio")]
    [Tooltip("Maximum number of guns allowed on this vessel.")]
    [SerializeField] protected int _maxGuns = PowerupManager.MAX_GUNS;

    [Tooltip("Optional audio source played when weapons fire.")]
    [SerializeField] protected AudioSource _fireAudioSource;

    // Public properties
    public List<GunSocket> Sockets => _sockets;
    public List<Gun> Guns => _guns;
    public Transform GunsContainer
    {
        get => _gunsContainer;
        set => _gunsContainer = value;
    }
    public int MaxGuns
    {
        get => _maxGuns;
        set => _maxGuns = Mathf.Max(1, value);
    }
    public int SocketCount => GetSockets().Count;
    public int GunCount => GetEquippedGuns().Count;

    // Events
    public event Action<GunSocket, Gun> OnGunMounted;
    public event Action<GunSocket, Gun> OnGunDetached;
    public event Action OnWeaponsFired;

    protected virtual void Awake()
    {
        if (_fireAudioSource == null)
        {
            _fireAudioSource = GetComponentInChildren<AudioSource>();
        }

        if (_gunsContainer == null)
        {
            _gunsContainer = FindDefaultContainer();
        }
    }

    /// <summary>
    /// Finds a sensible default container transform for new sockets and guns.
    /// </summary>
    protected virtual Transform FindDefaultContainer()
    {
        Transform t = transform.Find("hull/gun_sockets");
        if (t != null) return t;

        t = transform.Find("gun_sockets");
        if (t != null) return t;

        t = transform.Find("gun_container");
        if (t != null) return t;

        t = transform.Find("hull");
        if (t != null) return t;

        return transform;
    }

    #region Inspection & Queries

    /// <summary>
    /// Checks if this vessel has any weapon sockets (GunSocket components).
    /// Inspects both the internal _sockets list and child GameObjects.
    /// </summary>
    public virtual bool HasSockets(bool includeInactive = true)
    {
        if (_sockets != null)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i] != null && (includeInactive || _sockets[i].gameObject.activeInHierarchy))
                    return true;
            }
        }

        GunSocket[] inChildren = GetComponentsInChildren<GunSocket>(includeInactive);
        return inChildren != null && inChildren.Length > 0;
    }

    public bool HasAnySockets(bool includeInactive = true) => HasSockets(includeInactive);

    /// <summary>
    /// Checks if this vessel has any weapon socket currently unoccupied by a mounted gun.
    /// </summary>
    public virtual bool HasAvailableSocket(bool includeInactive = true)
    {
        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && !sockets[i].HasGun)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if this vessel has any guns equipped or mounted.
    /// </summary>
    public virtual bool HasGuns(bool includeInactive = true)
    {
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && (includeInactive || _guns[i].gameObject.activeInHierarchy))
                    return true;
            }
        }

        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && sockets[i].HasGun)
            {
                if (includeInactive || (sockets[i].MountedGun != null && sockets[i].MountedGun.gameObject.activeInHierarchy))
                    return true;
            }
        }

        Gun[] guns = GetComponentsInChildren<Gun>(includeInactive);
        return guns != null && guns.Length > 0;
    }

    public bool HasAnyGuns(bool includeInactive = true) => HasGuns(includeInactive);

    public bool HasSocketsAndGuns(bool includeInactive = true) => HasSockets(includeInactive) && HasGuns(includeInactive);
    public bool HasAnySocketsAndGuns(bool includeInactive = true) => HasSocketsAndGuns(includeInactive);
    public bool HasAnySocketsOrGuns(bool includeInactive = true) => HasSockets(includeInactive) || HasGuns(includeInactive);

    public bool CheckSocketsAndGuns(out bool hasSockets, out bool hasGuns, bool includeInactive = true)
    {
        hasSockets = HasSockets(includeInactive);
        hasGuns = HasGuns(includeInactive);
        return hasSockets && hasGuns;
    }

    public (bool hasSockets, bool hasGuns) CheckSocketsAndGunsStatus(bool includeInactive = true)
    {
        return (HasSockets(includeInactive), HasGuns(includeInactive));
    }

    /// <summary>
    /// Returns all unique GunSocket components present on or under this vessel.
    /// </summary>
    public virtual List<GunSocket> GetSockets(bool includeInactive = true)
    {
        List<GunSocket> result = new List<GunSocket>();
        HashSet<GunSocket> seen = new HashSet<GunSocket>();

        if (_sockets != null)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                GunSocket s = _sockets[i];
                if (s != null && (includeInactive || s.gameObject.activeInHierarchy))
                {
                    if (seen.Add(s))
                    {
                        result.Add(s);
                    }
                }
            }
        }

        GunSocket[] inChildren = GetComponentsInChildren<GunSocket>(includeInactive);
        if (inChildren != null)
        {
            for (int i = 0; i < inChildren.Length; i++)
            {
                GunSocket s = inChildren[i];
                if (s != null && seen.Add(s))
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all distinct Gun components mounted on sockets or directly attached to this vessel.
    /// </summary>
    public virtual List<Gun> GetEquippedGuns(bool includeInactive = true)
    {
        List<Gun> result = new List<Gun>();
        HashSet<Gun> seen = new HashSet<Gun>();

        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                Gun g = _guns[i];
                if (g != null && (includeInactive || g.gameObject.activeInHierarchy))
                {
                    if (seen.Add(g))
                    {
                        result.Add(g);
                    }
                }
            }
        }

        Gun[] childGuns = GetComponentsInChildren<Gun>(includeInactive);
        if (childGuns != null)
        {
            for (int i = 0; i < childGuns.Length; i++)
            {
                Gun g = childGuns[i];
                if (g != null && seen.Add(g))
                {
                    result.Add(g);
                }
            }
        }

        List<GunSocket> sockets = GetSockets(includeInactive);
        for (int i = 0; i < sockets.Count; i++)
        {
            GunSocket s = sockets[i];
            if (s != null && s.HasGun && s.MountedGun != null)
            {
                if (includeInactive || s.MountedGun.gameObject.activeInHierarchy)
                {
                    if (seen.Add(s.MountedGun))
                    {
                        result.Add(s.MountedGun);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Legacy compatibility accessor returning equipped guns as Component list.
    /// </summary>
    public List<Component> GetGuns(bool includeInactive = true)
    {
        List<Component> result = new List<Component>();
        List<Gun> equipped = GetEquippedGuns(includeInactive);
        for (int i = 0; i < equipped.Count; i++)
        {
            result.Add(equipped[i]);
        }
        return result;
    }

    #endregion

    #region Targeting & Combat

    /// <summary>
    /// Aims all equipped weapons (GunSockets and standalone Guns) toward the target world position.
    /// </summary>
    public virtual void AimWeapons(Vector3 targetWorldPosition, float turnSpeedDegrees = -1f)
    {
        // 1. Aim mounted guns on GunSockets
        List<GunSocket> sockets = GetSockets(false);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null)
            {
                sockets[i].AimMountedGun(targetWorldPosition, turnSpeedDegrees);
            }
        }

        // 2. Aim standalone modern Guns not mounted on a GunSocket
        Gun[] directGuns = GetComponentsInChildren<Gun>(false);
        for (int i = 0; i < directGuns.Length; i++)
        {
            if (directGuns[i] != null && directGuns[i].GetComponentInParent<GunSocket>() == null)
            {
                directGuns[i].AimAt(targetWorldPosition, turnSpeedDegrees);
            }
        }

        // 3. Aim standalone guns in _guns
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && _guns[i].GetComponentInParent<GunSocket>() == null)
                {
                    _guns[i].AimAt(targetWorldPosition, turnSpeedDegrees);
                }
            }
        }
    }

    /// <summary>
    /// Triggers weapon fire on all ready weapon systems (mounted socket guns and standalone guns).
    /// Returns true if at least one weapon fired.
    /// </summary>
    public virtual bool FireWeapons()
    {
        bool anyFired = false;

        // 1. Fire mounted guns on GunSockets
        List<GunSocket> sockets = GetSockets(false);
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i] != null && sockets[i].HasGun)
            {
                if (sockets[i].TryFireMountedGun())
                {
                    anyFired = true;
                }
            }
        }

        // 2. Fire direct Gun components not attached to a socket
        Gun[] directGuns = GetComponentsInChildren<Gun>(false);
        for (int i = 0; i < directGuns.Length; i++)
        {
            if (directGuns[i] != null && directGuns[i].GetComponentInParent<GunSocket>() == null)
            {
                if (directGuns[i].TryFire())
                {
                    anyFired = true;
                }
            }
        }

        // 3. Fire direct guns in _guns
        if (_guns != null)
        {
            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i] != null && _guns[i].GetComponentInParent<GunSocket>() == null)
                {
                    if (_guns[i].TryFire())
                    {
                        anyFired = true;
                    }
                }
            }
        }

        if (anyFired)
        {
            if (_fireAudioSource != null && !_fireAudioSource.isPlaying)
            {
                _fireAudioSource.Play();
            }
            OnWeaponsFired?.Invoke();
        }

        return anyFired;
    }

    /// <summary>
    /// Fires the weapon mounted on a specific socket index.
    /// Ideal for boss patterns, alternating salvos, or sequential bursts.
    /// </summary>
    public virtual bool FireSocket(int socketIndex)
    {
        List<GunSocket> sockets = GetSockets(false);
        if (socketIndex >= 0 && socketIndex < sockets.Count && sockets[socketIndex] != null)
        {
            if (sockets[socketIndex].HasGun && sockets[socketIndex].TryFireMountedGun())
            {
                if (_fireAudioSource != null && !_fireAudioSource.isPlaying)
                {
                    _fireAudioSource.Play();
                }
                OnWeaponsFired?.Invoke();
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Socket & Gun Lifecycle Management

    /// <summary>
    /// Adds a new GunSocket component to this vessel.
    /// </summary>
    public virtual GunSocket AddSocket(string socketId = null, Vector3? localPosition = null)
    {
        Transform container = _gunsContainer != null ? _gunsContainer : FindDefaultContainer();
        int index = SocketCount;
        string name = string.IsNullOrEmpty(socketId) ? $"socket_{index}" : socketId;

        GameObject socketObj = new GameObject(name);
        socketObj.transform.SetParent(container);
        socketObj.transform.localPosition = localPosition ?? Vector3.zero;
        socketObj.transform.localRotation = Quaternion.identity;

        GunSocket socket = socketObj.AddComponent<GunSocket>();
        if (_sockets == null) _sockets = new List<GunSocket>();
        _sockets.Add(socket);

        return socket;
    }

    /// <summary>
    /// Mounts a gun prefab onto the specified socket index or the first unoccupied socket.
    /// </summary>
    public virtual bool MountGun(Gun gunPrefab, int socketIndex = -1)
    {
        if (gunPrefab == null) return false;

        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count)
        {
            GunSocket targetSocket = sockets[socketIndex];
            bool attached = targetSocket.AttachGun(gunPrefab, gameObject);
            if (attached && targetSocket.MountedGun != null)
            {
                OnGunAttached(targetSocket, targetSocket.MountedGun);
                OnGunMounted?.Invoke(targetSocket, targetSocket.MountedGun);
            }
            return attached;
        }

        for (int i = 0; i < sockets.Count; i++)
        {
            GunSocket s = sockets[i];
            if (s != null && !s.HasGun)
            {
                bool attached = s.AttachGun(gunPrefab, gameObject);
                if (attached && s.MountedGun != null)
                {
                    OnGunAttached(s, s.MountedGun);
                    OnGunMounted?.Invoke(s, s.MountedGun);
                }
                return attached;
            }
        }

        return false;
    }

    /// <summary>
    /// Mounts an existing gun GameObject instance onto the specified socket index or the first unoccupied socket.
    /// </summary>
    public virtual bool MountGunInstance(Gun gunInstance, int socketIndex = -1)
    {
        if (gunInstance == null) return false;

        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count)
        {
            GunSocket targetSocket = sockets[socketIndex];
            bool attached = targetSocket.AttachGunInstance(gunInstance, gameObject);
            if (attached)
            {
                OnGunAttached(targetSocket, gunInstance);
                OnGunMounted?.Invoke(targetSocket, gunInstance);
            }
            return attached;
        }

        for (int i = 0; i < sockets.Count; i++)
        {
            GunSocket s = sockets[i];
            if (s != null && !s.HasGun)
            {
                bool attached = s.AttachGunInstance(gunInstance, gameObject);
                if (attached)
                {
                    OnGunAttached(s, gunInstance);
                    OnGunMounted?.Invoke(s, gunInstance);
                }
                return attached;
            }
        }

        return false;
    }

    /// <summary>
    /// Detaches and returns the mounted gun from a socket.
    /// </summary>
    public virtual Gun DetachGun(int socketIndex = 0)
    {
        List<GunSocket> sockets = GetSockets(true);
        if (socketIndex >= 0 && socketIndex < sockets.Count && sockets[socketIndex] != null)
        {
            GunSocket s = sockets[socketIndex];
            Gun detached = s.DetachGun();
            if (detached != null)
            {
                OnGunDetached?.Invoke(s, detached);
            }
            return detached;
        }
        return null;
    }

    /// <summary>
    /// Adds a gun to the vessel, mounting onto an available empty socket,
    /// creating a new socket up to maxGuns, or attaching as a direct child.
    /// </summary>
    public virtual bool AddGun(Gun gunPrefab = null)
    {
        if (GunCount >= _maxGuns) return false;

        // 1. Fallback default gun prefab if none specified
        if (gunPrefab == null)
        {
            gunPrefab = PrefabManager.Get<Gun>(PrefabId.GunKinetic);
            if (gunPrefab == null)
            {
                gunPrefab = PrefabManager.Get<Gun>(PrefabId.GunPlasma);
            }
        }

        if (gunPrefab == null) return false;

        // 2. Mount to an existing empty socket
        if (HasAvailableSocket())
        {
            if (MountGun(gunPrefab))
            {
                return true;
            }
        }

        // 3. Create a new socket if below capacity
        if (SocketCount < _maxGuns)
        {
            GunSocket newSocket = AddSocket();
            if (newSocket != null)
            {
                bool mounted = newSocket.AttachGun(gunPrefab, gameObject);
                if (mounted && newSocket.MountedGun != null)
                {
                    OnGunAttached(newSocket, newSocket.MountedGun);
                    OnGunMounted?.Invoke(newSocket, newSocket.MountedGun);
                }
                return mounted;
            }
        }

        // 4. Fallback: attach directly under guns container
        Transform container = _gunsContainer != null ? _gunsContainer : FindDefaultContainer();
        Gun newGunInstance = Instantiate(gunPrefab, container.position, container.rotation, container);
        newGunInstance.SetOwner(gameObject);
        OnGunAttached(null, newGunInstance);
        if (_guns == null) _guns = new List<Gun>();
        _guns.Add(newGunInstance);
        return true;
    }

    /// <summary>
    /// Virtual lifecycle hook called when a gun is attached or mounted.
    /// Subclasses (e.g., PlayerWeapons) can override this to synchronize stats or trigger UI events.
    /// </summary>
    protected virtual void OnGunAttached(GunSocket socket, Gun gun)
    {
    }

    #endregion

    #region Gun Maintenance & Upgrades

    /// <summary>
    /// Upgrades all equipped guns matching the gunId (or all guns if gunId is null/empty).
    /// Returns the number of guns upgraded.
    /// </summary>
    public virtual int UpgradeGuns(string gunId, float dmgBonus = 0f, float rateBonus = 0f, float forceBonus = 0f, int burstBonus = 0)
    {
        int count = 0;
        List<Gun> guns = GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            Gun g = guns[i];
            if (g == null) continue;
            if (string.IsNullOrEmpty(gunId) || (g.Data != null && g.Data.gunId == gunId))
            {
                g.UpgradeStats(dmgBonus, rateBonus, forceBonus, burstBonus);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Specifically upgrades Kinetic guns equipped on this vessel.
    /// </summary>
    public int UpgradeKineticGuns(float dmgBonus = 1f, float forceBonus = 2f)
    {
        return UpgradeGuns(GunKinetic.DEFAULT_GUN_ID, dmgBonus, 0f, forceBonus, 0);
    }

    /// <summary>
    /// Specifically upgrades Plasma guns equipped on this vessel.
    /// </summary>
    public int UpgradePlasmaGuns(float dmgBonus = 0.5f, float rateBonus = 0.5f, int burstBonus = 0)
    {
        return UpgradeGuns(GunPlasma.DEFAULT_GUN_ID, dmgBonus, rateBonus, 0f, burstBonus);
    }

    /// <summary>
    /// Refills ammunition on all finite-ammo guns equipped on this vessel.
    /// </summary>
    public virtual void RefillAllWeaponsAmmo(int amount = -1)
    {
        List<Gun> guns = GetEquippedGuns();
        for (int i = 0; i < guns.Count; i++)
        {
            if (guns[i] == null) continue;
            if (amount < 0)
            {
                guns[i].RefillAmmo();
            }
            else
            {
                guns[i].AddAmmo(amount);
            }
        }
    }

    #endregion
}

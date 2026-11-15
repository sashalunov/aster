# Modular Weapon & Socket System Architecture

## 1. Overview & Motivation

In the initial implementation of the project, all weapon and socket management logic was embedded directly inside `Assets/Scripts/Player/player.cs` (~650 lines out of ~1,310 total lines). This included:
- Socket tracking, creation, and mounting/unmounting
- Weapon aiming and firing loops
- Player-specific stat progression (damage, force, fire rate) and upgrade points spending
- Weapon HUD event emission and floating UI feedback

As the project scales to introduce **bosses, elite ships, and multi-turret enemies**, duplicating socket management and weapon firing across different AI controllers is error-prone. 

This document outlines the decoupled architecture separating generic vessel combat capabilities (**`WeaponSystem`**) from player-specific progression (**`PlayerWeapons`**), while providing a clean integration path for upcoming boss encounters.

---

## 2. Architectural Hierarchy

```text
                     ┌─────────────────────────────────────────┐
                     │          WeaponSystem (Base)            │
                     │  (Assets/Scripts/Combat/WeaponSystem.cs)│
                     │  - Sockets & Guns discovery/storage     │
                     │  - MountGun / MountGunInstance / Detach │
                     │  - AddSocket / AddGun                   │
                     │  - AimWeapons / FireWeapons             │
                     │  - Per-socket firing & ammo refills     │
                     └────────────────────┬────────────────────┘
                                          │
                  ┌───────────────────────┴───────────────────────┐
                  │                                               │
                  ▼                                               ▼
   ┌───────────────────────────────┐               ┌──────────────────────────────┐
   │         PlayerWeapons         │               │     Boss / AI Integration    │
   │(Assets/Scripts/Player/        │               │   (e.g., BossAI, BossShip)   │
   │ PlayerWeapons.cs)             │               │                              │
   │  - Player stat attributes     │               │  - Uses WeaponSystem base    │
   │    (_bullet_dmg, force, rate) │               │  - Phase-based firing logic  │
   │  - SpendUpgradePoints / Upgr. │               │  - Staggered multi-socket    │
   │  - Gun stat synchronization   │               │    salvos & barrage patterns │
   │  - OnWeaponStatsChanged event │               │  - Target lock on player     │
   │  - Floating combat text HUD   │               └──────────────────────────────┘
   └──────────────┬────────────────┘
                  │
                  ▼
   ┌───────────────────────────────┐
   │           player.cs           │
   │  - Slimmed down core          │
   │  - Weapons property façade    │
   │  - Backward-compatible API    │
   │    forwarding                 │
   └───────────────────────────────┘
```

---

## 3. Class Responsibility Breakdown

### A. `WeaponSystem` (Base `MonoBehaviour`)
**Location:** `Assets/Scripts/Combat/WeaponSystem.cs`  
**Purpose:** Reusable for *any* vessel (Player, Boss, AI Elite, Station) with weapon sockets.

* **State & Configuration:**
  * `List<GunSocket> _sockets`: Serialized/discovered socket mounts.
  * `List<Gun> _guns`: Direct or standalone guns attached to the vessel.
  * `Transform _gunsContainer`: Root container for dynamically spawned sockets/guns.
  * `int maxGuns`: Maximum capacity of weapons (default: 8).
* **Socket & Gun Lifecycle:**
  * `HasSockets(bool includeInactive)`: Checks if vessel has any socket.
  * `HasAvailableSocket(bool includeInactive)`: Checks for unoccupied sockets.
  * `HasGuns(bool includeInactive)`: Checks if any gun is mounted or equipped.
  * `GetSockets(bool includeInactive)`: Returns all unique `GunSocket` instances.
  * `GetEquippedGuns(bool includeInactive)`: Returns all mounted/attached `Gun` instances.
  * `AddSocket(string socketId, Vector3? localPosition)`: Dynamically creates and tracks a new socket.
  * `MountGun(Gun gunPrefab, int socketIndex)`: Instantiates and attaches a gun prefab to a socket.
  * `MountGunInstance(Gun gunInstance, int socketIndex)`: Mounts an existing gun GameObject.
  * `DetachGun(int socketIndex)`: Detaches and returns the gun mounted at a socket.
  * `AddGun(Gun gunPrefab)`: Auto-selects an empty socket, creates one if below capacity, or attaches to container.
* **Targeting & Combat:**
  * `AimWeapons(Vector3 targetWorldPosition)`: Rotates all mounted socket guns and direct guns toward the target point.
  * `FireWeapons()`: Fires all ready weapons simultaneously.
  * `FireSocket(int socketIndex)`: Fires a specific socket (enabling staggered salvos and burst sequencing for bosses).
* **Maintenance & Base Upgrades:**
  * `RefillAllWeaponsAmmo(int amount)`: Restores ammunition on finite-ammo weapons.
  * `UpgradeGuns(string gunId, float dmgBonus, float rateBonus, float forceBonus, int burstBonus)`: Modifies base stats across all matching equipped guns.

---

### B. `PlayerWeapons` (Subclass of `WeaponSystem`)
**Location:** `Assets/Scripts/Player/PlayerWeapons.cs`  
**Purpose:** Player-specific weapon progression, tuning stats, and UI feedback.

* **Player Base Combat Stats:**
  * `float _bullet_dmg` (default: 1.0)
  * `float _bullet_force` (default: 1.0)
  * `float _fire_hz` (default: 1.0)
  * `float _fire_rate` (default: 1.0)
* **Upgrade Point Allocation:**
  * `SpendUpgradePoints(WeaponStatType stat, int pointsCost, string gunId)`
  * `UpgradeDamageWithPoints(int pointsCost, string gunId)`
  * `UpgradeForceWithPoints(int pointsCost, string gunId)`
  * `UpgradeFireRateWithPoints(int pointsCost, string gunId)`
  * Specific helpers: `UpgradeKineticGuns()`, `UpgradePlasmaGuns()`
* **Gun Synchronization:**
  * `SyncGunWithPlayerUpgrades(Gun g)`: Ensures newly mounted guns receive the player's accumulated powerup/meta bonuses.
  * `SyncAllEquippedGunsWithPlayerUpgrades()`: Re-synchronizes all current guns after meta-progression or stat changes.
* **HUD & Feedback Integration:**
  * `event Action<float, float, float> OnWeaponStatsChanged`: Broadcasts `(force, fireRateInterval, damage)` to HUD.
  * `UpdateWeaponHUD()`: Calculates effective lead weapon stats and fires the event.
  * Triggers floating combat texts via `PowerupManager.Instance.SpawnFloatingFeedback(...)`.

---

### C. `player.cs` (Integration & Façade)
**Location:** `Assets/Scripts/Player/player.cs`

`player.cs` delegates all weapon tasks to its attached `PlayerWeapons` component while maintaining existing method signatures so external callers (`PowerupManager`, `StandardPowerup`, `DebugCheats`, `PlayerMetaProgression`) require zero changes.

```csharp
[RequireComponent(typeof(PlayerWeapons))]
public class player : MonoBehaviour
{
    private PlayerWeapons _weapons;
    public PlayerWeapons Weapons
    {
        get
        {
            if (_weapons == null)
            {
                _weapons = GetComponent<PlayerWeapons>();
                if (_weapons == null)
                {
                    _weapons = gameObject.AddComponent<PlayerWeapons>();
                }
            }
            return _weapons;
        }
    }

    // Forwarding methods for seamless backward compatibility
    public List<Gun> GetEquippedGuns(bool includeInactive = true) => Weapons.GetEquippedGuns(includeInactive);
    public List<GunSocket> GetSockets(bool includeInactive = true) => Weapons.GetSockets(includeInactive);
    public bool AddGun(Gun gunPrefab = null) => Weapons.AddGun(gunPrefab);
    public bool MountGun(Gun gunPrefab, int socketIndex = -1) => Weapons.MountGun(gunPrefab, socketIndex);
    public void AimWeapons(Vector3 targetWorldPosition) => Weapons.AimWeapons(targetWorldPosition);
    public bool FireWeapons() => Weapons.FireWeapons();
    public int UpgradeGuns(string gunId, float dmg = 0f, float rate = 0f, float force = 0f, int burst = 0) 
        => Weapons.UpgradeGuns(gunId, dmg, rate, force, burst);
    public void RefillAllWeaponsAmmo(int amount = -1) => Weapons.RefillAllWeaponsAmmo(amount);
    public bool SpendUpgradePoints(WeaponStatType stat, int pointsCost = 1, string gunId = null) 
        => Weapons.SpendUpgradePoints(stat, pointsCost, gunId);
}
```

---

## 4. Boss & Enemy Ship Integration Guide

When creating a multi-gun boss ship or carrier:

1. **Attach `WeaponSystem` directly to the Boss GameObject.**
2. Place child GameObjects on the boss model for weapon mounting points, each having a `GunSocket` component.
3. Configure `_sockets` or allow `WeaponSystem` to auto-discover child sockets in `Awake()`.
4. In the Boss AI script (`BossAI.cs`), drive aiming and firing via `WeaponSystem`:

```csharp
using UnityEngine;

public class BossAI : MonoBehaviour
{
    [SerializeField] private WeaponSystem _weapons;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private float _fireInterval = 0.4f;

    private float _fireTimer = 0f;
    private int _staggerSocketIndex = 0;

    private void Awake()
    {
        if (_weapons == null)
        {
            _weapons = GetComponent<WeaponSystem>();
        }
    }

    private void Update()
    {
        if (_playerTransform == null) return;

        // 1. Aim all boss sockets toward the player
        _weapons.AimWeapons(_playerTransform.position);

        // 2. Multi-socket staggered firing pattern
        _fireTimer += Time.deltaTime;
        if (_fireTimer >= _fireInterval)
        {
            _fireTimer = 0f;

            // Option A: Full broadside barrage
            // _weapons.FireWeapons();

            // Option B: Sequential / alternating socket fire
            int totalSockets = _weapons.SocketCount;
            if (totalSockets > 0)
            {
                _weapons.FireSocket(_staggerSocketIndex % totalSockets);
                _staggerSocketIndex++;
            }
        }
    }
}
```

---

## 5. Serialization & Prefab Migration Strategy

To avoid breaking existing player ship prefabs in `Assets/Prefabs/` (such as `lola1.prefab`, `lola2.prefab`, etc.) that currently have serialized `_sockets` and `_guns_container` assigned directly on the `player` component:

1. **Automatic Fallback Migration in `PlayerWeapons.Awake()`**:
   ```csharp
   private void Awake()
   {
       player p = GetComponent<player>();
       if (p != null)
       {
           // Migrate serialized references if PlayerWeapons is uninitialized
           if ((_sockets == null || _sockets.Count == 0) && p._sockets != null && p._sockets.Count > 0)
           {
               _sockets = new List<GunSocket>(p._sockets);
           }
           if (_gunsContainer == null && p._guns_container != null)
           {
               _gunsContainer = p._guns_container;
           }
       }
   }
   ```
2. **Editor Menu Utility**:
   An Editor script (e.g., `Tools > Weapons > Migrate Player Prefabs to PlayerWeapons`) can iterate player prefabs, add `PlayerWeapons`, copy references, and mark prefabs dirty for clean project asset serialization.

---

## 6. Implementation Checklist

- [ ] Create `Assets/Scripts/Combat/WeaponSystem.cs` (base class for sockets, mounting, aiming, firing).
- [ ] Create `Assets/Scripts/Player/PlayerWeapons.cs` (player subclass for upgrades, HUD sync, stat tracking).
- [ ] Update `Assets/Scripts/Player/player.cs` to use `PlayerWeapons` and provide backward-compatible facades.
- [ ] Verify that external systems (`PowerupManager`, `StandardPowerup`, `DebugCheats`, `PlayerMetaProgression`) compile and operate seamlessly.
- [ ] Add unit tests in `Assets/Editor/Tests/WeaponSystemTests.cs` to test socket mounting, aiming, firing, and upgrade synchronization.

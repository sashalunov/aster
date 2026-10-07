using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development cheat and debugging component to quickly inspect and accelerate gameplay progression.
/// Provides functions to add XP, health, shields, weapons, and upgrade points via code, hotkeys, GUI, and Inspector context menus.
/// </summary>
[DisallowMultipleComponent]
public class DebugCheats : MonoBehaviour
{
    private static DebugCheats _instance;
    public static DebugCheats Instance => _instance;

    [Header("Target & Configuration")]
    [Tooltip("Target player component to apply cheats to. Auto-resolves if left empty.")]
    [SerializeField] private player targetPlayer;

    [Tooltip("Whether keyboard shortcuts are active.")]
    [SerializeField] private bool enableHotkeys = true;

    [Tooltip("Whether to display the on-screen debug cheat GUI overlay.")]
    [SerializeField] private bool showGui = false;

    [Header("Hotkeys Configuration")]
    [SerializeField] private KeyCode toggleGuiKey = KeyCode.BackQuote;
    [SerializeField] private KeyCode toggleGuiKeyAlt = KeyCode.F1;
    [SerializeField] private KeyCode addXpKey = KeyCode.F2;
    [SerializeField] private KeyCode addBigXpKey = KeyCode.F3;
    [SerializeField] private KeyCode addPointsKey = KeyCode.F4;
    [SerializeField] private KeyCode addKineticKey = KeyCode.F5;
    [SerializeField] private KeyCode addPlasmaKey = KeyCode.F6;
    [SerializeField] private KeyCode addFlakKey = KeyCode.F7;
    [SerializeField] private KeyCode fullHealKey = KeyCode.F8;
    [SerializeField] private KeyCode refillAmmoKey = KeyCode.F9;
    [SerializeField] private KeyCode toggleGodModeKey = KeyCode.F10;

    private Rect _windowRect = new Rect(10, 10, 260, 420);

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        ResolvePlayer();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void ResolvePlayer()
    {
        if (targetPlayer == null)
        {
            targetPlayer = GetComponent<player>();
        }
        if (targetPlayer == null)
        {
            targetPlayer = FindAnyObjectByType<player>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleGuiKey) || Input.GetKeyDown(toggleGuiKeyAlt))
        {
            showGui = !showGui;
        }

        if (!enableHotkeys) return;

        if (Input.GetKeyDown(addXpKey)) AddXP(100);
        if (Input.GetKeyDown(addBigXpKey)) AddXP(500);
        if (Input.GetKeyDown(addPointsKey)) AddUpgradePoints(5);
        if (Input.GetKeyDown(addKineticKey)) AddKineticGun();
        if (Input.GetKeyDown(addPlasmaKey)) AddPlasmaGun();
        if (Input.GetKeyDown(addFlakKey)) AddFlakGun();
        if (Input.GetKeyDown(fullHealKey)) FullRestore();
        if (Input.GetKeyDown(refillAmmoKey)) RefillAmmo();
        if (Input.GetKeyDown(toggleGodModeKey)) ToggleGodMode();
    }

    #region Cheat Functions

    /// <summary>
    /// Adds the specified amount of XP to the player and triggers progression thresholds.
    /// </summary>
    [ContextMenu("Cheat: Add 100 XP")]
    public void AddXP100() => AddXP(100);

    [ContextMenu("Cheat: Add 500 XP")]
    public void AddXP500() => AddXP(500);

    public void AddXP(int amount)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddXP(amount);
            Debug.Log($"[DebugCheats] Added {amount} XP. Total XP: {targetPlayer._xp_value}");
        }
        else
        {
            Debug.LogWarning("[DebugCheats] No target player found to add XP.");
        }
    }

    /// <summary>
    /// Restores or adds health to the player ship hull.
    /// </summary>
    [ContextMenu("Cheat: Add 5 Health")]
    public void AddHealth5() => AddHealth(5f);

    public void AddHealth(float amount)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddHealth(amount);
            Debug.Log($"[DebugCheats] Added {amount} Health. Current: {targetPlayer.health_value}/{targetPlayer.health_max_value}");
        }
        else
        {
            Debug.LogWarning("[DebugCheats] No target player found to add health.");
        }
    }

    /// <summary>
    /// Restores or adds shield energy to the player.
    /// </summary>
    [ContextMenu("Cheat: Add 5 Shield")]
    public void AddShield5() => AddShield(5f);

    public void AddShield(float amount)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddShield(amount);
            Debug.Log($"[DebugCheats] Added {amount} Shield. Current: {targetPlayer.shield_value}/{targetPlayer.shield_max_value}");
        }
        else
        {
            Debug.LogWarning("[DebugCheats] No target player found to add shield.");
        }
    }

    /// <summary>
    /// Fully restores both health and shield to maximum capacity.
    /// </summary>
    [ContextMenu("Cheat: Full Restore (Health & Shield)")]
    public void FullRestore()
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.health_value = targetPlayer.health_max_value;
            targetPlayer.shield_value = targetPlayer.shield_max_value;
            targetPlayer.UpdateHealthHUD();
            targetPlayer.UpdateShieldHUD();
            Debug.Log("[DebugCheats] Full restore applied (Health & Shield at 100%).");
        }
    }

    /// <summary>
    /// Equips or mounts a gun by technical name or archetype.
    /// Supports 'gunKinetic', 'gunPlasma', 'gunFlak', 'kinetic', 'plasma', 'flak'.
    /// </summary>
    public bool AddGun(string gunType)
    {
        ResolvePlayer();
        if (targetPlayer == null)
        {
            Debug.LogWarning("[DebugCheats] No target player found to equip gun.");
            return false;
        }

        string cleanName = gunType.ToLowerInvariant();
        if (cleanName.Contains("kinetic"))
        {
            return AddKineticGun();
        }
        else if (cleanName.Contains("plasma"))
        {
            return AddPlasmaGun();
        }
        else if (cleanName.Contains("flak"))
        {
            return AddFlakGun();
        }

        // Generic fallback via PowerupManager
        if (PowerupManager.Instance != null)
        {
            return PowerupManager.Instance.ApplyGun(targetPlayer, gunType);
        }
        return false;
    }

    /// <summary>
    /// Mounts a Kinetic Cannon onto the player ship.
    /// </summary>
    [ContextMenu("Cheat: Add Kinetic Gun")]
    public bool AddKineticGun()
    {
        ResolvePlayer();
        if (targetPlayer == null) return false;

        Gun prefab = PrefabManager.Get<Gun>(PrefabId.GunKinetic);
        bool success = targetPlayer.AddGun(prefab);
        Debug.Log($"[DebugCheats] Add Kinetic Gun: {(success ? "Success" : "Failed (Limit Reached or Missing Prefab)")}");
        return success;
    }

    /// <summary>
    /// Mounts a Plasma Repeater onto the player ship.
    /// </summary>
    [ContextMenu("Cheat: Add Plasma Gun")]
    public bool AddPlasmaGun()
    {
        ResolvePlayer();
        if (targetPlayer == null) return false;

        Gun prefab = PrefabManager.Get<Gun>(PrefabId.GunPlasma);
        bool success = targetPlayer.AddGun(prefab);
        Debug.Log($"[DebugCheats] Add Plasma Gun: {(success ? "Success" : "Failed (Limit Reached or Missing Prefab)")}");
        return success;
    }

    /// <summary>
    /// Mounts a Flak Cannon onto the player ship.
    /// </summary>
    [ContextMenu("Cheat: Add Flak Gun")]
    public bool AddFlakGun()
    {
        ResolvePlayer();
        if (targetPlayer == null) return false;

        Gun prefab = PrefabManager.Get<Gun>(PrefabId.GunFlak);
        bool success = targetPlayer.AddGun(prefab);
        Debug.Log($"[DebugCheats] Add Flak Gun: {(success ? "Success" : "Failed (Limit Reached or Missing Prefab)")}");
        return success;
    }

    /// <summary>
    /// Grants upgrade points to the player.
    /// </summary>
    [ContextMenu("Cheat: Add 5 Upgrade Points")]
    public void AddUpgradePoints5() => AddUpgradePoints(5);

    public void AddUpgradePoints(int amount)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddUpgradePoints(amount);
            Debug.Log($"[DebugCheats] Added {amount} upgrade points. Total: {targetPlayer.UpgradePoints}");
        }
    }

    /// <summary>
    /// Directly boosts weapon damage stat.
    /// </summary>
    [ContextMenu("Cheat: Upgrade Damage (+1)")]
    public void UpgradeDamage(int points = 1)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddUpgradePoints(points);
            targetPlayer.UpgradeDamageWithPoints(points);
            Debug.Log($"[DebugCheats] Upgraded Damage by {points}. Player Bullet Dmg: {targetPlayer._bullet_dmg}");
        }
    }

    /// <summary>
    /// Directly boosts weapon fire rate stat.
    /// </summary>
    [ContextMenu("Cheat: Upgrade Fire Rate (+1)")]
    public void UpgradeFireRate(int points = 1)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddUpgradePoints(points);
            targetPlayer.UpgradeFireRateWithPoints(points);
            Debug.Log($"[DebugCheats] Upgraded Fire Rate by {points}. Player Fire Hz: {targetPlayer._fire_hz}");
        }
    }

    /// <summary>
    /// Directly boosts projectile impulse force stat.
    /// </summary>
    [ContextMenu("Cheat: Upgrade Force (+1)")]
    public void UpgradeForce(int points = 1)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.AddUpgradePoints(points);
            targetPlayer.UpgradeForceWithPoints(points);
            Debug.Log($"[DebugCheats] Upgraded Force by {points}. Player Force: {targetPlayer._bullet_force}");
        }
    }

    /// <summary>
    /// Refills all finite-ammo weapons currently mounted on the player.
    /// </summary>
    [ContextMenu("Cheat: Refill All Ammo")]
    public void RefillAmmo(int amount = -1)
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.RefillAllWeaponsAmmo(amount);
            Debug.Log("[DebugCheats] Refilled ammunition on all weapons.");
        }
    }

    /// <summary>
    /// Toggles invulnerability / god mode on the player.
    /// </summary>
    [ContextMenu("Cheat: Toggle God Mode")]
    public void ToggleGodMode()
    {
        ResolvePlayer();
        if (targetPlayer != null)
        {
            targetPlayer.isInvulnerable = !targetPlayer.isInvulnerable;
            Debug.Log($"[DebugCheats] God Mode: {(targetPlayer.isInvulnerable ? "ENABLED" : "DISABLED")}");
        }
    }

    #endregion

    #region GUI Overlay

    private void OnGUI()
    {
        if (!showGui) return;

        GUI.color = Color.white;
        _windowRect = GUI.Window(9999, _windowRect, DrawCheatWindow, "DEBUG CHEATS [~ / F1]");
    }

    private void DrawCheatWindow(int windowID)
    {
        ResolvePlayer();

        GUILayout.BeginVertical();

        if (targetPlayer != null)
        {
            GUILayout.Label($"HP: {(int)targetPlayer.health_value}/{(int)targetPlayer.health_max_value} | SH: {(int)targetPlayer.shield_value}/{(int)targetPlayer.shield_max_value}");
            GUILayout.Label($"XP: {targetPlayer._xp_value} | PTS: {targetPlayer.UpgradePoints} | GUNS: {targetPlayer.GunCount}");
            if (targetPlayer.isInvulnerable)
            {
                GUILayout.Label("<color=yellow><b>★ GOD MODE ACTIVE ★</b></color>");
            }
        }
        else
        {
            GUILayout.Label("<color=red>No player found</color>");
        }

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+100 XP (F2)")) AddXP(100);
        if (GUILayout.Button("+500 XP (F3)")) AddXP(500);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 Health")) AddHealth(1f);
        if (GUILayout.Button("+5 Health")) AddHealth(5f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+5 Shield")) AddShield(5f);
        if (GUILayout.Button("Full Restore (F8)")) FullRestore();
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+Kinetic (F5)")) AddKineticGun();
        if (GUILayout.Button("+Plasma (F6)")) AddPlasmaGun();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+Flak (F7)")) AddFlakGun();
        if (GUILayout.Button("Refill Ammo (F9)")) RefillAmmo();
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+5 Upgrade Pts (F4)")) AddUpgradePoints(5);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+DMG")) UpgradeDamage(1);
        if (GUILayout.Button("+RATE")) UpgradeFireRate(1);
        if (GUILayout.Button("+FORCE")) UpgradeForce(1);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        string godModeLabel = (targetPlayer != null && targetPlayer.isInvulnerable) ? "Disable God Mode (F10)" : "Enable God Mode (F10)";
        if (GUILayout.Button(godModeLabel))
        {
            ToggleGodMode();
        }

        if (GUILayout.Button("Close Overlay (~ / F1)"))
        {
            showGui = false;
        }

        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    #endregion
}

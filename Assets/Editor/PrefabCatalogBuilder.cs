using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PrefabCatalogBuilder
{
    public const string CatalogPath = "Assets/Data/GamePrefabCatalog.asset";

    [MenuItem("Aster/Catalog/Sync Prefab Catalog")]
    public static PrefabCatalog SyncCatalog()
    {
        string dir = Path.GetDirectoryName(CatalogPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        PrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // Map known PrefabIds to primary prefab names
        var idToName = new Dictionary<PrefabId, string>
        {
            { PrefabId.PlayerShip, "Player_ship" },
            { PrefabId.UltraDeath, "UltraDeath" },
            { PrefabId.Cross1Marker, "cross1_marker" },
            { PrefabId.TapMarker, "tap_marker" },
            { PrefabId.AsteroidGrid, "AsteroidGrid" },
            { PrefabId.AsteroidBase, "AsteroidBase" },
            { PrefabId.BlockAsteroid, "BlockAsteroid" },
            { PrefabId.BlockAsteroidCore, "BlockAsteroidCore" },
            { PrefabId.GunKinetic, "gunKinetic" },
            { PrefabId.GunPlasma, "gunPlasma" },
            { PrefabId.GunFlak, "gunFlak" },
            { PrefabId.BulletKinetic, "bulletKinetic" },
            { PrefabId.BulletPlasma, "bulletPlasma" },
            { PrefabId.BulletEnemy, "bulletAI" },
            { PrefabId.BlockHitFx, "show_blockhit" },
            { PrefabId.CoreHitFx, "show_corehit" },
            { PrefabId.BlockDestroyFx, "blockdestroy" },
            { PrefabId.MuzzleFlashFx, "ps_muzzle" },
            { PrefabId.ShieldDamageFx, "shield_damage" },
            { PrefabId.BonusPowerupFx, "ps_bonus_pwrup" },
            { PrefabId.AdditiveBonusFx, "additive_bonus" },
            { PrefabId.ThrusterFlameFx, "thrusterflame" },
            { PrefabId.ShowUpgradeFx, "show_upgrade" },
            { PrefabId.ShieldUpgradeFx, "shield_upgrade" },
            { PrefabId.PowerupDefault, "powerup" },
            { PrefabId.PowerupShield, "pwpShieldUp" },
            { PrefabId.PowerupAmmo, "pwpAmmo" },
            { PrefabId.PowerupUpgradePoint, "pwpUpgradePoint" },
            { PrefabId.PowerupBulletSpeed, "pup_bullet_speed1" },
            { PrefabId.PowerupXP, "pwpXP" },
            { PrefabId.PowerupGunKinetic, "pwpGunKinetic" },
            { PrefabId.PowerupGunPlasma, "pwpGunPlasma" },
            { PrefabId.LightTankA, "lightTank_a" },
            { PrefabId.LightTankB, "lightTank_b" },
            { PrefabId.TurretGun0, "AI_gun0" },
            { PrefabId.AsteroidFieldSpawner, "AsteroidFieldSpawner" }
        };

        var entries = new List<PrefabCatalog.Entry>();
        var registeredPrefabs = new HashSet<GameObject>();

        // 1. Register explicit PrefabId entries
        foreach (var pair in idToName)
        {
            GameObject prefab = FindPrefabByName(pair.Value);
            if (prefab != null)
            {
                entries.Add(new PrefabCatalog.Entry(pair.Key, prefab, pair.Value));
                registeredPrefabs.Add(prefab);
            }
            else
            {
                Debug.LogWarning($"[PrefabCatalogBuilder] Prefab not found for {pair.Key} ('{pair.Value}')");
            }
        }

        // 2. Discover any other prefabs under Assets/Prefabs and Assets/Resources
        string[] searchFolders = new[] { "Assets/Prefabs", "Assets/Resources" };
        string[] allGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && !registeredPrefabs.Contains(go))
            {
                entries.Add(new PrefabCatalog.Entry(PrefabId.None, go, go.name));
                registeredPrefabs.Add(go);
            }
        }

        catalog.SetEntries(entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // 3. Ensure catalog is in PlayerSettings.PreloadedAssets
        var preloadedList = PlayerSettings.GetPreloadedAssets().ToList();
        if (!preloadedList.Contains(catalog))
        {
            preloadedList.Add(catalog);
            PlayerSettings.SetPreloadedAssets(preloadedList.ToArray());
        }

        // 4. Activate in runtime manager
        PrefabManager.RegisterCatalog(catalog);

        Debug.Log($"[PrefabCatalogBuilder] Successfully synced PrefabCatalog with {entries.Count} entries.");
        return catalog;
    }

    private static GameObject FindPrefabByName(string name)
    {
        string[] searchFolders = new[] { "Assets/Prefabs", "Assets/Resources" };
        string[] guids = AssetDatabase.FindAssets($"{name} t:Prefab", searchFolders);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        return null;
    }
}

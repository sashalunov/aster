using System;
using UnityEngine;

public static class PrefabManager
{
    private static PrefabCatalog _activeCatalog;

    public static bool IsInitialized => _activeCatalog != null;
    public static PrefabCatalog ActiveCatalog => _activeCatalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureCatalogLoaded();
    }

    public static void RegisterCatalog(PrefabCatalog catalog)
    {
        if (catalog != null)
        {
            _activeCatalog = catalog;
            _activeCatalog.InitializeCache();
        }
    }

    public static void SetCatalog(PrefabCatalog catalog)
    {
        RegisterCatalog(catalog);
    }

    public static void ResetForTesting()
    {
        _activeCatalog = null;
    }

    public static PrefabCatalog EnsureCatalogLoaded()
    {
        if (_activeCatalog != null) return _activeCatalog;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PrefabCatalog");
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<PrefabCatalog>(path);
            if (catalog != null)
            {
                RegisterCatalog(catalog);
                return _activeCatalog;
            }
        }
#endif

        return _activeCatalog;
    }

    public static GameObject Get(PrefabId id)
    {
        if (id == PrefabId.None) return null;

        EnsureCatalogLoaded();
        if (_activeCatalog != null)
        {
            var go = _activeCatalog.Get(id);
            if (go != null) return go;
        }

        // Interim fallback to Resources while migrating legacy code
        return FallbackResourceLoad(id.ToString());
    }

    public static GameObject Get(string nameOrKey)
    {
        if (string.IsNullOrEmpty(nameOrKey)) return null;

        EnsureCatalogLoaded();
        if (_activeCatalog != null)
        {
            var go = _activeCatalog.Get(nameOrKey);
            if (go != null) return go;
        }

        // Interim fallback to Resources while migrating legacy code
        return FallbackResourceLoad(nameOrKey);
    }

    public static bool TryGet(PrefabId id, out GameObject prefab)
    {
        prefab = Get(id);
        return prefab != null;
    }

    public static bool TryGet(string nameOrKey, out GameObject prefab)
    {
        prefab = Get(nameOrKey);
        return prefab != null;
    }

    public static T Get<T>(PrefabId id) where T : Component
    {
        var go = Get(id);
        return go != null ? go.GetComponent<T>() : null;
    }

    public static T Get<T>(string nameOrKey) where T : Component
    {
        var go = Get(nameOrKey);
        return go != null ? go.GetComponent<T>() : null;
    }

    public static GameObject Instantiate(PrefabId id, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        GameObject prefab = Get(id);
        if (prefab == null)
        {
            Debug.LogWarning($"[PrefabManager] Failed to instantiate: PrefabId.{id} was not found.");
            return null;
        }
        return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
    }

    public static GameObject Instantiate(string nameOrKey, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        GameObject prefab = Get(nameOrKey);
        if (prefab == null)
        {
            Debug.LogWarning($"[PrefabManager] Failed to instantiate: '{nameOrKey}' was not found.");
            return null;
        }
        return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
    }

    public static T Instantiate<T>(PrefabId id, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        GameObject go = Instantiate(id, position, rotation, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    public static T Instantiate<T>(string nameOrKey, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        GameObject go = Instantiate(nameOrKey, position, rotation, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    private static GameObject FallbackResourceLoad(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        return Resources.Load<GameObject>(resourceName);
    }
}

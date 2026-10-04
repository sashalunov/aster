using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GamePrefabCatalog", menuName = "Aster/Catalog/Prefab Catalog")]
public class PrefabCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public PrefabId id;
        public string customKey;
        public GameObject prefab;

        public Entry(PrefabId id, GameObject prefab, string customKey = null)
        {
            this.id = id;
            this.prefab = prefab;
            this.customKey = customKey;
        }
    }

    [SerializeField]
    private List<Entry> entries = new List<Entry>();

    private Dictionary<PrefabId, GameObject> _idCache;
    private Dictionary<string, GameObject> _nameCache;

    public IReadOnlyList<Entry> Entries => entries;

    private void OnEnable()
    {
        InitializeCache();
        PrefabManager.RegisterCatalog(this);
    }

    public void InitializeCache()
    {
        _idCache = new Dictionary<PrefabId, GameObject>();
        _nameCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.prefab == null) continue;

            if (entry.id != PrefabId.None && !_idCache.ContainsKey(entry.id))
            {
                _idCache[entry.id] = entry.prefab;
            }

            // Index by prefab name
            string prefabName = entry.prefab.name;
            if (!_nameCache.ContainsKey(prefabName))
            {
                _nameCache[prefabName] = entry.prefab;
            }

            // Index by enum name
            if (entry.id != PrefabId.None)
            {
                string idName = entry.id.ToString();
                if (!_nameCache.ContainsKey(idName))
                {
                    _nameCache[idName] = entry.prefab;
                }
            }

            // Index by custom key if specified
            if (!string.IsNullOrEmpty(entry.customKey) && !_nameCache.ContainsKey(entry.customKey))
            {
                _nameCache[entry.customKey] = entry.prefab;
            }
        }
    }

    public GameObject Get(PrefabId id)
    {
        if (_idCache == null) InitializeCache();
        return _idCache.TryGetValue(id, out var prefab) ? prefab : null;
    }

    public GameObject Get(string nameOrKey)
    {
        if (string.IsNullOrEmpty(nameOrKey)) return null;
        if (_nameCache == null) InitializeCache();
        return _nameCache.TryGetValue(nameOrKey, out var prefab) ? prefab : null;
    }

    public bool TryGet(PrefabId id, out GameObject prefab)
    {
        prefab = Get(id);
        return prefab != null;
    }

    public bool TryGet(string nameOrKey, out GameObject prefab)
    {
        prefab = Get(nameOrKey);
        return prefab != null;
    }

    public T Get<T>(PrefabId id) where T : Component
    {
        var go = Get(id);
        return go != null ? go.GetComponent<T>() : null;
    }

    public T Get<T>(string nameOrKey) where T : Component
    {
        var go = Get(nameOrKey);
        return go != null ? go.GetComponent<T>() : null;
    }

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
        InitializeCache();
    }

    public void AddEntry(PrefabId id, GameObject prefab, string customKey = null)
    {
        if (prefab == null) return;
        entries.Add(new Entry(id, prefab, customKey));
        InitializeCache();
    }

    public void ClearEntries()
    {
        entries.Clear();
        InitializeCache();
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PlayerLevelDropdownAttribute))]
public class PlayerLevelDropdownDrawer : PropertyDrawer
{
    private static string[] _options;
    private static int[] _values;
    private static ulong[] _lastThresholds;

    private static void RefreshOptionsIfNeeded()
    {
        ulong[] thresholds = ResolveThresholds();

        // Check if thresholds changed since last draw
        if (_options != null && AreThresholdsEqual(_lastThresholds, thresholds))
        {
            return;
        }

        _lastThresholds = thresholds != null ? (ulong[])thresholds.Clone() : null;

        if (thresholds == null || thresholds.Length == 0)
        {
            _options = new string[] { "None (Use Raw XP)" };
            _values = new int[] { 0 };
            return;
        }

        var optList = new List<string> { "None (Use Raw XP)" };
        var valList = new List<int> { 0 };

        for (int i = 0; i < thresholds.Length; i++)
        {
            // 1-based level display: reaching thresholds[0] advances player from Level 1 to Level 2
            optList.Add($"Level {i + 2} ({thresholds[i]} XP)");
            valList.Add(i + 1);
        }

        _options = optList.ToArray();
        _values = valList.ToArray();
    }

    private static ulong[] ResolveThresholds()
    {
        // 1. Try scene instance first (picks up scene overrides)
        var sceneProg = Object.FindAnyObjectByType<PlayerProgression>();
        if (sceneProg != null && sceneProg.levelUps != null && sceneProg.levelUps.Length > 0)
        {
            return sceneProg.levelUps;
        }

        // 2. Try Player_ship prefab
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Player_ship.prefab");
        if (playerPrefab != null)
        {
            var prefabProg = playerPrefab.GetComponentInChildren<PlayerProgression>(true);
            if (prefabProg != null && prefabProg.levelUps != null && prefabProg.levelUps.Length > 0)
            {
                return prefabProg.levelUps;
            }
        }

        // 3. Fallback: Search all prefabs for any PlayerProgression
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                var prog = go.GetComponentInChildren<PlayerProgression>(true);
                if (prog != null && prog.levelUps != null && prog.levelUps.Length > 0)
                {
                    return prog.levelUps;
                }
            }
        }

        // 4. Default fallback: instantiate temporary instance
        var tempGo = new GameObject();
        var tempProg = tempGo.AddComponent<PlayerProgression>();
        ulong[] defaults = tempProg.levelUps;
        Object.DestroyImmediate(tempGo);
        return defaults;
    }

    private static bool AreThresholdsEqual(ulong[] a, ulong[] b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null) return;
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.LabelField(position, label, new GUIContent("Use [PlayerLevelDropdown] with int."));
            return;
        }

        RefreshOptionsIfNeeded();

        int currentValue = property.intValue;
        int selectedIndex = 0;

        for (int i = 0; i < _values.Length; i++)
        {
            if (_values[i] == currentValue)
            {
                selectedIndex = i;
                break;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, _options);
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = _values[newIndex];
        }
        EditorGUI.EndProperty();
    }
}

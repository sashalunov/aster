using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PlayerLevelDropdownAttribute))]
public class PlayerLevelDropdownDrawer : PropertyDrawer
{
    private static string[] _options;
    private static int[] _values;

    private static void InitializeOptions()
    {
        if (_options != null) return;

        // Instantiate a temporary object to read defaults from PlayerProgression
        var go = new GameObject();
        var prog = go.AddComponent<PlayerProgression>();
        ulong[] thresholds = prog.levelUps;
        Object.DestroyImmediate(go);

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
            optList.Add($"Level {i + 1} ({thresholds[i]} XP)");
            valList.Add(i + 1);
        }

        _options = optList.ToArray();
        _values = valList.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.LabelField(position, label.text, "Use [PlayerLevelDropdown] with int.");
            return;
        }

        InitializeOptions();

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

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, _options);
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = _values[newIndex];
        }
    }
}

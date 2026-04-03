#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMovement))]
public class PlayerMovementEditor : Editor
{
    private sealed class Section
    {
        public string Name;
        public readonly List<string> PropertyNames = new();
    }

    private const string DefaultSectionName = "General";
    private const string FoldoutPrefsPrefix = "PlayerMovementEditor.Foldout.";
    private static readonly Dictionary<string, bool> FoldoutStates = new();
    private readonly List<Section> sections = new();

    private void OnEnable()
    {
        BuildSections();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        for (int i = 0; i < sections.Count; i++)
        {
            Section section = sections[i];
            if (!TryGetFoldoutState(section.Name, out bool expanded))
                expanded = true;

            expanded = EditorGUILayout.Foldout(expanded, section.Name, true, EditorStyles.foldoutHeader);
            SetFoldoutState(section.Name, expanded);

            if (!expanded)
                continue;

            EditorGUI.indentLevel++;
            for (int p = 0; p < section.PropertyNames.Count; p++)
            {
                SerializedProperty prop = serializedObject.FindProperty(section.PropertyNames[p]);
                if (prop != null)
                    EditorGUILayout.PropertyField(prop, true);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4f);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void BuildSections()
    {
        sections.Clear();

        Dictionary<string, Section> byName = new();

        string currentHeader = DefaultSectionName;
        EnsureSection(currentHeader);

        FieldInfo[] fields = typeof(PlayerMovement).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!IsSerializedField(field))
                continue;

            HeaderAttribute header = field.GetCustomAttribute<HeaderAttribute>(true);
            if (header != null && !string.IsNullOrWhiteSpace(header.header))
            {
                currentHeader = header.header;
                EnsureSection(currentHeader);
            }

            byName[currentHeader].PropertyNames.Add(field.Name);
        }

        void EnsureSection(string sectionName)
        {
            if (byName.ContainsKey(sectionName))
                return;

            Section section = new Section { Name = sectionName };
            byName.Add(sectionName, section);
            sections.Add(section);
        }
    }

    private static bool IsSerializedField(FieldInfo field)
    {
        if (field.IsStatic)
            return false;

        if (field.IsDefined(typeof(HideInInspector), true))
            return false;

        bool hasSerializeField = field.IsDefined(typeof(SerializeField), true);
        bool isPublicSerializable = field.IsPublic && !field.IsDefined(typeof(NonSerializedAttribute), true);

        return hasSerializeField || isPublicSerializable;
    }

    private bool TryGetFoldoutState(string sectionName, out bool expanded)
    {
        if (FoldoutStates.TryGetValue(sectionName, out expanded))
            return true;

        string key = GetFoldoutPrefKey(sectionName);
        if (!EditorPrefs.HasKey(key))
            return false;

        expanded = EditorPrefs.GetBool(key, true);
        FoldoutStates[sectionName] = expanded;
        return true;
    }

    private void SetFoldoutState(string sectionName, bool expanded)
    {
        FoldoutStates[sectionName] = expanded;
        EditorPrefs.SetBool(GetFoldoutPrefKey(sectionName), expanded);
    }

    private string GetFoldoutPrefKey(string sectionName)
    {
        string cleanSection = string.IsNullOrWhiteSpace(sectionName)
            ? DefaultSectionName
            : sectionName.Trim();

        return $"{FoldoutPrefsPrefix}{target.GetType().FullName}.{cleanSection}";
    }
}
#endif

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This creates the scale settings in the project settings window.
    /// </summary>
    public static class ScaleSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider CreateScaleSettingsProvider()
        {
            ScalesManager scales = ScalesManager.instance;

            if (scales.Units.Count == 0)
            {
                scales.Reset();
            }

            SettingsProvider provider = new("Project/Tiny Giant Studio/Scale Settings", SettingsScope.Project)
            {
                label = "Scale Settings",
                guiHandler = _ =>
                {
                    EditorGUI.BeginChangeCheck();

                    GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(500)); //full custom unit settings

                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField("Name", EditorStyles.miniLabel, GUILayout.MaxWidth(290));
                    EditorGUILayout.LabelField("Value", EditorStyles.miniLabel, GUILayout.MaxWidth(210));
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < scales.Units.Count; i++)
                    {
                        GUILayout.BeginHorizontal();

                        string newName = EditorGUILayout.TextField(scales.Units[i].name, GUILayout.MaxWidth(300));
                        if (newName != scales.Units[i].name)
                        {
                            scales.Units[i].name = newName;
                            EditorUtility.SetDirty(scales);
                        }

                        float newValue = EditorGUILayout.FloatField(scales.Units[i].value, GUILayout.MaxWidth(150));
                        if (!Mathf.Approximately(newValue, scales.Units[i].value))
                        {
                            scales.Units[i].value = newValue;
                            EditorUtility.SetDirty(scales);
                        }

                        if (GUILayout.Button("Remove", GUILayout.MaxWidth(70)))
                        {
                            scales.Units.RemoveAt(i);
                            EditorUtility.SetDirty(scales);
                        }

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(10);
                    if (GUILayout.Button("Add new Unit", GUILayout.MaxWidth(500), GUILayout.Height(20)))
                    {
                        scales.Units.Add(new("New unit", 1));
                        EditorUtility.SetDirty(scales);
                    }

                    GUILayout.Space(30);
                    if (GUILayout.Button("Reset to default"))
                    {
                        if (EditorUtility.DisplayDialog("Restore default unit values?",
                                "Are you sure you want to restore default values? This will overwrite all changes to the units.",
                                "Yes", "No"))
                        {
                            scales.Reset();
                            EditorUtility.SetDirty(scales);
                        }
                    }

                    GUILayout.EndVertical();

                    if (EditorGUI.EndChangeCheck())
                    {
                    }
                },

                keywords = new HashSet<string>(new[] { "Scale", "Settings" })
            };

            return provider;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
    public static class SettingsFilePathManager
    {
        const string SYMBOL = "BETTEREDITOR_USE_PROJECTSETTINGS";

        public static void SetupSettingsPathConfigurationUI(VisualElement container)
        {
            container.Q<Button>("SettingsPathExplanation").clicked += () => { Application.OpenURL("https://ferdowsur.gitbook.io/better-transform/settings-save-path"); };

            Button userSettings = container.Q<Button>("SaveToUserSettings");
            Button projectSettings = container.Q<Button>("SaveToProjectSettings");
#if BETTEREDITOR_USE_PROJECTSETTINGS
            userSettings.style.opacity = 0.5f;
            userSettings.tooltip = "This is NOT used.";

            projectSettings.style.opacity = 1f;
            projectSettings.tooltip = "Currently saved to ProjectSettings.";
            projectSettings.SetEnabled(false);
#else
            userSettings.style.opacity = 1.0f;
            userSettings.tooltip = "Currently saved to UserSettings";
            userSettings.SetEnabled(false);

            projectSettings.style.opacity = 0.5f;
            projectSettings.tooltip = "This is NOT used.";
#endif

            userSettings.clicked += () =>
            {
                string message = "Please read the information in the 'How it works' link below the button before proceeding.";
                if (EditorUtility.DisplayDialog("Confirm switching save path to User Settings", message, "Yes", "Cancel"))
                {
                    SetUseProjectSettings(false);
                    Selection.activeObject = null;
                    AssetDatabase.SaveAssets();
                }
            };
            projectSettings.clicked += () =>
            {
                string message = "Please read the information in the 'How it works' link below the button before proceeding.";
                if (EditorUtility.DisplayDialog("Confirm switching save path to Projects Settings", message, "Yes", "Cancel"))
                {
                    SetUseProjectSettings(true);
                    Selection.activeObject = null;
                    AssetDatabase.SaveAssets();
                }
            };
        }


        static void SetUseProjectSettings(bool enable)
        {
            // Go through all valid build target groups
            foreach (BuildTargetGroup group in GetAllBuildTargetGroups())
            {
                string defines = GetDefines(group);
                var allDefines = defines.Split(';')
                    .Select(d => d.Trim())
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToList();

                bool changed = false;

                if (enable)
                {
                    if (!allDefines.Contains(SYMBOL))
                    {
                        allDefines.Add(SYMBOL);
                        changed = true;
                    }
                }
                else
                {
                    if (allDefines.Remove(SYMBOL))
                        changed = true;
                }

                if (changed)
                {
                    string newDefines = string.Join(";", allDefines);
                    // PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
                    SetDefines(group, newDefines);
                    UnityEngine.Debug.Log($"[{SYMBOL}] {(enable ? "enabled" : "disabled")} for {group}");
                }
            }

            AssetDatabase.Refresh();
        }

        static string GetDefines(BuildTargetGroup group)
        {
#if UNITY_2023_1_OR_NEWER
            NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            return PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
        }

        static void SetDefines(BuildTargetGroup group, string defines)
        {
#if UNITY_2023_1_OR_NEWER
            NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
#endif
        }

        static IEnumerable<BuildTargetGroup> GetAllBuildTargetGroups()
        {
            // Filter out obsolete and unknown build target groups
            return System.Enum.GetValues(typeof(BuildTargetGroup))
                .Cast<BuildTargetGroup>()
                .Where(g =>
                    g != BuildTargetGroup.Unknown &&
                    !IsObsolete(g));
        }

        static bool IsObsolete(BuildTargetGroup group)
        {
            FieldInfo fi = typeof(BuildTargetGroup).GetField(group.ToString());
            return fi != null && fi.GetCustomAttributes(typeof(System.ObsoleteAttribute), false).Length > 0;
        }
    }
}
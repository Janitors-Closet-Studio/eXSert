using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
#if BETTEREDITOR_USE_PROJECTSETTINGS
    [FilePath("ProjectSettings/Tiny Giant Studio/Better Editor/Inspector Settings.asset", FilePathAttribute.Location.ProjectFolder)]
#else
    [FilePath("UserSettings/Tiny Giant Studio/Better Editor/Inspector Settings.asset", FilePathAttribute.Location.ProjectFolder)]
    #endif
    public class BetterInspectorEditorSettings : ScriptableSingleton<BetterInspectorEditorSettings>
    {
        public bool useAnimatedFoldoutStyleSheet;
        public int selectedFoldoutStyle = 1;
        public int selectedButtonStyle = 1;

        public Transform selectedTransform;
        VisualElement _selectedTransformRoot;

        /// <summary>
        /// This MUST be updated AFTER selectedTransform is updated.
        /// Updated by Better Transform
        /// </summary>
        public VisualElement SelectedBetterTransformEditorRoot
        {
            get => _selectedTransformRoot;
            set
            {
                _selectedTransformRoot = value;
                BetterTransformSelectionUpdate?.Invoke();
            }
        }


        /// <summary>
        /// This is used to pass context menu items to Better Transform
        /// </summary>
        public Dictionary<string, Action> BetterTransformContextMenuItems;

        public Action BetterTransformSelectionUpdate;


        void OnEnable()
        {
            BetterTransformContextMenuItems ??= new();
        }

        public void Save() => Save(true);

        public void Reset()
        {
            selectedButtonStyle = 1;
            selectedFoldoutStyle = 1;
        }

        #region Context Menu

        public void Register(string menuItemName, Action action)
        {
            BetterTransformContextMenuItems ??= new();

            if (!BetterTransformContextMenuItems.TryAdd(menuItemName, action))
                BetterTransformContextMenuItems[menuItemName] += action;
        }

        #endregion
    }
}
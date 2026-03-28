using UnityEditor;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
    public static class StyleSheetsManager
    {
        static StyleSheet _animatedFoldoutStyleSheet;
        const string AnimatedFoldoutStyleSheetFileLocation = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Foldout/Foldout Animation.uss";
        const string AnimatedFoldoutStyleSheetGuid = "920070771e2f6c747b78ea05534a8a79";
        
        static StyleSheet _foldoutStyleSheet1;
        const string FileLocationFoldoutStyle1 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Foldout/Foldout Style 1.uss";
        const string GuidForFoldoutStyle1 = "46a1e9f07dfc8da49a73be794970c7b2";
        
        static StyleSheet _foldoutStyleSheet2;
        const string FileLocationFoldoutStyle2 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Foldout/Foldout Style 2.uss";
        const string GuidForFoldoutStyle2 = "fa4dbc50196807549b696fe088efb36f";
        
        static StyleSheet _foldoutStyleSheet3;
        const string FileLocationFoldoutStyle3 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Foldout/Foldout Style 3.uss";
        const string GuidForFoldoutStyle3 = "778d4ba8ba5039540a0a6445f96542ac";

        static StyleSheet _buttonStyleSheet1;
        const string FileLocationButtonStyleSheet1 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Button/Button Style 1.uss";
        const string GuidForButtonStyleSheet1 = "59803e0920eb13142ab503bd9be870fe";

        static StyleSheet _buttonStyleSheet2;
        const string FileLocationButtonStyleSheet2 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Button/Button Style 2.uss";
        const string GuidForButtonStyleSheet2 = "2d050522d26d0fc449b98c46647dd990";
        
        static StyleSheet _buttonStyleSheet3;
        const string FileLocationButtonStyleSheet3 = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/Button/Button Style 3.uss";
        const string GuidForButtonStyleSheet3 = "ebf57d3250622714996e19c09ba25c12";


        public static void UpdateStyleSheet(VisualElement root)
        {
            _animatedFoldoutStyleSheet = GetStyleSheet(_animatedFoldoutStyleSheet, AnimatedFoldoutStyleSheetFileLocation,
                AnimatedFoldoutStyleSheetGuid);

            if (_animatedFoldoutStyleSheet != null)
                if (BetterInspectorEditorSettings.instance.useAnimatedFoldoutStyleSheet)
                    root.styleSheets.Add(_animatedFoldoutStyleSheet);

            UpdateFoldoutStyle(root);
            UpdateButtonStyle(root);
        }

        static void UpdateFoldoutStyle(VisualElement root)
        {
            _foldoutStyleSheet1 = GetStyleSheet(_foldoutStyleSheet1, FileLocationFoldoutStyle1, GuidForFoldoutStyle1);
            _foldoutStyleSheet2 = GetStyleSheet(_foldoutStyleSheet2, FileLocationFoldoutStyle2, GuidForFoldoutStyle2);
            _foldoutStyleSheet3 = GetStyleSheet(_foldoutStyleSheet3, FileLocationFoldoutStyle3, GuidForFoldoutStyle3);

            if (!_foldoutStyleSheet1 || !_foldoutStyleSheet2 || !_foldoutStyleSheet3) return;

            switch (BetterInspectorEditorSettings.instance.selectedFoldoutStyle)
            {
                case 1:
                    root.styleSheets.Add(_foldoutStyleSheet1);
                    root.styleSheets.Remove(_foldoutStyleSheet2);
                    root.styleSheets.Remove(_foldoutStyleSheet3);
                    break;
                case 2:
                    root.styleSheets.Remove(_foldoutStyleSheet1);
                    root.styleSheets.Add(_foldoutStyleSheet2);
                    root.styleSheets.Remove(_foldoutStyleSheet3);
                    break;
                case 3:
                    root.styleSheets.Remove(_foldoutStyleSheet1);
                    root.styleSheets.Remove(_foldoutStyleSheet2);
                    root.styleSheets.Add(_foldoutStyleSheet3);
                    break;
            }
        }

        static void UpdateButtonStyle(VisualElement root)
        {
            _buttonStyleSheet1 =
                GetStyleSheet(_buttonStyleSheet1, FileLocationButtonStyleSheet1, GuidForButtonStyleSheet1);
            _buttonStyleSheet2 =
                GetStyleSheet(_buttonStyleSheet2, FileLocationButtonStyleSheet2, GuidForButtonStyleSheet2);
            _buttonStyleSheet3 =
                GetStyleSheet(_buttonStyleSheet3, FileLocationButtonStyleSheet3, GuidForButtonStyleSheet3);
            // Debug.Log(AssetDatabase.GUIDFromAssetPath(FileLocationButtonStyleSheet3));

            if (!_buttonStyleSheet1 || !_buttonStyleSheet2 || !_buttonStyleSheet3) return;

            switch (BetterInspectorEditorSettings.instance.selectedButtonStyle)
            {
                case 1:
                    root.styleSheets.Add(_buttonStyleSheet1);
                    root.styleSheets.Remove(_buttonStyleSheet2);
                    root.styleSheets.Remove(_buttonStyleSheet3);
                    break;
                case 2:
                    root.styleSheets.Remove(_buttonStyleSheet1);
                    root.styleSheets.Add(_buttonStyleSheet2);
                    root.styleSheets.Remove(_buttonStyleSheet3);
                    break;
                case 3:
                    root.styleSheets.Remove(_buttonStyleSheet1);
                    root.styleSheets.Remove(_buttonStyleSheet2);
                    root.styleSheets.Add(_buttonStyleSheet3);
                    break;
            }
        }


        /// <summary>
        /// If the style sheet isn't loaded yet, loads it from the given location.
        /// If it isn't found at the location, load it using GUID 
        /// </summary>
        static StyleSheet GetStyleSheet(StyleSheet currentStyleSheet, string location, string guid)
        {
            if (currentStyleSheet) return currentStyleSheet;

            currentStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(location);
            if (currentStyleSheet) return currentStyleSheet;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
        }
    }
}
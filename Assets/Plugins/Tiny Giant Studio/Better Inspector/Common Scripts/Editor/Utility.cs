using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterEditor
{
    public static class Utility
    {
        public static Texture2D GetTexture2D(string location, string guid, bool logGuid = false)
        {
            Texture2D texture = EditorGUIUtility.Load(location) as Texture2D;
            if (logGuid) Debug.Log(AssetDatabase.AssetPathToGUID(location));
            if (texture) return texture;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(assetPath) ? null : EditorGUIUtility.Load(assetPath) as Texture2D;
        }

        public static VisualTreeAsset GetVisualTreeAsset(string location, string guid, bool logGuid = false)
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(location);
            if (logGuid) Debug.Log(AssetDatabase.AssetPathToGUID(location));
            if (template) return template;

            //Debug.Log("Could not load template asset from path, falling back to using guid.\n" + location);

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
        }

        public static Rect GetMenuRect(VisualElement anchor, float xMin = 0, float xMax = 260)
        {
            Rect worldBound = anchor.worldBound;
            worldBound.xMin += xMin;
            worldBound.xMax += xMax;
            return worldBound;
        }
    }
}
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ObjectiveData))]
public class ObjectiveDataDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            float currentY = foldoutRect.yMax + VerticalSpacing;
            DrawChild(ref currentY, position, property, "interactionToActivate");
            DrawChild(ref currentY, position, property, "triggerSource");
            DrawChild(ref currentY, position, property, "triggerZoneSource");
            DrawChild(ref currentY, position, property, "triggerOnPuzzleInteractionComplete");
            DrawChild(ref currentY, position, property, "triggerOnWaveCompletion");
            DrawChild(ref currentY, position, property, "objectiveID");
            DrawChild(ref currentY, position, property, "objectiveText");
            DrawChild(ref currentY, position, property, "objectiveType");
            DrawChild(ref currentY, position, property, "disableInteraction");
            DrawChild(ref currentY, position, property, "interactionToDisable");
            DrawChild(ref currentY, position, property, "priority");

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        height += VerticalSpacing;
        height += GetChildHeight(property, "interactionToActivate");
        height += GetChildHeight(property, "triggerSource");
        height += GetChildHeight(property, "triggerZoneSource");
        height += GetChildHeight(property, "triggerOnPuzzleInteractionComplete");
        height += GetChildHeight(property, "triggerOnWaveCompletion");
        height += GetChildHeight(property, "objectiveID");
        height += GetChildHeight(property, "objectiveText");
        height += GetChildHeight(property, "objectiveType");
        height += GetChildHeight(property, "disableInteraction");
        height += GetChildHeight(property, "interactionToDisable");
        height += GetChildHeight(property, "priority");

        return height;
    }

    private static void DrawChild(ref float currentY, Rect totalRect, SerializedProperty parent, string childName)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
            return;

        float height = EditorGUI.GetPropertyHeight(child, true);
        Rect childRect = new Rect(totalRect.x, currentY, totalRect.width, height);
        EditorGUI.PropertyField(childRect, child, true);
        currentY += height + VerticalSpacing;
    }

    private static float GetChildHeight(SerializedProperty parent, string childName)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
            return 0f;

        return EditorGUI.GetPropertyHeight(child, true) + VerticalSpacing;
    }
}
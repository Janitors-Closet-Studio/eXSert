using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialHandler))]
public class TutorialHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMessageSection(
            "Initial Message",
            "initialMessage",
            "initialMessageUseSelectedIcon",
            "initialMessageAction");

        DrawMessageSection(
            "Single Target Fight Message",
            "singleTargetFightMessage",
            "singleTargetFightMessageUseSelectedIcon",
            "singleTargetFightMessageAction");

        DrawMessageSection(
            "AOE Target Fight Message",
            "aoeTargetFightMessage",
            "aoeTargetFightMessageUseSelectedIcon",
            "aoeTargetFightMessageAction");

        DrawMessageSection(
            "Correct Button Pressed Message",
            "correctButtonPressedMessage",
            "correctButtonPressedMessageUseSelectedIcon",
            "correctButtonPressedMessageAction");

        DrawMessageSection(
            "Tutorial Complete Message",
            "tutorialCompleteMessage",
            "tutorialCompleteMessageUseSelectedIcon",
            "tutorialCompleteMessageAction");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shared Tutorial Icon Styling", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tutorialIconColor"), new GUIContent("Icon Color"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tutorialIconSize"), new GUIContent("Icon Size"));
        EditorGUILayout.HelpBox("Any [[bind:]] placeholder in these messages will use the selected action above. Any tutorial bind token without an explicit size or color will inherit these shared values.", MessageType.Info);

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Tutorial Progression References", EditorStyles.boldLabel);
        DrawProperty("tutorialEntry");
        DrawProperty("singleTargetFight");
        DrawProperty("aoeTargetFight");
        DrawProperty("keycardToEnable");
        DrawProperty("loadNextSceneOnComplete");
        DrawProperty("nextScene");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMessageSection(string label, string messagePropertyName, string togglePropertyName, string actionPropertyName)
    {
        SerializedProperty messageProperty = serializedObject.FindProperty(messagePropertyName);
        SerializedProperty toggleProperty = serializedObject.FindProperty(togglePropertyName);
        SerializedProperty actionProperty = serializedObject.FindProperty(actionPropertyName);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        toggleProperty.boolValue = EditorGUILayout.ToggleLeft("Use action icon", toggleProperty.boolValue, GUILayout.Width(110f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(messageProperty, GUIContent.none, true);

        if (toggleProperty.boolValue)
        {
            EditorGUILayout.PropertyField(actionProperty, new GUIContent("Input Action"));

            if (!messageProperty.stringValue.Contains("[[bind:]]"))
                EditorGUILayout.HelpBox("Put [[bind:]] in the text where the selected action icon should appear.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawProperty(string propertyName)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
    }
}
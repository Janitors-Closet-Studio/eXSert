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

        DrawOptionalStepSection(
            "Dash Step",
            "useDashStep",
            "dashMessage",
            "dashMessageUseSelectedIcon",
            "dashMessageAction",
            "dashFight");

        DrawOptionalStepSection(
            "Guard Step",
            "useGuardStep",
            "guardFightMessage",
            "guardFightMessageUseSelectedIcon",
            "guardFightMessageAction",
            "guardFight");

        DrawOptionalStepSection(
            "Parry Step",
            "useParryStep",
            "parryFightMessage",
            "parryFightMessageUseSelectedIcon",
            "parryFightMessageAction",
            "parryFight");

        DrawRandomizedMessageSection(
            "Player Turn Message",
            "playerTurnReadyMessage",
            "playerTurnReadyMessageOptions",
            "playerTurnReadyMessageUseSelectedIcon",
            "playerTurnReadyMessageAction");

        DrawRandomizedMessageSection(
            "Post-Fight Success Message",
            "correctButtonPressedMessage",
            "correctButtonPressedMessageOptions",
            "correctButtonPressedMessageUseSelectedIcon",
            "correctButtonPressedMessageAction");

        DrawMessageSection(
            "Tutorial Complete Message",
            "tutorialCompleteMessage",
            "tutorialCompleteMessageUseSelectedIcon",
            "tutorialCompleteMessageAction");

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Tutorial Safety", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("keepPlayerAtFullHealthUntilTutorialComplete"),
            new GUIContent("Recover Player When HP Is Low")
        );
        if (serializedObject.FindProperty("keepPlayerAtFullHealthUntilTutorialComplete").boolValue)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("playerRecoveryHealthThreshold"),
                new GUIContent("Recovery Threshold")
            );
            EditorGUILayout.HelpBox("When enabled, the tutorial full-heals the player only after their health drops to or below this percentage of max HP.", MessageType.Info);
        }
        DrawProperty("enemiesInvulnerableUntilTutorialActionSucceeds");
        DrawProperty("makeDashEnemyHitHard");
        DrawProperty("makeGuardEnemyHitHard");
        DrawProperty("makeParryEnemyHitHard");
        if (ShouldShowTutorialDamageMultiplier())
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("tutorialEnemyDamageMultiplier"),
                new GUIContent("Enemy Damage Multiplier")
            );

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Tutorial Progression References", EditorStyles.boldLabel);
        DrawProperty("tutorialEntry");
        DrawProperty("singleTargetFight");
        DrawProperty("aoeTargetFight");
        DrawProperty("dashFight");
        DrawProperty("guardFight");
        DrawProperty("parryFight");
        DrawProperty("playerMovement");
        DrawProperty("tutorialObjectiveIcon");
        DrawProperty("keycardToEnable");
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("postEncounterFeedbackDelay"),
            new GUIContent("Post-Fight Message Delay")
        );
        DrawProperty("playerTurnMessageRestoreDelay");
        DrawProperty("tutorialIconColor");
        DrawProperty("tutorialIconSize");
        DrawProperty("tutorialIconGrowthPerCorrectPress");
        DrawProperty("tutorialIconPulseDuration");
        DrawProperty("loadNextSceneOnComplete");
        DrawProperty("nextScene");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMessageSection(string label, string messagePropertyName, string togglePropertyName, string actionPropertyName)
    {
        DrawMessageSection(label, messagePropertyName, togglePropertyName, actionPropertyName, null);
    }

    private void DrawOptionalStepSection(string label, string enabledPropertyName, string messagePropertyName, string togglePropertyName, string actionPropertyName, string encounterPropertyName = null)
    {
        SerializedProperty enabledProperty = serializedObject.FindProperty(enabledPropertyName);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        enabledProperty.boolValue = EditorGUILayout.ToggleLeft(label, enabledProperty.boolValue);

        if (enabledProperty.boolValue)
            DrawMessageSectionBody(messagePropertyName, togglePropertyName, actionPropertyName, encounterPropertyName);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawMessageSection(string label, string messagePropertyName, string togglePropertyName, string actionPropertyName, string encounterPropertyName)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        EditorGUILayout.EndHorizontal();

        DrawMessageSectionBody(messagePropertyName, togglePropertyName, actionPropertyName, encounterPropertyName);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawRandomizedMessageSection(string label, string fallbackMessagePropertyName, string messageOptionsPropertyName, string togglePropertyName, string actionPropertyName)
    {
        SerializedProperty messageOptionsProperty = serializedObject.FindProperty(messageOptionsPropertyName);
        SerializedProperty fallbackMessageProperty = serializedObject.FindProperty(fallbackMessagePropertyName);
        SerializedProperty toggleProperty = serializedObject.FindProperty(togglePropertyName);
        SerializedProperty actionProperty = serializedObject.FindProperty(actionPropertyName);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        EditorGUILayout.EndHorizontal();

        toggleProperty.boolValue = EditorGUILayout.ToggleLeft("Use action icon", toggleProperty.boolValue, GUILayout.Width(110f));

        EditorGUILayout.PropertyField(messageOptionsProperty, new GUIContent("Message Variants"), true);

        if (messageOptionsProperty.arraySize == 0)
        {
            EditorGUILayout.PropertyField(fallbackMessageProperty, new GUIContent("Fallback Message"), true);
            EditorGUILayout.HelpBox("Add one or more message variants to have the tutorial pick a random line. If none are set, the fallback message is used.", MessageType.Info);
        }

        if (toggleProperty.boolValue)
            EditorGUILayout.PropertyField(actionProperty, new GUIContent("Icon Action"));

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawMessageSectionBody(string messagePropertyName, string togglePropertyName, string actionPropertyName, string encounterPropertyName)
    {
        SerializedProperty messageProperty = serializedObject.FindProperty(messagePropertyName);
        SerializedProperty toggleProperty = serializedObject.FindProperty(togglePropertyName);
        SerializedProperty actionProperty = serializedObject.FindProperty(actionPropertyName);

        toggleProperty.boolValue = EditorGUILayout.ToggleLeft("Use action icon", toggleProperty.boolValue, GUILayout.Width(110f));

        EditorGUILayout.PropertyField(messageProperty, GUIContent.none, true);

        if (!string.IsNullOrEmpty(encounterPropertyName))
            EditorGUILayout.PropertyField(serializedObject.FindProperty(encounterPropertyName), new GUIContent("Encounter"));

        if (toggleProperty.boolValue)
            EditorGUILayout.PropertyField(actionProperty, new GUIContent("Icon Action"));
    }

    private void DrawProperty(string propertyName)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
    }

    private bool ShouldShowTutorialDamageMultiplier()
    {
        return serializedObject.FindProperty("makeDashEnemyHitHard").boolValue
            || serializedObject.FindProperty("makeGuardEnemyHitHard").boolValue
            || serializedObject.FindProperty("makeParryEnemyHitHard").boolValue;
    }
}
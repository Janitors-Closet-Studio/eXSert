#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GuidelineLightRow))]
public class GuidelineLightRowEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var row = (GuidelineLightRow)target;

            if (GUILayout.Button("Latch All On"))
            {
                row.LatchAllOn();
            }

            if (GUILayout.Button("Play Once"))
            {
                row.PlayOnce();
            }

            if (GUILayout.Button("Play Indefinitely"))
            {
                row.PlayIndefinitely();
            }

            if (GUILayout.Button("Turn Off"))
            {
                row.TurnOff();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use debug controls.", MessageType.Info);
        }
    }
}
#endif

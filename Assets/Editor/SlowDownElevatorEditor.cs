using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SlowDownElevator))]
public class SlowDownElevatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Test: Run Full Elevator Sequence"))
            {
                var slowDown = (SlowDownElevator)target;
                slowDown.Debug_RunFullSequence();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use the test button.", MessageType.Info);
    }
}

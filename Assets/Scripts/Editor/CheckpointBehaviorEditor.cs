using Progression.Checkpoints;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CheckpointBehavior))]
public class CheckpointBehaviorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        CheckpointBehavior checkpoint = (CheckpointBehavior)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Force Show Attached Notice"))
                checkpoint.ForceShowAttachedNotice();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use the notice replay button.", MessageType.Info);
    }
}
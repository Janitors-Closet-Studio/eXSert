using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Wobble))]
public class WobbleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Wobble wobbleScript = (Wobble)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Testing Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Play FlowWater()", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                wobbleScript.TestFlowWater();
            }
            else
            {
                Debug.LogWarning("Water flow requires Time.deltaTime and Coroutines. Please enter Play Mode first!");
            }
        }

        if (GUILayout.Button("Reset to Initial State", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                wobbleScript.ResetWater();
            }
            else
            {
                Debug.LogWarning("Please enter Play Mode to test the reset functionality.");
            }
        }
    }
}
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DoorHandler))]
public class DoorHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DoorHandler doorHandler = (DoorHandler)target;
        if (GUILayout.Button("Open Door"))
        {
            doorHandler.Interact();
        }
    }
}

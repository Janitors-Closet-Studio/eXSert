using UnityEditor;

[CustomEditor(typeof(DoorInteractions), true)]
public class DoorInteractionsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "interactOnce");
        serializedObject.ApplyModifiedProperties();
    }
}
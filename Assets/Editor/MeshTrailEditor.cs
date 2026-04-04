using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshTrail))]
public class MeshTrailEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        MeshTrail meshTrail = (MeshTrail)target;

        if (meshTrail.sourceRenderers == null || meshTrail.sourceRenderers.Length == 0)
        {
            EditorGUILayout.HelpBox("Source Renderers is empty. MeshTrail will use every SkinnedMeshRenderer in children, which can capture unintended oversized meshes on complex rigs.", MessageType.Warning);
        }

        if (GUILayout.Button("Collect Source Renderers"))
        {
            Undo.RecordObject(meshTrail, "Collect MeshTrail Source Renderers");
            meshTrail.CollectSourceRenderers();
            EditorUtility.SetDirty(meshTrail);
        }

        if (GUILayout.Button("Turn Trail On"))
        {
            meshTrail.TurnTrailOn();
        }

        if (GUILayout.Button("Turn Trail Off"))
        {
            meshTrail.TurnTrailOff();
        }
    }
}
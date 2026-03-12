using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BombCarrierEnemy))]
public class BombCarrierEnemyEditor : BaseEnemyEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        BombCarrierEnemy bomb = (BombCarrierEnemy)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bomb Bot Debug", EditorStyles.boldLabel);

        bool isPlaying = Application.isPlaying;
        if (!isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to preview bomb warning and explosion visuals.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!isPlaying))
        {
            if (GUILayout.Button("Test Countdown Warning"))
            {
                bomb.DebugPreviewCountdownWarning();
            }

            if (GUILayout.Button("Test Explosion Sequence"))
            {
                bomb.DebugPreviewExplosionSequence();
            }

            if (GUILayout.Button("Stop Preview"))
            {
                bomb.CancelDebugPreview();
            }
        }
    }
}
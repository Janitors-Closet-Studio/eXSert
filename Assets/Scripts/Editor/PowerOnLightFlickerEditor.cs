using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(PowerOnLightFlicker))]
public sealed class PowerOnLightFlickerEditor : Editor
{
    private double previewStartTime;
    private float previewSeed;
    private bool isPreviewing;

    private void OnDisable()
    {
        StopPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test Controls", EditorStyles.boldLabel);

        if (!Application.isPlaying && targets.Length > 1)
        {
            EditorGUILayout.HelpBox(
                "Edit-mode flicker preview only runs for a single selected object. Instant On and Instant Off still apply to all selected objects.",
                MessageType.Info
            );
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Test Flicker On"))
                TestFlicker();

            if (GUILayout.Button("Instant On"))
                TriggerWithUndoAll(static component => component.TurnOnInstant(), "Set Light On");

            if (GUILayout.Button("Instant Off"))
                TriggerWithUndoAll(static component => component.TurnOffInstant(), "Set Light Off");
        }

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Buttons run against the live scene object."
                : "Test Flicker On previews the light-only startup animation in edit mode. The power-on curve reaches full value at the end of the configured duration.",
            MessageType.Info
        );
    }

    private void TestFlicker()
    {
        PowerOnLightFlicker flicker = target as PowerOnLightFlicker;
        if (flicker == null)
            return;

        if (Application.isPlaying)
        {
            TriggerWithUndoAll(static component => component.TurnOnWithFlicker(), "Test Flicker On");
            return;
        }

        if (targets.Length > 1)
        {
            Debug.LogWarning("Select a single PowerOnLightFlicker object to preview the flicker animation in edit mode.", flicker);
            return;
        }

        TriggerWithUndo(flicker, static component => component.TurnOffInstant(), "Prepare Flicker Preview");

        previewStartTime = EditorApplication.timeSinceStartup;
        previewSeed = Random.Range(0f, 1000f);

        if (!isPreviewing)
        {
            EditorApplication.update += PreviewUpdate;
            isPreviewing = true;
        }
    }

    private void PreviewUpdate()
    {
        if (target is not PowerOnLightFlicker flicker)
        {
            StopPreview();
            return;
        }

        float duration = flicker.GetFlickerDuration();
        if (duration <= 0f)
        {
            flicker.TurnOnInstant();
            StopPreview();
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - previewStartTime;
        float normalizedTime = Mathf.Clamp01((float)(elapsed / duration));
        float value = flicker.EvaluatePowerValueAt(normalizedTime, (float)elapsed, previewSeed);

        flicker.SetPowerValue(value);
        EditorUtility.SetDirty(flicker.gameObject);
        SceneView.RepaintAll();
        Repaint();

        if (normalizedTime >= 1f)
        {
            flicker.TurnOnInstant();
            StopPreview();
        }
    }

    private void StopPreview()
    {
        if (!isPreviewing)
            return;

        EditorApplication.update -= PreviewUpdate;
        isPreviewing = false;
    }

    private static void TriggerWithUndo(PowerOnLightFlicker flicker, System.Action<PowerOnLightFlicker> action, string undoLabel)
    {
        if (flicker == null)
            return;

        Undo.RecordObject(flicker.gameObject, undoLabel);
        action(flicker);
        EditorUtility.SetDirty(flicker.gameObject);
    }

    private void TriggerWithUndoAll(System.Action<PowerOnLightFlicker> action, string undoLabel)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            PowerOnLightFlicker flicker = targets[i] as PowerOnLightFlicker;
            TriggerWithUndo(flicker, action, undoLabel);
        }
    }
}
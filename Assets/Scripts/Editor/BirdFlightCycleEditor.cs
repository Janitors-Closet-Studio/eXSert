using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BirdFlightCycle))]
public class BirdFlightCycleEditor : Editor
{
    private static BirdFlightCycle activePreviewBird;
    private static double previewStartTime;
    private static bool activePreviewReturnTrip;

    private float previewProgress;
    private bool previewReturnTrip;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

        BirdFlightCycle birdFlightCycle = (BirdFlightCycle)target;

        EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        previewReturnTrip = EditorGUILayout.Toggle("Preview Return Trip", previewReturnTrip);
        previewProgress = EditorGUILayout.Slider("Preview Progress", previewProgress, 0f, 1f);

        if (EditorGUI.EndChangeCheck() && !Application.isPlaying)
        {
            PreviewBird(birdFlightCycle);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Snap To Origin"))
            {
                StopPreviewIfActive(birdFlightCycle);
                Undo.RecordObject(birdFlightCycle.transform, "Snap Bird To Origin");
                birdFlightCycle.SnapToEditorOrigin();
                EditorUtility.SetDirty(birdFlightCycle);
            }

            if (GUILayout.Button("Snap To Destination"))
            {
                StopPreviewIfActive(birdFlightCycle);
                Undo.RecordObject(birdFlightCycle.transform, "Snap Bird To Destination");
                birdFlightCycle.SnapToEditorDestination();
                EditorUtility.SetDirty(birdFlightCycle);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying || !birdFlightCycle.CanEditorPreview()))
            {
                if (GUILayout.Button("Start Flight Preview"))
                {
                    StartPreview(birdFlightCycle, previewReturnTrip);
                }
            }

            using (new EditorGUI.DisabledScope(activePreviewBird != birdFlightCycle))
            {
                if (GUILayout.Button("Stop Preview"))
                {
                    StopPreviewIfActive(birdFlightCycle);
                }
            }
        }

        if (!Application.isPlaying && GUILayout.Button("Preview Current Position"))
        {
            StopPreviewIfActive(birdFlightCycle);
            PreviewBird(birdFlightCycle);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Runtime", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Start Flight To Destination"))
            {
                birdFlightCycle.DebugStartOutboundFlight();
            }

            if (GUILayout.Button("Start Flight To Origin"))
            {
                birdFlightCycle.DebugStartReturnFlight();
            }

            if (GUILayout.Button("Reset Bird To Origin"))
            {
                birdFlightCycle.DebugResetToOrigin();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Edit Mode preview moves the bird along the spline for placement checks. Runtime buttons still require Play Mode.",
                MessageType.Info
            );
        }
    }

    private void PreviewBird(BirdFlightCycle birdFlightCycle)
    {
        Undo.RecordObject(birdFlightCycle.transform, "Preview Bird On Spline");
        birdFlightCycle.EditorPreviewAlongPath(previewProgress, previewReturnTrip);
        EditorUtility.SetDirty(birdFlightCycle);
        SceneView.RepaintAll();
    }

    private static void StartPreview(BirdFlightCycle birdFlightCycle, bool returnTrip)
    {
        StopPreview();

        activePreviewBird = birdFlightCycle;
        activePreviewReturnTrip = returnTrip;
        previewStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += UpdatePreview;
    }

    private static void StopPreviewIfActive(BirdFlightCycle birdFlightCycle)
    {
        if (activePreviewBird == birdFlightCycle)
        {
            StopPreview();
        }
    }

    private static void StopPreview()
    {
        EditorApplication.update -= UpdatePreview;
        activePreviewBird = null;
        previewStartTime = 0d;
        activePreviewReturnTrip = false;
    }

    private static void UpdatePreview()
    {
        if (activePreviewBird == null)
        {
            StopPreview();
            return;
        }

        float duration = activePreviewBird.GetEditorPreviewDuration(activePreviewReturnTrip);
        if (duration <= 0f)
        {
            StopPreview();
            return;
        }

        float elapsed = (float)(EditorApplication.timeSinceStartup - previewStartTime);
        float progress = Mathf.Clamp01(elapsed / duration);

        activePreviewBird.EditorPreviewAlongPath(progress, activePreviewReturnTrip);
        EditorUtility.SetDirty(activePreviewBird);
        SceneView.RepaintAll();

        if (progress >= 1f)
        {
            StopPreview();
        }
    }
}
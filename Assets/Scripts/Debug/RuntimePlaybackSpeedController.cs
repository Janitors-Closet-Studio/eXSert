using Managers.TimeLord;
using UnityEngine;

/// <summary>
/// Scene-level runtime playback speed override for testing animation timing in play mode.
/// Add this to any loaded scene and adjust Playback Percent during runtime.
/// </summary>
[AddComponentMenu("Debug/Runtime Playback Speed Controller")]
[DisallowMultipleComponent]
public sealed class RuntimePlaybackSpeedController : MonoBehaviour
{
    private const string OwnerPrefix = "RuntimePlaybackSpeedController";

    [SerializeField]
    [Range(1f, 100f)]
    [Tooltip("Requested runtime playback speed as a percentage of normal time. 100 = normal, 50 = half speed.")]
    private float playbackPercent = 100f;

    [SerializeField]
    [Tooltip("If enabled, the time-scale request is applied automatically whenever this component is enabled in play mode.")]
    private bool applyOnEnable = true;

    [SerializeField]
    [Tooltip("Logs changes when the requested playback percentage updates during play mode.")]
    private bool logChanges;

    private string ownerId;
    private float lastAppliedScale = -1f;
    private bool hasActiveRequest;

    private void OnEnable()
    {
        if (!Application.isPlaying || !applyOnEnable)
            return;

        ApplyRequestedScale(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        ApplyRequestedScale();
    }

    private void OnDisable()
    {
        ReleaseRequestedScale();
    }

    private void OnDestroy()
    {
        ReleaseRequestedScale();
    }

    private void OnValidate()
    {
        playbackPercent = Mathf.Clamp(playbackPercent, 1f, 100f);

        if (!Application.isPlaying || !isActiveAndEnabled || !applyOnEnable)
            return;

        ApplyRequestedScale(force: true);
    }

    private void ApplyRequestedScale(bool force = false)
    {
        float requestedScale = playbackPercent * 0.01f;
        if (!force && hasActiveRequest && Mathf.Approximately(lastAppliedScale, requestedScale))
            return;

        ownerId ??= $"{OwnerPrefix}_{GetInstanceID()}";
        PauseCoordinator.RequestTimeScale(ownerId, requestedScale);
        hasActiveRequest = true;

        if (logChanges && !Mathf.Approximately(lastAppliedScale, requestedScale))
            Debug.Log($"[RuntimePlaybackSpeedController] Requested playback percent {playbackPercent:F0}% ({requestedScale:F2}x).", this);

        lastAppliedScale = requestedScale;
    }

    private void ReleaseRequestedScale()
    {
        if (!hasActiveRequest || string.IsNullOrWhiteSpace(ownerId))
            return;

        PauseCoordinator.ReleaseTimeScale(ownerId);
        hasActiveRequest = false;
        lastAppliedScale = -1f;
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Singletons;

/// <summary>
/// Adjusts brightness through a dedicated runtime global volume so scene-specific
/// post-process profiles cannot override the player's brightness setting.
/// </summary>
[DisallowMultipleComponent]
public class BrightnessOverlayController : Singleton<BrightnessOverlayController>
{
    [Header("Volume References")]
    [SerializeField]
    [Tooltip("Explicit volume profile reference. Preferred when brightness is driven from a shared post-process profile asset.")]
    private VolumeProfile targetVolumeProfile;

    [SerializeField]
    [Tooltip("Explicit volume reference (auto-assigned when left empty).")]
    private Volume targetVolume;

    private Volume runtimeVolume;
    private VolumeProfile runtimeProfile;
    private LiftGammaGain liftGammaGain;

    [SerializeField]
    [Tooltip("Emit debug information whenever brightness is applied.")]
    private bool logChanges;

    [Header("Gamma Curve")]
    [SerializeField]
    [Tooltip("Gamma value when the slider is at its minimum.")]
    private float minGamma = 0.5f;

    [SerializeField]
    [Tooltip("Gamma value when the slider is at its maximum.")]
    private float maxGamma = 1.5f;

    [Header("Slider Bounds")]
    [SerializeField]
    [Tooltip("Highest brightness slider value (used for normalization).")]
    private float maxSliderValue = 1f;
    [SerializeField]
    [Tooltip("Lowest brightness slider value (used for normalization).")]
    private float minSliderValue = 0f;

    [Header("Runtime Volume")]
    [SerializeField]
    [Tooltip("Priority used by the runtime brightness volume. Keep this above scene volumes so brightness always applies.")]
    private float runtimeVolumePriority = 1000f;

    protected override void Awake()
    {
        base.Awake();

        EnsureBrightnessOverride();
    }

    /// <summary>
    /// Applies brightness adjustments by scaling the Lift/Gamma/Gain override.
    /// </summary>
    public void ApplyBrightness(float brightness, float defaultBrightness)
    {
        if (!EnsureBrightnessOverride())
            return;

        if (Mathf.Approximately(defaultBrightness, 0f))
            defaultBrightness = 0.01f;

        float sliderMin = minSliderValue;
        float sliderMax = Mathf.Max(maxSliderValue, sliderMin + 0.0001f);

        float normalizedBrightness = Mathf.InverseLerp(
            sliderMin,
            sliderMax,
            Mathf.Clamp(brightness, sliderMin, sliderMax));

        float gammaAlpha = Mathf.Lerp(minGamma, maxGamma, normalizedBrightness);

        liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, gammaAlpha));
        liftGammaGain.gamma.overrideState = true;
        liftGammaGain.active = true;
        runtimeVolume.weight = 1f;

        if (logChanges)
        {
            Debug.Log(
            $"[BrightnessOverlayController] Brightness:{brightness:F3} -> GammaAlpha:{liftGammaGain.gamma.value.w:F3} via {runtimeVolume.name}");
        }
    }

    private void EnsureOverridesEnabled()
    {
        if (liftGammaGain == null)
            return;

        liftGammaGain.gamma.overrideState = true;
    }

    private bool EnsureBrightnessOverride()
    {
        if (runtimeVolume == null)
            CreateRuntimeBrightnessVolume();

        if (runtimeVolume == null)
        {
            Debug.LogError("[BrightnessOverlayController] Failed to create runtime brightness volume.");
            return false;
        }

        if (runtimeProfile == null)
        {
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeVolume.sharedProfile = runtimeProfile;
        }

        if (liftGammaGain == null && !runtimeProfile.TryGet(out liftGammaGain))
        {
            liftGammaGain = runtimeProfile.Add<LiftGammaGain>(true);
        }

        EnsureOverridesEnabled();
        return true;
    }

    private void CreateRuntimeBrightnessVolume()
    {
        GameObject runtimeVolumeObject = new GameObject("Runtime Brightness Volume");
        runtimeVolumeObject.layer = ResolveVolumeLayer();
        DontDestroyOnLoad(runtimeVolumeObject);

        runtimeVolume = runtimeVolumeObject.AddComponent<Volume>();
        runtimeVolume.isGlobal = true;
        runtimeVolume.priority = runtimeVolumePriority;
        runtimeVolume.weight = 1f;
    }

    private int ResolveVolumeLayer()
    {
        if (targetVolume == null)
            targetVolume = GetComponent<Volume>();

        if (targetVolume != null)
            return targetVolume.gameObject.layer;

        Volume[] sceneVolumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Volume sceneVolume in sceneVolumes)
        {
            if (sceneVolume != null)
                return sceneVolume.gameObject.layer;
        }

        return gameObject.layer;
    }

}



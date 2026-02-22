using UnityEngine;

public class GuidelineLightBulb : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Renderers that should change emission when the bulb turns on/off.")]
    [SerializeField]
    private Renderer[] targetRenderers;

    [Tooltip("Emission color property (URP/Lit + many shaders: _EmissionColor).")]
    [SerializeField]
    private string emissionColorProperty = "_EmissionColor";

    [ColorUsage(true, true)]
    [SerializeField]
    private Color offEmissionColor = Color.black;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color onEmissionColor = Color.white;

    [Header("Initialization")]
    [Tooltip("Applies the initial state automatically when the object enables (recommended).")]
    [SerializeField]
    private bool applyInitialStateOnEnable = true;

    [Tooltip("If Apply Initial State On Enable is true, this controls whether the bulb starts on.")]
    [SerializeField]
    private bool startOn = false;

    [Header("Debug")]
    [Tooltip("Warn if material emission appears disabled.")]
    [SerializeField]
    private bool warnIfEmissionDisabled = true;

    [Header("Lights")]
    [Tooltip("Optional real-time Light components to enable/disable with the bulb.")]
    [SerializeField]
    private Light[] targetLights;

    [Header("Volumetric Fog")]
    [Tooltip(
        "Optional VolumetricAdditionalLight components (com.cqf.urpvolumetricfog) to enable/disable with the bulb."
    )]
    [SerializeField]
    private VolumetricAdditionalLight[] targetVolumetricLights;

    private MaterialPropertyBlock _mpb;
    private int _emissionColorPropId;
    private bool _hasCachedIds;
    private bool _warnedAboutEmissionKeyword;

    private float[] _initialLightIntensities;
    private bool _cachedInitialLightIntensities;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        CachePropertyIds();
        CacheInitialLightIntensities();
    }

    private void OnEnable()
    {
        if (applyInitialStateOnEnable)
            SetState(startOn);
    }

    private void Reset()
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true);
        targetLights = GetComponentsInChildren<Light>(true);
        targetVolumetricLights = GetComponentsInChildren<VolumetricAdditionalLight>(true);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(emissionColorProperty))
            emissionColorProperty = "_EmissionColor";

        CachePropertyIds();
    }

    private void CachePropertyIds()
    {
        _emissionColorPropId = Shader.PropertyToID(emissionColorProperty);

        _hasCachedIds = true;
    }

    public void SetOn()
    {
        SetState(true);
    }

    public void SetOff()
    {
        SetState(false);
    }

    public void SetState(bool on)
    {
        if (!_hasCachedIds)
            CachePropertyIds();

        IsOn = on;

        ApplyEmission(on ? onEmissionColor : offEmissionColor);

        if (targetLights != null)
        {
            if (!_cachedInitialLightIntensities)
                CacheInitialLightIntensities();

            for (int i = 0; i < targetLights.Length; i++)
            {
                if (targetLights[i] != null)
                {
                    // When turning off, restore original intensities before disabling.
                    if (!on)
                        RestoreInitialLightIntensity(i);

                    targetLights[i].enabled = on;
                }
            }
        }

        if (targetVolumetricLights != null)
        {
            for (int i = 0; i < targetVolumetricLights.Length; i++)
            {
                if (targetVolumetricLights[i] != null)
                    targetVolumetricLights[i].enabled = on;
            }
        }
    }

    // Sets intensity on the referenced Light components (does not enable/disable them).
    // Used by completion/latched states to dim lights without reauthoring prefabs.
    public void SetLightIntensity(float intensity)
    {
        if (targetLights == null || targetLights.Length == 0)
            return;

        if (!_cachedInitialLightIntensities)
            CacheInitialLightIntensities();

        float clamped = Mathf.Max(0f, intensity);

        for (int i = 0; i < targetLights.Length; i++)
        {
            if (targetLights[i] != null)
                targetLights[i].intensity = clamped;
        }
    }

    private void CacheInitialLightIntensities()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            _initialLightIntensities = null;
            _cachedInitialLightIntensities = true;
            return;
        }

        _initialLightIntensities = new float[targetLights.Length];
        for (int i = 0; i < targetLights.Length; i++)
        {
            _initialLightIntensities[i] = targetLights[i] != null ? targetLights[i].intensity : 0f;
        }

        _cachedInitialLightIntensities = true;
    }

    private void RestoreInitialLightIntensity(int index)
    {
        if (!_cachedInitialLightIntensities || _initialLightIntensities == null)
            return;
        if (index < 0 || index >= _initialLightIntensities.Length)
            return;
        if (targetLights == null || index >= targetLights.Length)
            return;

        var light = targetLights[index];
        if (light == null)
            return;

        light.intensity = _initialLightIntensities[index];
    }

    private void ApplyEmission(Color emissionColor)
    {
        if (targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null)
                continue;

            if (warnIfEmissionDisabled && !_warnedAboutEmissionKeyword)
            {
                var mat = r.sharedMaterial;
                if (mat != null)
                {
                    // For URP/Lit and Standard, emission is typically controlled by the _EMISSION keyword.
                    // Property blocks cannot enable keywords; the material must have Emission enabled.
                    if (mat.HasProperty(_emissionColorPropId) && !mat.IsKeywordEnabled("_EMISSION"))
                    {
                        Debug.LogWarning(
                            $"[{nameof(GuidelineLightBulb)}] Emission appears disabled on material '{mat.name}' for renderer '{r.name}'. Enable the material's Emission toggle (keyword _EMISSION) or the emission color will not visibly glow.",
                            this
                        );
                        _warnedAboutEmissionKeyword = true;
                    }
                }
            }

            r.GetPropertyBlock(_mpb);

            _mpb.SetColor(_emissionColorPropId, emissionColor);

            r.SetPropertyBlock(_mpb);
        }
    }

    [ContextMenu("Debug/Set On")]
    private void DebugSetOn()
    {
        SetOn();
    }

    [ContextMenu("Debug/Set Off")]
    private void DebugSetOff()
    {
        SetOff();
    }

    [ContextMenu("Collect Renderers From Children")]
    private void CollectRenderersFromChildren()
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    [ContextMenu("Collect Lights From Children")]
    private void CollectLightsFromChildren()
    {
        targetLights = GetComponentsInChildren<Light>(true);
    }

    [ContextMenu("Collect VolumetricAdditionalLight From Children")]
    private void CollectVolumetricAdditionalLightsFromChildren()
    {
        targetVolumetricLights = GetComponentsInChildren<VolumetricAdditionalLight>(true);
    }
}

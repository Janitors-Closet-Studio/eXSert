using UnityEngine;

/// <summary>
/// Randomly flickers a light on and off with configurable timing.
/// Attach to a GameObject with a Light component.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField]
    [Tooltip("Minimum time the light stays on (seconds)")]
    private float minOnTime = 0.1f;

    [SerializeField]
    [Tooltip("Maximum time the light stays on (seconds)")]
    private float maxOnTime = 0.5f;

    [SerializeField]
    [Tooltip("Minimum time the light stays off (seconds)")]
    private float minOffTime = 0.05f;

    [SerializeField]
    [Tooltip("Maximum time the light stays off (seconds)")]
    private float maxOffTime = 0.3f;

    [SerializeField]
    [Tooltip(
        "If disabled, the light starts stable and only begins flickering when triggered by an event."
    )]
    private bool startFlickeringOnEnable = false;

    [Header("Intensity Variation (Optional)")]
    [SerializeField]
    [Tooltip("Enable random intensity variations when light is on")]
    private bool varyIntensity = false;

    [SerializeField]
    [Tooltip("Minimum intensity multiplier (0-1)")]
    [Range(0f, 1f)]
    private float minIntensityMultiplier = 0.5f;

    [SerializeField]
    [Tooltip("Maximum intensity multiplier (0-1)")]
    [Range(0f, 1f)]
    private float maxIntensityMultiplier = 1f;

    [Header("Flicker Probability")]
    [SerializeField]
    [Tooltip("Chance the light will flicker off (0-1). 1 = always flickers, 0 = never flickers")]
    [Range(0f, 1f)]
    private float flickerChance = 1f;

    [Header("Material Emission (Optional)")]
    [SerializeField]
    [Tooltip("Renderer to control emission on (leave empty to auto-find on this object)")]
    private Renderer targetRenderer;

    [SerializeField]
    [Tooltip("Material index to control emission on (if renderer has multiple materials)")]
    private int materialIndex;

    [SerializeField]
    [Tooltip("Enable/disable emission based on light state")]
    private bool controlEmission = true;

    private Light lightComponent;
    private float baseIntensity;
    private float nextFlickerTime;
    private bool isLightOn = true;
    private bool isFlickerActive;
    private Material targetMaterial;
    private bool hasEmission;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Color baseEmissionColor;

    private void Awake()
    {
        lightComponent = GetComponent<Light>();
        baseIntensity = lightComponent.intensity;
        isFlickerActive = startFlickeringOnEnable;

        if (controlEmission)
            SetupEmissionControl();

        ScheduleNextFlicker();
    }

    private void SetupEmissionControl()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer != null && targetRenderer.materials.Length > materialIndex)
        {
            targetMaterial = targetRenderer.materials[materialIndex];

            if (targetMaterial.HasProperty(EmissionColor))
            {
                hasEmission = true;
                baseEmissionColor = targetMaterial.GetColor(EmissionColor);

                if (baseEmissionColor != Color.black)
                    targetMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                Debug.LogWarning(
                    $"[LightFlicker] Material on {gameObject.name} does not have an _EmissionColor property."
                );
            }
        }
        else if (targetRenderer == null)
        {
            Debug.LogWarning(
                $"[LightFlicker] No Renderer found on {gameObject.name}. Emission control disabled."
            );
        }
    }

    private void Update()
    {
        if (!isFlickerActive)
            return;

        if (Time.time >= nextFlickerTime)
        {
            ToggleLight();
            ScheduleNextFlicker();
        }
    }

    private void ToggleLight()
    {
        if (isLightOn)
        {
            if (Random.value <= flickerChance)
            {
                lightComponent.enabled = false;
                isLightOn = false;

                if (controlEmission && hasEmission && targetMaterial != null)
                {
                    targetMaterial.SetColor(EmissionColor, Color.black);
                    targetMaterial.DisableKeyword("_EMISSION");
                }
            }
            else if (varyIntensity)
            {
                float multiplier = Random.Range(minIntensityMultiplier, maxIntensityMultiplier);
                lightComponent.intensity = baseIntensity * multiplier;

                if (controlEmission && hasEmission && targetMaterial != null)
                    targetMaterial.SetColor(EmissionColor, baseEmissionColor * multiplier);
            }
        }
        else
        {
            lightComponent.enabled = true;
            isLightOn = true;

            float multiplier = 1f;
            if (varyIntensity)
            {
                multiplier = Random.Range(minIntensityMultiplier, maxIntensityMultiplier);
                lightComponent.intensity = baseIntensity * multiplier;
            }
            else
            {
                lightComponent.intensity = baseIntensity;
            }

            if (controlEmission && hasEmission && targetMaterial != null)
            {
                targetMaterial.SetColor(EmissionColor, baseEmissionColor * multiplier);
                targetMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    private void ScheduleNextFlicker()
    {
        float delay = isLightOn
            ? Random.Range(minOnTime, maxOnTime)
            : Random.Range(minOffTime, maxOffTime);

        nextFlickerTime = Time.time + delay;
    }

    public void SetFlickerChance(float chance)
    {
        flickerChance = Mathf.Clamp01(chance);
    }

    public void EnableFlicker(bool enable)
    {
        isFlickerActive = enable;

        if (enable)
        {
            ScheduleNextFlicker();
            return;
        }

        RestoreStableLightState();
    }

    public void StartFlickering()
    {
        EnableFlicker(true);
    }

    public void StopFlickering()
    {
        EnableFlicker(false);
    }

    private void RestoreStableLightState()
    {
        lightComponent.enabled = true;
        lightComponent.intensity = baseIntensity;
        isLightOn = true;

        if (controlEmission && hasEmission && targetMaterial != null)
        {
            targetMaterial.SetColor(EmissionColor, baseEmissionColor);
            targetMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void OnDestroy()
    {
        if (targetMaterial != null && targetRenderer != null)
        {
            // Unity automatically cleans up renderer material instances.
        }
    }

    private void OnValidate()
    {
        if (maxOnTime < minOnTime)
            maxOnTime = minOnTime;

        if (maxOffTime < minOffTime)
            maxOffTime = minOffTime;

        if (maxIntensityMultiplier < minIntensityMultiplier)
            maxIntensityMultiplier = minIntensityMultiplier;
    }
}

using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerOnLightFlicker : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField]
    private Light[] targetLights;

    [ColorUsage(false, true)]
    [SerializeField]
    private Color poweredOnLightColor = Color.cyan;

    [Header("Powered On Light")]
    [SerializeField]
    private bool affectLightColor = true;

    [SerializeField]
    private bool affectLightIntensity = true;

    [SerializeField]
    private float poweredOnIntensity = 1f;

    [SerializeField]
    private float poweredOffIntensity = 0f;

    [Header("Power On")]
    [SerializeField]
    private float powerOnDuration = 2f;

    [SerializeField]
    private float flickerSpeed = 28f;

    [SerializeField, Range(0f, 1f)]
    private float maxValueDip = 0.8f;

    [SerializeField]
    private AnimationCurve powerCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 0.05f),
        new Keyframe(0.35f, 0.4f),
        new Keyframe(0.65f, 0.7f),
        new Keyframe(1f, 1f)
    );

    [Header("Initialization")]
    [SerializeField]
    private bool startPoweredOff = true;

    [SerializeField]
    private bool playOnEnable;

    private Coroutine powerRoutine;

    private void Awake()
    {
        if (targetLights == null || targetLights.Length == 0)
            targetLights = GetComponentsInChildren<Light>(true);

        if (startPoweredOff)
            ApplyPowerValue(0f);
        else
            ApplyPowerValue(1f);
    }

    private void OnEnable()
    {
        if (playOnEnable)
            TurnOnWithFlicker();
    }

    private void Reset()
    {
        targetLights = GetComponentsInChildren<Light>(true);
    }

    private void OnDisable()
    {
        if (powerRoutine != null)
        {
            StopCoroutine(powerRoutine);
            powerRoutine = null;
        }
    }

    public void TurnOnWithFlicker()
    {
        if (powerRoutine != null)
            StopCoroutine(powerRoutine);

        powerRoutine = StartCoroutine(TurnOnWithFlickerRoutine());
    }

    public void TurnOnInstant()
    {
        StopActiveRoutine();
        ApplyPowerValue(1f);
    }

    public void TurnOffInstant()
    {
        StopActiveRoutine();
        ApplyPowerValue(0f);
    }

    public void SetPowerValue(float normalizedValue)
    {
        StopActiveRoutine();
        ApplyPowerValue(normalizedValue);
    }

    [ContextMenu("Test Flicker On")]
    private void ContextMenuTestFlickerOn()
    {
        TurnOnWithFlicker();
    }

    [ContextMenu("Instant On")]
    private void ContextMenuTurnOnInstant()
    {
        TurnOnInstant();
    }

    [ContextMenu("Instant Off")]
    private void ContextMenuTurnOffInstant()
    {
        TurnOffInstant();
    }

    public float GetFlickerDuration()
    {
        return powerOnDuration;
    }

    public float EvaluatePowerValueAt(float normalizedTime, float timeSample, float seed)
    {
        if (powerOnDuration <= 0f)
            return 1f;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        float baseValue = Mathf.Clamp01(powerCurve.Evaluate(clampedTime));
        float noise = Mathf.PerlinNoise(seed, timeSample * flickerSpeed);
        float dipStrength = (1f - clampedTime) * maxValueDip;

        return Mathf.Clamp01(baseValue - ((1f - noise) * dipStrength));
    }

    private IEnumerator TurnOnWithFlickerRoutine()
    {
        if (powerOnDuration <= 0f)
        {
            ApplyPowerValue(1f);
            powerRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        float seed = Random.Range(0f, 1000f);

        while (elapsed < powerOnDuration)
        {
            float normalizedTime = elapsed / powerOnDuration;
            float value = EvaluatePowerValueAt(normalizedTime, Time.time, seed);

            ApplyPowerValue(value);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPowerValue(1f);
        powerRoutine = null;
    }

    private void ApplyPowerValue(float normalizedValue)
    {
        float value = Mathf.Clamp01(normalizedValue);

        ApplyLightState(value);
    }

    private void ApplyLightState(float normalizedValue)
    {
        if (targetLights == null)
            return;

        Color lightColor = affectLightColor
            ? ApplyValueToColor(poweredOnLightColor, normalizedValue, false)
            : poweredOnLightColor;

        float intensity = affectLightIntensity
            ? Mathf.Lerp(poweredOffIntensity, poweredOnIntensity, normalizedValue)
            : poweredOnIntensity;

        for (int i = 0; i < targetLights.Length; i++)
        {
            Light targetLight = targetLights[i];
            if (targetLight == null)
                continue;

            if (affectLightColor)
                targetLight.color = lightColor;

            if (affectLightIntensity)
                targetLight.intensity = intensity;

            targetLight.enabled = normalizedValue > 0.001f;
        }
    }

    private static Color ApplyValueToColor(Color sourceColor, float normalizedValue, bool hdr)
    {
        Color.RGBToHSV(sourceColor, out float hue, out float saturation, out float value);
        Color result = Color.HSVToRGB(hue, saturation, value * normalizedValue, hdr);
        result.a = sourceColor.a;
        return result;
    }

    private void StopActiveRoutine()
    {
        if (powerRoutine == null)
            return;

        StopCoroutine(powerRoutine);
        powerRoutine = null;
    }
}

using System.Collections;
using Unity.Cinemachine;
using UnityEngine;



public class CameraShake : MonoBehaviour
{
    public CinemachineCamera guardCamera;
    public CinemachineCamera combatCamera;
    private CinemachineBasicMultiChannelPerlin guardNoise;
    private CinemachineBasicMultiChannelPerlin combatNoise;
    [SerializeField] private float defaultAmplitude = 1f;
    [SerializeField] private float defaultFrequency = 2f;
    [SerializeField] private float defaultDuration = 0.1f;
    [SerializeField] private float defaultTimeToReset = 0.1f;

    private Coroutine activeShakeRoutine;

    private void Awake()
    {
        if (guardCamera != null)
            guardNoise = guardCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (combatCamera != null)
            combatNoise = combatCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public void TriggerShake(float amplitude = -1f, float frequency = -1f, float duration = -1f, float timeToReset = 0.1f)
    {
        if (!SettingsManager.Instance.cameraShake)
        {
            Debug.Log("Camera shake disabled in settings, skipping shake");
            ResetNoise();
            return;
        }

        float resolvedAmplitude = amplitude >= 0f ? amplitude : defaultAmplitude;
        float resolvedFrequency = frequency >= 0f ? frequency : defaultFrequency;
        float resolvedDuration = duration >= 0f ? duration : defaultDuration;
        float resolvedTimeToReset = timeToReset >= 0f ? timeToReset : defaultTimeToReset;

        resolvedAmplitude = Mathf.Max(0f, resolvedAmplitude);
        resolvedFrequency = Mathf.Max(0f, resolvedFrequency);
        resolvedDuration = Mathf.Max(0.0001f, resolvedDuration);
        resolvedTimeToReset = Mathf.Max(0.0001f, resolvedTimeToReset);

        Debug.Log("Camera shake triggered");

        if (activeShakeRoutine != null)
            StopCoroutine(activeShakeRoutine);

        activeShakeRoutine = StartCoroutine(ShakeCinemachineCameras(resolvedAmplitude, resolvedFrequency, resolvedDuration, resolvedTimeToReset));
    }

    private IEnumerator ShakeCinemachineCameras(float amplitude = -1f, float frequency = -1f, float duration = -1f, float timeToReset = 0.1f)
    {
        float elapsed = 0f;
        // Set amplitude and frequency
        if (guardNoise != null)
        {
            guardNoise.AmplitudeGain = amplitude;
            guardNoise.FrequencyGain = frequency;
        }
        if (combatNoise != null)
        {
            combatNoise.AmplitudeGain = amplitude;
            combatNoise.FrequencyGain = frequency;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < timeToReset)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / timeToReset);
            // Cubic ease-out for smooth slowdown
            float easeT = 1f - Mathf.Pow(1f - t, 3);
            if (guardNoise != null)
            {
                guardNoise.AmplitudeGain = Mathf.Lerp(amplitude, 0f, easeT);
                guardNoise.FrequencyGain = Mathf.Lerp(frequency, 0f, easeT);
            }
            if (combatNoise != null)
            {
                combatNoise.AmplitudeGain = Mathf.Lerp(amplitude, 0f, easeT);
                combatNoise.FrequencyGain = Mathf.Lerp(frequency, 0f, easeT);
            }
            yield return null;
        }

        ResetNoise();
        activeShakeRoutine = null;
    }

    private void OnDisable()
    {
        if (activeShakeRoutine != null)
            StopCoroutine(activeShakeRoutine);

        activeShakeRoutine = null;
        ResetNoise();
    }

    private void ResetNoise()
    {
        if (guardNoise != null)
        {
            guardNoise.AmplitudeGain = 0f;
            guardNoise.FrequencyGain = 0f;
        }

        if (combatNoise != null)
        {
            combatNoise.AmplitudeGain = 0f;
            combatNoise.FrequencyGain = 0f;
        }
    }

    
}

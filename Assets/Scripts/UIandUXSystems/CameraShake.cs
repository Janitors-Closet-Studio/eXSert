using System.Collections;
using Unity.Cinemachine;
using UnityEngine;



public class CameraShake : MonoBehaviour
{
    public CinemachineCamera guardCamera;
    public CinemachineCamera combatCamera;
    private CinemachineBasicMultiChannelPerlin guardNoise;
    private CinemachineBasicMultiChannelPerlin combatNoise;

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
            return;
        }
        Debug.Log("Camera shake triggered");

        StopAllCoroutines();
        StartCoroutine(ShakeCinemachineCameras(amplitude, frequency, duration, timeToReset));
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
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < timeToReset)
        {
            elapsed += Time.deltaTime;
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

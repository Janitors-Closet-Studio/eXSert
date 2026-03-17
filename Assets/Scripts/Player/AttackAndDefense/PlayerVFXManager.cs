using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Utilities.Combat.Attacks;

/// <summary>
/// Manages player combat VFX and related lighting.
/// Driven by animation events (LeftFire/RightFire) and player movement events.
/// </summary>
public sealed class PlayerVFXManager : MonoBehaviour
{
    [SerializeField]
    private PlayerAttackManager attackManager;

    [SerializeField]
    private PlayerMovement playerMovement;
    private AudioSource audioSource;

    [Header("Attack VFX")]
    [SerializeField]
    [Tooltip("Rig-mounted left-hand VFX (enabled by LeftFire animation event).")]
    private GameObject leftAttackVfx;

    [SerializeField]
    [Tooltip("Rig-mounted right-hand VFX (enabled by RightFire animation event).")]
    private GameObject rightAttackVfx;

    [SerializeField]
    [Tooltip("Duration before attack VFX are disabled again.")]
    private float attackDuration = 1f;

    [SerializeField]
    [Tooltip("Audio clip played when attack VFX enable.")]
    private AudioClip attackAudioClip;

    [Header("Attack VFX Parameters")]
    [SerializeField]
    private string emberRateProperty = "EmberRate";

    [SerializeField]
    private string burstLifeTimeProperty = "Burst LifeTime";

    [SerializeField]
    private float emberRateOff = 0f;

    [SerializeField]
    private float emberRateOn = 2000f;

    [SerializeField]
    private float burstLifeTimeOff = 0f;

    [SerializeField]
    private float burstLifeTimeOn = 1f;

    [Header("Attack VFX Timing")]
    [SerializeField]
    [Tooltip(
        "Delay (seconds) after enabling Burst LifeTime before turning EmberRate on. Use a small value (e.g. 0.02-0.06) to ensure Burst LifeTime is applied before emission starts."
    )]
    private float emberRateDelayAfterBurstSeconds = 0.03f;

    [SerializeField]
    [Tooltip(
        "When turning attack VFX off, reinitializes the graph to clear any already-spawned particles."
    )]
    private bool reinitOnAttackVfxDisable = true;

    [Header("Attack Lights")]
    [SerializeField]
    [Tooltip("Point lights that should turn on while LEFT-hand embers are emitting.")]
    private Light[] leftHandPointLights = Array.Empty<Light>();

    [SerializeField]
    [Tooltip("Point lights that should turn on while RIGHT-hand embers are emitting.")]
    private Light[] rightHandPointLights = Array.Empty<Light>();

    [SerializeField]
    [Tooltip(
        "If true and a hand light list is empty, automatically finds Light components under that hand's VFX object."
    )]
    private bool autoFindHandLights = true;

    [Header("Air Move VFX (Double Jump & Air Dash)")]
    [SerializeField]
    [Tooltip("Rig-mounted VFX toggled for double jump / air dash.")]
    private GameObject[] airMoveVfxObjects = Array.Empty<GameObject>();

    [SerializeField]
    [Tooltip("Duration before air-move VFX are hidden again.")]
    private float airMoveDuration = 0.75f;

    [SerializeField]
    [Tooltip("Audio clip played when double jump / air dash VFX enable.")]
    private AudioClip airMoveAudioClip;

    [Header("Ground Explosion VFX (AY3 / Plunge)")]
    [SerializeField]
    [Tooltip("Short-lived shockwave VFX toggled by the GroundExplosion animation event.")]
    private GameObject fireShockWaveVfx;

    [SerializeField]
    [Tooltip("Audio clip played when ground explosion VFX enable.")]
    private AudioClip fireShockWaveAudioClip;

    [SerializeField]
    [Tooltip("How long the FireShockWave object stays enabled after GroundExplosion is invoked.")]
    private float fireShockWaveDuration = 0.35f;

    [SerializeField]
    [Tooltip(
        "If true, forces the FireShockWave local rotation X/Z to 0 so it always faces upward."
    )]
    private bool keepFireShockWaveUpright = true;

    private Coroutine leftAttackDeactivateRoutine;
    private Coroutine rightAttackDeactivateRoutine;
    private Coroutine airMoveDeactivateRoutine;
    private Coroutine leftEmberDelayRoutine;
    private Coroutine rightEmberDelayRoutine;
    private Coroutine fireShockWaveDisableRoutine;
    private bool airMoveCallbacksRegistered;

    private VisualEffect leftAttackEffect;
    private VisualEffect rightAttackEffect;
    private bool leftAttackActive;
    private bool rightAttackActive;
    private readonly Dictionary<Light, float> originalLightIntensity = new();

    private void Awake()
    {
        if (string.Equals(burstLifeTimeProperty, "BurstLifeTime", StringComparison.Ordinal))
            burstLifeTimeProperty = "Burst LifeTime";

        attackManager ??=
            GetComponentInChildren<PlayerAttackManager>() ?? GetComponent<PlayerAttackManager>();

        playerMovement ??=
            GetComponentInChildren<PlayerMovement>()
            ?? GetComponent<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>();
        audioSource = SoundManager.Instance?.sfxSource;

        EnsureAttackVfxWired();

        SetVfxActive(airMoveVfxObjects, false);

        if (fireShockWaveVfx != null)
            fireShockWaveVfx.SetActive(false);

        DisableHandAttackVfx(leftAttackEffect, ref leftEmberDelayRoutine, leftHandPointLights);
        DisableHandAttackVfx(rightAttackEffect, ref rightEmberDelayRoutine, rightHandPointLights);
        SetLeftLightsActive(false);
        SetRightLightsActive(false);
    }

    private void OnValidate()
    {
        // Migration for older serialized values.
        if (string.Equals(burstLifeTimeProperty, "BurstLifeTime", StringComparison.Ordinal))
            burstLifeTimeProperty = "Burst LifeTime";
    }

    private void OnEnable()
    {
        playerMovement ??=
            GetComponentInChildren<PlayerMovement>()
            ?? GetComponent<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>();
        PlayerAttackManager.OnAttack += HandleAttackStarted;
        RegisterAirMoveCallbacks();
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttackStarted;
        UnregisterAirMoveCallbacks();

        StopAndClearRoutine(ref leftAttackDeactivateRoutine);
        StopAndClearRoutine(ref rightAttackDeactivateRoutine);
        StopAndClearRoutine(ref airMoveDeactivateRoutine);
        StopAndClearRoutine(ref leftEmberDelayRoutine);
        StopAndClearRoutine(ref rightEmberDelayRoutine);
        StopAndClearRoutine(ref fireShockWaveDisableRoutine);

        leftAttackActive = false;
        rightAttackActive = false;
        DisableHandAttackVfx(leftAttackEffect, ref leftEmberDelayRoutine, leftHandPointLights);
        DisableHandAttackVfx(rightAttackEffect, ref rightEmberDelayRoutine, rightHandPointLights);
        SetLeftLightsActive(false);
        SetRightLightsActive(false);

        SetVfxActive(airMoveVfxObjects, false);

        if (fireShockWaveVfx != null)
            fireShockWaveVfx.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!keepFireShockWaveUpright)
            return;

        if (fireShockWaveVfx == null)
            return;

        Vector3 euler = fireShockWaveVfx.transform.localEulerAngles;
        if (!Mathf.Approximately(euler.x, 0f) || !Mathf.Approximately(euler.z, 0f))
            fireShockWaveVfx.transform.localRotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void EnsureAttackVfxWired()
    {
        if (leftAttackVfx != null)
        {
            leftAttackVfx.SetActive(true);
            leftAttackEffect =
                leftAttackVfx.GetComponent<VisualEffect>()
                ?? leftAttackVfx.GetComponentInChildren<VisualEffect>(true);
        }

        if (rightAttackVfx != null)
        {
            rightAttackVfx.SetActive(true);
            rightAttackEffect =
                rightAttackVfx.GetComponent<VisualEffect>()
                ?? rightAttackVfx.GetComponentInChildren<VisualEffect>(true);
        }

        if (autoFindHandLights)
        {
            if (leftHandPointLights == null || leftHandPointLights.Length == 0)
                leftHandPointLights = CollectLights(leftAttackVfx);

            if (rightHandPointLights == null || rightHandPointLights.Length == 0)
                rightHandPointLights = CollectLights(rightAttackVfx);
        }

        CacheOriginalLightIntensities(leftHandPointLights);
        CacheOriginalLightIntensities(rightHandPointLights);
    }

    private static Light[] CollectLights(GameObject root)
    {
        if (root == null)
            return Array.Empty<Light>();

        return root.GetComponentsInChildren<Light>(true) ?? Array.Empty<Light>();
    }

    private void CacheOriginalLightIntensities(Light[] lights)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (!originalLightIntensity.ContainsKey(light))
                originalLightIntensity.Add(light, light.intensity);
        }
    }

    private void HandleAirMoveTriggered()
    {
        if (airMoveVfxObjects == null || airMoveVfxObjects.Length == 0)
            return;

        SetVfxActive(airMoveVfxObjects, true);
        PlayAudio(airMoveAudioClip);
        PlayAudio(fireShockWaveAudioClip);
        RestartGroupRoutine(
            ref airMoveDeactivateRoutine,
            airMoveDuration,
            airMoveVfxObjects,
            () =>
            {
                airMoveDeactivateRoutine = null;
            }
        );
    }

    private void HandleAttackStarted(PlayerAttack attack)
    {
        if (attack == null)
            return;

        bool isAerial =
            attack.attackType == AttackType.LightAerial
            || attack.attackType == AttackType.HeavyAerial;
        bool isLauncher = string.Equals(
            attack.attackId,
            "Launcher",
            StringComparison.OrdinalIgnoreCase
        );
        bool isAirDash = string.Equals(
            attack.attackId,
            "AirDash",
            StringComparison.OrdinalIgnoreCase
        );

        if ((isAerial || isLauncher) && !isAirDash)
            PlayAudio(airMoveAudioClip);
    }

    public void LeftFire() => TriggerLeftAttackVfx();

    public void RightFire() => TriggerRightAttackVfx();

    // Animation Event: add this event to AY3 + Plunge at landing/finish frame.
    public void GroundExplosion()
    {
        if (fireShockWaveVfx == null)
            return;

        StopAndClearRoutine(ref fireShockWaveDisableRoutine);

        if (keepFireShockWaveUpright)
        {
            Vector3 euler = fireShockWaveVfx.transform.localEulerAngles;
            fireShockWaveVfx.transform.localRotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        fireShockWaveVfx.SetActive(true);
        PlayAudio(fireShockWaveAudioClip);

        float duration = Mathf.Max(0f, fireShockWaveDuration);
        if (duration <= 0f)
        {
            fireShockWaveVfx.SetActive(false);
            return;
        }

        fireShockWaveDisableRoutine = StartCoroutine(DisableFireShockWaveAfter(duration));
    }

    private IEnumerator DisableFireShockWaveAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (fireShockWaveVfx != null)
            fireShockWaveVfx.SetActive(false);
        fireShockWaveDisableRoutine = null;
    }

    private void TriggerLeftAttackVfx()
    {
        if (leftAttackEffect == null && leftAttackVfx == null)
            return;

        if (leftAttackEffect == null)
            leftAttackEffect =
                leftAttackVfx.GetComponent<VisualEffect>()
                ?? leftAttackVfx.GetComponentInChildren<VisualEffect>(true);

        leftAttackActive = true;
        EnableHandAttackVfx(leftAttackEffect, ref leftEmberDelayRoutine, leftHandPointLights);
        PlayAudio(attackAudioClip);
        PlayAudio(fireShockWaveAudioClip);

        RestartSingleRoutine(
            ref leftAttackDeactivateRoutine,
            attackDuration,
            () =>
            {
                leftAttackActive = false;
                DisableHandAttackVfx(
                    leftAttackEffect,
                    ref leftEmberDelayRoutine,
                    leftHandPointLights
                );
                leftAttackDeactivateRoutine = null;
            }
        );
    }

    private void TriggerRightAttackVfx()
    {
        if (rightAttackEffect == null && rightAttackVfx == null)
            return;

        if (rightAttackEffect == null)
            rightAttackEffect =
                rightAttackVfx.GetComponent<VisualEffect>()
                ?? rightAttackVfx.GetComponentInChildren<VisualEffect>(true);

        rightAttackActive = true;
        EnableHandAttackVfx(rightAttackEffect, ref rightEmberDelayRoutine, rightHandPointLights);
        PlayAudio(attackAudioClip);

        RestartSingleRoutine(
            ref rightAttackDeactivateRoutine,
            attackDuration,
            () =>
            {
                rightAttackActive = false;
                DisableHandAttackVfx(
                    rightAttackEffect,
                    ref rightEmberDelayRoutine,
                    rightHandPointLights
                );
                rightAttackDeactivateRoutine = null;
            }
        );
    }

    private void EnableHandAttackVfx(
        VisualEffect effect,
        ref Coroutine emberDelayRoutine,
        Light[] handLights
    )
    {
        if (effect == null)
            return;

        StopAndClearRoutine(ref emberDelayRoutine);

        // Apply burst parameters first (some graphs read these only at spawn time).
        TrySetFloat(effect, burstLifeTimeProperty, burstLifeTimeOn);

        // Keep emission off until we're ready to light + emit.
        TrySetFloat(effect, emberRateProperty, emberRateOff);

        effect.Play();
        

        float delay = Mathf.Max(0f, emberRateDelayAfterBurstSeconds);
        if (delay <= 0f)
        {
            TrySetFloat(effect, emberRateProperty, emberRateOn);
            SetLightsActive(handLights, active: true);
            return;
        }

        emberDelayRoutine = StartCoroutine(EnableEmberAfterDelay(effect, delay, handLights));
    }

    private IEnumerator EnableEmberAfterDelay(
        VisualEffect effect,
        float delaySeconds,
        Light[] handLights
    )
    {
        yield return new WaitForSeconds(delaySeconds);

        // Re-apply burst right before emission in case another system touched it.
        TrySetFloat(effect, burstLifeTimeProperty, burstLifeTimeOn);
        TrySetFloat(effect, emberRateProperty, emberRateOn);
        SetLightsActive(handLights, active: true);
        PlayAudio(fireShockWaveAudioClip);
    }

    private void DisableHandAttackVfx(
        VisualEffect effect,
        ref Coroutine emberDelayRoutine,
        Light[] handLights
    )
    {
        StopAndClearRoutine(ref emberDelayRoutine);
        SetLightsActive(handLights, active: false);

        if (effect == null)
            return;

        TrySetFloat(effect, emberRateProperty, emberRateOff);
        TrySetFloat(effect, burstLifeTimeProperty, burstLifeTimeOff);

        effect.Stop();
        if (reinitOnAttackVfxDisable)
            effect.Reinit();
    }

    private void TrySetFloat(VisualEffect effect, string propertyName, float value)
    {
        if (effect == null)
            return;

        if (string.IsNullOrWhiteSpace(propertyName))
            return;

        if (!effect.HasFloat(propertyName))
            return;

        effect.SetFloat(propertyName, value);
    }

    private void SetLeftLightsActive(bool active) => SetLightsActive(leftHandPointLights, active);

    private void SetRightLightsActive(bool active) => SetLightsActive(rightHandPointLights, active);

    private void SetLightsActive(Light[] lights, bool active)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (active)
            {
                light.enabled = true;
                if (originalLightIntensity.TryGetValue(light, out float intensity))
                    light.intensity = intensity;
            }
            else
            {
                light.intensity = 0f;
                light.enabled = false;
            }
        }
    }

    private void RestartGroupRoutine(
        ref Coroutine routine,
        float duration,
        GameObject[] targets,
        Action onComplete
    )
    {
        StopAndClearRoutine(ref routine);

        if (duration <= 0f)
        {
            SetVfxActive(targets, false);
            onComplete?.Invoke();
            return;
        }

        routine = StartCoroutine(DisableAfter(duration, targets, onComplete));
    }

    private void RestartSingleRoutine(ref Coroutine routine, float duration, Action onComplete)
    {
        StopAndClearRoutine(ref routine);

        if (duration <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        routine = StartCoroutine(DisableAfter(duration, onComplete));
    }

    private IEnumerator DisableAfter(float duration, GameObject[] targets, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        SetVfxActive(targets, false);
        onComplete?.Invoke();
    }

    private IEnumerator DisableAfter(float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }

    private void SetVfxActive(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(active);
        }
    }

    private void RegisterAirMoveCallbacks()
    {
        if (playerMovement == null || airMoveCallbacksRegistered)
            return;

        playerMovement.DoubleJumpPerformed += HandleAirMoveTriggered;
        playerMovement.AirDashPerformed += HandleAirMoveTriggered;
        airMoveCallbacksRegistered = true;
    }

    private void UnregisterAirMoveCallbacks()
    {
        if (playerMovement == null || !airMoveCallbacksRegistered)
            return;

        playerMovement.DoubleJumpPerformed -= HandleAirMoveTriggered;
        playerMovement.AirDashPerformed -= HandleAirMoveTriggered;
        airMoveCallbacksRegistered = false;
    }

    private void StopAndClearRoutine(ref Coroutine routine)
    {
        if (routine == null)
            return;

        StopCoroutine(routine);
        routine = null;
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}

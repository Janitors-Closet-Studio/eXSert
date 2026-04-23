using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable CS0414
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
    private CharacterController characterController;

    [Header("Attack VFX")]
    [SerializeField]
    [Tooltip("Rig-mounted left-hand VFX (enabled by LeftFire animation event).")]
    private GameObject leftAttackVfx;

    [SerializeField]
    [Tooltip("Rig-mounted right-hand VFX (enabled by RightFire animation event).")]
    private GameObject rightAttackVfx;

    [SerializeField]
    [Tooltip("Rig-mounted idle fire VFX (enabled by IdleFire animation event).")]
    private GameObject idleFireVfx;

    [SerializeField]
    [Tooltip("Duration before attack VFX are disabled again.")]
    private float attackDuration = 1f;

    [SerializeField]
    [Tooltip("Duration before the idle fire event disables the right-hand fire again.")]
    private float idleFireDuration = 0.35f;

    [SerializeField]
    [Tooltip("Extra time to wait after setting idle fire EmberRate to 0 before disabling the GameObject.")]
    private float idleFireShutdownDelay = 1f;

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

    [Header("Dash Dust VFX")]
    [SerializeField]
    [Tooltip("Dash dust root triggered by the DashDust animation event.")]
    private GameObject dashDustVfx;

    [Header("Landing Dust VFX")]
    [SerializeField]
    [Tooltip("Smoke/dust root triggered when the player lands after being airborne. Falls back to Dash Dust when unassigned.")]
    private GameObject landingDustVfx;

    [SerializeField]
    [Tooltip("Audio clip played when landing dust VFX enable. Falls back to air-move audio when unassigned.")]
    private AudioClip landingDustAudioClip;

    [SerializeField]
    [Tooltip("Layers considered valid ground when snapping landing dust to the floor.")]
    private LayerMask landingDustGroundLayers = Physics.DefaultRaycastLayers;

    [SerializeField]
    [Tooltip("How high above the player base the landing dust ground probe starts.")]
    private float landingDustProbeStartHeight = 1f;

    [SerializeField]
    [Tooltip("How far downward to probe for ground when placing landing dust.")]
    private float landingDustProbeDistance = 3f;

    [SerializeField]
    [Tooltip("Final vertical offset applied after snapping landing dust to the hit point.")]
    private float landingDustVerticalOffset = 0f;

    [Header("Punch Dust VFX")]
    [SerializeField]
    [Tooltip("Left punch ring root triggered by the PunchDust_L animation event.")]
    private GameObject leftPunchDustVfx;

    [SerializeField]
    [Tooltip("Right punch ring root triggered by the PunchDust_R animation event.")]
    private GameObject rightPunchDustVfx;

    [Header("Gauntlet Piledriver")]
    [SerializeField]
    [Tooltip("Piledriver transform on the player gauntlet that slides along local X.")]
    private Transform gauntletPiledriver;

    [SerializeField]
    [Tooltip("Front spark VFX root triggered by the ExtendSpark_F animation event.")]
    private GameObject piledriverExtendSparkFrontVfx;

    [SerializeField]
    [Tooltip("Back spark VFX root triggered by the ExtendSpark_B animation event.")]
    private GameObject piledriverExtendSparkBackVfx;

    [SerializeField]
    [Tooltip("How long the front piledriver spark should stay active before stopping.")]
    private float piledriverExtendSparkFrontDuration = 0.12f;

    [SerializeField]
    [Tooltip("How long the back piledriver spark should stay active before stopping.")]
    private float piledriverExtendSparkBackDuration = 0.12f;

    [SerializeField]
    [Tooltip("Resting local X position for the piledriver.")]
    private float piledriverRestingLocalX = 0.08f;

    [SerializeField]
    [Tooltip("Retracted local X position for the piledriver.")]
    private float piledriverRetractedLocalX = 0.16f;

    [SerializeField]
    [Tooltip("Fully extended local X position for the piledriver.")]
    private float piledriverFullExtensionLocalX = -0.11f;

    [SerializeField]
    [Tooltip("How long the piledriver takes to move to the retracted position.")]
    private float piledriverRetractDuration = 0.05f;

    [SerializeField]
    [Tooltip("How long the piledriver takes to move to the fully extended position.")]
    private float piledriverExtendDuration = 0.04f;

    [SerializeField]
    [Tooltip("How long the piledriver takes to return to the resting position after full extension.")]
    private float piledriverReturnDuration = 0.08f;

    [Header("Exhaust Flame VFX")]
    [SerializeField]
    [Tooltip("Left-hand exhaust flame root triggered by the LExhaust animation event.")]
    private GameObject leftExhaustVfx;

    [SerializeField]
    [Tooltip("Right-hand exhaust flame root triggered by the RExhaust animation event.")]
    private GameObject rightExhaustVfx;

    [SerializeField]
    [Tooltip("How long the exhaust stays at full authored emission before tapering.")]
    private float exhaustFullEmissionDuration = 0.18f;

    [SerializeField]
    [Tooltip("How long the exhaust takes to fall from full emission to the tail value.")]
    private float exhaustFadeDuration = 0.14f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Emission multiplier after the taper finishes. 0.1 means authored 10 becomes 1.")]
    private float exhaustTailEmissionScale = 0.1f;

    [SerializeField]
    [Tooltip("How long to let already-spawned exhaust particles finish before hiding the root object.")]
    private float exhaustDisableDelay = 0.35f;

    [SerializeField]
    [Tooltip("Audio clip played when an exhaust flame is triggered.")]
    private AudioClip exhaustAudioClip;

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
    private Coroutine idleFireDeactivateRoutine;
    private Coroutine airMoveDeactivateRoutine;
    private Coroutine leftEmberDelayRoutine;
    private Coroutine rightEmberDelayRoutine;
    private Coroutine fireShockWaveDisableRoutine;
    private Coroutine leftExhaustRoutine;
    private Coroutine rightExhaustRoutine;
    private Coroutine piledriverRoutine;
    private Coroutine piledriverExtendSparkFrontRoutine;
    private Coroutine piledriverExtendSparkBackRoutine;
    private bool airMoveCallbacksRegistered;

    private VisualEffect leftAttackEffect;
    private VisualEffect rightAttackEffect;
    private VisualEffect idleFireEffect;
    private VisualEffect[] dashDustEffects = Array.Empty<VisualEffect>();
    private VisualEffect[] landingDustEffects = Array.Empty<VisualEffect>();
    private VisualEffect[] piledriverExtendSparkFrontEffects = Array.Empty<VisualEffect>();
    private VisualEffect[] piledriverExtendSparkBackEffects = Array.Empty<VisualEffect>();
    private ParticleSystem[] dashDustParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] landingDustParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] leftPunchDustParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] rightPunchDustParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] piledriverExtendSparkFrontParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] piledriverExtendSparkBackParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] leftExhaustParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem[] rightExhaustParticles = Array.Empty<ParticleSystem>();
    private bool leftAttackActive;
    private bool rightAttackActive;
    private bool idleFireHasCachedEmberRate;
    private float idleFireCachedEmberRate;
    private readonly Dictionary<Light, float> originalLightIntensity = new();
    private readonly Dictionary<ParticleSystem, float> originalExhaustEmissionRates = new();

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
        characterController ??=
            GetComponent<CharacterController>()
            ?? GetComponentInChildren<CharacterController>()
            ?? GetComponentInParent<CharacterController>();
        audioSource = SoundManager.Instance?.sfxSource;

        EnsureAttackVfxWired();
        EnsureDashDustVfxWired();
        EnsurePunchDustVfxWired();
        EnsureExhaustVfxWired();
        EnsurePiledriverVfxWired();
        SetGauntletPiledriverLocalX(piledriverRestingLocalX);

        SetVfxActive(airMoveVfxObjects, false);

        if (fireShockWaveVfx != null)
            fireShockWaveVfx.SetActive(false);

        DisableHandAttackVfx(leftAttackEffect, ref leftEmberDelayRoutine, leftHandPointLights);
        DisableHandAttackVfx(rightAttackEffect, ref rightEmberDelayRoutine, rightHandPointLights);
        if (idleFireVfx != null)
        {
            idleFireEffect =
                idleFireVfx.GetComponent<VisualEffect>()
                ?? idleFireVfx.GetComponentInChildren<VisualEffect>(true);

            if (idleFireEffect != null && idleFireEffect.HasFloat(emberRateProperty))
            {
                idleFireCachedEmberRate = idleFireEffect.GetFloat(emberRateProperty);
                idleFireHasCachedEmberRate = true;
            }

            idleFireVfx.SetActive(false);
        }
        StopAndClearParticleSystems(dashDustParticles);
            StopAndClearParticleSystems(landingDustParticles);
        StopAndClearParticleSystems(leftPunchDustParticles);
        StopAndClearParticleSystems(rightPunchDustParticles);
        StopAndClearParticleSystems(piledriverExtendSparkFrontParticles);
        StopAndClearParticleSystems(piledriverExtendSparkBackParticles);
        SetPiledriverSparkIdle(
            piledriverExtendSparkFrontVfx,
            piledriverExtendSparkFrontParticles,
            piledriverExtendSparkFrontEffects
        );
        SetPiledriverSparkIdle(
            piledriverExtendSparkBackVfx,
            piledriverExtendSparkBackParticles,
            piledriverExtendSparkBackEffects
        );
        SetExhaustVfxIdle(leftExhaustVfx, leftExhaustParticles, hideRoot: true);
        SetExhaustVfxIdle(rightExhaustVfx, rightExhaustParticles, hideRoot: true);
        SetLeftLightsActive(false);
        SetRightLightsActive(false);
    }
#pragma warning restore CS0414

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
        characterController ??=
            GetComponent<CharacterController>()
            ?? GetComponentInChildren<CharacterController>()
            ?? GetComponentInParent<CharacterController>();
        PlayerAttackManager.OnAttack += HandleAttackStarted;
        RegisterAirMoveCallbacks();
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttackStarted;
        UnregisterAirMoveCallbacks();

        StopAndClearRoutine(ref leftAttackDeactivateRoutine);
        StopAndClearRoutine(ref rightAttackDeactivateRoutine);
        StopAndClearRoutine(ref idleFireDeactivateRoutine);
        StopAndClearRoutine(ref airMoveDeactivateRoutine);
        StopAndClearRoutine(ref leftEmberDelayRoutine);
        StopAndClearRoutine(ref rightEmberDelayRoutine);
        StopAndClearRoutine(ref fireShockWaveDisableRoutine);
        StopAndClearRoutine(ref leftExhaustRoutine);
        StopAndClearRoutine(ref rightExhaustRoutine);
        StopAndClearRoutine(ref piledriverRoutine);
        StopAndClearRoutine(ref piledriverExtendSparkFrontRoutine);
        StopAndClearRoutine(ref piledriverExtendSparkBackRoutine);

        leftAttackActive = false;
        rightAttackActive = false;
        DisableHandAttackVfx(leftAttackEffect, ref leftEmberDelayRoutine, leftHandPointLights);
        DisableHandAttackVfx(rightAttackEffect, ref rightEmberDelayRoutine, rightHandPointLights);
        if (idleFireVfx != null)
            idleFireVfx.SetActive(false);
        StopAndClearParticleSystems(dashDustParticles);
            StopAndClearParticleSystems(landingDustParticles);
        StopAndClearParticleSystems(leftPunchDustParticles);
        StopAndClearParticleSystems(rightPunchDustParticles);
        StopAndClearParticleSystems(piledriverExtendSparkFrontParticles);
        StopAndClearParticleSystems(piledriverExtendSparkBackParticles);
        SetPiledriverSparkIdle(
            piledriverExtendSparkFrontVfx,
            piledriverExtendSparkFrontParticles,
            piledriverExtendSparkFrontEffects
        );
        SetPiledriverSparkIdle(
            piledriverExtendSparkBackVfx,
            piledriverExtendSparkBackParticles,
            piledriverExtendSparkBackEffects
        );
        SetExhaustVfxIdle(leftExhaustVfx, leftExhaustParticles, hideRoot: true);
        SetExhaustVfxIdle(rightExhaustVfx, rightExhaustParticles, hideRoot: true);
        SetLeftLightsActive(false);
        SetRightLightsActive(false);

        SetVfxActive(airMoveVfxObjects, false);
        SetGauntletPiledriverLocalX(piledriverRestingLocalX);

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

        if (idleFireVfx != null)
            idleFireVfx.SetActive(false);

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

    private void EnsureDashDustVfxWired()
    {
        dashDustParticles = CollectParticleSystems(dashDustVfx);
        dashDustEffects = CollectVisualEffects(dashDustVfx);
        landingDustParticles = CollectParticleSystems(landingDustVfx);
        landingDustEffects = CollectVisualEffects(landingDustVfx);
    }

    private void EnsurePunchDustVfxWired()
    {
        leftPunchDustParticles = CollectParticleSystems(leftPunchDustVfx);
        rightPunchDustParticles = CollectParticleSystems(rightPunchDustVfx);
    }

    private void EnsureExhaustVfxWired()
    {
        leftExhaustParticles = CollectParticleSystems(leftExhaustVfx);
        rightExhaustParticles = CollectParticleSystems(rightExhaustVfx);

        CacheOriginalExhaustEmissionRates(leftExhaustParticles);
        CacheOriginalExhaustEmissionRates(rightExhaustParticles);
    }

    private void EnsurePiledriverVfxWired()
    {
        piledriverExtendSparkFrontParticles = CollectParticleSystems(piledriverExtendSparkFrontVfx);
        piledriverExtendSparkBackParticles = CollectParticleSystems(piledriverExtendSparkBackVfx);
        piledriverExtendSparkFrontEffects = CollectVisualEffects(piledriverExtendSparkFrontVfx);
        piledriverExtendSparkBackEffects = CollectVisualEffects(piledriverExtendSparkBackVfx);
    }

    private static Light[] CollectLights(GameObject root)
    {
        if (root == null)
            return Array.Empty<Light>();

        return root.GetComponentsInChildren<Light>(true) ?? Array.Empty<Light>();
    }

    private static ParticleSystem[] CollectParticleSystems(GameObject root)
    {
        if (root == null)
            return Array.Empty<ParticleSystem>();

        return root.GetComponentsInChildren<ParticleSystem>(true) ?? Array.Empty<ParticleSystem>();
    }

    private static VisualEffect[] CollectVisualEffects(GameObject root)
    {
        if (root == null)
            return Array.Empty<VisualEffect>();

        return root.GetComponentsInChildren<VisualEffect>(true) ?? Array.Empty<VisualEffect>();
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

    private void CacheOriginalExhaustEmissionRates(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (!originalExhaustEmissionRates.ContainsKey(particleSystem))
                originalExhaustEmissionRates.Add(
                    particleSystem,
                    particleSystem.emission.rateOverTimeMultiplier
                );
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

    private void HandleLandingTriggered()
    {
        GameObject landingRoot = landingDustVfx != null ? landingDustVfx : dashDustVfx;
        if (landingRoot == null)
            return;

        SnapLandingVfxToGround(landingRoot);

        ParticleSystem[] landingParticles = landingDustVfx != null
            ? landingDustParticles
            : dashDustParticles;
        VisualEffect[] landingEffects = landingDustVfx != null
            ? landingDustEffects
            : dashDustEffects;

        if (landingParticles == null || landingParticles.Length == 0)
        {
            landingParticles = CollectParticleSystems(landingRoot);

            if (landingDustVfx != null)
                landingDustParticles = landingParticles;
            else
                dashDustParticles = landingParticles;
        }

        if (landingEffects == null || landingEffects.Length == 0)
        {
            landingEffects = CollectVisualEffects(landingRoot);

            if (landingDustVfx != null)
                landingDustEffects = landingEffects;
            else
                dashDustEffects = landingEffects;
        }

        PlayOneShotVfx(landingRoot, landingParticles, landingEffects);
        PlayAudio(landingDustAudioClip != null ? landingDustAudioClip : airMoveAudioClip);
    }

    private void SnapLandingVfxToGround(GameObject landingRoot)
    {
        if (landingRoot == null)
            return;

        Vector3 worldPosition = landingRoot.transform.position;
        Vector3 probeOrigin = characterController != null
            ? characterController.bounds.center
            : transform.position;
        probeOrigin.y += Mathf.Max(0f, landingDustProbeStartHeight);

        if (
            Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit hit,
                Mathf.Max(0.1f, landingDustProbeDistance),
                landingDustGroundLayers,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            worldPosition.y = hit.point.y + landingDustVerticalOffset;
        }
        else if (characterController != null)
        {
            worldPosition.y = characterController.bounds.min.y + landingDustVerticalOffset;
        }
        else
        {
            worldPosition.y = transform.position.y + landingDustVerticalOffset;
        }

        landingRoot.transform.position = worldPosition;
    }

    private void HandleAttackStarted(PlayerAttack attack)
    {
        if (attack == null)
            return;

        ResetPiledriverToRestingPosition();

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

    private void ResetPiledriverToRestingPosition()
    {
        StopAndClearRoutine(ref piledriverRoutine);
        SetGauntletPiledriverLocalX(piledriverRestingLocalX);
    }

    public void LeftFire() => TriggerLeftAttackVfx();

    public void RightFire() => TriggerRightAttackVfx();

    public void IdleFire() => TriggerIdleFireVfx();

    public void DashDust() => TriggerDashDustVfx();

    public void PunchDust_L() => TriggerLeftPunchDustVfx();

    public void PunchDust_R() => TriggerRightPunchDustVfx();

    public void RetractPile() => TriggerPileRetract();

    public void FullExtension() => TriggerPileFullExtension();

    public void LExhaust() => TriggerLeftExhaustVfx();

    public void RExhaust() => TriggerRightExhaustVfx();

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
        TriggerRightAttackVfx(attackDuration);
    }

    private void TriggerRightAttackVfx(float duration)
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
            duration,
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

    private void TriggerIdleFireVfx()
    {
        if (idleFireVfx == null)
            return;

        if (idleFireEffect == null)
            idleFireEffect =
                idleFireVfx.GetComponent<VisualEffect>()
                ?? idleFireVfx.GetComponentInChildren<VisualEffect>(true);

        if (idleFireEffect != null && idleFireHasCachedEmberRate)
            TrySetFloat(idleFireEffect, emberRateProperty, idleFireCachedEmberRate);

        idleFireVfx.SetActive(true);
        PlayAudio(attackAudioClip);

        RestartSingleRoutine(
            ref idleFireDeactivateRoutine,
            idleFireDuration,
            () =>
            {
                idleFireDeactivateRoutine = StartCoroutine(DisableIdleFireAfterTail());
            }
        );
    }

    private IEnumerator DisableIdleFireAfterTail()
    {
        if (idleFireEffect == null && idleFireVfx != null)
            idleFireEffect =
                idleFireVfx.GetComponent<VisualEffect>()
                ?? idleFireVfx.GetComponentInChildren<VisualEffect>(true);

        TrySetFloat(idleFireEffect, emberRateProperty, emberRateOff);

        float shutdownDelay = Mathf.Max(0f, idleFireShutdownDelay);
        if (shutdownDelay > 0f)
            yield return new WaitForSeconds(shutdownDelay);

        if (idleFireVfx != null)
            idleFireVfx.SetActive(false);

        idleFireDeactivateRoutine = null;
    }

    private void TriggerDashDustVfx()
    {
        if (dashDustVfx == null)
            return;

        if (dashDustParticles == null || dashDustParticles.Length == 0)
            dashDustParticles = CollectParticleSystems(dashDustVfx);

        if (dashDustEffects == null || dashDustEffects.Length == 0)
            dashDustEffects = CollectVisualEffects(dashDustVfx);

        PlayOneShotVfx(dashDustVfx, dashDustParticles, dashDustEffects);
    }

    private void TriggerLeftPunchDustVfx()
    {
        if (leftPunchDustVfx == null)
            return;

        if (leftPunchDustParticles == null || leftPunchDustParticles.Length == 0)
            leftPunchDustParticles = CollectParticleSystems(leftPunchDustVfx);

        PlayOneShotVfx(leftPunchDustVfx, leftPunchDustParticles, Array.Empty<VisualEffect>());
    }

    private void TriggerRightPunchDustVfx()
    {
        if (rightPunchDustVfx == null)
            return;

        if (rightPunchDustParticles == null || rightPunchDustParticles.Length == 0)
            rightPunchDustParticles = CollectParticleSystems(rightPunchDustVfx);

        PlayOneShotVfx(rightPunchDustVfx, rightPunchDustParticles, Array.Empty<VisualEffect>());
    }

    private void TriggerPileRetract()
    {
        if (gauntletPiledriver == null)
            return;

        TriggerPiledriverExtendSparkBackVfx();

        StopAndClearRoutine(ref piledriverRoutine);
        piledriverRoutine = StartCoroutine(
            MovePiledriverToLocalX(
                piledriverRetractedLocalX,
                piledriverRetractDuration,
                () => piledriverRoutine = null
            )
        );
    }

    private void TriggerPileFullExtension()
    {
        if (gauntletPiledriver == null)
            return;

        TriggerPiledriverExtendSparkFrontVfx();

        StopAndClearRoutine(ref piledriverRoutine);
        piledriverRoutine = StartCoroutine(PiledriverFullExtensionRoutine());
    }

    private void TriggerPiledriverExtendSparkFrontVfx()
    {
        if (piledriverExtendSparkFrontVfx == null)
            return;

        if (piledriverExtendSparkFrontParticles == null || piledriverExtendSparkFrontParticles.Length == 0)
            piledriverExtendSparkFrontParticles = CollectParticleSystems(piledriverExtendSparkFrontVfx);

        if (piledriverExtendSparkFrontEffects == null || piledriverExtendSparkFrontEffects.Length == 0)
            piledriverExtendSparkFrontEffects = CollectVisualEffects(piledriverExtendSparkFrontVfx);

        RestartPiledriverSparkRoutine(
            piledriverExtendSparkFrontVfx,
            piledriverExtendSparkFrontParticles,
            piledriverExtendSparkFrontEffects,
            piledriverExtendSparkFrontDuration,
            ref piledriverExtendSparkFrontRoutine,
            () => piledriverExtendSparkFrontRoutine = null
        );
    }

    private void TriggerPiledriverExtendSparkBackVfx()
    {
        if (piledriverExtendSparkBackVfx == null)
            return;

        if (piledriverExtendSparkBackParticles == null || piledriverExtendSparkBackParticles.Length == 0)
            piledriverExtendSparkBackParticles = CollectParticleSystems(piledriverExtendSparkBackVfx);

        if (piledriverExtendSparkBackEffects == null || piledriverExtendSparkBackEffects.Length == 0)
            piledriverExtendSparkBackEffects = CollectVisualEffects(piledriverExtendSparkBackVfx);

        RestartPiledriverSparkRoutine(
            piledriverExtendSparkBackVfx,
            piledriverExtendSparkBackParticles,
            piledriverExtendSparkBackEffects,
            piledriverExtendSparkBackDuration,
            ref piledriverExtendSparkBackRoutine,
            () => piledriverExtendSparkBackRoutine = null
        );
    }

    private void RestartPiledriverSparkRoutine(
        GameObject root,
        ParticleSystem[] particleSystems,
        VisualEffect[] visualEffects,
        float duration,
        ref Coroutine routine,
        Action onComplete
    )
    {
        bool hasParticles = particleSystems != null && particleSystems.Length > 0;
        bool hasVisualEffects = visualEffects != null && visualEffects.Length > 0;

        if (root == null || (!hasParticles && !hasVisualEffects))
            return;

        StopAndClearRoutine(ref routine);
        SetPiledriverSparkActive(root, particleSystems, visualEffects);
        routine = StartCoroutine(RunPiledriverSpark(root, particleSystems, visualEffects, duration, onComplete));
    }

    private IEnumerator RunPiledriverSpark(
        GameObject root,
        ParticleSystem[] particleSystems,
        VisualEffect[] visualEffects,
        float duration,
        Action onComplete
    )
    {
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration > 0f)
            yield return new WaitForSeconds(clampedDuration);

        SetPiledriverSparkIdle(root, particleSystems, visualEffects);
        onComplete?.Invoke();
    }

    private void SetPiledriverSparkActive(GameObject root, ParticleSystem[] particleSystems, VisualEffect[] visualEffects)
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (!particleSystem.gameObject.activeSelf)
                    particleSystem.gameObject.SetActive(true);

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        if (visualEffects != null)
        {
            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                visualEffect.Reinit();
                visualEffect.Play();
            }
        }
    }

    private void SetPiledriverSparkIdle(GameObject root, ParticleSystem[] particleSystems, VisualEffect[] visualEffects)
    {
        StopAndClearParticleSystems(particleSystems);

        if (visualEffects != null)
        {
            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                visualEffect.Stop();
                visualEffect.Reinit();
            }
        }

        if (root != null && root.activeSelf)
            root.SetActive(false);
    }

    private IEnumerator PiledriverFullExtensionRoutine()
    {
        yield return MovePiledriverToLocalX(piledriverFullExtensionLocalX, piledriverExtendDuration);
        yield return MovePiledriverToLocalX(piledriverRestingLocalX, piledriverReturnDuration);
        piledriverRoutine = null;
    }

    private IEnumerator MovePiledriverToLocalX(float targetLocalX, float duration, Action onComplete = null)
    {
        if (gauntletPiledriver == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector3 startLocalPosition = gauntletPiledriver.localPosition;
        Vector3 targetLocalPosition = new Vector3(
            targetLocalX,
            startLocalPosition.y,
            startLocalPosition.z
        );

        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
        {
            gauntletPiledriver.localPosition = targetLocalPosition;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / clampedDuration);
            gauntletPiledriver.localPosition = Vector3.Lerp(
                startLocalPosition,
                targetLocalPosition,
                t
            );
            yield return null;
        }

        gauntletPiledriver.localPosition = targetLocalPosition;
        onComplete?.Invoke();
    }

    private void SetGauntletPiledriverLocalX(float localX)
    {
        if (gauntletPiledriver == null)
            return;

        Vector3 localPosition = gauntletPiledriver.localPosition;
        gauntletPiledriver.localPosition = new Vector3(localX, localPosition.y, localPosition.z);
    }

    private void TriggerLeftExhaustVfx()
    {
        if (leftExhaustVfx == null)
            return;

        if (leftExhaustParticles == null || leftExhaustParticles.Length == 0)
        {
            leftExhaustParticles = CollectParticleSystems(leftExhaustVfx);
            CacheOriginalExhaustEmissionRates(leftExhaustParticles);
        }

        RestartExhaustRoutine(
            leftExhaustVfx,
            leftExhaustParticles,
            ref leftExhaustRoutine,
            () => leftExhaustRoutine = null
        );
    }

    private void TriggerRightExhaustVfx()
    {
        if (rightExhaustVfx == null)
            return;

        if (rightExhaustParticles == null || rightExhaustParticles.Length == 0)
        {
            rightExhaustParticles = CollectParticleSystems(rightExhaustVfx);
            CacheOriginalExhaustEmissionRates(rightExhaustParticles);
        }

        RestartExhaustRoutine(
            rightExhaustVfx,
            rightExhaustParticles,
            ref rightExhaustRoutine,
            () => rightExhaustRoutine = null
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

    private void RestartExhaustRoutine(
        GameObject root,
        ParticleSystem[] particleSystems,
        ref Coroutine routine,
        Action onComplete
    )
    {
        if (root == null || particleSystems == null || particleSystems.Length == 0)
            return;

        StopAndClearRoutine(ref routine);
        PrepareExhaustVfx(root, particleSystems);
        PlayAudio(exhaustAudioClip);
        routine = StartCoroutine(RunExhaustVfx(root, particleSystems, onComplete));
    }

    private void PlayOneShotVfx(
        GameObject root,
        ParticleSystem[] particleSystems,
        VisualEffect[] visualEffects
    )
    {
        bool hasParticles = particleSystems != null && particleSystems.Length > 0;
        bool hasVisualEffects = visualEffects != null && visualEffects.Length > 0;
        if (root == null || (!hasParticles && !hasVisualEffects))
            return;

        if (!root.activeSelf)
            root.SetActive(true);

        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (!particleSystem.gameObject.activeSelf)
                    particleSystem.gameObject.SetActive(true);

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        if (visualEffects != null)
        {
            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                visualEffect.Reinit();
                visualEffect.Play();
            }
        }
    }

    private void StopAndClearParticleSystems(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }
    }

    private void PrepareExhaustVfx(GameObject root, ParticleSystem[] particleSystems)
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        SetExhaustEmissionScale(particleSystems, 1f);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (!particleSystem.gameObject.activeSelf)
                particleSystem.gameObject.SetActive(true);

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private IEnumerator RunExhaustVfx(
        GameObject root,
        ParticleSystem[] particleSystems,
        Action onComplete
    )
    {
        float fullEmissionDuration = Mathf.Max(0f, exhaustFullEmissionDuration);
        if (fullEmissionDuration > 0f)
            yield return new WaitForSeconds(fullEmissionDuration);

        float fadeDuration = Mathf.Max(0f, exhaustFadeDuration);
        float tailScale = Mathf.Clamp01(exhaustTailEmissionScale);

        if (fadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                SetExhaustEmissionScale(particleSystems, Mathf.Lerp(1f, tailScale, progress));
                yield return null;
            }
        }

        SetExhaustEmissionScale(particleSystems, tailScale);
        StopExhaustEmission(particleSystems);

        float disableDelay = Mathf.Max(0f, exhaustDisableDelay);
        if (disableDelay > 0f)
            yield return new WaitForSeconds(disableDelay);

        SetExhaustVfxIdle(root, particleSystems, hideRoot: true);
        onComplete?.Invoke();
    }

    private void StopExhaustEmission(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SetExhaustVfxIdle(GameObject root, ParticleSystem[] particleSystems, bool hideRoot)
    {
        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                RestoreExhaustEmissionRate(particleSystem);
            }
        }

        if (hideRoot && root != null && root.activeSelf)
            root.SetActive(false);
    }

    private void SetExhaustEmissionScale(ParticleSystem[] particleSystems, float scale)
    {
        if (particleSystems == null)
            return;

        scale = Mathf.Max(0f, scale);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (originalExhaustEmissionRates.TryGetValue(particleSystem, out float originalRate))
                emission.rateOverTimeMultiplier = originalRate * scale;
            else
                emission.rateOverTimeMultiplier *= scale;
        }
    }

    private void RestoreExhaustEmissionRate(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return;

        if (!originalExhaustEmissionRates.TryGetValue(particleSystem, out float originalRate))
            return;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTimeMultiplier = originalRate;
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

        playerMovement.DashPerformed += HandleLandingTriggered;
        playerMovement.DoubleJumpPerformed += HandleAirMoveTriggered;
        playerMovement.AirDashPerformed += HandleAirMoveTriggered;
        playerMovement.Landed += HandleLandingTriggered;
        airMoveCallbacksRegistered = true;
    }

    private void UnregisterAirMoveCallbacks()
    {
        if (playerMovement == null || !airMoveCallbacksRegistered)
            return;

        playerMovement.DashPerformed -= HandleLandingTriggered;
        playerMovement.DoubleJumpPerformed -= HandleAirMoveTriggered;
        playerMovement.AirDashPerformed -= HandleAirMoveTriggered;
        playerMovement.Landed -= HandleLandingTriggered;
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

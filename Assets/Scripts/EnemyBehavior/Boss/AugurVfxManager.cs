using System.Collections;
using System;
using UnityEngine;
using UnityEngine.VFX;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Boss-specific VFX coordinator for Augur.
    /// Uses a mix of explicit boss callbacks and light state watching so VFX setup does not depend on adding
    /// animation events to every boss clip.
    /// </summary>
    public sealed class AugurVfxManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossRoombaBrain bossBrain;
        [SerializeField] private BossRoombaController bossController;
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private AudioSource audioSource;

        [Header("Alarm Whistle")]
        [SerializeField, Tooltip("Optional renderer used for alarm flashing. If left empty, the VFX manager will look for a child named 'whistle_low'.")]
        private Renderer alarmFlashRenderer;
        [SerializeField, Tooltip("Renderer material property used for emission color.")]
        private string alarmFlashEmissionProperty = "_EmissionColor";
        [SerializeField, Tooltip("Base emission color used for the whistle alarm flash.")]
        private Color alarmFlashEmissionColor = Color.red;
        [SerializeField, Tooltip("Minimum emission multiplier while the alarm is idle between flashes.")]
        private float alarmFlashMinIntensity = 0f;
        [SerializeField, Tooltip("Maximum emission multiplier while adds are actively spawning.")]
        private float alarmFlashMaxIntensity = 10f;
        [SerializeField, Tooltip("How quickly the whistle emission pulses while the alarm is active.")]
        private float alarmFlashSpeed = 5f;
        [SerializeField, Tooltip("Optional transform rotated while the alarm is active. If left empty, the VFX manager will look for a child named 'Lights'.")]
        private Transform alarmLightsTransform;
        [SerializeField, Tooltip("Y-axis rotation speed for the alarm lights while the alarm is active.")]
        private float alarmLightsRotationSpeed = 240f;
        [SerializeField, Tooltip("When the alarm lights rotation passes this Y angle, it snaps back to 0 and continues rotating.")]
        private float alarmLightsSnapThreshold = 260f;

        [Header("Exhaust Fire")]
        [SerializeField, Tooltip("Roots that pulse when Augur gets aggressive or starts an action.")]
        private GameObject[] exhaustVfxRoots = Array.Empty<GameObject>();
        [SerializeField] private float actionExhaustDuration = 0.45f;
        [SerializeField] private float enragedExhaustDuration = 1.15f;
        [SerializeField] private float exhaustShutdownDelay = 0.2f;
        [SerializeField] private AudioClip exhaustAudioClip;

        [Header("Dash Indicator")]
        [SerializeField, Tooltip("Anchor used as the spawn origin for the dash indicator prefab.")]
        private Transform dashVfxTargetLocation;
        [SerializeField, Tooltip("Prefab spawned before a dash to show the committed dash distance.")]
        private GameObject dashIndicatorVfxPrefab;
        [SerializeField, Tooltip("Applied to the computed dash distance before sizing the telegraph. Use this to compensate for authored prefab length.")]
        private float dashIndicatorLengthOffset = -10f;
        [SerializeField, Tooltip("Extra lifetime after the dash telegraph is hidden before the prefab is destroyed.")]
        private float dashIndicatorDestroyDelay = 0.05f;
        [SerializeField] private AudioClip dashIndicatorAudioClip;

        [Header("Dash Ring")]
        [SerializeField, Tooltip("Optional anchor used to spawn the dash-start ring on the boss. Falls back to the dash telegraph anchor or boss transform.")]
        private Transform dashRingVfxTargetLocation;
        [SerializeField, Tooltip("Prefab spawned when a dash starts. Its particle lifetime is set from the planned dash duration.")]
        private GameObject dashRingVfxPrefab;
        [SerializeField, Tooltip("Extra lifetime after the dash ring is hidden before the prefab is destroyed.")]
        private float dashRingDestroyDelay = 0.05f;

        [Header("Static Charge VFX")]
        [SerializeField, Tooltip("Anchor for the static charge telegraph (falls back to dashVfxTargetLocation).")]
        private Transform staticChargeVfxTargetLocation;
        [SerializeField, Tooltip("Telegraph prefab shown before a static charge. Falls back to dashIndicatorVfxPrefab if null.")]
        private GameObject staticChargeTelegraphVfxPrefab;
        [SerializeField, Tooltip("Applied to the computed charge distance before sizing the static charge telegraph. Use to compensate for authored prefab length.")]
        private float staticChargeIndicatorLengthOffset = -10f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowStaticChargeTelegraph is called before the telegraph VFX actually spawns.")]
        private float staticChargeTelegraphStartDelay = 0f;
        [SerializeField, Tooltip("Ring prefab spawned when the static charge launches. Falls back to dashRingVfxPrefab if null.")]
        private GameObject staticChargeRingVfxPrefab;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowStaticChargeRing is called before the ring VFX actually spawns.")]
        private float staticChargeRingStartDelay = 0f;
        [SerializeField, Tooltip("Audio clip played alongside the static charge telegraph.")]
        private AudioClip staticChargeTelegraphAudioClip;
        [SerializeField, Tooltip("Secondary VFX prefab (e.g. speed lines) shown when a static charge launches. Positioned/sized the same way as the telegraph, but independently delayed. Falls back to nothing if null.")]
        private GameObject staticChargeSpeedLinesPrefab;
        [SerializeField, Tooltip("Applied to the computed charge distance before sizing the speed lines VFX.")]
        private float staticChargeSpeedLinesLengthOffset = -10f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowStaticChargeTelegraph is called before the speed lines VFX spawns. Position, rotation, and size are snapshotted at call time so the VFX always appears at the charge launch location even if the delay pushes it past the end of the windup.")]
        private float staticChargeSpeedLinesStartDelay = 0f;
        [SerializeField, Min(0f), Tooltip("How long the speed lines VFX persists after it spawns.")]
        private float staticChargeSpeedLinesDuration = 1f;

        [Header("Targeted Charge VFX")]
        [SerializeField, Tooltip("Anchor for the targeted charge telegraph (falls back to dashVfxTargetLocation).")]
        private Transform targetedChargeVfxTargetLocation;
        [SerializeField, Tooltip("Telegraph prefab shown before a targeted charge. Falls back to dashIndicatorVfxPrefab if null.")]
        private GameObject targetedChargeTelegraphVfxPrefab;
        [SerializeField, Tooltip("Applied to the computed charge distance before sizing the targeted charge telegraph.")]
        private float targetedChargeIndicatorLengthOffset = -10f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowTargetedChargeTelegraph is called before the telegraph VFX actually spawns.")]
        private float targetedChargeTelegraphStartDelay = 0f;
        [SerializeField, Tooltip("Ring prefab spawned when the targeted charge launches. Falls back to dashRingVfxPrefab if null.")]
        private GameObject targetedChargeRingVfxPrefab;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowTargetedChargeRing is called before the ring VFX actually spawns.")]
        private float targetedChargeRingStartDelay = 0f;
        [SerializeField, Tooltip("Audio clip played alongside the targeted charge telegraph.")]
        private AudioClip targetedChargeTelegraphAudioClip;
        [SerializeField, Tooltip("Secondary VFX prefab (e.g. speed lines) shown when a targeted charge launches. Positioned/sized the same way as the telegraph, but independently delayed. Falls back to nothing if null.")]
        private GameObject targetedChargeSpeedLinesPrefab;
        [SerializeField, Tooltip("Applied to the computed charge distance before sizing the speed lines VFX.")]
        private float targetedChargeSpeedLinesLengthOffset = -10f;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after ShowTargetedChargeTelegraph is called before the speed lines VFX spawns. Position, rotation, and size are snapshotted at call time so the VFX always appears at the charge launch location even if the delay pushes it past the end of the windup.")]
        private float targetedChargeSpeedLinesStartDelay = 0f;
        [SerializeField, Min(0f), Tooltip("How long the speed lines VFX persists after it spawns.")]
        private float targetedChargeSpeedLinesDuration = 1f;

        [Header("Panel Break")]
        [SerializeField, Tooltip("Optional electricity prefab spawned when a side panel breaks.")]
        private GameObject panelBreakElectricityPrefab;
        [SerializeField, Tooltip("Optional shared explosion prefab spawned when the alarm or a panel breaks.")]
        private GameObject breakExplosionPrefab;
        [SerializeField, Tooltip("Optional spawn location for the alarm break explosion.")]
        private Transform alarmBreakExplosionLocation;
        [SerializeField, Tooltip("Optional spawn locations for panel break explosions. Array index should match panel index.")]
        private Transform[] panelBreakExplosionLocations = Array.Empty<Transform>();
        [SerializeField] private Vector3 panelBreakOffset = Vector3.zero;
        [SerializeField] private float panelBreakScale = 1f;
        [SerializeField] private float panelBreakLifetime = 5f;
        [SerializeField] private AudioClip panelBreakAudioClip;

        [Header("Death")]
        [SerializeField] private Transform deathVfxAnchor;
        [SerializeField] private GameObject deathElectricityPrefab;
        [SerializeField] private GameObject deathExplosionPrefab;
        [SerializeField] private Vector3 deathVfxOffset = Vector3.zero;
        [SerializeField] private float deathVfxScale = 1f;
        [SerializeField] private float deathVfxLifetime = 8f;
        [SerializeField] private float deathExplosionRepeatInterval = 1f;
        [SerializeField] private Vector2 deathExplosionRandomScaleRange = new Vector2(1.5f, 2.5f);
        [SerializeField] private AudioClip deathAudioClip;

        private Coroutine exhaustRoutine;
        private Coroutine dashIndicatorRoutine;
        private Coroutine alarmFlashRoutine;
        private Coroutine deathExplosionRoutine;
        private bool deathTriggered;
        private bool dashExhaustActive;
        private bool alarmFlashActive;
        private bool cachedFormInitialized;
        private RoombaForm cachedForm;
        private GameObject activeDashIndicatorInstance;
        private GameObject activeDashRingInstance;
        private ParticleSystem[] activeDashIndicatorParticles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] activeDashRingParticles = Array.Empty<ParticleSystem>();
        private GameObject activeStaticChargeTelegraphInstance;
        private GameObject activeStaticChargeRingInstance;
        private ParticleSystem[] activeStaticChargeTelegraphParticles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] activeStaticChargeRingParticles = Array.Empty<ParticleSystem>();
        private GameObject activeTargetedChargeTelegraphInstance;
        private GameObject activeTargetedChargeRingInstance;
        private ParticleSystem[] activeTargetedChargeTelegraphParticles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] activeTargetedChargeRingParticles = Array.Empty<ParticleSystem>();
        private Coroutine staticChargeTelegraphRoutine;
        private Coroutine staticChargeRingRoutine;
        private Coroutine staticChargeSpeedLinesRoutine;
        private Coroutine targetedChargeTelegraphRoutine;
        private Coroutine targetedChargeRingRoutine;
        private Coroutine targetedChargeSpeedLinesRoutine;
        private GameObject activeStaticChargeSpeedLinesInstance;
        private ParticleSystem[] activeStaticChargeSpeedLinesParticles = Array.Empty<ParticleSystem>();
        private GameObject activeTargetedChargeSpeedLinesInstance;
        private ParticleSystem[] activeTargetedChargeSpeedLinesParticles = Array.Empty<ParticleSystem>();
        private MaterialPropertyBlock alarmFlashPropertyBlock;
        private int alarmFlashEmissionPropertyId;
        private Color alarmFlashOriginalEmissionColor = Color.black;
        private bool alarmFlashHasOriginalEmission;
        private Vector3 alarmLightsInitialLocalEulerAngles;
        private bool alarmLightsHasInitialRotation;

        private void Awake()
        {
            if (bossBrain == null)
                bossBrain = GetComponentInParent<BossRoombaBrain>();

            if (bossController == null)
                bossController = GetComponentInParent<BossRoombaController>();

            if (bossHealth == null)
                bossHealth = GetComponentInParent<BossHealth>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = GetComponentInParent<AudioSource>();

            if (deathVfxAnchor == null)
                deathVfxAnchor = transform;

            if (bossBrain != null)
            {
                cachedForm = bossBrain.CurrentForm;
                cachedFormInitialized = true;
            }

            InitializeAlarmFlashRenderer();
            InitializeAlarmLightsTransform();

            ResetManagedState();
        }

        private void OnEnable()
        {
            if (bossHealth != null)
                bossHealth.BossDefeated += HandleBossDefeated;

            if (bossBrain != null)
                bossBrain.SidePanelDestroyed += HandleSidePanelDestroyed;

            if (bossController != null)
                bossController.AlarmDestroyed += HandleAlarmDestroyed;

            ResetManagedState();
        }

        private void OnDisable()
        {
            if (bossHealth != null)
                bossHealth.BossDefeated -= HandleBossDefeated;

            if (bossBrain != null)
                bossBrain.SidePanelDestroyed -= HandleSidePanelDestroyed;

            if (bossController != null)
                bossController.AlarmDestroyed -= HandleAlarmDestroyed;

            if (exhaustRoutine != null)
            {
                StopCoroutine(exhaustRoutine);
                exhaustRoutine = null;
            }

            if (dashIndicatorRoutine != null)
            {
                StopCoroutine(dashIndicatorRoutine);
                dashIndicatorRoutine = null;
            }

            if (deathExplosionRoutine != null)
            {
                StopCoroutine(deathExplosionRoutine);
                deathExplosionRoutine = null;
            }

            StopAlarmFlash();

            ResetManagedState();
        }

        private void Update()
        {
            if (deathTriggered || bossBrain == null)
                return;

            if (!cachedFormInitialized)
            {
                cachedForm = bossBrain.CurrentForm;
                cachedFormInitialized = true;
            }
            else if (cachedForm != bossBrain.CurrentForm)
            {
                RoombaForm previousForm = cachedForm;
                cachedForm = bossBrain.CurrentForm;
                if (previousForm != RoombaForm.CageBull && cachedForm == RoombaForm.CageBull)
                    TriggerEnragedExhaust();
            }

            bool shouldFlashAlarm = ShouldFlashAlarmWhistle();
            if (shouldFlashAlarm != alarmFlashActive)
            {
                if (shouldFlashAlarm)
                    StartAlarmFlash();
                else
                    StopAlarmFlash();
            }

            UpdateAlarmLightsRotation(shouldFlashAlarm);

        }

        public void NotifyAttackWindup()
        {
            if (deathTriggered)
                return;

            TriggerExhaustBurst(actionExhaustDuration);
        }

        public void NotifyDashLungeStarted()
        {
            NotifyDashLungeStarted(actionExhaustDuration);
        }

        public void NotifyDashLungeStarted(float dashDuration)
        {
            if (deathTriggered)
                return;

            HideDashTelegraph();
            StartDashExhaust();
        }

        public void NotifyDashLungeEnded()
        {
            if (!deathTriggered)
            {
                StopDashExhaust();
                HideDashRing();
            }
        }

        public void TriggerExhaustBurst()
        {
            TriggerExhaustBurst(actionExhaustDuration);
        }

        public void TriggerEnragedExhaust()
        {
            TriggerExhaustBurst(enragedExhaustDuration);
        }

        public void ShowDashTelegraph(Vector3 dashDestination, float windupDelay)
        {
            if (deathTriggered || dashIndicatorVfxPrefab == null)
                return;

            HideDashTelegraph();

            Transform spawnAnchor = dashVfxTargetLocation != null ? dashVfxTargetLocation : transform;
            Vector3 spawnPosition = spawnAnchor.position;
            Quaternion spawnRotation = GetDashIndicatorRotation(spawnAnchor, dashDestination);
            float dashDistance = Mathf.Max(0.01f, GetFlatDistance(spawnPosition, dashDestination) + dashIndicatorLengthOffset);
            float telegraphLifetime = Mathf.Max(0.01f, windupDelay);

            activeDashIndicatorInstance = Instantiate(dashIndicatorVfxPrefab, spawnPosition, spawnRotation);
            activeDashIndicatorParticles = activeDashIndicatorInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureDashIndicator(activeDashIndicatorParticles, dashDistance, telegraphLifetime);
            RestartObject(activeDashIndicatorInstance);
            PlayClip(dashIndicatorAudioClip);
            dashIndicatorRoutine = StartCoroutine(HideDashTelegraphAfterDelay(telegraphLifetime));
        }

        public void HideDashTelegraph()
        {
            if (dashIndicatorRoutine != null)
            {
                StopCoroutine(dashIndicatorRoutine);
                dashIndicatorRoutine = null;
            }

            if (activeDashIndicatorInstance == null)
                return;

            StopEffects(activeDashIndicatorInstance);
            Destroy(activeDashIndicatorInstance, Mathf.Max(0f, dashIndicatorDestroyDelay));
            activeDashIndicatorInstance = null;
            activeDashIndicatorParticles = Array.Empty<ParticleSystem>();
        }

        public void ShowDashRing(float dashDuration)
        {
            if (deathTriggered || dashRingVfxPrefab == null)
                return;

            HideDashRing();

            Transform spawnAnchor = dashRingVfxTargetLocation != null
                ? dashRingVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform);
            float ringLifetime = Mathf.Max(5f, dashDuration);

            activeDashRingInstance = Instantiate(dashRingVfxPrefab, spawnAnchor.position, spawnAnchor.rotation);
            activeDashRingInstance.transform.SetParent(spawnAnchor, worldPositionStays: true);
            activeDashRingParticles = activeDashRingInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureParticleLifetime(activeDashRingParticles, ringLifetime);
            RestartObject(activeDashRingInstance);
        }

        public void HideDashRing()
        {
            if (activeDashRingInstance == null)
                return;

            StopEffects(activeDashRingInstance);
            Destroy(activeDashRingInstance, Mathf.Max(0f, dashRingDestroyDelay));
            activeDashRingInstance = null;
            activeDashRingParticles = Array.Empty<ParticleSystem>();
        }

        // ─── Static Charge VFX ───────────────────────────────────────────────

        public void ShowStaticChargeTelegraph(Vector3 chargeDestination, float windupDelay)
        {
            if (deathTriggered)
                return;

            HideStaticChargeTelegraph();
            staticChargeTelegraphRoutine = StartCoroutine(SpawnStaticChargeTelegraph(chargeDestination, windupDelay));
        }

        private IEnumerator SpawnStaticChargeTelegraph(Vector3 chargeDestination, float windupDelay)
        {
            if (staticChargeTelegraphStartDelay > 0f)
                yield return new WaitForSeconds(staticChargeTelegraphStartDelay);

            if (deathTriggered) yield break;

            GameObject prefab = staticChargeTelegraphVfxPrefab != null ? staticChargeTelegraphVfxPrefab : dashIndicatorVfxPrefab;
            if (prefab == null) yield break;

            Transform anchor = staticChargeVfxTargetLocation != null ? staticChargeVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform);
            float dist = Mathf.Max(0.01f, GetFlatDistance(anchor.position, chargeDestination) + staticChargeIndicatorLengthOffset);
            float lifetime = Mathf.Max(0.01f, windupDelay - staticChargeTelegraphStartDelay);
            Quaternion rot = GetDashIndicatorRotation(anchor, chargeDestination);

            activeStaticChargeTelegraphInstance = Instantiate(prefab, anchor.position, rot);
            activeStaticChargeTelegraphParticles = activeStaticChargeTelegraphInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureDashIndicator(activeStaticChargeTelegraphParticles, dist, lifetime);
            RestartObject(activeStaticChargeTelegraphInstance);

            AudioClip clip = staticChargeTelegraphAudioClip != null ? staticChargeTelegraphAudioClip : dashIndicatorAudioClip;
            PlayClip(clip);

            yield return new WaitForSeconds(lifetime);
            HideStaticChargeTelegraph();
        }

        public void HideStaticChargeTelegraph()
        {
            if (staticChargeTelegraphRoutine != null)
            {
                StopCoroutine(staticChargeTelegraphRoutine);
                staticChargeTelegraphRoutine = null;
            }

            if (activeStaticChargeTelegraphInstance == null)
                return;

            StopEffects(activeStaticChargeTelegraphInstance);
            Destroy(activeStaticChargeTelegraphInstance, Mathf.Max(0f, dashIndicatorDestroyDelay));
            activeStaticChargeTelegraphInstance = null;
            activeStaticChargeTelegraphParticles = Array.Empty<ParticleSystem>();
        }


        public void ShowStaticChargeRing(float chargeDuration)
        {
            if (deathTriggered)
                return;

            HideStaticChargeRing();
            staticChargeRingRoutine = StartCoroutine(SpawnStaticChargeRing(chargeDuration));
        }

        private IEnumerator SpawnStaticChargeRing(float chargeDuration)
        {
            if (staticChargeRingStartDelay > 0f)
                yield return new WaitForSeconds(staticChargeRingStartDelay);

            if (deathTriggered) yield break;

            GameObject prefab = staticChargeRingVfxPrefab != null ? staticChargeRingVfxPrefab : dashRingVfxPrefab;
            if (prefab == null) yield break;

            Transform anchor = staticChargeVfxTargetLocation != null ? staticChargeVfxTargetLocation
                : (dashRingVfxTargetLocation != null ? dashRingVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform));
            float lifetime = Mathf.Max(5f, chargeDuration);

            activeStaticChargeRingInstance = Instantiate(prefab, anchor.position, anchor.rotation);
            activeStaticChargeRingInstance.transform.SetParent(anchor, worldPositionStays: true);
            activeStaticChargeRingParticles = activeStaticChargeRingInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureParticleLifetime(activeStaticChargeRingParticles, lifetime);
            RestartObject(activeStaticChargeRingInstance);
            staticChargeRingRoutine = null;
        }

        public void HideStaticChargeRing()
        {
            if (staticChargeRingRoutine != null)
            {
                StopCoroutine(staticChargeRingRoutine);
                staticChargeRingRoutine = null;
            }

            if (activeStaticChargeRingInstance == null)
                return;

            StopEffects(activeStaticChargeRingInstance);
            Destroy(activeStaticChargeRingInstance, Mathf.Max(0f, dashRingDestroyDelay));
            activeStaticChargeRingInstance = null;
            activeStaticChargeRingParticles = Array.Empty<ParticleSystem>();
        }

        /// <summary>
        /// Shows the static charge speed lines VFX. Position, rotation, and size are snapshotted
        /// immediately from the anchor so the VFX always spawns at the correct launch location
        /// regardless of how long the start delay is.
        /// </summary>
        public void ShowStaticChargeSpeedLines(Vector3 chargeDestination)
        {
            if (deathTriggered || staticChargeSpeedLinesPrefab == null)
                return;

            HideStaticChargeSpeedLines();

            // Snapshot everything now, before any delay, so the VFX lands correctly
            // even if the delay outlasts the windup and the boss is already moving.
            Transform anchor = staticChargeVfxTargetLocation != null ? staticChargeVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform);
            Vector3 spawnPos = anchor.position;
            Quaternion spawnRot = GetDashIndicatorRotation(anchor, chargeDestination);
            float dist = Mathf.Max(0.01f, GetFlatDistance(spawnPos, chargeDestination) + staticChargeSpeedLinesLengthOffset);
            float duration = Mathf.Max(0.01f, staticChargeSpeedLinesDuration);

            staticChargeSpeedLinesRoutine = StartCoroutine(SpawnStaticChargeSpeedLines(spawnPos, spawnRot, dist, duration));
        }

        private IEnumerator SpawnStaticChargeSpeedLines(Vector3 spawnPos, Quaternion spawnRot, float dist, float duration)
        {
            if (staticChargeSpeedLinesStartDelay > 0f)
                yield return new WaitForSeconds(staticChargeSpeedLinesStartDelay);

            if (deathTriggered || staticChargeSpeedLinesPrefab == null) yield break;

            activeStaticChargeSpeedLinesInstance = Instantiate(staticChargeSpeedLinesPrefab, spawnPos, spawnRot);
            activeStaticChargeSpeedLinesParticles = activeStaticChargeSpeedLinesInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureDashIndicator(activeStaticChargeSpeedLinesParticles, dist, duration);
            RestartObject(activeStaticChargeSpeedLinesInstance);

            yield return new WaitForSeconds(duration);
            HideStaticChargeSpeedLines();
        }

        public void HideStaticChargeSpeedLines()
        {
            if (staticChargeSpeedLinesRoutine != null)
            {
                StopCoroutine(staticChargeSpeedLinesRoutine);
                staticChargeSpeedLinesRoutine = null;
            }

            if (activeStaticChargeSpeedLinesInstance == null)
                return;

            StopEffects(activeStaticChargeSpeedLinesInstance);
            Destroy(activeStaticChargeSpeedLinesInstance, Mathf.Max(0f, dashIndicatorDestroyDelay));
            activeStaticChargeSpeedLinesInstance = null;
            activeStaticChargeSpeedLinesParticles = Array.Empty<ParticleSystem>();
        }

        // ─── Targeted Charge VFX ─────────────────────────────────────────────

        public void ShowTargetedChargeTelegraph(Vector3 chargeDestination, float windupDelay)
        {
            if (deathTriggered)
                return;

            HideTargetedChargeTelegraph();
            targetedChargeTelegraphRoutine = StartCoroutine(SpawnTargetedChargeTelegraph(chargeDestination, windupDelay));
        }

        private IEnumerator SpawnTargetedChargeTelegraph(Vector3 chargeDestination, float windupDelay)
        {
            if (targetedChargeTelegraphStartDelay > 0f)
                yield return new WaitForSeconds(targetedChargeTelegraphStartDelay);

            if (deathTriggered) yield break;

            GameObject prefab = targetedChargeTelegraphVfxPrefab != null ? targetedChargeTelegraphVfxPrefab : dashIndicatorVfxPrefab;
            if (prefab == null) yield break;

            Transform anchor = targetedChargeVfxTargetLocation != null ? targetedChargeVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform);
            float dist = Mathf.Max(0.01f, GetFlatDistance(anchor.position, chargeDestination) + targetedChargeIndicatorLengthOffset);
            float lifetime = Mathf.Max(0.01f, windupDelay - targetedChargeTelegraphStartDelay);
            Quaternion rot = GetDashIndicatorRotation(anchor, chargeDestination);

            activeTargetedChargeTelegraphInstance = Instantiate(prefab, anchor.position, rot);
            activeTargetedChargeTelegraphParticles = activeTargetedChargeTelegraphInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureDashIndicator(activeTargetedChargeTelegraphParticles, dist, lifetime);
            RestartObject(activeTargetedChargeTelegraphInstance);

            AudioClip clip = targetedChargeTelegraphAudioClip != null ? targetedChargeTelegraphAudioClip : dashIndicatorAudioClip;
            PlayClip(clip);

            yield return new WaitForSeconds(lifetime);
            HideTargetedChargeTelegraph();
        }

        public void HideTargetedChargeTelegraph()
        {
            if (targetedChargeTelegraphRoutine != null)
            {
                StopCoroutine(targetedChargeTelegraphRoutine);
                targetedChargeTelegraphRoutine = null;
            }

            if (activeTargetedChargeTelegraphInstance == null)
                return;

            StopEffects(activeTargetedChargeTelegraphInstance);
            Destroy(activeTargetedChargeTelegraphInstance, Mathf.Max(0f, dashIndicatorDestroyDelay));
            activeTargetedChargeTelegraphInstance = null;
            activeTargetedChargeTelegraphParticles = Array.Empty<ParticleSystem>();
        }


        public void ShowTargetedChargeRing(float chargeDuration)
        {
            if (deathTriggered)
                return;

            HideTargetedChargeRing();
            targetedChargeRingRoutine = StartCoroutine(SpawnTargetedChargeRing(chargeDuration));
        }

        private IEnumerator SpawnTargetedChargeRing(float chargeDuration)
        {
            if (targetedChargeRingStartDelay > 0f)
                yield return new WaitForSeconds(targetedChargeRingStartDelay);

            if (deathTriggered) yield break;

            GameObject prefab = targetedChargeRingVfxPrefab != null ? targetedChargeRingVfxPrefab : dashRingVfxPrefab;
            if (prefab == null) yield break;

            Transform anchor = targetedChargeVfxTargetLocation != null ? targetedChargeVfxTargetLocation
                : (dashRingVfxTargetLocation != null ? dashRingVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform));
            float lifetime = Mathf.Max(5f, chargeDuration);

            activeTargetedChargeRingInstance = Instantiate(prefab, anchor.position, anchor.rotation);
            activeTargetedChargeRingInstance.transform.SetParent(anchor, worldPositionStays: true);
            activeTargetedChargeRingParticles = activeTargetedChargeRingInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureParticleLifetime(activeTargetedChargeRingParticles, lifetime);
            RestartObject(activeTargetedChargeRingInstance);
            targetedChargeRingRoutine = null;
        }

        public void HideTargetedChargeRing()
        {
            if (targetedChargeRingRoutine != null)
            {
                StopCoroutine(targetedChargeRingRoutine);
                targetedChargeRingRoutine = null;
            }

            if (activeTargetedChargeRingInstance == null)
                return;

            StopEffects(activeTargetedChargeRingInstance);
            Destroy(activeTargetedChargeRingInstance, Mathf.Max(0f, dashRingDestroyDelay));
            activeTargetedChargeRingInstance = null;
            activeTargetedChargeRingParticles = Array.Empty<ParticleSystem>();
        }

        /// <summary>
        /// Shows the targeted charge speed lines VFX. Position, rotation, and size are snapshotted
        /// immediately from the anchor so the VFX always spawns at the correct launch location
        /// regardless of how long the start delay is.
        /// </summary>
        public void ShowTargetedChargeSpeedLines(Vector3 chargeDestination)
        {
            if (deathTriggered || targetedChargeSpeedLinesPrefab == null)
                return;

            HideTargetedChargeSpeedLines();

            // Snapshot everything now, before any delay, so the VFX lands correctly
            // even if the delay outlasts the windup and the boss is already moving.
            Transform anchor = targetedChargeVfxTargetLocation != null ? targetedChargeVfxTargetLocation
                : (dashVfxTargetLocation != null ? dashVfxTargetLocation : transform);
            Vector3 spawnPos = anchor.position;
            Quaternion spawnRot = GetDashIndicatorRotation(anchor, chargeDestination);
            float dist = Mathf.Max(0.01f, GetFlatDistance(spawnPos, chargeDestination) + targetedChargeSpeedLinesLengthOffset);
            float duration = Mathf.Max(0.01f, targetedChargeSpeedLinesDuration);

            targetedChargeSpeedLinesRoutine = StartCoroutine(SpawnTargetedChargeSpeedLines(spawnPos, spawnRot, dist, duration));
        }

        private IEnumerator SpawnTargetedChargeSpeedLines(Vector3 spawnPos, Quaternion spawnRot, float dist, float duration)
        {
            if (targetedChargeSpeedLinesStartDelay > 0f)
                yield return new WaitForSeconds(targetedChargeSpeedLinesStartDelay);

            if (deathTriggered || targetedChargeSpeedLinesPrefab == null) yield break;

            activeTargetedChargeSpeedLinesInstance = Instantiate(targetedChargeSpeedLinesPrefab, spawnPos, spawnRot);
            activeTargetedChargeSpeedLinesParticles = activeTargetedChargeSpeedLinesInstance.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureDashIndicator(activeTargetedChargeSpeedLinesParticles, dist, duration);
            RestartObject(activeTargetedChargeSpeedLinesInstance);

            yield return new WaitForSeconds(duration);
            HideTargetedChargeSpeedLines();
        }

        public void HideTargetedChargeSpeedLines()
        {
            if (targetedChargeSpeedLinesRoutine != null)
            {
                StopCoroutine(targetedChargeSpeedLinesRoutine);
                targetedChargeSpeedLinesRoutine = null;
            }

            if (activeTargetedChargeSpeedLinesInstance == null)
                return;

            StopEffects(activeTargetedChargeSpeedLinesInstance);
            Destroy(activeTargetedChargeSpeedLinesInstance, Mathf.Max(0f, dashIndicatorDestroyDelay));
            activeTargetedChargeSpeedLinesInstance = null;
            activeTargetedChargeSpeedLinesParticles = Array.Empty<ParticleSystem>();
        }



        private void TriggerExhaustBurst(float duration)
        {
            if (exhaustVfxRoots == null || exhaustVfxRoots.Length == 0)
                return;

            if (dashExhaustActive)
                return;

            if (exhaustRoutine != null)
                StopCoroutine(exhaustRoutine);

            RestartObjects(exhaustVfxRoots);
            PlayClip(exhaustAudioClip);
            exhaustRoutine = StartCoroutine(DisableObjectsAfterDelay(exhaustVfxRoots, Mathf.Max(0f, duration), Mathf.Max(0f, exhaustShutdownDelay), clearRoutine: true));
        }

        private void StartDashExhaust()
        {
            if (exhaustVfxRoots == null || exhaustVfxRoots.Length == 0)
                return;

            dashExhaustActive = true;

            if (exhaustRoutine != null)
            {
                StopCoroutine(exhaustRoutine);
                exhaustRoutine = null;
            }

            RestartObjects(exhaustVfxRoots);
            PlayClip(exhaustAudioClip);
        }

        private void StopDashExhaust()
        {
            if (!dashExhaustActive)
                return;

            dashExhaustActive = false;

            if (exhaustRoutine != null)
            {
                StopCoroutine(exhaustRoutine);
                exhaustRoutine = null;
            }

            exhaustRoutine = StartCoroutine(
                DisableObjectsAfterDelay(
                    exhaustVfxRoots,
                    0f,
                    Mathf.Max(0f, exhaustShutdownDelay),
                    clearRoutine: true
                )
            );
        }

        private void HandleSidePanelDestroyed(int panelIndex, Transform panelAnchor)
        {
            if (deathTriggered)
                return;

            Transform anchor = panelAnchor != null ? panelAnchor : transform;
            SpawnDetachedEffect(panelBreakElectricityPrefab, anchor, panelBreakOffset, panelBreakScale, panelBreakLifetime);
            SpawnBreakExplosion(GetPanelBreakExplosionAnchor(panelIndex, anchor));
            PlayClip(panelBreakAudioClip);
        }

        private void HandleAlarmDestroyed(Transform alarmAnchor)
        {
            if (deathTriggered)
                return;

            SpawnBreakExplosion(alarmBreakExplosionLocation != null ? alarmBreakExplosionLocation : alarmAnchor);
        }

        private void HandleBossDefeated()
        {
            if (deathTriggered)
                return;

            deathTriggered = true;
            HideDashTelegraph();
            HideDashRing();
            HideStaticChargeTelegraph();
            HideStaticChargeRing();
            HideStaticChargeSpeedLines();
            HideTargetedChargeTelegraph();
            HideTargetedChargeRing();
            HideTargetedChargeSpeedLines();
            StopAlarmFlash();
            StopExhaustImmediately();

            Transform explosionAnchor = deathVfxAnchor != null ? deathVfxAnchor : transform;
            RestartObject(deathElectricityPrefab);
            deathExplosionRoutine = StartCoroutine(RepeatDeathExplosions(explosionAnchor));
            PlayClip(deathAudioClip);
        }

        private IEnumerator RepeatDeathExplosions(Transform anchor)
        {
            float repeatInterval = Mathf.Max(0.01f, deathExplosionRepeatInterval);

            while (deathTriggered)
            {
                SpawnDetachedEffect(
                    deathExplosionPrefab,
                    anchor,
                    deathVfxOffset,
                    deathVfxScale * GetRandomDeathExplosionScale(),
                    deathVfxLifetime
                );

                yield return WaitForSecondsCache.Get(repeatInterval);
            }

            deathExplosionRoutine = null;
        }

        private IEnumerator HideDashTelegraphAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return WaitForSecondsCache.Get(delay);

            dashIndicatorRoutine = null;
            HideDashTelegraph();
        }

        private static float GetFlatDistance(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        private static Quaternion GetDashIndicatorRotation(Transform spawnAnchor, Vector3 dashDestination)
        {
            Vector3 origin = spawnAnchor.position;
            Vector3 flatDirection = dashDestination - origin;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude <= 0.0001f)
                return Quaternion.Euler(90f, spawnAnchor.eulerAngles.y, 0f);

            float yaw = Quaternion.LookRotation(flatDirection.normalized, Vector3.up).eulerAngles.y;
            return Quaternion.Euler(90f, yaw, 0f);
        }

        private IEnumerator DisableObjectsAfterDelay(GameObject[] roots, float activeDuration, float shutdownDelay, bool clearRoutine)
        {
            if (activeDuration > 0f)
                yield return WaitForSecondsCache.Get(activeDuration);

            StopEffects(roots);
            if (shutdownDelay > 0f)
                yield return WaitForSecondsCache.Get(shutdownDelay);

            if (roots != null)
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] != null)
                        roots[i].SetActive(false);
                }
            }

            if (clearRoutine)
                exhaustRoutine = null;
        }

        private void StopExhaustImmediately()
        {
            dashExhaustActive = false;

            if (exhaustRoutine != null)
            {
                StopCoroutine(exhaustRoutine);
                exhaustRoutine = null;
            }

            StopEffects(exhaustVfxRoots);

            if (exhaustVfxRoots == null)
                return;

            for (int i = 0; i < exhaustVfxRoots.Length; i++)
            {
                if (exhaustVfxRoots[i] != null)
                    exhaustVfxRoots[i].SetActive(false);
            }
        }

        private void SpawnDetachedEffect(GameObject prefab, Transform anchor, Vector3 offset, float scaleMultiplier, float lifetime)
        {
            if (prefab == null || anchor == null)
                return;

            GameObject instance = Instantiate(prefab, anchor.position + anchor.TransformVector(offset), anchor.rotation);
            if (!Mathf.Approximately(scaleMultiplier, 1f))
                instance.transform.localScale *= scaleMultiplier;

            RestartObject(instance);

            float destroyDelay = Mathf.Max(0f, lifetime);
            if (destroyDelay > 0f)
                Destroy(instance, destroyDelay);
        }

        private void SpawnBreakExplosion(Transform anchor)
        {
            if (breakExplosionPrefab == null || anchor == null)
                return;

            SpawnDetachedEffect(breakExplosionPrefab, anchor, Vector3.zero, panelBreakScale, panelBreakLifetime);
        }

        private Transform GetPanelBreakExplosionAnchor(int panelIndex, Transform fallbackAnchor)
        {
            if (panelBreakExplosionLocations != null && panelIndex >= 0 && panelIndex < panelBreakExplosionLocations.Length)
            {
                Transform configuredAnchor = panelBreakExplosionLocations[panelIndex];
                if (configuredAnchor != null)
                    return configuredAnchor;
            }

            return fallbackAnchor;
        }

        private void ResetManagedState()
        {
            if (deathExplosionRoutine != null)
            {
                StopCoroutine(deathExplosionRoutine);
                deathExplosionRoutine = null;
            }

            HideDashTelegraph();
            HideDashRing();
            HideStaticChargeTelegraph();
            HideStaticChargeRing();
            HideStaticChargeSpeedLines();
            HideTargetedChargeTelegraph();
            HideTargetedChargeRing();
            HideTargetedChargeSpeedLines();
            StopAlarmFlash();
            RestoreAlarmLightsRotation();
            dashExhaustActive = false;
            StopEffects(exhaustVfxRoots);
            StopEffects(deathElectricityPrefab);

            if (deathElectricityPrefab != null)
                deathElectricityPrefab.SetActive(false);

            if (exhaustVfxRoots != null)
            {
                for (int i = 0; i < exhaustVfxRoots.Length; i++)
                {
                    if (exhaustVfxRoots[i] != null)
                        exhaustVfxRoots[i].SetActive(false);
                }
            }

            deathTriggered = false;
        }

        private float GetRandomDeathExplosionScale()
        {
            float minScale = Mathf.Min(deathExplosionRandomScaleRange.x, deathExplosionRandomScaleRange.y);
            float maxScale = Mathf.Max(deathExplosionRandomScaleRange.x, deathExplosionRandomScaleRange.y);
            return UnityEngine.Random.Range(minScale, maxScale);
        }

        private static void ConfigureDashIndicator(ParticleSystem[] particleSystems, float dashDistance, float lifetime)
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                main.startSize3D = true;
                main.startSizeY = new ParticleSystem.MinMaxCurve(dashDistance);
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime);
            }
        }

        private static void ConfigureParticleLifetime(ParticleSystem[] particleSystems, float lifetime)
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                main.duration = lifetime;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime);
            }
        }

        private void InitializeAlarmFlashRenderer()
        {
            alarmFlashPropertyBlock ??= new MaterialPropertyBlock();
            alarmFlashEmissionPropertyId = Shader.PropertyToID(
                string.IsNullOrWhiteSpace(alarmFlashEmissionProperty) ? "_EmissionColor" : alarmFlashEmissionProperty
            );

            if (alarmFlashRenderer == null)
            {
                Transform searchRoot = bossBrain != null ? bossBrain.transform : transform.root;
                Transform whistle = searchRoot.Find("Roomba_Model/RoombaLoPoly/Body/whistle_low");
                if (whistle != null)
                    alarmFlashRenderer = whistle.GetComponent<Renderer>();

                if (alarmFlashRenderer == null)
                {
                    Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null && renderers[i].name == "whistle_low")
                        {
                            alarmFlashRenderer = renderers[i];
                            break;
                        }
                    }
                }
            }

            CacheAlarmFlashOriginalEmission();
            RestoreAlarmFlashEmission();
        }

        private void InitializeAlarmLightsTransform()
        {
            if (alarmLightsTransform == null)
            {
                Transform searchRoot = bossBrain != null ? bossBrain.transform : transform.root;
                Transform lights = searchRoot.Find("Roomba_Model/CTRL_BodyBase/DoombaRoot/D_BodyBase/D_BodyTop/D_Alarm/Lights");
                if (lights != null)
                    alarmLightsTransform = lights;

                if (alarmLightsTransform == null)
                {
                    Transform[] allChildren = searchRoot.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        if (allChildren[i] != null && allChildren[i].name == "Lights")
                        {
                            alarmLightsTransform = allChildren[i];
                            break;
                        }
                    }
                }
            }

            if (alarmLightsTransform != null)
            {
                alarmLightsInitialLocalEulerAngles = alarmLightsTransform.localEulerAngles;
                alarmLightsHasInitialRotation = true;
            }
        }

        private bool ShouldFlashAlarmWhistle()
        {
            if (deathTriggered || bossController == null)
                return false;

            return bossController.IsAlarmActive;
        }

        private void CacheAlarmFlashOriginalEmission()
        {
            if (alarmFlashRenderer == null)
                return;

            Material sharedMaterial = alarmFlashRenderer.sharedMaterial;
            if (sharedMaterial != null && sharedMaterial.HasProperty(alarmFlashEmissionPropertyId))
            {
                alarmFlashOriginalEmissionColor = sharedMaterial.GetColor(alarmFlashEmissionPropertyId);
                alarmFlashHasOriginalEmission = true;
                return;
            }

            alarmFlashOriginalEmissionColor = Color.black;
            alarmFlashHasOriginalEmission = false;
        }

        private void StartAlarmFlash()
        {
            if (alarmFlashRenderer == null)
                InitializeAlarmFlashRenderer();

            if (alarmFlashRenderer == null)
                return;

            alarmFlashActive = true;

            if (alarmFlashRoutine != null)
                StopCoroutine(alarmFlashRoutine);

            alarmFlashRoutine = StartCoroutine(AlarmFlashRoutine());
        }

        private void StopAlarmFlash()
        {
            alarmFlashActive = false;

            if (alarmFlashRoutine != null)
            {
                StopCoroutine(alarmFlashRoutine);
                alarmFlashRoutine = null;
            }

            RestoreAlarmFlashEmission();
        }

        private IEnumerator AlarmFlashRoutine()
        {
            while (alarmFlashActive && ShouldFlashAlarmWhistle())
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, alarmFlashSpeed) * Mathf.PI * 2f);
                float intensity = Mathf.Lerp(alarmFlashMinIntensity, alarmFlashMaxIntensity, pulse);
                ApplyAlarmFlashEmission(alarmFlashEmissionColor * intensity);
                yield return null;
            }

            alarmFlashRoutine = null;
            alarmFlashActive = false;
            RestoreAlarmFlashEmission();
        }

        private void RestoreAlarmFlashEmission()
        {
            if (alarmFlashRenderer == null)
                return;

            ApplyAlarmFlashEmission(alarmFlashHasOriginalEmission ? alarmFlashOriginalEmissionColor : Color.black);
        }

        private void UpdateAlarmLightsRotation(bool shouldRotate)
        {
            if (alarmLightsTransform == null)
                return;

            // Show/hide the lights GameObject to match active state
            if (alarmLightsTransform.gameObject.activeSelf != shouldRotate)
                alarmLightsTransform.gameObject.SetActive(shouldRotate);

            if (!shouldRotate)
            {
                RestoreAlarmLightsRotation();
                return;
            }

            Vector3 euler = alarmLightsTransform.localEulerAngles;
            float nextY = euler.y + Mathf.Max(0f, alarmLightsRotationSpeed) * Time.deltaTime;
            if (nextY > alarmLightsSnapThreshold)
                nextY = 0f;

            alarmLightsTransform.localEulerAngles = new Vector3(euler.x, nextY, euler.z);
        }

        private void RestoreAlarmLightsRotation()
        {
            if (alarmLightsTransform == null || !alarmLightsHasInitialRotation)
                return;

            alarmLightsTransform.localEulerAngles = alarmLightsInitialLocalEulerAngles;
        }

        private void ApplyAlarmFlashEmission(Color emissionColor)
        {
            if (alarmFlashRenderer == null)
                return;

            Material sharedMaterial = alarmFlashRenderer.sharedMaterial;
            if (sharedMaterial == null || !sharedMaterial.HasProperty(alarmFlashEmissionPropertyId))
                return;

            if (!sharedMaterial.IsKeywordEnabled("_EMISSION"))
                sharedMaterial.EnableKeyword("_EMISSION");

            alarmFlashRenderer.GetPropertyBlock(alarmFlashPropertyBlock);
            alarmFlashPropertyBlock.SetColor(alarmFlashEmissionPropertyId, emissionColor);
            alarmFlashRenderer.SetPropertyBlock(alarmFlashPropertyBlock);
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private static void RestartObjects(GameObject[] roots)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
                RestartObject(roots[i]);
        }

        private static void RestartObject(GameObject root)
        {
            if (root == null)
                return;

            if (!root.activeSelf)
                root.SetActive(true);

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                particleSystem.gameObject.SetActive(true);
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(true);
            }

            VisualEffect[] visualEffects = root.GetComponentsInChildren<VisualEffect>(true);
            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                visualEffect.Stop();
                visualEffect.Reinit();
                visualEffect.Play();
            }
        }

        private static void StopEffects(GameObject root)
        {
            if (root == null)
                return;

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);

            VisualEffect[] visualEffects = root.GetComponentsInChildren<VisualEffect>(true);
            for (int i = 0; i < visualEffects.Length; i++)
                visualEffects[i].Stop();
        }

        private static void StopEffects(GameObject[] roots)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
                StopEffects(roots[i]);
        }
    }
}
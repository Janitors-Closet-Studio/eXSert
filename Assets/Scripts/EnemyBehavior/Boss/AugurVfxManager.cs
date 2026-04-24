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
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private AudioSource audioSource;

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

        [Header("Panel Break")]
        [SerializeField, Tooltip("Optional electricity prefab spawned when a side panel breaks.")]
        private GameObject panelBreakElectricityPrefab;
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
        [SerializeField] private AudioClip deathAudioClip;

        private Coroutine exhaustRoutine;
        private Coroutine dashIndicatorRoutine;
        private bool deathTriggered;
        private bool dashExhaustActive;
        private bool cachedFormInitialized;
        private RoombaForm cachedForm;
        private GameObject activeDashIndicatorInstance;
        private GameObject activeDashRingInstance;
        private ParticleSystem[] activeDashIndicatorParticles = Array.Empty<ParticleSystem>();
        private ParticleSystem[] activeDashRingParticles = Array.Empty<ParticleSystem>();

        private void Awake()
        {
            if (bossBrain == null)
                bossBrain = GetComponentInParent<BossRoombaBrain>();

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

            ResetManagedState();
        }

        private void OnEnable()
        {
            if (bossHealth != null)
                bossHealth.BossDefeated += HandleBossDefeated;

            if (bossBrain != null)
                bossBrain.SidePanelDestroyed += HandleSidePanelDestroyed;

            ResetManagedState();
        }

        private void OnDisable()
        {
            if (bossHealth != null)
                bossHealth.BossDefeated -= HandleBossDefeated;

            if (bossBrain != null)
                bossBrain.SidePanelDestroyed -= HandleSidePanelDestroyed;

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
            PlayClip(panelBreakAudioClip);
        }

        private void HandleBossDefeated()
        {
            if (deathTriggered)
                return;

            deathTriggered = true;
            HideDashTelegraph();
            HideDashRing();
            StopExhaustImmediately();

            Transform anchor = deathVfxAnchor != null ? deathVfxAnchor : transform;
            SpawnDetachedEffect(deathElectricityPrefab, anchor, deathVfxOffset, deathVfxScale, deathVfxLifetime);
            SpawnDetachedEffect(deathExplosionPrefab, anchor, deathVfxOffset, deathVfxScale, deathVfxLifetime);
            PlayClip(deathAudioClip);
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

        private void ResetManagedState()
        {
            HideDashTelegraph();
            HideDashRing();
            dashExhaustActive = false;
            StopEffects(exhaustVfxRoots);

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
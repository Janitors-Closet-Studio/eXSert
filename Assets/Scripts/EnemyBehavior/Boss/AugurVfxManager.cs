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

        [Header("Dash Travel")]
        [SerializeField, Tooltip("Root VFX that should stay active while Augur is lunging/charging forward.")]
        private GameObject dashTravelVfxRoot;
        [SerializeField] private float dashTravelShutdownDelay = 0.25f;
        [SerializeField] private AudioClip dashTravelAudioClip;

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
        private Coroutine dashShutdownRoutine;
        private bool dashTrailRequested;
        private bool chargeTrailRequested;
        private bool deathTriggered;
        private bool cachedFormInitialized;
        private RoombaForm cachedForm;

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

            if (dashShutdownRoutine != null)
            {
                StopCoroutine(dashShutdownRoutine);
                dashShutdownRoutine = null;
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

            bool nextChargeTrailRequested = bossBrain.IsCharging;
            if (nextChargeTrailRequested != chargeTrailRequested)
            {
                chargeTrailRequested = nextChargeTrailRequested;
                if (chargeTrailRequested)
                    TriggerExhaustBurst(actionExhaustDuration);

                RefreshDashTrailState();
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
            if (deathTriggered)
                return;

            dashTrailRequested = true;
            TriggerExhaustBurst(actionExhaustDuration);
            RefreshDashTrailState();
        }

        public void NotifyDashLungeEnded()
        {
            dashTrailRequested = false;
            RefreshDashTrailState();

            if (!deathTriggered)
                TriggerExhaustBurst(actionExhaustDuration);
        }

        public void TriggerExhaustBurst()
        {
            TriggerExhaustBurst(actionExhaustDuration);
        }

        public void TriggerEnragedExhaust()
        {
            TriggerExhaustBurst(enragedExhaustDuration);
        }

        public void ShowDashTelegraph()
        {
            if (!deathTriggered)
                bossBrain?.ShowAttackIndicator();
        }

        public void HideDashTelegraph()
        {
            bossBrain?.HideAttackIndicator();
        }

        private void TriggerExhaustBurst(float duration)
        {
            if (exhaustVfxRoots == null || exhaustVfxRoots.Length == 0)
                return;

            if (exhaustRoutine != null)
                StopCoroutine(exhaustRoutine);

            RestartObjects(exhaustVfxRoots);
            PlayClip(exhaustAudioClip);
            exhaustRoutine = StartCoroutine(DisableObjectsAfterDelay(exhaustVfxRoots, Mathf.Max(0f, duration), Mathf.Max(0f, exhaustShutdownDelay), clearRoutine: true));
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
            dashTrailRequested = false;
            chargeTrailRequested = false;
            RefreshDashTrailState();
            StopExhaustImmediately();

            Transform anchor = deathVfxAnchor != null ? deathVfxAnchor : transform;
            SpawnDetachedEffect(deathElectricityPrefab, anchor, deathVfxOffset, deathVfxScale, deathVfxLifetime);
            SpawnDetachedEffect(deathExplosionPrefab, anchor, deathVfxOffset, deathVfxScale, deathVfxLifetime);
            PlayClip(deathAudioClip);
        }

        private void RefreshDashTrailState()
        {
            bool shouldBeActive = (dashTrailRequested || chargeTrailRequested) && !deathTriggered;
            if (shouldBeActive)
            {
                if (dashShutdownRoutine != null)
                {
                    StopCoroutine(dashShutdownRoutine);
                    dashShutdownRoutine = null;
                }

                RestartObject(dashTravelVfxRoot);
                PlayClip(dashTravelAudioClip);
                return;
            }

            if (dashTravelVfxRoot == null)
                return;

            if (dashShutdownRoutine != null)
                StopCoroutine(dashShutdownRoutine);

            dashShutdownRoutine = StartCoroutine(DisableDashTrailAfterDelay());
        }

        private IEnumerator DisableDashTrailAfterDelay()
        {
            StopEffects(dashTravelVfxRoot);
            float delay = Mathf.Max(0f, dashTravelShutdownDelay);
            if (delay > 0f)
                yield return WaitForSecondsCache.Get(delay);

            if (dashTravelVfxRoot != null)
                dashTravelVfxRoot.SetActive(false);

            dashShutdownRoutine = null;
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
            StopEffects(exhaustVfxRoots);

            if (exhaustVfxRoots != null)
            {
                for (int i = 0; i < exhaustVfxRoots.Length; i++)
                {
                    if (exhaustVfxRoots[i] != null)
                        exhaustVfxRoots[i].SetActive(false);
                }
            }

            StopEffects(dashTravelVfxRoot);
            if (dashTravelVfxRoot != null)
                dashTravelVfxRoot.SetActive(false);

            dashTrailRequested = false;
            chargeTrailRequested = false;
            deathTriggered = false;
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
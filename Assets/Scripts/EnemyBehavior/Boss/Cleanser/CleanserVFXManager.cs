using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Centralizes Cleanser VFX triggered by animation events or attack-state callbacks.
    /// </summary>
    public sealed class CleanserVFXManager : MonoBehaviour
    {
        [Serializable]
        private sealed class NamedVfxEntry
        {
            [Tooltip("Animation-event id used to trigger this VFX entry.")]
            public string Id;

            [Tooltip("Prefab instantiated when this entry is triggered.")]
            public GameObject Prefab;

            [Tooltip(
                "Optional spawn anchor. Falls back to this VFX manager transform when not assigned."
            )]
            public Transform Anchor;

            [Tooltip("Local-space offset applied from the anchor.")]
            public Vector3 LocalOffset = Vector3.zero;

            [Tooltip("Local-space rotation offset applied from the anchor.")]
            public Vector3 LocalEulerAngles = Vector3.zero;

            [Tooltip("Scale multiplier applied after instantiation.")]
            public Vector3 Scale = Vector3.one;

            [Tooltip("If true, parents the spawned instance to the anchor.")]
            public bool ParentToAnchor;

            [Tooltip("Optional destroy delay. Set to 0 to leave lifecycle to the prefab.")]
            [Min(0f)]
            public float Lifetime = 3f;
        }

        [Header("References")]
        [SerializeField]
        private CleanserBrain cleanserBrain;

        [Header("Weapon Trail Objects")]
        [FormerlySerializedAs("weaponTrailRoots")]
        [SerializeField]
        [Tooltip(
            "Simple trail GameObjects attached to the weapon that should toggle on at Trail and off at TrailEnd."
        )]
        private GameObject[] weaponTrailObjects = Array.Empty<GameObject>();

        [Header("Wing Trail Objects")]
        [SerializeField]
        [Tooltip(
            "Simple trail GameObjects attached to the wing that should toggle on at Wing and off at WingEnd."
        )]
        private GameObject[] wingTrailObjects = Array.Empty<GameObject>();

        [Header("Dash VFX Objects")]
        [SerializeField]
        [Tooltip("Particle-system roots that should toggle on at DashOn and off at DashOff.")]
        private GameObject[] dashVfxObjects = Array.Empty<GameObject>();

        [Header("Phase Break / Death VFX")]
        [SerializeField]
        [Tooltip("Electricity, sparks, or other VFX roots shared by both the second-phase break/stun state and Cleanser's death.")]
        private GameObject[] phaseBreakDeathVfxObjects = Array.Empty<GameObject>();

        [SerializeField]
        [Min(0f)]
        [Tooltip("Delay before the shared phase-break/death VFX actually turns on.")]
        private float phaseBreakDeathVfxDelay = 1f;

        [Header("Airborne VFX")]
        [SerializeField]
        [Tooltip("VFX roots that stay active while Cleanser is airborne during the second-phase ultimate hover and shut off when he lands.")]
        private GameObject[] airborneVfxObjects = Array.Empty<GameObject>();

        [SerializeField]
        [Tooltip("Optional target transform used as the spawn anchor for airborne VFX roots when they turn on.")]
        private Transform airborneVfxTarget;

        [Header("AfterImageTrail")]
        [SerializeField]
        [Tooltip("MeshTrail afterimage effect kept active for the full fast-circling AnimeDash pattern.")]
        private MeshTrail afterImageTrail;

        [Header("Spark VFX")]
        [SerializeField]
        [Tooltip("Visual Effect Graph root played by the BladeSpark animation event.")]
        private GameObject bladeSparkVfx;

        [SerializeField]
        [Tooltip("Visual Effect Graph root played by the PommelSpark animation event.")]
        private GameObject pommelSparkVfx;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How long the PommelSpark VFX stays active before it is stopped.")]
        private float pommelSparkDuration = 0.2f;

        [SerializeField]
        [Tooltip("Visual Effect Graph root played by the WingSpark animation event.")]
        private GameObject wingSparkVfx;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How long the WingSpark VFX stays active before it is stopped.")]
        private float wingSparkDuration = 0.2f;

        [Header("Named Spawned VFX")]
        [SerializeField]
        [Tooltip(
            "Prefab-based VFX entries you can trigger from animation events through PlayNamedVfx."
        )]
        private NamedVfxEntry[] namedVfx = Array.Empty<NamedVfxEntry>();

        private readonly Dictionary<string, NamedVfxEntry> namedVfxLookup = new Dictionary<
            string,
            NamedVfxEntry
        >(StringComparer.OrdinalIgnoreCase);

        private VisualEffect[] bladeSparkEffects = Array.Empty<VisualEffect>();
        private VisualEffect[] pommelSparkEffects = Array.Empty<VisualEffect>();
        private VisualEffect[] wingSparkEffects = Array.Empty<VisualEffect>();

        private Coroutine pommelSparkRoutine;
        private Coroutine wingSparkRoutine;
        private Coroutine delayedPhaseBreakDeathVfxRoutine;

        private void Awake()
        {
            if (cleanserBrain == null)
                cleanserBrain = GetComponentInParent<CleanserBrain>();

            RebuildLookup();
            CacheSparkEffects();
            SetWeaponTrailActive(false);
            SetWingTrailActive(false);
            SetDashVfxActive(false);
            SetEffectObjectsActive(phaseBreakDeathVfxObjects, false);
            SetEffectObjectsActive(airborneVfxObjects, false);
            SetSparkIdle(bladeSparkVfx, bladeSparkEffects);
            SetSparkIdle(pommelSparkVfx, pommelSparkEffects);
            SetSparkIdle(wingSparkVfx, wingSparkEffects);
        }

        private void OnDisable()
        {
            StopAllLoopingVfx();
        }

        public void Trail()
        {
            SetWeaponTrailActive(true);
        }

        public void TrailEnd()
        {
            SetWeaponTrailActive(false);
        }

        public void Wing()
        {
            SetWingTrailActive(true);
        }

        public void WingEnd()
        {
            SetWingTrailActive(false);
        }

        public void DashOn()
        {
            SetDashVfxActive(true);
        }

        public void DashOff()
        {
            SetDashVfxActive(false);
        }

        public void BeginAnimeDashMeshTrail()
        {
            afterImageTrail?.TurnTrailOn();
        }

        public void EndAnimeDashMeshTrail()
        {
            afterImageTrail?.TurnTrailOff();
        }

        public void StopAttackLoopingVfx()
        {
            SetWeaponTrailActive(false);
            SetWingTrailActive(false);
            SetDashVfxActive(false);
            SetSparkIdle(bladeSparkVfx, bladeSparkEffects);
            StopSparkRoutine(ref pommelSparkRoutine, pommelSparkVfx, pommelSparkEffects);
            StopSparkRoutine(ref wingSparkRoutine, wingSparkVfx, wingSparkEffects);
        }

        public void StopAllLoopingVfx()
        {
            StopAttackLoopingVfx();
            EndAnimeDashMeshTrail();
            StopDelayedPhaseBreakDeathVfx();
            SetEffectObjectsActive(phaseBreakDeathVfxObjects, false);
            SetEffectObjectsActive(airborneVfxObjects, false);
        }

        public void BeginPhaseBreakVfx()
        {
            StartDelayedPhaseBreakDeathVfx();
        }

        public void EndPhaseBreakVfx()
        {
            StopDelayedPhaseBreakDeathVfx();
            SetEffectObjectsActive(phaseBreakDeathVfxObjects, false);
        }

        public void PlayDeathVfx()
        {
            StopAttackLoopingVfx();
            EndAnimeDashMeshTrail();
            SetEffectObjectsActive(airborneVfxObjects, false);
            StartDelayedPhaseBreakDeathVfx();
        }

        public void BeginAirborneVfx()
        {
            Transform airborneAnchor = airborneVfxTarget != null ? airborneVfxTarget : transform;
            SetEffectObjectsActive(airborneVfxObjects, true, airborneAnchor);
        }

        public void EndAirborneVfx()
        {
            SetEffectObjectsActive(airborneVfxObjects, false);
        }

        public void BladeSpark()
        {
            SetSparkActive(bladeSparkVfx, bladeSparkEffects);
        }

        public void BSparkEnd()
        {
            SetSparkIdle(bladeSparkVfx, bladeSparkEffects);
        }

        public void PommelSpark()
        {
            PlaySpark(
                pommelSparkVfx,
                pommelSparkEffects,
                pommelSparkDuration,
                ref pommelSparkRoutine,
                () => pommelSparkRoutine = null
            );
        }

        public void WingSpark()
        {
            PlaySpark(
                wingSparkVfx,
                wingSparkEffects,
                wingSparkDuration,
                ref wingSparkRoutine,
                () => wingSparkRoutine = null
            );
        }

        public void PlayNamedVfx(string vfxId)
        {
            if (string.IsNullOrWhiteSpace(vfxId))
                return;

            if (namedVfxLookup.Count != namedVfx.Length)
                RebuildLookup();

            if (
                !namedVfxLookup.TryGetValue(vfxId, out NamedVfxEntry entry)
                || entry == null
                || entry.Prefab == null
            )
                return;

            Transform anchor = entry.Anchor != null ? entry.Anchor : transform;
            Vector3 spawnPosition = anchor.TransformPoint(entry.LocalOffset);
            Quaternion spawnRotation = anchor.rotation * Quaternion.Euler(entry.LocalEulerAngles);

            GameObject instance = Instantiate(entry.Prefab, spawnPosition, spawnRotation);
            instance.transform.localScale = Vector3.Scale(
                instance.transform.localScale,
                entry.Scale
            );

            if (entry.ParentToAnchor)
                instance.transform.SetParent(anchor, true);

            if (entry.Lifetime > 0f)
                Destroy(instance, entry.Lifetime);
        }

        private void RebuildLookup()
        {
            namedVfxLookup.Clear();

            for (int index = 0; index < namedVfx.Length; index++)
            {
                NamedVfxEntry entry = namedVfx[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                namedVfxLookup[entry.Id] = entry;
            }
        }

        private void CacheSparkEffects()
        {
            bladeSparkEffects = CollectVisualEffects(bladeSparkVfx);
            pommelSparkEffects = CollectVisualEffects(pommelSparkVfx);
            wingSparkEffects = CollectVisualEffects(wingSparkVfx);
        }

        private void SetWeaponTrailActive(bool isActive)
        {
            SetTrailObjectsActive(weaponTrailObjects, isActive);
        }

        private void SetWingTrailActive(bool isActive)
        {
            SetTrailObjectsActive(wingTrailObjects, isActive);
        }

        private void SetDashVfxActive(bool isActive)
        {
            SetTrailObjectsActive(dashVfxObjects, isActive);
        }

        private void StartDelayedPhaseBreakDeathVfx()
        {
            StopDelayedPhaseBreakDeathVfx();
            delayedPhaseBreakDeathVfxRoutine = StartCoroutine(RunDelayedPhaseBreakDeathVfx());
        }

        private void StopDelayedPhaseBreakDeathVfx()
        {
            if (delayedPhaseBreakDeathVfxRoutine == null)
                return;

            StopCoroutine(delayedPhaseBreakDeathVfxRoutine);
            delayedPhaseBreakDeathVfxRoutine = null;
        }

        private IEnumerator RunDelayedPhaseBreakDeathVfx()
        {
            float delay = Mathf.Max(0f, phaseBreakDeathVfxDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            delayedPhaseBreakDeathVfxRoutine = null;
            SetEffectObjectsActive(phaseBreakDeathVfxObjects, true);
        }

        private static void SetEffectObjectsActive(GameObject[] effectObjects, bool isActive)
        {
            SetEffectObjectsActive(effectObjects, isActive, null);
        }

        private static void SetEffectObjectsActive(GameObject[] effectObjects, bool isActive, Transform targetAnchor)
        {
            for (int index = 0; index < effectObjects.Length; index++)
            {
                GameObject effectObject = effectObjects[index];
                if (effectObject == null)
                    continue;

                if (isActive)
                {
                    if (targetAnchor != null)
                        effectObject.transform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);

                    effectObject.SetActive(true);
                    PlayParticles(effectObject);
                    PlayVisualEffects(effectObject);
                }
                else
                {
                    StopVisualEffects(effectObject);
                    StopParticles(effectObject);
                    effectObject.SetActive(false);
                }
            }
        }

        private static void SetTrailObjectsActive(GameObject[] trailObjects, bool isActive)
        {
            for (int index = 0; index < trailObjects.Length; index++)
            {
                GameObject trailObject = trailObjects[index];
                if (trailObject == null)
                    continue;

                if (isActive)
                {
                    trailObject.SetActive(true);
                    PlayParticles(trailObject);
                    PlayVisualEffects(trailObject);
                }
                else
                {
                    StopVisualEffects(trailObject);
                    StopParticles(trailObject);
                    trailObject.SetActive(false);
                }
            }
        }

        private void PlaySpark(
            GameObject root,
            VisualEffect[] visualEffects,
            float duration,
            ref Coroutine routine,
            Action onComplete = null
        )
        {
            if (root == null)
                return;

            if (visualEffects == null || visualEffects.Length == 0)
                visualEffects = CollectVisualEffects(root);

            StopSparkRoutine(ref routine, root, visualEffects);
            SetSparkActive(root, visualEffects);
            routine = StartCoroutine(RunSpark(root, visualEffects, duration, onComplete));
        }

        private IEnumerator RunSpark(
            GameObject root,
            VisualEffect[] visualEffects,
            float duration,
            Action onComplete
        )
        {
            float clampedDuration = Mathf.Max(0f, duration);
            if (clampedDuration > 0f)
                yield return new WaitForSeconds(clampedDuration);

            SetSparkIdle(root, visualEffects);
            onComplete?.Invoke();
        }

        private void StopSparkRoutine(
            ref Coroutine routine,
            GameObject root,
            VisualEffect[] visualEffects
        )
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            SetSparkIdle(root, visualEffects);
        }

        private static VisualEffect[] CollectVisualEffects(GameObject root)
        {
            if (root == null)
                return Array.Empty<VisualEffect>();

            return root.GetComponentsInChildren<VisualEffect>(true) ?? Array.Empty<VisualEffect>();
        }

        private static void SetSparkActive(GameObject root, VisualEffect[] visualEffects)
        {
            if (root != null && !root.activeSelf)
                root.SetActive(true);

            if (visualEffects == null)
                return;

            for (int index = 0; index < visualEffects.Length; index++)
            {
                VisualEffect visualEffect = visualEffects[index];
                if (visualEffect == null)
                    continue;

                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                visualEffect.Reinit();
                visualEffect.Play();
            }
        }

        private static void SetSparkIdle(GameObject root, VisualEffect[] visualEffects)
        {
            if (visualEffects != null)
            {
                for (int index = 0; index < visualEffects.Length; index++)
                {
                    VisualEffect visualEffect = visualEffects[index];
                    if (visualEffect == null)
                        continue;

                    visualEffect.Stop();
                    visualEffect.Reinit();
                }
            }

            if (root != null && root.activeSelf)
                root.SetActive(false);
        }

        private static void PlayParticles(GameObject root)
        {
            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                    continue;

                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private static void PlayVisualEffects(GameObject root)
        {
            VisualEffect[] visualEffects = root.GetComponentsInChildren<VisualEffect>(true);
            for (int index = 0; index < visualEffects.Length; index++)
            {
                VisualEffect visualEffect = visualEffects[index];
                if (visualEffect == null)
                    continue;

                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                visualEffect.Reinit();
                visualEffect.Play();
            }
        }

        private static void StopParticles(GameObject root)
        {
            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void StopVisualEffects(GameObject root)
        {
            VisualEffect[] visualEffects = root.GetComponentsInChildren<VisualEffect>(true);
            for (int index = 0; index < visualEffects.Length; index++)
            {
                VisualEffect visualEffect = visualEffects[index];
                if (visualEffect == null)
                    continue;

                visualEffect.Stop();
                visualEffect.Reinit();
            }
        }
    }
}

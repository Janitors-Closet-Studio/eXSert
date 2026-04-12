using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

public sealed class EnemyDeathVfxController : MonoBehaviour
{
    private enum VfxTriggerPhase
    {
        DeathStarted,
        DeathCompleted,
    }

    [Serializable]
    private struct VfxEntry
    {
        [SerializeField, FormerlySerializedAs("prefab")]
        private GameObject effectObject;

        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private Vector3 positionOffset;

        [SerializeField]
        private Vector3 rotationOffset;

        [SerializeField]
        private Vector3 scaleMultiplier;

        [SerializeField]
        private bool attachToAnchor;

        [SerializeField]
        private VfxTriggerPhase triggerPhase;

        [SerializeField]
        private bool repeatVisualEffects;

        [SerializeField]
        private Vector2 repeatDelayRange;

        [
            SerializeField,
            Tooltip("Seconds to wait after the death event before this entry is spawned.")
        ]
        private float spawnDelay;

        public GameObject EffectObject => effectObject;
        public Transform Anchor => anchor;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 ScaleMultiplier =>
            scaleMultiplier == Vector3.zero ? Vector3.one : scaleMultiplier;
        public bool AttachToAnchor => attachToAnchor;
        public VfxTriggerPhase TriggerPhase => triggerPhase;
        public bool RepeatVisualEffects => repeatVisualEffects;
        public Vector2 RepeatDelayRange => new Vector2(
            Mathf.Max(0f, repeatDelayRange.x),
            Mathf.Max(0f, repeatDelayRange.y)
        );
        public float SpawnDelay => Mathf.Max(0f, spawnDelay);
    }

    private sealed class SpawnedVfxHandle
    {
        public GameObject Instance;
        public VfxTriggerPhase Phase;
        public bool StopRequested;
        public Coroutine ReplayCoroutine;
        public Coroutine CleanupCoroutine;
        public bool IsAttachedInstance;
    }

    [Header("Target")]
    [
        SerializeField,
        Tooltip(
            "Optional explicit enemy core reference. If left empty, the component searches on this object and its parents."
        )
    ]
    private BaseEnemyCore enemyCoreOverride;

    [Header("Death VFX")]
    [
        SerializeField,
        Tooltip(
            "Effects spawned when the enemy death event fires. Entries are spawned before the enemy is deactivated for pooling."
        )
    ]
    private VfxEntry[] vfxEntries = Array.Empty<VfxEntry>();

    private BaseEnemyCore enemyCore;
    private bool subscribed;
    private bool hasPlayedDeathStartedThisLife;
    private bool hasPlayedDeathCompletedThisLife;
    private readonly List<SpawnedVfxHandle> activeInstances = new();

    private static DeathVfxCoroutineRunner coroutineRunner;

    private void Awake()
    {
        CacheEnemyCore();
    }

    private void OnEnable()
    {
        hasPlayedDeathStartedThisLife = false;
        hasPlayedDeathCompletedThisLife = false;
        CacheEnemyCore();
        ResetAllManagedVfx();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetAllManagedVfx();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    [ContextMenu("Debug/Spawn Death VFX")]
    public void DebugSpawnDeathVfx()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                $"[{nameof(EnemyDeathVfxController)}] Enter Play Mode to preview death VFX.",
                this
            );
            return;
        }

        SpawnConfiguredVfx(VfxTriggerPhase.DeathStarted);
        SpawnConfiguredVfx(VfxTriggerPhase.DeathCompleted);
    }

    [ContextMenu("Debug/Replay Active VFX")]
    public void DebugReplayActiveVfx()
    {
        ReplayActiveInstances();
    }

    private void CacheEnemyCore()
    {
        enemyCore =
            enemyCoreOverride != null ? enemyCoreOverride : GetComponentInParent<BaseEnemyCore>();
    }

    private void Subscribe()
    {
        if (subscribed || enemyCore == null)
            return;

        enemyCore.OnDeathStarted += HandleEnemyDeathStarted;
        enemyCore.OnDeath += HandleEnemyDeath;
        enemyCore.OnSpawn += HandleEnemySpawned;
        enemyCore.OnReset += HandleEnemyReset;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || enemyCore == null)
            return;

        enemyCore.OnDeathStarted -= HandleEnemyDeathStarted;
        enemyCore.OnDeath -= HandleEnemyDeath;
        enemyCore.OnSpawn -= HandleEnemySpawned;
        enemyCore.OnReset -= HandleEnemyReset;
        subscribed = false;
    }

    private void HandleEnemySpawned(BaseEnemyCore spawnedEnemy)
    {
        if (spawnedEnemy == null)
            return;

        hasPlayedDeathStartedThisLife = false;
        hasPlayedDeathCompletedThisLife = false;
        ResetAllManagedVfx();
    }

    private void HandleEnemyReset(BaseEnemyCore resetEnemy)
    {
        if (resetEnemy == null)
            return;

        hasPlayedDeathStartedThisLife = false;
        hasPlayedDeathCompletedThisLife = false;
        ResetAllManagedVfx();
    }

    private void HandleEnemyDeathStarted(BaseEnemyCore deadEnemy)
    {
        if (hasPlayedDeathStartedThisLife || deadEnemy == null)
            return;

        hasPlayedDeathStartedThisLife = true;
        SpawnConfiguredVfx(VfxTriggerPhase.DeathStarted);
    }

    private void HandleEnemyDeath(BaseEnemyCore deadEnemy)
    {
        if (hasPlayedDeathCompletedThisLife || deadEnemy == null)
            return;

        hasPlayedDeathCompletedThisLife = true;
        StopPhaseInstances(VfxTriggerPhase.DeathStarted);
        SpawnConfiguredVfx(VfxTriggerPhase.DeathCompleted);
    }

    private void SpawnConfiguredVfx(VfxTriggerPhase phase)
    {
        if (vfxEntries == null || vfxEntries.Length == 0)
            return;

        for (int i = 0; i < vfxEntries.Length; i++)
        {
            if (vfxEntries[i].TriggerPhase == phase)
                ScheduleVfxEntry(vfxEntries[i]);
        }
    }

    private void ScheduleVfxEntry(VfxEntry entry)
    {
        if (entry.EffectObject == null)
            return;

        Transform anchor = entry.Anchor != null ? entry.Anchor : transform;
        if (entry.SpawnDelay <= 0f)
        {
            SpawnVfxEntry(entry, anchor, this);
            return;
        }

        EnsureCoroutineRunner().StartCoroutine(SpawnVfxEntryAfterDelay(entry, anchor, this));
    }

    private static IEnumerator SpawnVfxEntryAfterDelay(VfxEntry entry, Transform anchor, EnemyDeathVfxController owner)
    {
        yield return new WaitForSecondsRealtime(entry.SpawnDelay);

        if (entry.EffectObject == null)
            yield break;

        if (anchor == null)
            yield break;

        SpawnVfxEntry(entry, anchor, owner);
    }

    private void ReplayActiveInstances()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            SpawnedVfxHandle handle = activeInstances[i];
            if (handle == null || handle.Instance == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            if (handle.IsAttachedInstance)
                continue;

            ReplayInstanceNow(handle.Instance);
        }
    }

    private static void SpawnVfxEntry(VfxEntry entry, Transform anchor, EnemyDeathVfxController owner)
    {
        if (entry.EffectObject == null)
            return;

        if (owner != null && owner.ShouldUseAttachedInstance(entry.EffectObject))
        {
            owner.ActivateAttachedInstance(entry, anchor);
            return;
        }

        Vector3 spawnPosition = anchor.TransformPoint(entry.PositionOffset);
        Quaternion spawnRotation = anchor.rotation * Quaternion.Euler(entry.RotationOffset);

        GameObject instance = Instantiate(entry.EffectObject, spawnPosition, spawnRotation);
        if (!instance.activeSelf)
            instance.SetActive(true);

        instance.transform.localScale = Vector3.Scale(
            instance.transform.localScale,
            entry.ScaleMultiplier
        );

        if (entry.AttachToAnchor)
            instance.transform.SetParent(anchor, worldPositionStays: true);

        RefreshVfxPropertyBinders(instance);

        ReplayInstanceNow(instance);

        if (owner != null)
            owner.TrackSpawnedInstance(instance, entry);
    }

    private bool ShouldUseAttachedInstance(GameObject effectObject)
    {
        if (effectObject == null)
            return false;

        return effectObject.scene.IsValid() && effectObject.transform.IsChildOf(transform);
    }

    private void ActivateAttachedInstance(VfxEntry entry, Transform anchor)
    {
        GameObject instance = entry.EffectObject;
        if (instance == null)
            return;

        if (entry.AttachToAnchor && anchor != null && instance.transform.parent != anchor)
            instance.transform.SetParent(anchor, worldPositionStays: true);

        if (!instance.activeSelf)
            instance.SetActive(true);

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, entry.ScaleMultiplier);
        RefreshVfxPropertyBinders(instance);
        ReplayInstanceNow(instance);

        TrackAttachedInstance(instance, entry);
    }

    private void TrackSpawnedInstance(GameObject instance, VfxEntry entry)
    {
        var handle = new SpawnedVfxHandle
        {
            Instance = instance,
            Phase = entry.TriggerPhase,
        };

        activeInstances.Add(handle);

        if (entry.RepeatVisualEffects && HasReplayableEffects(instance))
        {
            handle.ReplayCoroutine = EnsureCoroutineRunner().StartCoroutine(
                ReplayLoop(handle, entry.RepeatDelayRange)
            );
            return;
        }

        handle.CleanupCoroutine = EnsureCoroutineRunner().StartCoroutine(DestroyWhenFinished(handle));
    }

    private void TrackAttachedInstance(GameObject instance, VfxEntry entry)
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            SpawnedVfxHandle existing = activeInstances[i];
            if (existing == null || existing.Instance == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            if (existing.Instance == instance)
                return;
        }

        activeInstances.Add(
            new SpawnedVfxHandle
            {
                Instance = instance,
                Phase = entry.TriggerPhase,
                IsAttachedInstance = true,
            }
        );
    }

    private void StopPhaseInstances(VfxTriggerPhase phase)
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            SpawnedVfxHandle handle = activeInstances[i];
            if (handle == null || handle.Instance == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            if (handle.Phase != phase)
                continue;

            handle.StopRequested = true;
            if (handle.IsAttachedInstance)
            {
                StopInstance(handle.Instance);
                activeInstances.RemoveAt(i);
                continue;
            }

            if (handle.ReplayCoroutine != null)
            {
                EnsureCoroutineRunner().StopCoroutine(handle.ReplayCoroutine);
                handle.ReplayCoroutine = null;
            }

            if (handle.CleanupCoroutine == null)
                handle.CleanupCoroutine = EnsureCoroutineRunner().StartCoroutine(DestroyWhenFinished(handle));
        }
    }

    private void ResetAllManagedVfx()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            SpawnedVfxHandle handle = activeInstances[i];
            if (handle == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            handle.StopRequested = true;

            if (handle.ReplayCoroutine != null)
            {
                EnsureCoroutineRunner().StopCoroutine(handle.ReplayCoroutine);
                handle.ReplayCoroutine = null;
            }

            if (handle.CleanupCoroutine != null)
            {
                EnsureCoroutineRunner().StopCoroutine(handle.CleanupCoroutine);
                handle.CleanupCoroutine = null;
            }

            if (handle.Instance != null)
            {
                if (handle.IsAttachedInstance)
                    StopInstance(handle.Instance);
                else
                    Destroy(handle.Instance);
            }

            activeInstances.RemoveAt(i);
        }

        if (vfxEntries == null)
            return;

        for (int i = 0; i < vfxEntries.Length; i++)
        {
            GameObject effectObject = vfxEntries[i].EffectObject;
            if (!ShouldUseAttachedInstance(effectObject))
                continue;

            StopInstance(effectObject);
        }
    }

    private static IEnumerator ReplayLoop(SpawnedVfxHandle handle, Vector2 delayRange)
    {
        while (handle != null && handle.Instance != null && !handle.StopRequested)
        {
            yield return WaitForInstanceToFinish(handle.Instance);

            if (handle.StopRequested || handle.Instance == null)
                yield break;

            float minDelay = Mathf.Min(delayRange.x, delayRange.y);
            float maxDelay = Mathf.Max(delayRange.x, delayRange.y);
            float delay = maxDelay > minDelay ? UnityEngine.Random.Range(minDelay, maxDelay) : minDelay;

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            if (handle.StopRequested || handle.Instance == null)
                yield break;

            ReplayInstanceNow(handle.Instance);
        }
    }

    private static IEnumerator DestroyWhenFinished(SpawnedVfxHandle handle)
    {
        if (handle == null || handle.Instance == null)
            yield break;

        yield return WaitForInstanceToFinish(handle.Instance);

        if (handle.Instance != null)
            Destroy(handle.Instance);
    }

    private static IEnumerator WaitForInstanceToFinish(GameObject instance)
    {
        if (instance == null)
            yield break;

        // Give the systems one frame to react to Play/Reinit before polling.
        yield return null;

        const float minimumVisibleTime = 0.5f;
        float visibleTime = 0f;
        float quietTime = 0f;
        while (instance != null)
        {
            visibleTime += Time.unscaledDeltaTime;

            if (HasActiveEffects(instance))
            {
                quietTime = 0f;
            }
            else
            {
                quietTime += Time.unscaledDeltaTime;
                if (visibleTime >= minimumVisibleTime && quietTime >= 0.15f)
                    yield break;
            }

            yield return null;
        }
    }

    private static bool HasReplayableEffects(GameObject instance)
    {
        if (instance == null)
            return false;

        if (instance.GetComponentsInChildren<VisualEffect>(true).Length > 0)
            return true;

        return instance.GetComponentsInChildren<ParticleSystem>(true).Length > 0;
    }

    private static bool HasActiveEffects(GameObject instance)
    {
        if (instance == null)
            return false;

        var visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < visualEffects.Length; i++)
        {
            VisualEffect visualEffect = visualEffects[i];
            if (visualEffect == null || !visualEffect.enabled || !visualEffect.gameObject.activeInHierarchy)
                continue;

            if (visualEffect.aliveParticleCount > 0)
                return true;
        }

        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem != null && particleSystem.IsAlive(true))
                return true;
        }

        return false;
    }

    private static void ReplayInstanceNow(GameObject instance)
    {
        RestartParticleSystems(instance);
        RestartVisualEffects(instance);
    }

    private static void StopInstance(GameObject instance)
    {
        if (instance == null)
            return;

        var visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < visualEffects.Length; i++)
        {
            VisualEffect visualEffect = visualEffects[i];
            if (visualEffect == null)
                continue;

            visualEffect.Stop();
        }

        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }

        instance.SetActive(false);
    }

    private static void RefreshVfxPropertyBinders(GameObject instance)
    {
        var binders = instance.GetComponentsInChildren<VFXPropertyBinder>(true);
        for (int i = 0; i < binders.Length; i++)
        {
            VFXPropertyBinder binder = binders[i];
            if (binder == null)
                continue;

            bool wasEnabled = binder.enabled;
            binder.enabled = false;
            binder.enabled = wasEnabled;

            if (!wasEnabled)
                binder.enabled = true;
        }
    }

    private static DeathVfxCoroutineRunner EnsureCoroutineRunner()
    {
        if (coroutineRunner != null)
            return coroutineRunner;

        GameObject runnerObject = new GameObject("EnemyDeathVfxController_Runner");
        DontDestroyOnLoad(runnerObject);
        coroutineRunner = runnerObject.AddComponent<DeathVfxCoroutineRunner>();
        return coroutineRunner;
    }

    private static void RestartParticleSystems(GameObject instance)
    {
        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
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

    private static void RestartVisualEffects(GameObject instance)
    {
        var visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < visualEffects.Length; i++)
        {
            VisualEffect visualEffect = visualEffects[i];
            if (visualEffect == null)
                continue;

            if (!visualEffect.gameObject.activeSelf)
                visualEffect.gameObject.SetActive(true);

            if (!visualEffect.enabled)
                visualEffect.enabled = true;

            visualEffect.Stop();
            visualEffect.Reinit();
            visualEffect.Play();
        }
    }

    private sealed class DeathVfxCoroutineRunner : MonoBehaviour { }
}

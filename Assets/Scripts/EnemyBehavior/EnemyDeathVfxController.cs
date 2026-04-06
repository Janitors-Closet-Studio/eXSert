using System;
using UnityEngine;
using UnityEngine.VFX;

public sealed class EnemyDeathVfxController : MonoBehaviour
{
    [Serializable]
    private struct VfxEntry
    {
        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private Vector3 positionOffset;

        [SerializeField]
        private Vector3 rotationOffset;

        [SerializeField]
        private Vector3 scaleMultiplier;

        [SerializeField]
        private bool followAnchorAfterSpawn;

        [SerializeField]
        private bool restartParticleSystems;

        [SerializeField]
        private bool restartVisualEffects;

        [
            SerializeField,
            Tooltip(
                "Seconds before the spawned instance is destroyed. Values at or below 0 fall back to 3 seconds."
            )
        ]
        private float lifetime;

        public GameObject Prefab => prefab;
        public Transform Anchor => anchor;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 ScaleMultiplier =>
            scaleMultiplier == Vector3.zero ? Vector3.one : scaleMultiplier;
        public bool FollowAnchorAfterSpawn => followAnchorAfterSpawn;
        public bool RestartParticleSystems => restartParticleSystems;
        public bool RestartVisualEffects => restartVisualEffects;
        public float Lifetime => lifetime > 0f ? lifetime : 3f;
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
    private bool hasPlayedThisLife;

    private void Awake()
    {
        CacheEnemyCore();
    }

    private void OnEnable()
    {
        hasPlayedThisLife = false;
        CacheEnemyCore();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
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

        SpawnConfiguredVfx();
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

        enemyCore.OnDeath += HandleEnemyDeath;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || enemyCore == null)
            return;

        enemyCore.OnDeath -= HandleEnemyDeath;
        subscribed = false;
    }

    private void HandleEnemyDeath(BaseEnemyCore deadEnemy)
    {
        if (hasPlayedThisLife || deadEnemy == null)
            return;

        hasPlayedThisLife = true;
        SpawnConfiguredVfx();
    }

    private void SpawnConfiguredVfx()
    {
        if (vfxEntries == null || vfxEntries.Length == 0)
            return;

        for (int i = 0; i < vfxEntries.Length; i++)
        {
            SpawnVfxEntry(vfxEntries[i]);
        }
    }

    private void SpawnVfxEntry(VfxEntry entry)
    {
        if (entry.Prefab == null)
            return;

        Transform anchor = entry.Anchor != null ? entry.Anchor : transform;
        Vector3 spawnPosition = anchor.TransformPoint(entry.PositionOffset);
        Quaternion spawnRotation = anchor.rotation * Quaternion.Euler(entry.RotationOffset);

        GameObject instance = Instantiate(entry.Prefab, spawnPosition, spawnRotation);
        if (!instance.activeSelf)
            instance.SetActive(true);

        instance.transform.localScale = Vector3.Scale(
            instance.transform.localScale,
            entry.ScaleMultiplier
        );

        if (entry.FollowAnchorAfterSpawn)
            instance.transform.SetParent(anchor, worldPositionStays: true);

        if (entry.RestartParticleSystems)
            RestartParticleSystems(instance);

        if (entry.RestartVisualEffects)
            RestartVisualEffects(instance);

        Destroy(instance, entry.Lifetime);
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

            visualEffect.Reinit();
            visualEffect.Play();
        }
    }
}

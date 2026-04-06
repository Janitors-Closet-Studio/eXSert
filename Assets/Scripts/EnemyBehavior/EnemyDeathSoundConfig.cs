using UnityEngine;

public class EnemyDeathSoundConfig : MonoBehaviour
{
    private enum PlaybackMode
    {
        Auto,
        SoundManager,
        Detached3DSource,
        CustomAudioSource
    }

    [Header("Target")]
    [SerializeField]
    [Tooltip("Optional explicit enemy core reference. If empty, the component searches on this object and its parents.")]
    private BaseEnemyCore enemyCoreOverride;

    [Header("Death Sound")]
    [SerializeField]
    [Tooltip("The sound to play when this enemy dies.")]
    private AudioClip deathSound;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume for the death sound.")]
    private float volume = 0.8f;

    [SerializeField]
    [Range(-3f, 3f)]
    [Tooltip("Pitch applied when the death sound plays.")]
    private float pitch = 1f;

    [SerializeField]
    [Tooltip("How the sound should be played. Auto prefers SoundManager, then falls back to a detached temporary 3D AudioSource.")]
    private PlaybackMode playbackMode = PlaybackMode.Auto;

    [Header("Optional Source Overrides")]
    [SerializeField]
    [Tooltip("Optional custom AudioSource. Only used when Playback Mode is Custom Audio Source.")]
    private AudioSource customAudioSource;

    [SerializeField]
    [Tooltip("Optional position anchor for detached playback. If empty, the enemy transform is used.")]
    private Transform soundAnchor;

    [Header("Detached 3D Source")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("3D blend used by detached playback. 1 is fully 3D.")]
    private float detachedSpatialBlend = 1f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Minimum distance used by detached playback.")]
    private float detachedMinDistance = 1f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Maximum distance used by detached playback.")]
    private float detachedMaxDistance = 25f;

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

    public void PlayDeathSound()
    {
        if (deathSound == null)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyDeathSoundConfig), $"{gameObject.name}: No death sound assigned.");
            return;
        }

        switch (playbackMode)
        {
            case PlaybackMode.SoundManager:
                if (!TryPlayThroughSoundManager())
                    PlayDetached();
                break;

            case PlaybackMode.Detached3DSource:
                PlayDetached();
                break;

            case PlaybackMode.CustomAudioSource:
                if (!TryPlayThroughCustomSource())
                    PlayDetached();
                break;

            default:
                if (!TryPlayThroughSoundManager())
                    PlayDetached();
                break;
        }
    }

    private void CacheEnemyCore()
    {
        enemyCore = enemyCoreOverride != null ? enemyCoreOverride : GetComponentInParent<BaseEnemyCore>();
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
        PlayDeathSound();
    }

    private bool TryPlayThroughSoundManager()
    {
        if (SoundManager.Instance == null || SoundManager.Instance.sfxSource == null)
            return false;

        AudioSource source = SoundManager.Instance.sfxSource;
        float originalPitch = source.pitch;
        source.pitch = pitch;
        source.PlayOneShot(deathSound, volume);
        source.pitch = originalPitch;
        return true;
    }

    private bool TryPlayThroughCustomSource()
    {
        if (customAudioSource == null)
            return false;

        float originalPitch = customAudioSource.pitch;
        customAudioSource.pitch = pitch;
        customAudioSource.PlayOneShot(deathSound, volume);
        customAudioSource.pitch = originalPitch;
        return true;
    }

    private void PlayDetached()
    {
        Transform anchor = soundAnchor != null ? soundAnchor : transform;
        GameObject audioRoot = new GameObject($"{name}_DeathAudio");
        audioRoot.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        AudioSource source = audioRoot.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = detachedSpatialBlend;
        source.minDistance = detachedMinDistance;
        source.maxDistance = detachedMaxDistance;
        source.pitch = pitch;
        source.clip = deathSound;
        source.volume = volume;
        source.Play();

        float pitchAbs = Mathf.Max(0.01f, Mathf.Abs(pitch));
        Destroy(audioRoot, deathSound.length / pitchAbs + 0.1f);
    }
}

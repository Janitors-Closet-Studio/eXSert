using System.Collections;
using UnityEngine;
#pragma warning disable CS0414
using Unity.VisualScripting;
using NUnit.Framework;
using System;




#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class MusicBox : MonoBehaviour
{
    // Track the currently active MusicBox
    private static MusicBox currentActiveBox;
    public static MusicBox CurrentActiveBox => currentActiveBox;
    [SerializeField] private AudioClip levelMusic;
    [SerializeField] private AudioClip ambienceClip;
    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private float cachedMusicVolume;
    private float cachedAmbienceVolume;
    [SerializeField] private bool loopMusic = true;

    [Header("Debugging")]
    [SerializeField] private bool showHitBox = true;

    [Header("Music Box Settings")]
    [SerializeField] private Vector3 boxSize = Vector3.one;

    [SerializeField] private string sceneName;

    private BoxCollider boxCollider;
    private Rigidbody rb;
    private Coroutine fadeOutMusicRoutine;
    private Coroutine fadeOutAmbienceRoutine;
    private Coroutine autoActivateRoutine;
    private bool canPlay = false;

    private SoundManager cachedSoundManager;
    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.size = boxSize;
        boxCollider.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        cachedSoundManager = SoundManager.Instance;
        UpdateCachedVolumes();

        TryBindMusicSource();
    }
#pragma warning restore CS0414

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        CutsceneManager.CutsceneFinished -= HandleCutsceneFinished;
        CutsceneManager.CutsceneFinished += HandleCutsceneFinished;
        StartAutoActivateProbe();
    }

    public void UpdateCachedVolumes()
    {
        if (cachedSoundManager == null)
            cachedSoundManager = SoundManager.Instance;

        if (cachedSoundManager == null)
            return;

        if (cachedSoundManager.ambienceSource != null)
            cachedAmbienceVolume = cachedSoundManager.ambienceSource.volume;

        if (cachedSoundManager.musicSource != null)
            cachedMusicVolume = cachedSoundManager.musicSource.volume;
    }

    private bool IsSceneLoaded(string sceneToCheck)
    {
        if (string.IsNullOrWhiteSpace(sceneToCheck))
            return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == sceneToCheck)
                return true;
        }
        return false;
    }

    private string ResolveTargetSceneName()
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
            return sceneName;

        Scene ownScene = gameObject.scene;
        return ownScene.IsValid() ? ownScene.name : string.Empty;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isActiveAndEnabled)
            return;

        string targetScene = ResolveTargetSceneName();
        if (string.Equals(scene.name, targetScene, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scene.name, "PlayerScene", StringComparison.OrdinalIgnoreCase))
        {
            StartAutoActivateProbe();
        }
    }

    private void HandleCutsceneFinished()
    {
        if (!isActiveAndEnabled)
            return;

        StartAutoActivateProbe();
    }

    private void StartAutoActivateProbe()
    {
        if (autoActivateRoutine != null)
            StopCoroutine(autoActivateRoutine);

        autoActivateRoutine = StartCoroutine(AutoActivateIfPlayerStartsInside());
    }

    private IEnumerator AutoActivateIfPlayerStartsInside()
    {
        const float maxProbeDurationSeconds = 8f;
        float elapsed = 0f;

        while (elapsed < maxProbeDurationSeconds)
        {
            if (TryAutoActivateFromPlayerPosition())
            {
                autoActivateRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        autoActivateRoutine = null;
    }

    private bool TryAutoActivateFromPlayerPosition()
    {
        if (CutsceneManager.IsCutscenePlaying)
            return false;

        string targetScene = ResolveTargetSceneName();
        if (!IsSceneLoaded(targetScene))
            return false;

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();

        if (boxCollider == null)
            return false;

        if (!IsAnyPlayerInsideMusicBox())
            return false;

        if (levelMusic != null && !TryBindMusicSource())
            return false;

        if (ambienceClip != null)
        {
            if (cachedSoundManager == null)
                cachedSoundManager = SoundManager.Instance;

            if (cachedSoundManager == null || cachedSoundManager.ambienceSource == null)
                return false;

            ambienceSource = cachedSoundManager.ambienceSource;
        }

        ActivateMusicBox();
        return true;
    }

    private bool IsAnyPlayerInsideMusicBox()
    {
        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < taggedPlayers.Length; i++)
        {
            GameObject candidate = taggedPlayers[i];
            if (candidate == null || !candidate.activeInHierarchy)
                continue;

            if (boxCollider.bounds.Contains(candidate.transform.position))
                return true;

            Collider[] colliders = candidate.GetComponentsInChildren<Collider>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider candidateCollider = colliders[colliderIndex];
                if (candidateCollider == null || !candidateCollider.enabled)
                    continue;

                if (boxCollider.bounds.Intersects(candidateCollider.bounds))
                    return true;
            }
        }

        if (Player.TryGetPlayerObject(out GameObject playerObject) && playerObject != null)
        {
            if (boxCollider.bounds.Contains(playerObject.transform.position))
                return true;

            Collider[] colliders = playerObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidateCollider = colliders[i];
                if (candidateCollider == null || !candidateCollider.enabled)
                    continue;

                if (boxCollider.bounds.Intersects(candidateCollider.bounds))
                    return true;
            }
        }

        return false;
    }

    private void StopAudioTransitionCoroutines()
    {
        if (fadeOutMusicRoutine != null)
        {
            StopCoroutine(fadeOutMusicRoutine);
            fadeOutMusicRoutine = null;
        }

        if (fadeOutAmbienceRoutine != null)
        {
            StopCoroutine(fadeOutAmbienceRoutine);
            fadeOutAmbienceRoutine = null;
        }
    }

    private void ActivateMusicBox()
    {
        if (currentActiveBox != null && currentActiveBox != this)
        {
            currentActiveBox.StopAudioTransitionCoroutines();
        }

        currentActiveBox = this;
        StopAudioTransitionCoroutines();
        PlayLevelMusic();
        PlayAmbience();
    }

    private void PlayLevelMusic()
    {
        if (levelMusic == null)
            return;

        if (CutsceneManager.IsCutscenePlaying)
            return;

        if (!TryBindMusicSource())
            return;

        // If fading out, stop fade and fade back in if needed
        if (fadeOutMusicRoutine != null)
        {
            StopCoroutine(fadeOutMusicRoutine);
            fadeOutMusicRoutine = null;
            // If music is playing but volume is low, fade in
            if (musicSource.isPlaying && musicSource.volume < cachedMusicVolume)
            {
                StartCoroutine(FadeInMusic(1f));
                return;
            }
        }

        // Only update if clip or loop state changed
        if (musicSource.isPlaying && musicSource.clip == levelMusic && musicSource.loop == loopMusic)
        {
            // If volume is low, fade in
            if (musicSource.volume < cachedMusicVolume)
                StartCoroutine(FadeInMusic(1f));
            return;
        }

        if (musicSource.clip != levelMusic)
            musicSource.clip = levelMusic;
        if (musicSource.loop != loopMusic)
            musicSource.loop = loopMusic;
        musicSource.volume = 0f;
        musicSource.Play();
        StartCoroutine(FadeInMusic(1f));
    }

    private IEnumerator FadeInMusic(float fadeDuration)
    {
        if (musicSource == null)
            yield break;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float startVolume = musicSource.volume;
        float targetVolume = cachedMusicVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (musicSource == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (musicSource != null)
            musicSource.volume = targetVolume;
    }

    private void PlayAmbience()
    {
        if (ambienceClip == null)
            return;

        if (CutsceneManager.IsCutscenePlaying)
            return;

        if (ambienceSource == null)
        {
            if (cachedSoundManager == null || cachedSoundManager.ambienceSource == null)
                return;
            ambienceSource = cachedSoundManager.ambienceSource;
        }

        if (ambienceSource.isPlaying && ambienceSource.clip == ambienceClip)
            return;

        if (ambienceSource.clip != ambienceClip)
            ambienceSource.clip = ambienceClip;
        if (!ambienceSource.loop)
            ambienceSource.loop = true;
        ambienceSource.volume = cachedAmbienceVolume;   
        ambienceSource.Play();
    }

    public IEnumerator FadeOutMusic(float fadeDuration)
    {
        if (musicSource == null || !musicSource.isPlaying)
            yield break;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float startVolume = musicSource.volume;
        AudioClip fadingClip = musicSource.clip;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // If another MusicBox took over this shared source, do not stop its audio.
            if (musicSource == null || musicSource.clip != fadingClip)
            {
                fadeOutMusicRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (musicSource != null && musicSource.clip == fadingClip)
        {
            musicSource.Stop();
            musicSource.volume = cachedMusicVolume; // Reset volume for next time
        }

        fadeOutMusicRoutine = null;
    }

    public IEnumerator FadeOutAmbience(float fadeDuration)
    {
        if (ambienceSource == null || !ambienceSource.isPlaying)
            yield break;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float startVolume = ambienceSource.volume;
        AudioClip fadingClip = ambienceSource.clip;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // If another MusicBox took over this shared source, do not stop its audio.
            if (ambienceSource == null || ambienceSource.clip != fadingClip)
            {
                fadeOutAmbienceRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ambienceSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (ambienceSource != null && ambienceSource.clip == fadingClip)
        {
            ambienceSource.Stop();
            ambienceSource.volume = cachedAmbienceVolume; // Reset volume for next time
        }

        fadeOutAmbienceRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && IsSceneLoaded(ResolveTargetSceneName()) && CutsceneManager.IsCutscenePlaying == false)
        {
            ActivateMusicBox();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Only fade out if this is still the active box
        if (currentActiveBox == this)
        {
            if (musicSource == null || !musicSource.isPlaying)
                return;

            if (fadeOutMusicRoutine != null)
                StopCoroutine(fadeOutMusicRoutine);

            fadeOutMusicRoutine = StartCoroutine(FadeOutMusic(2f));

            if (fadeOutAmbienceRoutine != null)
                StopCoroutine(fadeOutAmbienceRoutine);

            fadeOutAmbienceRoutine = StartCoroutine(FadeOutAmbience(2f));
        }

    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CutsceneManager.CutsceneFinished -= HandleCutsceneFinished;

        if (autoActivateRoutine != null)
        {
            StopCoroutine(autoActivateRoutine);
            autoActivateRoutine = null;
        }

        if (currentActiveBox == this)
            currentActiveBox = null;
    }

    private void OnDestroy()
    {
        if (currentActiveBox == this)
            currentActiveBox = null;
    }

    private void OnValidate()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();
        
        if (boxCollider != null)
        {
            boxCollider.size = boxSize;
            boxCollider.isTrigger = true;
        }

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

#if UNITY_EDITOR
    [MenuItem("GameObject/Environment/MusicBox", false, 10)]
    public static void CreateMusicBox(MenuCommand menuCommand)
    {
        GameObject musicBoxGO = new GameObject("MusicBox");
        musicBoxGO.AddComponent<MusicBox>();
        GameObjectUtility.SetParentAndAlign(musicBoxGO, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(musicBoxGO, "Create MusicBox");
        Selection.activeObject = musicBoxGO;
    }
#endif


    private void OnDrawGizmos()
    {
        if (showHitBox)
        {
            Gizmos.color = Color.orange * new Color(1, 1, 1, 0.25f);
            Gizmos.DrawCube(transform.position, boxSize);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, boxSize);
        }
    }

    private bool TryBindMusicSource()
    {
        if (musicSource != null)
            return true;

        if (cachedSoundManager == null)
            cachedSoundManager = SoundManager.Instance;
        if (cachedSoundManager == null)
            return false;

        musicSource = cachedSoundManager.levelMusicSource;
        return musicSource != null;
    }
}

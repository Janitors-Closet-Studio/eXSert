using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class MusicBox : MonoBehaviour
{
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

    private SoundManager cachedSoundManager;
    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.size = boxSize;
        boxCollider.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        cachedAmbienceVolume = SoundManager.Instance != null ? SoundManager.Instance.ambienceSource.volume : 1.0f;
        cachedMusicVolume = SoundManager.Instance != null ? SoundManager.Instance.musicSource.volume : 1.0f;

        cachedSoundManager = SoundManager.Instance;
        TryBindMusicSource();
    }

    private void PlayLevelMusic()
    {
        if (levelMusic == null)
            return;

        if (!TryBindMusicSource())
            return;

        if (fadeOutMusicRoutine != null)
        {
            StopCoroutine(fadeOutMusicRoutine);
            fadeOutMusicRoutine = null;
        }

        // Only update if clip or loop state changed
        if (musicSource.isPlaying && musicSource.clip == levelMusic && musicSource.loop == loopMusic)
            return;

        if (musicSource.clip != levelMusic)
            musicSource.clip = levelMusic;
        if (musicSource.loop != loopMusic)
            musicSource.loop = loopMusic;
        musicSource.volume = cachedMusicVolume; // Ensure correct volume immediately
        musicSource.Play();
    }

    private void PlayAmbience()
    {
        if (ambienceClip == null)
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

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0, cachedMusicVolume * (Time.deltaTime / fadeDuration));
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = cachedMusicVolume; // Reset volume for next time
        fadeOutMusicRoutine = null;
    }

    public IEnumerator FadeOutAmbience(float fadeDuration)
    {
        if (ambienceSource == null || !ambienceSource.isPlaying)
            yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, 0, cachedAmbienceVolume * (Time.deltaTime / fadeDuration));
            yield return null;
        }
        ambienceSource.Stop();
        ambienceSource.volume = cachedAmbienceVolume; // Reset volume for next time
        fadeOutAmbienceRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")){
            StopAllCoroutines();    
            PlayLevelMusic();
            PlayAmbience();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (musicSource == null || !musicSource.isPlaying)
            return;

        if (fadeOutMusicRoutine != null)
            StopCoroutine(fadeOutMusicRoutine);

        fadeOutMusicRoutine = StartCoroutine(FadeOutMusic(2f));

        if (fadeOutAmbienceRoutine != null)
            StopCoroutine(fadeOutAmbienceRoutine);

        fadeOutAmbienceRoutine = StartCoroutine(FadeOutAmbience(2f));

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

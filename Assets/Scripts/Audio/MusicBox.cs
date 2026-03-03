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

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.size = boxSize;
        boxCollider.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        TryBindMusicSource();
    }

    private void PlayLevelMusic()
    {


        if (levelMusic == null)
        {
            return;
        }

        if (!TryBindMusicSource())
        {
            return;
        }

        if (fadeOutMusicRoutine != null)
        {
            StopCoroutine(fadeOutMusicRoutine);
            fadeOutMusicRoutine = null;
        }

        if (musicSource.isPlaying && musicSource.clip == levelMusic)
            return;

        musicSource.clip = levelMusic;
        musicSource.loop = loopMusic;
        musicSource.Play();
        Debug.Log($"🎵 [MusicBox] Playing level music: {levelMusic.name}");
    }

    private void PlayAmbience()
    {
        if (ambienceClip == null)
        {
            return;
        }

        if (ambienceSource == null)
        {
            var sm = SoundManager.Instance;
            if (sm == null || sm.ambienceSource == null)
            {
                return;
            }
            ambienceSource = sm.ambienceSource;
        }

        if (ambienceSource.isPlaying && ambienceSource.clip == ambienceClip)
            return;

        ambienceSource.clip = ambienceClip;
        ambienceSource.loop = true;
        ambienceSource.Play();
    }

    public IEnumerator FadeOutMusic(float fadeDuration)
    {
        if (musicSource == null || !musicSource.isPlaying)
            yield break;

        float startVolume = musicSource.volume;

        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume; // Reset volume for next time
    }

    public IEnumerator FadeOutAmbience(float fadeDuration)
    {
        if (ambienceSource == null || !ambienceSource.isPlaying)
            yield break;

        float startVolume = ambienceSource.volume;

        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            ambienceSource.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
            yield return null;
        }

        ambienceSource.Stop();
        ambienceSource.volume = startVolume; // Reset volume for next time
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")){
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

        var sm = SoundManager.Instance;
        if (sm == null)
            return false;

        musicSource = sm.levelMusicSource;
        return musicSource != null;
    }
}

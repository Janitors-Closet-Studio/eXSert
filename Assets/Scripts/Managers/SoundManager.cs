/*
    This singleton will be used to manage the playing of music and sfx in any given scene. Here you can assign the source and adjust the volume of said source.
*/

using UnityEngine;
#pragma warning disable CS0414
using Singletons;
using System.Collections;
public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] protected override bool ShouldPersistAcrossScenes => true;

    private float masterVolume = 1f;

    // Main Categories
    public AudioSource masterSource;
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource voiceSource;
    [Header("Player Action SFX")]
    [SerializeField, Tooltip("Dedicated source for player action SFX (guard/dash/attack). Falls back to SFX/Voice source when missing.")]
    private AudioSource playerActionSfxSource;
    [SerializeField, Range(0f, 0.5f)] private float playerActionFadeOutSeconds = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float playerActionFadeInSeconds = 0.04f;

    // Sub Categories
    public AudioSource ambienceSource;
    public AudioSource uiSource;
    public AudioSource puzzleSource;
    public AudioSource levelMusicSource;

    private float ogSfxVolume = 1f;
    private float ogMusicVolume = 1f;
    private float ogLevelMusicVolume = 1f;
    private float ogAmbienceVolume = 1f;
    private float ogVoiceVolume = 1f;   
    private float ogPlayerActionVolume = 1f;
    private Coroutine playerActionSfxRoutine;
    private int playerActionSfxPriority = -1;
    private int playerActionSfxRequestToken;

    //Debug logs
    private void PersistAudioSource(AudioSource source)
    {
        if (source == null) return;
        DontDestroyOnLoad(source.gameObject);
    }
#pragma warning restore CS0414

    override protected void Awake()
    {
        PersistAudioSource(masterSource);
        PersistAudioSource(musicSource);
        PersistAudioSource(levelMusicSource);
        PersistAudioSource(sfxSource);
        PersistAudioSource(voiceSource);
        PersistAudioSource(playerActionSfxSource);

        base.Awake();

        if (SoundManager.Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ApplySavedVolumes();
    }
    private void ApplySavedVolumes()
    {
        float masterRaw = masterSource != null
            ? (PlayerPrefs.HasKey("masterVolume") ? PlayerPrefs.GetFloat("masterVolume") : masterSource.volume)
            : 0f;

        float musicRaw = musicSource != null
            ? (PlayerPrefs.HasKey("musicVolume") ? PlayerPrefs.GetFloat("musicVolume") : GetRawVolume(musicSource.volume, masterRaw))
            : 0f;

        float sfxRaw = sfxSource != null
            ? (PlayerPrefs.HasKey("sfxVolume") ? PlayerPrefs.GetFloat("sfxVolume") : GetRawVolume(sfxSource.volume, masterRaw))
            : 0f;

        float voiceRaw = voiceSource != null
            ? (PlayerPrefs.HasKey("voiceVolume") ? PlayerPrefs.GetFloat("voiceVolume") : GetRawVolume(voiceSource.volume, masterRaw))
            : 0f;

        if (masterSource != null)
            masterSource.volume = masterRaw;

        if (musicSource != null)
            musicSource.volume = musicRaw * masterRaw;

        if (sfxSource != null)
            sfxSource.volume = sfxRaw * masterRaw;

        if (voiceSource != null)
            voiceSource.volume = voiceRaw * masterRaw;

        if (playerActionSfxSource != null)
            playerActionSfxSource.volume = sfxSource != null ? sfxSource.volume : (voiceSource != null ? voiceSource.volume : masterRaw);

        if(ambienceSource != null)
            ambienceSource.volume = musicSource.volume * 0.2f; // Ambience is typically quieter than music

        if(uiSource != null)
            uiSource.volume = sfxSource.volume;

        if(puzzleSource != null)
            puzzleSource.volume = sfxSource.volume;
        
        if(levelMusicSource != null)
            levelMusicSource.volume = musicSource.volume;
    }

    public void PauseUnPauseAudio(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Pause();
        else if (source != null)
            source.UnPause();
    }

    private float GetRawVolume(float scaledVolume, float masterVolume)
    {
        if (masterVolume <= 0f)
            return 0f;

        return Mathf.Clamp01(scaledVolume / masterVolume);
    }

    public void FadeOutMusic(float duration)
    {
        if (musicSource != null)
        {
            StartCoroutine(FadeOutCoroutine(musicSource, duration));
        }
    }

    public IEnumerator FadeOutGameplayAudio(float duration)
    {
        if (duration <= 0f)
        {
            StopGameplayAudioImmediate();
            yield break;
        }

        yield return FadeOutGameplaySourcesCoroutine(duration);
        ApplySavedVolumes();
    }

    public void StopGameplayAudioImmediate()
    {
        StopSource(levelMusicSource);
        StopSource(ambienceSource);
        ApplySavedVolumes();
    }

    private IEnumerator FadeOutGameplaySourcesCoroutine(float duration)
    {
        float elapsedTime = 0f;
        float initialLevelMusicVolume = levelMusicSource != null ? levelMusicSource.volume : 0f;
        float initialAmbienceVolume = ambienceSource != null ? ambienceSource.volume : 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            if (levelMusicSource != null && levelMusicSource.isPlaying)
                levelMusicSource.volume = Mathf.Lerp(initialLevelMusicVolume, 0f, t);

            if (ambienceSource != null && ambienceSource.isPlaying)
                ambienceSource.volume = Mathf.Lerp(initialAmbienceVolume, 0f, t);

            yield return null;
        }

        StopSource(levelMusicSource);
        StopSource(ambienceSource);
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
    }

    public IEnumerator FadeOutCoroutine(AudioSource musicSource, float fadeDuration)
    {
        if (musicSource == null || fadeDuration <= 0f)
            yield break;


        Debug.Log("Starting fade out coroutine for music source: " + musicSource.name);
        float startVolume = musicSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float newVolume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeDuration);
            musicSource.volume = newVolume;
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.Stop();
    }

    public void PauseAllMusic(bool shouldPause)
    {
        if (shouldPause)
        {
            if (sfxSource != null) ogSfxVolume = sfxSource.volume;
            if (musicSource != null) ogMusicVolume = musicSource.volume;
            if (levelMusicSource != null) ogLevelMusicVolume = levelMusicSource.volume;
            if (ambienceSource != null) ogAmbienceVolume = ambienceSource.volume;
            if (voiceSource != null) ogVoiceVolume = voiceSource.volume;
            if (playerActionSfxSource != null) ogPlayerActionVolume = playerActionSfxSource.volume;

            if (musicSource != null && musicSource.isPlaying)
                musicSource.Pause();
            if (levelMusicSource != null && levelMusicSource.isPlaying)
                levelMusicSource.Pause();
            if (ambienceSource != null && ambienceSource.isPlaying)
                ambienceSource.Pause();
            if (sfxSource != null && sfxSource.isPlaying)
                sfxSource.Pause();
            if (voiceSource != null && voiceSource.isPlaying)
                voiceSource.Pause();
            if (playerActionSfxSource != null && playerActionSfxSource.isPlaying)
                playerActionSfxSource.Pause();

            if (sfxSource != null) sfxSource.volume = 0f; // Mute SFX
            if (musicSource != null) musicSource.volume = 0f; // Mute music
            if (levelMusicSource != null) levelMusicSource.volume = 0f; // Mute level music
            if (ambienceSource != null) ambienceSource.volume = 0f; // Mute ambience
            if (voiceSource != null) voiceSource.volume = 0f; // Mute voice
            if (playerActionSfxSource != null) playerActionSfxSource.volume = 0f;
        }
        else
        {
            if (musicSource != null && !musicSource.isPlaying)
                musicSource.UnPause();
            if (levelMusicSource != null && !levelMusicSource.isPlaying)
                levelMusicSource.UnPause();
            if (ambienceSource != null && !ambienceSource.isPlaying)
                ambienceSource.UnPause();
            if (sfxSource != null && !sfxSource.isPlaying)
                sfxSource.UnPause();
            if (voiceSource != null && !voiceSource.isPlaying)
                voiceSource.UnPause();
            if (playerActionSfxSource != null && !playerActionSfxSource.isPlaying)
                playerActionSfxSource.UnPause();

            if (sfxSource != null) sfxSource.volume = ogSfxVolume;
            if (musicSource != null) musicSource.volume = ogMusicVolume;
            if (levelMusicSource != null) levelMusicSource.volume = ogLevelMusicVolume;
            if (ambienceSource != null) ambienceSource.volume = ogAmbienceVolume;
            if (voiceSource != null) voiceSource.volume = ogVoiceVolume;
            if (playerActionSfxSource != null) playerActionSfxSource.volume = ogPlayerActionVolume;

            // Ensure all volumes are reset to user settings
            ApplySavedVolumes();
        }
    }

    public bool TryPlayPlayerActionSfx(AudioClip clip, int priority)
    {
        if (clip == null)
            return false;

        AudioSource source = ResolvePlayerActionSource();
        if (source == null)
            return false;

        if (source.isPlaying && priority < playerActionSfxPriority)
            return false;

        playerActionSfxPriority = priority;
        playerActionSfxRequestToken++;

        if (playerActionSfxRoutine != null)
        {
            StopCoroutine(playerActionSfxRoutine);
            playerActionSfxRoutine = null;
        }

        playerActionSfxRoutine = StartCoroutine(PlayPlayerActionSfxRoutine(source, clip, playerActionSfxRequestToken));
        return true;
    }

    private AudioSource ResolvePlayerActionSource()
    {
        if (playerActionSfxSource != null)
            return playerActionSfxSource;

        if (sfxSource != null)
            return sfxSource;

        return voiceSource;
    }

    private IEnumerator PlayPlayerActionSfxRoutine(AudioSource source, AudioClip clip, int requestToken)
    {
        if (source == null || clip == null)
            yield break;

        float targetVolume = Mathf.Max(0.0001f, source.volume);
        if (source.isPlaying && playerActionFadeOutSeconds > 0f)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < playerActionFadeOutSeconds)
            {
                if (requestToken != playerActionSfxRequestToken)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / playerActionFadeOutSeconds));
                yield return null;
            }
        }

        if (requestToken != playerActionSfxRequestToken)
            yield break;

        source.Stop();
        source.clip = clip;
        source.volume = 0f;
        source.Play();

        if (playerActionFadeInSeconds <= 0f)
        {
            source.volume = targetVolume;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < playerActionFadeInSeconds)
            {
                if (requestToken != playerActionSfxRequestToken)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / playerActionFadeInSeconds));
                yield return null;
            }

            source.volume = targetVolume;
        }

        playerActionSfxRoutine = null;
    }
}

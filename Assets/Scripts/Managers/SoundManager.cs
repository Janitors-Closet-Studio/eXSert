/*
    This singleton will be used to manage the playing of music and sfx in any given scene. Here you can assign the source and adjust the volume of said source.
*/

using UnityEngine;
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

    // Sub Categories
    public AudioSource ambienceSource;
    public AudioSource uiSource;
    public AudioSource puzzleSource;
    public AudioSource levelMusicSource;

    //Debug logs
    private void PersistAudioSource(AudioSource source)
    {
        if (source == null) return;
        DontDestroyOnLoad(source.gameObject);
    }

    override protected void Awake()
    {
        PersistAudioSource(masterSource);
        PersistAudioSource(musicSource);
        PersistAudioSource(levelMusicSource);
        PersistAudioSource(sfxSource);
        PersistAudioSource(voiceSource);

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
}

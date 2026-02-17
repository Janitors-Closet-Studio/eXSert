using UnityEngine;

public class PlayLevelMusic : MonoBehaviour
{
    private AudioSource musicSource;
    [SerializeField] private AudioClip levelMusicClip;
    [SerializeField] private bool loopMusic = true;

    private void Start()
    {
        if(musicSource == null)
        {
            musicSource = SoundManager.Instance.musicSource;
        }

        PlayMusic();
    }

    private void PlayMusic()
    {
        if (levelMusicClip == null)
        {
            Debug.LogWarning("No level music clip assigned!");
            return;
        }

        if (musicSource == null)
        {
            Debug.LogError("SoundManager has no musicSource assigned!");
            return;
        }

        musicSource.clip = levelMusicClip;
        musicSource.loop = loopMusic;
        musicSource.Play();
        Debug.Log($"🎵 Playing level music: {levelMusicClip.name}");
    }
}
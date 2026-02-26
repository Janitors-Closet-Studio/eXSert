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
            return;
        }

        if (musicSource == null)
        {
            return;
        }

        musicSource.clip = levelMusicClip;
        musicSource.loop = loopMusic;
        musicSource.Play();
        Debug.Log($"🎵 Playing level music: {levelMusicClip.name}");
    }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// Helper component to play and fade sounds through the SoundManager singleton.
/// Automatically finds and uses the SoundManager's audio source for global SFX.
/// </summary>
public class PlaySoundThroughManager : MonoBehaviour
{
    [Header("Audio Clip")]
    [SerializeField] private AudioClip soundClip;
    
    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;
    [SerializeField] internal bool loop = false;

    [Header("Fade Out Settings")]
    [SerializeField] [Range(0f, 10f)] private float fadeOutDuration = 1f;
    
    private Coroutine _fadeOutCoroutine;
    
    /// <summary>
    /// Play the assigned sound through SoundManager.
    /// </summary>
    public void PlaySound()
    {
        if (soundClip == null)
        {
            return;
        }
        
        if (SoundManager.Instance == null || SoundManager.Instance.sfxSource == null)
        {
            return;
        }

        if(!loop)
        {
            SoundManager.Instance.sfxSource.PlayOneShot(soundClip, volume);
        } 
        else 
        {
            SoundManager.Instance.sfxSource.clip = soundClip;
            SoundManager.Instance.sfxSource.volume = volume;
            SoundManager.Instance.sfxSource.loop = true;
            SoundManager.Instance.sfxSource.Play();
        }
    }
    
    /// <summary>
    /// Play a specific clip through SoundManager.
    /// </summary>
    public void PlaySpecificSound(AudioClip clip)
    {
        if (clip == null || SoundManager.Instance == null || SoundManager.Instance.sfxSource == null)
            return;
        
        SoundManager.Instance.sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Fade out and stop the current sound.
    /// </summary>
    public void StopSound()
    {
        if (SoundManager.Instance == null || SoundManager.Instance.sfxSource == null)
            return;

        // Only start fade if one isn't already running
        if (_fadeOutCoroutine != null)
            return;

        _fadeOutCoroutine = StartCoroutine(FadeOutSound());
    }

    private IEnumerator FadeOutSound()
    {
        if (SoundManager.Instance == null || SoundManager.Instance.sfxSource == null)
            yield break;

        AudioSource audioSource = SoundManager.Instance.sfxSource;
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        audioSource.loop = false;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutDuration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        _fadeOutCoroutine = null;
    }
}

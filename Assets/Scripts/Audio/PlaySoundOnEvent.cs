using UnityEngine;
#pragma warning disable CS0414

/// <summary>
/// Simple helper component to play a sound when an event is triggered.
/// Attach this to any GameObject with an AudioSource to easily hook up to UnityEvents.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlaySoundOnEvent : MonoBehaviour
{
    [Header("Sound Settings")]
    [Tooltip("The audio clip to play when PlaySound() is called")]
    [SerializeField] private AudioClip soundClip;
    
    [Tooltip("Volume for the sound (0-1)")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    
    [Tooltip("Whether to use PlayOneShot (allows overlapping) or Play (stops previous sound)")]
    [SerializeField] private bool usePlayOneShot = true;

    [SerializeField] private bool loop = false;
    
    private AudioSource audioSource;
    
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }
#pragma warning restore CS0414
    
    /// <summary>
    /// Call this from UnityEvents to play the assigned sound clip.
    /// </summary>
    public void PlaySound()
    {
        Debug.Log($"🔊 PlaySound() called on {gameObject.name}");
        
        if (audioSource == null)
        {
            Debug.LogError($"🔊 {gameObject.name}: AudioSource is NULL!");
            return;
        }
        
        if (soundClip == null)
        {
            Debug.LogError($"🔊 {gameObject.name}: Sound clip is NULL! Assign a clip in the Inspector.");
            return;
        }
        
        Debug.Log($"🔊 {gameObject.name}: Playing sound '{soundClip.name}' at volume {volume}");
        
        if (usePlayOneShot)
        {
            audioSource.PlayOneShot(soundClip, volume);
        }
        else
        {
            audioSource.clip = soundClip;
            audioSource.volume = volume;
            audioSource.Play();
        }
        
        Debug.Log($"🔊 {gameObject.name}: Sound playback command sent successfully!");
    }
    
    /// <summary>
    /// Play a specific sound clip (can be called from UnityEvents with AudioClip parameter).
    /// </summary>
    public void PlaySpecificSound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot play sound - AudioSource or clip is missing!");
            return;
        }
        
        audioSource.PlayOneShot(clip, volume);
    }
    
    /// <summary>
    /// Play sound with custom volume.
    /// </summary>
    public void PlaySoundWithVolume(float customVolume)
    {
        if (audioSource == null || soundClip == null)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot play sound - AudioSource or clip is missing!");
            return;
        }
        
        audioSource.PlayOneShot(soundClip, customVolume);
    }
}

using UnityEngine;

/// <summary>
/// Add this component to any enemy GameObject to configure their death sound.
/// The DeathBehavior will automatically find and use this configuration.
/// </summary>
public class EnemyDeathSoundConfig : MonoBehaviour
{
    [Header("Death Sound")]
    [Tooltip("The sound to play when this enemy dies")]
    public AudioClip deathSound;
    
    [Tooltip("Volume for the death sound (0-1)")]
    [Range(0f, 1f)]
    public float volume = 0.8f;
    
    [Header("Optional: Play Through Specific Source")]
    [Tooltip("If set, will play through this AudioSource instead of SoundManager")]
    public AudioSource customAudioSource;
    
    /// <summary>
    /// Play the death sound manually (can be called from UnityEvents).
    /// </summary>
    public void PlayDeathSound()
    {
        if (deathSound == null)
        {
            EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyDeathSoundConfig), $"{gameObject.name}: No death sound assigned!");
            return;
        }
        
        // Try custom source first
        if (customAudioSource != null)
        {
            customAudioSource.PlayOneShot(deathSound, volume);
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyDeathSoundConfig), $"🔊 {gameObject.name} playing death sound through custom AudioSource");
            return;
        }
        
        // Fall back to SoundManager
        if (SoundManager.Instance != null && SoundManager.Instance.sfxSource != null)
        {
            SoundManager.Instance.sfxSource.PlayOneShot(deathSound, volume);
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyDeathSoundConfig), $"🔊 {gameObject.name} playing death sound through SoundManager");
            return;
        }
        
        EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyDeathSoundConfig), $"🔊 {gameObject.name}: Cannot play death sound - no AudioSource available!");
    }
}

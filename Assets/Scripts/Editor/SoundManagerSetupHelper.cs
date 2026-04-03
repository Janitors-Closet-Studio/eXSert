using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor helper to quickly set up a SoundManager in the scene.
/// Use: Tools > Setup Sound Manager in Scene
/// </summary>
public class SoundManagerSetupHelper : MonoBehaviour
{
    [MenuItem("Tools/Setup Sound Manager in Scene")]
    static void SetupSoundManager()
    {
        // Check if SoundManager already exists
        SoundManager existing = FindFirstObjectByType<SoundManager>();
        if (existing != null)
        {
            Debug.LogWarning("SoundManager already exists in scene! Select it to configure manually.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Create new GameObject with SoundManager
        GameObject soundManagerObj = new GameObject("SoundManager");
        SoundManager manager = soundManagerObj.AddComponent<SoundManager>();

        // Create SFX AudioSource
        AudioSource sfxSource = soundManagerObj.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D sound

        // Create Music AudioSource
        AudioSource musicSource = soundManagerObj.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        // Create Voice AudioSource
        AudioSource voiceSource = soundManagerObj.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        // Assign to SoundManager
        manager.sfxSource = sfxSource;
        manager.musicSource = musicSource;
        manager.voiceSource = voiceSource;
        manager.masterSource = sfxSource; // Use SFX as master for now

        // Select the new object
        Selection.activeGameObject = soundManagerObj;

        Debug.Log("✅ SoundManager created and configured! AudioSources are ready to use.");
        Debug.Log("Note: Don't forget to add Audio Mixer groups if you want volume controls!");
    }
}

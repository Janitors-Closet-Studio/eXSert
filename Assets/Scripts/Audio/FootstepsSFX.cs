using UnityEngine;

public class FootstepsSFX : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] walkClip;

    private void Start()
    {
        TryResolveAudioSource();
    }

    private bool TryResolveAudioSource()
    {
        if (audioSource != null)
            return true;

        SoundManager soundManager = FindAnyObjectByType<SoundManager>();
        if (soundManager == null || soundManager.sfxSource == null)
            return false;

        audioSource = soundManager.sfxSource;
        return true;
    }

    public void PlayFootstepSound()
    {
        if (walkClip == null || walkClip.Length == 0)
            return;

        if (!TryResolveAudioSource())
            return;

        int randomIndex = Random.Range(0, walkClip.Length);
        audioSource.PlayOneShot(walkClip[randomIndex]);
    }
}

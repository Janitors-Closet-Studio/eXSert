using UnityEngine;

public class FootstepsSFX : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] walkClip;
    [SerializeField] private AudioClip[] walkDirtClip;

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

        if (PlayDirtFootstepIfOnDirt())
        {
            AudioClip clip = walkDirtClip[Random.Range(0, walkDirtClip.Length)];
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioClip clip = walkClip[Random.Range(0, walkClip.Length)];
            audioSource.PlayOneShot(clip);
        }
    }

    private bool PlayDirtFootstepIfOnDirt()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1f))
        {
            if (hit.collider.CompareTag("Dirt"))
                return true;
            else
                return false;
        }
        return false;
    }

}

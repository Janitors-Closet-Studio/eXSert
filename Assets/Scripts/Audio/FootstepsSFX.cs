using UnityEngine;

public class FootstepsSFX : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] walkClip;

    private void Start()
    {
        audioSource = SoundManager.Instance.sfxSource;
    }
    public void PlayFootstepSound()
    {
        if (walkClip.Length > 0)
        {
            int randomIndex = Random.Range(0, walkClip.Length);
            audioSource.PlayOneShot(walkClip[randomIndex]);
        }
    }
}
